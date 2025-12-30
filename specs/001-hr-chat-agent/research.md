# Research: HR Chat Agent for Timesheet Management

**Feature**: 001-hr-chat-agent  
**Date**: 2025-12-30  
**Status**: Complete

## Overview

This document consolidates research findings for building a conversational AI agent for timesheet management using .NET 10, Microsoft Agent Framework, Aspire 13 orchestration, Vite React frontend with AG-UI protocol, and Azure infrastructure.

---

## 1. Microsoft Agent Framework for Conversational AI

### Decision: Use Microsoft Agent Framework (Not Semantic Kernel)

Microsoft Agent Framework is the direct successor to both Semantic Kernel and AutoGen, representing the unified next-generation platform for building AI agents.

### Rationale

**Why Microsoft Agent Framework:**
- **Modern Architecture**: Combines AutoGen's simplicity with Semantic Kernel's enterprise features
- **Enhanced Capabilities**: 
  - Graph-based workflows with explicit control over execution paths
  - Robust state management for long-running conversations
  - Type-safe message routing between components
  - Built-in checkpointing for workflow recovery
- **Official Recommendation**: Microsoft's recommended path forward for new AI agent projects
- **Azure Integration**: Native integration with Azure AI Foundry for LLM hosting
- **State Management**: `AgentThread` abstraction for conversation persistence across sessions

**Why NOT Semantic Kernel:**
- Semantic Kernel continues to receive only bug fixes and critical security patches
- Agent Framework supersedes it with enhanced multi-agent orchestration
- Better support for human-in-the-loop and complex conversation flows

### Architecture Patterns for HR Agent

**Intent Classification with Triage Agent:**
```csharp
var triageAgent = new ChatCompletionAgent(
    name: "TriageAgent",
    instructions: "Classify user intent: clock-in, clock-out, status query, historical query",
    plugins: [clockInAgent, clockOutAgent, queryAgent]
);
```

**Conversation State Management:**
```csharp
// Create or resume conversation thread
AgentThread thread = await threadStore.GetOrCreateAsync(sessionId);
var response = await agent.RunAsync(userMessage, thread);

// Persist thread to Cosmos DB
await threadStore.SaveAsync(thread);
```

**Multi-Turn Conversations:**
- Use `AgentThread` for maintaining context between messages
- Serialize threads to Cosmos DB (DocumentDB API) for persistence
- Implement `IChatReducer` to manage context window size
- Use `AIContextProvider` for injecting long-term memory (past timesheets, user preferences)

### Integration with Azure AI Foundry

```csharp
// Azure OpenAI integration with managed identity
var agent = new AzureOpenAIClient(
    new Uri("https://<foundry-resource>.openai.azure.com/openai/v1"),
    new DefaultAzureCredential())
    .GetChatClient(deploymentName: "gpt-4")
    .CreateAIAgent(
        name: "HRAgent",
        instructions: "You are an HR assistant helping employees manage timesheets..."
    );
```

**Key Integration Points:**
- **Model Catalog**: Access GPT-4, GPT-4o models through Azure AI Foundry
- **Observability**: Built-in tracing with OpenTelemetry integration
- **Content Safety**: Integration with Azure Content Safety services for filtering inappropriate content
- **Token Management**: Automatic token limit handling and context window management

### Service Structure in .NET 10

**Recommended Project Organization:**
```
src/
├── HRAgent.Api/
│   ├── Controllers/ConversationController.cs  # AG-UI endpoint
│   ├── Agents/
│   │   ├── TriageAgent.cs                     # Intent classification
│   │   ├── ClockInAgent.cs                    # Clock-in logic
│   │   ├── ClockOutAgent.cs                   # Clock-out logic
│   │   └── QueryAgent.cs                      # Status/historical queries
│   ├── Services/
│   │   ├── AgentOrchestrator.cs               # Coordinates agents
│   │   ├── ThreadManager.cs                   # Conversation persistence
│   │   └── FactorialHRService.cs              # External API integration
│   └── Middleware/AgentMiddleware.cs          # Logging, error handling
```

