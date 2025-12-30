using System.Security.Claims;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for extracting user information from JWT claims
/// </summary>
public interface IUserClaimsService
{
    string GetEmployeeId(ClaimsPrincipal user);
    string GetUserEmail(ClaimsPrincipal user);
    string GetUserName(ClaimsPrincipal user);
    string? GetUserTimezone(ClaimsPrincipal user);
    string? GetUserLanguage(ClaimsPrincipal user);
}

/// <summary>
/// Implementation of user claims extraction service
/// </summary>
public class UserClaimsService : IUserClaimsService
{
    /// <summary>
    /// Extracts employee ID from custom Entra ID extension attribute
    /// </summary>
    public string GetEmployeeId(ClaimsPrincipal user)
    {
        // Try extension attribute first (custom claim)
        var employeeId = user.FindFirst("extension_EmployeeId")?.Value
            ?? user.FindFirst("employeeId")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(employeeId))
        {
            throw new UnauthorizedAccessException("Employee ID not found in user claims");
        }
        
        return employeeId;
    }
    
    /// <summary>
    /// Extracts user email from standard email claim
    /// </summary>
    public string GetUserEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst("email")?.Value
            ?? throw new UnauthorizedAccessException("Email not found in user claims");
    }
    
    /// <summary>
    /// Extracts user display name
    /// </summary>
    public string GetUserName(ClaimsPrincipal user)
    {
        return user.FindFirst("name")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? "Unknown User";
    }
    
    /// <summary>
    /// Extracts user timezone from custom claim or profile
    /// </summary>
    public string? GetUserTimezone(ClaimsPrincipal user)
    {
        return user.FindFirst("timezone")?.Value
            ?? user.FindFirst("extension_Timezone")?.Value;
    }
    
    /// <summary>
    /// Extracts user language preference from claims
    /// </summary>
    public string? GetUserLanguage(ClaimsPrincipal user)
    {
        return user.FindFirst("language")?.Value
            ?? user.FindFirst("locale")?.Value
            ?? user.FindFirst("extension_Language")?.Value;
    }
}
