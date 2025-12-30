using HRAgent.Api.Configuration;
using HRAgent.Api.Middleware;
using HRAgent.Api.Services;
using HRAgent.Infrastructure.Persistence;
using HRAgent.Infrastructure.Security;
using HRAgent.Infrastructure.Telemetry;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Structured Logging Configuration
// ========================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddApplicationInsights();

// ========================================
// Service Defaults (OpenTelemetry, Health Checks, Service Discovery)
// ========================================
builder.AddServiceDefaults();

// ========================================
// Infrastructure Configuration
// ========================================

// Azure AI Foundry (OpenAI) for LLM
builder.Services.AddAzureAIFoundry(builder.Configuration);

// Cosmos DB / MongoDB for conversation storage
builder.Services.AddCosmosDb(builder.Configuration);

// Blob Storage for audit logs
builder.Services.AddBlobStorage(builder.Configuration);

// Custom Application Insights telemetry
builder.Services.AddCustomApplicationInsights(builder.Configuration);

// Azure Key Vault for secrets management
builder.Services.AddKeyVault(builder.Configuration);

// ========================================
// Business Logic Services
// ========================================

// User claims extraction
builder.Services.AddScoped<IUserClaimsService, UserClaimsService>();

// Event handlers for decoupled communication
builder.Services.AddScoped<HRAgent.Api.Events.ISubmissionFailedHandler, SubmissionFailedEventHandler>();

// Conversation storage
builder.Services.AddScoped<ConversationStore>();

// Audit logging
builder.Services.AddScoped<AuditLogger>();

// Session management
builder.Services.AddScoped<SessionManager>();

// Submission queue for durable retry
builder.Services.AddScoped<SubmissionQueue>();

// GDPR Compliance Services (T033i-T033q)
builder.Services.AddScoped<ConversationDeletionService>();
builder.Services.AddScoped<DeletionAuditLogger>();
builder.Services.AddSingleton(new EmailNotificationOptions
{
    Enabled = builder.Configuration.GetValue<bool>("Email:Enabled", false),
    SmtpHost = builder.Configuration["Email:SmtpHost"] ?? "smtp.office365.com",
    SmtpPort = builder.Configuration.GetValue<int>("Email:SmtpPort", 587),
    EnableSsl = builder.Configuration.GetValue<bool>("Email:EnableSsl", true),
    SmtpUsername = builder.Configuration["Email:SmtpUsername"] ?? string.Empty,
    SmtpPassword = builder.Configuration["Email:SmtpPassword"] ?? string.Empty,
    FromEmail = builder.Configuration["Email:FromEmail"] ?? "noreply@company.com",
    FromName = builder.Configuration["Email:FromName"] ?? "HR Agent"
});
builder.Services.AddScoped<EmailNotificationService>();

// Background service for GDPR deletion processing (30-day window)
builder.Services.AddHostedService<HRAgent.Api.Jobs.DeletionProcessor>();

// Factorial HR API client with resilience (retry, circuit breaker, timeout)
builder.Services.AddHttpClient<FactorialHRService>(client =>
{
    var baseUrl = builder.Configuration["FactorialHR:BaseUrl"] ?? "https://api.factorialhr.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
})
.AddStandardResilienceHandler(options =>
{
    // Retry policy: 3 retries with exponential backoff (1s, 2s, 4s)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    
    // Circuit breaker: Break after 50% failure rate over 10 requests, retry after 60s
    // Note: SamplingDuration must be at least 2x the AttemptTimeout
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60); // At least 2x AttemptTimeout
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);
    
    // Timeout per attempt: 30 seconds
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    
    // Total timeout: 2 minutes for all retries
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
});

// Background service for submission retry queue
builder.Services.AddHostedService<HRAgent.Api.Jobs.SubmissionRetryProcessor>();

// ========================================
// Authentication & Authorization
// ========================================

// Microsoft Entra ID (Azure AD) authentication
builder.Services.AddEntraIdAuthentication(builder.Configuration);

// ========================================
// API Services
// ========================================

// Controllers
builder.Services.AddControllers();

// OpenAPI/Swagger
builder.Services.AddOpenApi();

// CORS (configure for production domains)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Production: Configure specific origins
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? Array.Empty<string>();
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// ========================================
// Build Application
// ========================================

var app = builder.Build();

// ========================================
// HTTP Request Pipeline
// ========================================

// Development-only middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Global exception handler
app.UseGlobalExceptionHandler();

// CORS
app.UseCors();

// HTTPS redirection
app.UseHttpsRedirection();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

// Service defaults endpoints (health checks, metrics)
app.MapDefaultEndpoints();

// Note: Health checks are configured in HRAgent.ServiceDefaults/Extensions.cs
// To add custom health checks for Cosmos DB, Blob Storage, and Factorial HR:
// 1. Install Microsoft.Extensions.Diagnostics.HealthChecks.* packages
// 2. Add builder.Services.AddHealthChecks()
//    .AddCosmosDb(cosmosClient, tags: new[] { "ready", "db" })
//    .AddAzureBlobStorage(blobServiceClient, tags: new[] { "ready", "storage" })
//    .AddUrlGroup(new Uri(factorialHRBaseUrl), "Factorial HR API", tags: new[] { "ready", "external" });
// 3. Health checks are exposed via HealthController at /api/health, /api/health/ready, /api/health/live

// ========================================
// Run Application
// ========================================

app.Run();