**Dependency Injection Pattern:**
```csharp
// Program.cs
builder.Services.AddSingleton<OpenAIClient>(sp => 
    new OpenAIClient(builder.Configuration["AzureAI:Endpoint"]));
builder.Services.AddScoped<TriageAgent>();
builder.Services.AddScoped<IThreadStore, CosmosDbThreadStore>();
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
```

### Alternatives Considered

1. **Semantic Kernel**: Rejected - maintenance mode, less sophisticated orchestration
2. **LangChain**: Rejected - Python-first, .NET support limited
3. **AutoGen**: Rejected - merged into Agent Framework, no standalone path forward
4. **Custom LLM Integration**: Rejected - would require building conversation management, state persistence, tool calling from scratch

---

## 2. AG-UI Protocol for Frontend-Backend Communication

### Decision: Use AG-UI Protocol for Conversation Interface

AG-UI is an open, event-based protocol designed specifically for agent-to-user communication, standardizing how AI agents connect to user-facing applications.

### Rationale

**Why AG-UI:**
- **Purpose-Built for Agents**: Designed specifically for conversational AI interactions (vs generic REST)
- **Event Streaming**: Real-time message streaming with progressive updates
- **Standardized Events**: 16 event types across 7 categories (messages, tool calls, state updates, etc.)
- **State Synchronization**: Built-in snapshot-delta pattern for efficient state sync
- **Observable Pattern**: Natural fit for React state management with Zustand
- **Transparency**: Shows tool execution and agent reasoning to users

**Protocol Capabilities:**
- **Message Streaming**: `message.start`, `message.content`, `message.end` for progressive display
- **Tool Execution**: `tool_call.start`, `tool_call.end` for visualizing Factorial HR API calls
- **State Updates**: `state.snapshot`, `state.delta` for conversation state sync
- **Progress Indicators**: `activity.start`, `activity.end` for UX feedback during LLM processing

### Message Structure

**Base Event Format:**
```typescript
interface AGUIEvent {
  type: string;           // Event type (e.g., 'message.content')
  timestamp: number;      // Unix timestamp
  rawEvent?: unknown;     // Transport-specific data
}
```

**Message Events:**
```typescript
interface MessageContentEvent extends AGUIEvent {
  type: 'message.content';
  content: string;        // Incremental text chunk
  messageId?: string;     // Message identifier
}

interface MessageEndEvent extends AGUIEvent {
  type: 'message.end';
  messageId: string;
  metadata?: Record<string, unknown>;
}
```

**Tool Call Events:**
```typescript
interface ToolCallStartEvent extends AGUIEvent {
  type: 'tool_call.start';
  toolCallId: string;
  name: string;           // e.g., 'factorial_clock_in'
  input?: unknown;        // Call parameters
}

interface ToolCallEndEvent extends AGUIEvent {
  type: 'tool_call.end';
  toolCallId: string;
  output?: unknown;       // API response
  error?: Error;
}
```

### React Integration with Zustand

**Store Implementation:**
```typescript
interface ConversationStore {
  messages: Message[];
  isStreaming: boolean;
  currentActivity: string | null;
  toolCalls: ToolCall[];
  
  sendMessage: (content: string) => void;
  handleEvent: (event: AGUIEvent) => void;
}

const useConversationStore = create<ConversationStore>((set, get) => ({
  messages: [],
  isStreaming: false,
  currentActivity: null,
  toolCalls: [],
  
  sendMessage: async (content: string) => {
    const event$ = agentClient.send({ role: 'user', content });
    
    event$.subscribe({
      next: (event) => get().handleEvent(event),
      error: (err) => console.error(err)
    });
  },
  
  handleEvent: (event: AGUIEvent) => {
    switch(event.type) {
      case 'message.start':
        set({ isStreaming: true });
        break;
      case 'message.content':
        // Append to current message
        set(state => ({
          messages: appendToLastMessage(state.messages, event.content)
        }));
        break;
      case 'message.end':
        set({ isStreaming: false });
        break;
      case 'tool_call.start':
        // Show "Checking timesheet in Factorial HR..."
        break;
    }
  }
}));
```

