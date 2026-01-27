using System.Text.Json;

namespace Agi;

/// <summary>
/// Base exception for AGI SDK errors
/// </summary>
public class AgiException : Exception
{
    /// <summary>
    /// HTTP status code if applicable
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Raw response content
    /// </summary>
    public string? ResponseContent { get; }

    public AgiException(string message) : base(message) { }

    public AgiException(string message, int statusCode, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    public AgiException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Authentication failed (401)
/// </summary>
public class AuthenticationException : AgiException
{
    public AuthenticationException(string message = "Invalid or missing API key")
        : base(message, 401) { }

    public AuthenticationException(string message, string? responseContent)
        : base(message, 401, responseContent) { }
}

/// <summary>
/// Resource not found (404)
/// </summary>
public class NotFoundException : AgiException
{
    public NotFoundException(string message = "Resource not found")
        : base(message, 404) { }

    public NotFoundException(string message, string? responseContent)
        : base(message, 404, responseContent) { }
}

/// <summary>
/// Permission denied (403)
/// </summary>
public class PermissionException : AgiException
{
    public PermissionException(string message = "Permission denied")
        : base(message, 403) { }

    public PermissionException(string message, string? responseContent)
        : base(message, 403, responseContent) { }
}

/// <summary>
/// Rate limit exceeded (429)
/// </summary>
public class RateLimitException : AgiException
{
    /// <summary>
    /// Time to wait before retrying (if provided)
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public RateLimitException(string message = "Rate limit exceeded", TimeSpan? retryAfter = null)
        : base(message, 429)
    {
        RetryAfter = retryAfter;
    }

    public RateLimitException(string message, string? responseContent, TimeSpan? retryAfter = null)
        : base(message, 429, responseContent)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Validation error (422)
/// </summary>
public class ValidationException : AgiException
{
    /// <summary>
    /// Validation errors by field
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; }

    public ValidationException(string message = "Validation failed")
        : base(message, 422) { }

    public ValidationException(string message, Dictionary<string, string[]>? errors, string? responseContent = null)
        : base(message, 422, responseContent)
    {
        Errors = errors;
    }
}

/// <summary>
/// Server error (5xx)
/// </summary>
public class ApiException : AgiException
{
    public ApiException(string message, int statusCode, string? responseContent = null)
        : base(message, statusCode, responseContent) { }
}

/// <summary>
/// Agent execution failed
/// </summary>
public class AgentExecutionException : AgiException
{
    /// <summary>
    /// Session ID where the error occurred
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Step number where the error occurred
    /// </summary>
    public int? Step { get; }

    public AgentExecutionException(string message, string? sessionId = null, int? step = null)
        : base(message)
    {
        SessionId = sessionId;
        Step = step;
    }
}

/// <summary>
/// Timeout exceeded
/// </summary>
public class TimeoutException : AgiException
{
    public TimeoutException(string message = "Operation timed out")
        : base(message) { }

    public TimeoutException(string message, TimeSpan timeout)
        : base($"{message} (timeout: {timeout.TotalSeconds}s)") { }
}

/// <summary>
/// Connection error
/// </summary>
public class ConnectionException : AgiException
{
    public ConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
