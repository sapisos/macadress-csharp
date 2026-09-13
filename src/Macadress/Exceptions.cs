using System.Net;

namespace Macadress;

/// <summary>The well-known API failure modes. Check <see cref="ApiException.Kind"/> against these.</summary>
public enum ApiErrorKind
{
    /// <summary>Unclassified failure; inspect <see cref="ApiException.StatusCode"/> directly.</summary>
    Other,
    /// <summary>HTTP 400: the address or batch body did not parse.</summary>
    InvalidMac,
    /// <summary>HTTP 401: the API key is missing or invalid.</summary>
    Auth,
    /// <summary>HTTP 429 from the per-minute rate limiter, or a spent quota.</summary>
    RateLimited,
    /// <summary>HTTP 429 where the billing-cycle quota is spent.</summary>
    Quota,
}

/// <summary>
/// Thrown when the API responds with a non-2xx status, or with a body the
/// client cannot read. Check <see cref="Kind"/> for the well-known failure
/// modes rather than switching on <see cref="StatusCode"/> directly.
/// </summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? RequestId { get; }
    /// <summary>The Retry-After header, or null if absent.</summary>
    public TimeSpan? RetryAfter { get; }
    /// <summary>The decoded JSON error body, when the API sent one.</summary>
    public IReadOnlyDictionary<string, object?>? Body { get; }
    public ApiErrorKind Kind { get; }

    public ApiException(
        HttpStatusCode statusCode,
        string message,
        ApiErrorKind kind = ApiErrorKind.Other,
        string? requestId = null,
        TimeSpan? retryAfter = null,
        IReadOnlyDictionary<string, object?>? body = null)
        : base(FormatMessage(statusCode, message, requestId))
    {
        StatusCode = statusCode;
        RequestId = requestId;
        RetryAfter = retryAfter;
        Body = body;
        Kind = kind;
    }

    private static string FormatMessage(HttpStatusCode statusCode, string message, string? requestId) =>
        requestId is { Length: > 0 }
            ? $"macadress: API error {(int)statusCode}: {message} (request {requestId})"
            : $"macadress: API error {(int)statusCode}: {message}";
}

/// <summary>
/// Wraps a failure to get an HTTP response at all: DNS, connection, TLS,
/// timeout, or a canceled request. The cause is available through
/// <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class MacadressTransportException : Exception
{
    public MacadressTransportException(Exception inner)
        : base($"macadress: transport error: {inner.Message}", inner)
    {
    }
}