**Component Usage:**
```tsx
function ChatInterface() {
  const { messages, isStreaming, sendMessage } = useConversationStore();
  
  return (
    <div>
      {messages.map(msg => <MessageBubble key={msg.id} message={msg} />)}
      {isStreaming && <TypingIndicator />}
      <ChatInput onSend={sendMessage} disabled={isStreaming} />
    </div>
  );
}
```

### Transport Options

**Recommended: HTTP Server-Sent Events (SSE)**
```typescript
const agentClient = new HttpAgent({
  url: 'https://api.example.com/conversation',
  transport: 'sse'  // Server-Sent Events
});
```

**Why SSE:**
- Text-based, easy to debug
- Native browser support with `EventSource`
- Automatic reconnection handling
- Works through firewalls and proxies
- Sufficient for one-way streaming (backend → frontend)

**Alternative: WebSockets**
- Use if bidirectional streaming needed (user interrupting agent)
- More complex setup and connection management
- Requires WebSocket infrastructure in Azure Container Apps

### Backend Implementation (.NET)

**ASP.NET Core Controller:**
```csharp
[ApiController]
[Route("api/conversation")]
public class ConversationController : ControllerBase
{
    [HttpPost]
    public async Task HandleConversation(
        [FromBody] ConversationRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        
        await foreach (var agEvent in GenerateEventsAsync(request, cancellationToken))
        {
            var json = JsonSerializer.Serialize(agEvent);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
    
    async IAsyncEnumerable<AGUIEvent> GenerateEventsAsync(
        ConversationRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new MessageStartEvent { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        
        // Stream LLM response
        await foreach (var chunk in agentOrchestrator.StreamAsync(request.Message, ct))
        {
            yield return new MessageContentEvent 
            { 
                Content = chunk, 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() 
            };
        }
        
        yield return new MessageEndEvent { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
    }
}
```

### Alternatives Considered

1. **Standard REST API**: Rejected - no streaming, no real-time updates, poor UX for conversations
2. **GraphQL Subscriptions**: Rejected - more complex than needed, overkill for chat interface
3. **SignalR**: Rejected - .NET-specific, AG-UI is protocol-agnostic and standardized
4. **Custom WebSocket Protocol**: Rejected - reinventing the wheel, AG-UI provides standard events

---

## 3. Aspire 13 for Service Orchestration

### Decision: Use .NET Aspire 13 for Orchestrating Distributed Application

Aspire 13 provides cloud-native orchestration for .NET applications, managing services, databases, storage, and monitoring in both local development and production environments.

### Rationale

**Why Aspire 13:**
- **Simplified Orchestration**: Single AppHost project coordinates all services
- **Service Discovery**: Automatic DNS resolution and endpoint management
- **Configuration Management**: Centralized config with secrets support
- **Observability**: Built-in OpenTelemetry integration with Application Insights
- **Local Development**: Docker container orchestration for MongoDB, Azurite
- **Azure Deployment**: Direct deployment to Azure Container Apps with generated Bicep
- **.NET 10 Alignment**: Official .NET stack, first-class C# support

### AppHost Structure

**Core Orchestration Project:**
```csharp
// src/AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Databases
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume()
    .AddDatabase("conversations");

var cosmosdb = builder.AddAzureCosmosDB("cosmosdb")
    .AddDatabase("hrapp");

// Storage
var storage = builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("audit-logs");

// Monitoring
var appInsights = builder.AddApplicationInsights("monitoring");

// Backend API
var api = builder.AddProject<Projects.HRAgent_Api>("api")
    .WithReference(mongodb.Resource)      // Local dev
    .WithReference(cosmosdb)              // Production
    .WithReference(blobs)
    .WithReference(appInsights)
    .WithEnvironment("FactorialHR__ApiKey", builder.Configuration["FactorialHR:ApiKey"]);

// Frontend
var frontend = builder.AddNpmApp("frontend", "../frontend")
    .WithReference(api)
    .WithHttpEndpoint(port: 5173, name: "vite");

builder.Build().Run();
```

