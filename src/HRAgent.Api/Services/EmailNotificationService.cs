using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for sending email notifications for deletion requests
/// FR-014c: Confirm deletion completion via email
/// </summary>
public class EmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly EmailNotificationOptions _options;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        EmailNotificationOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Send confirmation email that deletion request was submitted
    /// </summary>
    public async Task SendDeletionRequestConfirmationAsync(
        string employeeEmail,
        string employeeName,
        string requestId,
        DateTimeOffset scheduledDeletionDate,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your Data Deletion Request Has Been Received";
        var body = $@"
Dear {employeeName},

Your request to delete your conversation data has been received and is now pending processing.

Request ID: {requestId}
Requested: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
Scheduled Deletion: {scheduledDeletionDate:yyyy-MM-dd}

Per our data retention policy (GDPR compliance), your conversation data will be permanently deleted on {scheduledDeletionDate:MMMM dd, yyyy}.

You have 30 days to cancel this request if you change your mind. To cancel, please contact support with your Request ID: {requestId}

Important Notes:
- Audit logs will be retained for compliance purposes (7 years) even after conversation deletion
- This action cannot be undone once processing is complete
- You will receive a confirmation email when deletion is complete

If you did not submit this request, please contact support immediately.

Thank you,
HR Agent Support Team
";

        await SendEmailAsync(employeeEmail, subject, body, cancellationToken);
    }

    /// <summary>
    /// Send confirmation email that deletion has been completed
    /// </summary>
    public async Task SendDeletionCompletedConfirmationAsync(
        string employeeEmail,
        string employeeName,
        string requestId,
        int conversationsDeleted,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your Data Deletion Has Been Completed";
        var body = $@"
Dear {employeeName},

Your conversation data deletion has been successfully completed.

Request ID: {requestId}
Completed: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
Conversations Deleted: {conversationsDeleted}

Your conversation history has been permanently removed from our systems. Audit logs have been retained for compliance purposes as required by law.

If you have any questions or concerns, please contact support.

Thank you,
HR Agent Support Team
";

        await SendEmailAsync(employeeEmail, subject, body, cancellationToken);
    }

    /// <summary>
    /// Send notification that deletion request was cancelled
    /// </summary>
    public async Task SendDeletionCancelledNotificationAsync(
        string employeeEmail,
        string employeeName,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your Data Deletion Request Has Been Cancelled";
        var body = $@"
Dear {employeeName},

Your data deletion request has been cancelled.

Request ID: {requestId}
Cancelled: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}

Your conversation data will remain in the system and will not be deleted. You can submit a new deletion request at any time through the application settings.

If you did not cancel this request, please contact support immediately.

Thank you,
HR Agent Support Team
";

        await SendEmailAsync(employeeEmail, subject, body, cancellationToken);
    }

    /// <summary>
    /// Send internal email using SMTP
    /// </summary>
    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Email notifications disabled. Would have sent to {Email}: {Subject}", 
                toEmail, subject);
            return;
        }

        try
        {
            using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}: {Subject}", toEmail, subject);
        }
#pragma warning disable CA1031 // Do not catch general exception types - Email failure should not break main process
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
            // Don't throw - email failure should not break deletion process
        }
    }
}

/// <summary>
/// Configuration options for email notifications
/// </summary>
public class EmailNotificationOptions
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = "smtp.office365.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@company.com";
    public string FromName { get; set; } = "HR Agent";
}