### Local Development Setup

**Docker Compose for Development Dependencies:**
```yaml
# docker-compose.dev.yml
services:
  mongodb:
    image: mongo:7
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_DATABASE: hrapp

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - "10000:10000"  # Blob
      - "10001:10001"  # Queue
      - "10002:10002"  # Table
    volumes:
      - azurite_data:/data

volumes:
  mongodb_data:
  azurite_data:
```

**Starting Local Environment:**
```bash
# Terminal 1: Start dependencies
docker-compose -f docker-compose.dev.yml up

# Terminal 2: Run Aspire app
cd src/AppHost
dotnet run

# Access Aspire Dashboard: http://localhost:15000
```

### Service-to-Service Communication

**HTTP with Service Discovery:**
```csharp
// HRAgent.Api - consuming Factorial HR service
public class FactorialHRService
{
    private readonly HttpClient _httpClient;
    
    public FactorialHRService(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("factorial");
    }
    
    public async Task<ClockInResult> ClockInAsync(int employeeId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/timesheets/clock-in",
            new { employee_id = employeeId, timestamp = DateTime.UtcNow }
        );
        return await response.Content.ReadFromJsonAsync<ClockInResult>();
    }
}

// Program.cs - register with resilience
builder.Services.AddHttpClient("factorial", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FactorialHR:BaseUrl"]);
    client.DefaultRequestHeaders.Add("Authorization", 
        $"Bearer {builder.Configuration["FactorialHR:ApiKey"]}");
})
.AddStandardResilienceHandler(); // Retry, circuit breaker, timeout
```

### Configuration and Secrets Management

**Local Development (User Secrets):**
```bash
# Set user secrets
dotnet user-secrets set "AzureAI:Endpoint" "https://myai.openai.azure.com"
dotnet user-secrets set "FactorialHR:ApiKey" "sk-..."
```

**AppHost Configuration:**
```csharp
// External parameters for deployment
var factorialApiKey = builder.AddParameter("factorial-api-key", secret: true);

var api = builder.AddProject<Projects.HRAgent_Api>("api")
    .WithEnvironment("FactorialHR__ApiKey", factorialApiKey);
```

**Azure Production (Key Vault):**
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://myvault.vault.azure.net/"),
    new DefaultAzureCredential()
);
```

### Application Insights Integration

**Aspire Service Defaults:**
```csharp
// src/HRAgent.ServiceDefaults/Extensions.cs
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Microsoft.AgentFramework"))
        .UseOtlpExporter();
    
    builder.Services.AddApplicationInsightsTelemetry();
    
    return builder;
}
```

**Custom Telemetry:**
```csharp
public class AgentOrchestrator
{
    private readonly ActivitySource _activitySource = new("HRAgent");
    
    public async Task<AgentResponse> ProcessAsync(string message)
    {
        using var activity = _activitySource.StartActivity("ProcessConversation");
        activity?.SetTag("message.length", message.Length);
        
        // Process with Agent Framework
        var response = await _triageAgent.RunAsync(message);
        
        activity?.SetTag("response.type", response.Type);
        return response;
    }
}
```

**Query Application Insights:**
```kusto
// P95 response time for clock-in operations
requests
| where name == "POST /api/conversation"
| where customDimensions.intent == "clock-in"
| summarize percentile(duration, 95) by bin(timestamp, 5m)
```

### Deployment to Azure Container Apps

**Deploy Command:**
```bash
# Authenticate to Azure
az login

# Deploy with Aspire
azd init
azd up

# Or direct deploy
dotnet publish --os linux --arch x64 -c Release
az containerapp up \
  --name hrapp \
  --resource-group rg-hrapp \
  --environment-variables ASPIRE_CONFIG=production
```

**Generated Bicep Infrastructure:**
```bicep
// infra/azure/main.bicep
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: 'cosmos-${resourceToken}'
  location: location
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [{ name: 'EnableServerless' }]
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-hrapp'
  location: location
  properties: {
    environmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      secrets: [
        { name: 'factorial-api-key', value: factorialApiKey }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registry.properties.loginServer}/hrapp:latest'
          resources: { cpu: 1, memory: '2Gi' }
        }
      ]
    }
  }
}
```

### Best Practices for .NET 10

1. **Project Structure**:
   - `AppHost` - Orchestration
   - `ServiceDefaults` - Shared observability config
   - Feature projects (`HRAgent.Api`, etc.)
   - Shared contracts library

2. **Service Defaults Pattern**:
   ```csharp
   // Every service starts with
   var builder = WebApplication.CreateBuilder(args);
   builder.AddServiceDefaults(); // Logging, metrics, health checks
   ```

3. **Resource Management**:
   - Use `.WithDataVolume()` for persistent data
   - Use `.WithDataBindMount()` for local dev
   - Always specify health checks: `.WithHealthCheck()`

4. **Secrets**:
   - Never commit secrets to AppHost
   - Use `AddParameter(secret: true)` for deployment params
   - Local: user secrets
   - Production: Azure Key Vault

5. **Performance**:
   - Enable HTTP/2: `.WithHttpsEndpoint()`
   - Use `.AddStandardResilienceHandler()` for all HTTP clients
   - Monitor with Application Insights custom metrics

### Alternatives Considered

1. **Docker Compose Only**: Rejected - no service discovery, manual config management, no Azure integration
2. **Kubernetes**: Rejected - over-engineered for initial deployment, higher operational overhead
3. **Azure Service Fabric**: Rejected - legacy, Aspire + Container Apps is modern path
4. **Manual Azure Resource Management**: Rejected - Aspire automates infrastructure, reduces errors

---

## 4. Data Storage Strategy

### Decision: Multi-Storage Approach (Cosmos DB + Blob Storage)

Use Azure Cosmos DB (DocumentDB API) for conversation history and Azure Blob Storage for immutable audit logs, with MongoDB and Azurite emulators for local development.

### Rationale

**Why Cosmos DB (DocumentDB API):**
- **JSON Document Model**: Natural fit for conversation threads (nested messages, metadata)
- **Global Distribution**: Low-latency access for distributed workforce
- **Automatic Indexing**: Query conversations by employee ID, date, session ID
- **TTL Support**: Automatically expire old conversations per retention policy
- **Serverless Tier**: Cost-effective for initial deployment, scales automatically

**Why Blob Storage for Audit Logs:**
- **Immutable Writes**: Append-only logs for compliance
- **Cost-Effective**: Cheaper than database for large log volumes
- **Retention Policies**: Automatic archiving to cool/archive tiers
- **Compliance**: WORM (Write Once Read Many) support for regulatory requirements

**Why MongoDB for Development:**
- **API Compatibility**: DocumentDB API mirrors MongoDB, easy local dev
- **Docker Image**: Simple setup with docker-compose
- **Parity**: Data models work identically in dev and prod

**Why Azurite for Development:**
- **Official Emulator**: Microsoft's local Azure Storage emulator
- **Full API Compatibility**: Blob, Queue, Table support
- **No Cloud Costs**: Free local development

### Data Models

**Conversation Thread (Cosmos DB):**
```csharp
public class ConversationThread
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; }
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; }
    
    [JsonPropertyName("messages")]
    public List<ConversationMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("state")]
    public ConversationState State { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; } = 2592000; // 30 days
}

public class ConversationMessage
{
    public string Id { get; set; }
    public string Role { get; set; } // "user" | "assistant"
    public string Content { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ConversationState
{
    public bool IsClockedIn { get; set; }
    public DateTimeOffset? LastClockIn { get; set; }
    public string? LastIntent { get; set; }
}
```

**Audit Log Entry (Blob Storage):**
```csharp
public class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EmployeeId { get; set; }
    public string Action { get; set; } // "clock-in", "clock-out", "query"
    public DateTimeOffset Timestamp { get; set; }
    public object RequestData { get; set; }
    public object ResponseData { get; set; }
    public string SourceIp { get; set; }
    public string UserAgent { get; set; }
}

// Blob path: audit-logs/{yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json
```

### Container/Collection Structure

**Cosmos DB Containers:**
- **conversations**: Partitioned by `employeeId` for efficient queries
  - Index: `/employeeId`, `/sessionId`, `/createdAt`
  - TTL: Enabled (30 days default)

**Blob Storage Containers:**
- **audit-logs**: Hierarchical namespace enabled
  - Path structure: `{yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json`
  - Lifecycle policy: Move to cool tier after 90 days, archive after 1 year

### Alternatives Considered

1. **SQL Database**: Rejected - JSON conversations awkward in relational model, Cosmos DB better fit
2. **Single Cosmos DB for Everything**: Rejected - audit logs are write-heavy, Blob is more cost-effective
3. **Redis for Conversations**: Rejected - need durable persistence, not just caching
4. **Table Storage**: Rejected - limited query capabilities vs Cosmos DB

---

## 5. React Frontend Stack

### Decision: Vite + React + shadcn/ui + Zustand

Modern React stack optimized for performance, developer experience, and production-ready UI components.

### Rationale

**Why Vite:**
- **Fast Dev Server**: ESM-based, instant HMR (Hot Module Replacement)
- **Optimized Builds**: Rollup-based production builds with code splitting
- **TypeScript Native**: First-class TypeScript support, no additional config
- **Modern Standards**: Leverages native ES modules, faster than Webpack/CRA

**Why shadcn/ui:**
- **Copy-Paste Components**: Components copied into project, full control
- **Radix UI Primitives**: Accessible, keyboard navigable, ARIA compliant
- **Tailwind Styling**: Utility-first CSS, easy customization
- **No Runtime Dependency**: Unlike UI libraries, no package bloat
- **Pre-built Patterns**: Chat interfaces, forms, modals out-of-the-box

**Why Zustand:**
- **Minimal API**: Simple `create()` function, no boilerplate
- **Hook-Based**: Natural React integration with `useStore()` hook
- **Performance**: Only re-renders components using changed state slices
- **Middleware**: Built-in persistence, devtools, immer support
- **No Context Providers**: Avoids React Context performance pitfalls

### Project Setup

**Vite Configuration:**
```typescript
// vite.config.ts
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',  // Aspire API endpoint
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor': ['react', 'react-dom'],
          'agui': ['@ag-ui/client'],
          'ui': ['@radix-ui/react-dialog', '@radix-ui/react-dropdown-menu']
        }
      }
    }
  }
});
```

**shadcn/ui Setup:**
```bash
# Initialize shadcn/ui
npx shadcn-ui@latest init

# Add chat-related components
npx shadcn-ui@latest add button
npx shadcn-ui@latest add card
npx shadcn-ui@latest add input
npx shadcn-ui@latest add scroll-area
npx shadcn-ui@latest add avatar
```

**Zustand Store Structure:**
```typescript
// src/store/conversationStore.ts
interface ConversationStore {
  // State
  messages: Message[];
  isStreaming: boolean;
  currentActivity: string | null;
  error: Error | null;
  
  // Actions
  sendMessage: (content: string) => Promise<void>;
  clearConversation: () => void;
  handleAGUIEvent: (event: AGUIEvent) => void;
}

export const useConversationStore = create<ConversationStore>()(
  persist(
    (set, get) => ({
      messages: [],
      isStreaming: false,
      currentActivity: null,
      error: null,
      
      sendMessage: async (content: string) => {
        const newMessage: Message = {
          id: crypto.randomUUID(),
          role: 'user',
          content,
          timestamp: Date.now()
        };
        
        set(state => ({ messages: [...state.messages, newMessage] }));
        
        // AG-UI integration
        const agentClient = getAGUIClient();
        const event$ = agentClient.send({ role: 'user', content });
        
        event$.subscribe({
          next: (event) => get().handleAGUIEvent(event),
          error: (err) => set({ error: err, isStreaming: false })
        });
      },
      
      clearConversation: () => set({ messages: [], error: null }),
      
      handleAGUIEvent: (event: AGUIEvent) => {
        // See AG-UI section for event handling
      }
    }),
    {
      name: 'conversation-storage',
      partialize: (state) => ({ messages: state.messages })
    }
  )
);
```

### Component Structure

```
frontend/src/
├── components/
│   ├── chat/
│   │   ├── ChatInterface.tsx         # Main conversation UI
│   │   ├── MessageList.tsx           # Scrollable message list
│   │   ├── MessageBubble.tsx         # Individual message display
│   │   ├── ChatInput.tsx             # Message input with send button
│   │   ├── TypingIndicator.tsx      # Streaming indicator
│   │   └── ToolCallDisplay.tsx       # Show Factorial HR API calls
│   ├── timesheet/
│   │   ├── TimesheetCard.tsx         # Display timesheet data
│   │   ├── ClockStatus.tsx           # Current clock-in status
│   │   └── HistoricalView.tsx        # Past timesheets
│   └── ui/                           # shadcn/ui components
│       ├── button.tsx
│       ├── card.tsx
│       └── ...
```

### Alternatives Considered

1. **Create React App**: Rejected - deprecated, slow builds, over-configured
2. **Next.js**: Rejected - overkill for SPA, don't need SSR/SSG
3. **Material-UI**: Rejected - heavy bundle size, less customizable than shadcn
4. **Redux**: Rejected - excessive boilerplate, Zustand simpler for conversation state
5. **Ant Design**: Rejected - opinionated styling, harder to customize

---

## 6. Performance and Scalability Patterns

### Decisions Summary

**Caching Strategy:**
- Redis cache for Factorial HR responses (5-minute TTL for clock-in status)
- Browser cache for AG-UI event history
- Cosmos DB query caching in .NET (MemoryCache)

**Query Optimization:**
- Partition by `employeeId` in Cosmos DB
- Composite indexes: `[employeeId, createdAt]`, `[sessionId, updatedAt]`
- Limit conversation thread queries to last 30 days

**Connection Pooling:**
- HTTP client factory with connection pooling for Factorial HR API
- Cosmos DB client reuse (singleton registration)
- SignalR connection management for AG-UI WebSockets

**Scalability Targets Met:**
- 100 concurrent conversations: Azure Container Apps auto-scaling (10-100 replicas)
- <200ms timesheet submission: Cosmos DB single-digit ms latency
- <2s status query: Indexed queries + caching
- <5s historical query: Pagination + lazy loading

---

## 7. Testing Strategy

### Test Levels

**Unit Tests (xUnit):**
- Intent classification logic
- Conversation state management
- Timesheet validation rules
- AG-UI event handlers

**Integration Tests (xUnit + Testcontainers):**
- Cosmos DB conversation persistence
- Blob Storage audit logging
- Agent Framework orchestration flows

**Contract Tests (Pact/WireMock):**
- Factorial HR API contract verification
- Mock external API responses
- Validate request/response schemas

**E2E Tests (Playwright):**
- Complete conversation flows (clock-in/out)
- Multi-turn conversations
- Error scenarios (API failures)
- Performance benchmarks

### TDD Workflow (Constitution Requirement)

1. **Write Test First** (for each user story acceptance criteria)
2. **Run Test** (verify it fails)
3. **Implement Minimum Code** (make test pass)
4. **Refactor** (improve design)
5. **Repeat** for next acceptance criterion

---

## Conclusion

All technical decisions documented. Key technologies:
- **Microsoft Agent Framework** for conversational AI (not Semantic Kernel)
- **AG-UI Protocol** for frontend-backend communication
- **Aspire 13** for service orchestration
- **Cosmos DB + Blob Storage** for multi-storage architecture
- **Vite + React + shadcn + Zustand** for frontend

Next phase: Generate data models and API contracts based on these architectural decisions.

---

**Research Status**: ✅ Complete  
**Constitution Compliance**: ✅ All clarifications resolved  
**Ready for Phase 1**: ✅ Yes
