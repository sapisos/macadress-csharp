using System.Net;
using System.Text;
using System.Text.Json;

namespace Macadress;

/// <summary>
/// A macadress.com API client. It is safe for concurrent use. Dispose it
/// when you supplied no <see cref="MacadressClientOptions.HttpClient"/> of
/// your own.
/// </summary>
public sealed class MacadressClient : IDisposable
{
    /// <summary>The most addresses <see cref="BatchAsync"/> will send in one request.</summary>
    public const int MaxBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new BlockTypeConverter(),
            new SnakeCaseEnumConverter<TransmissionType>(),
            new SnakeCaseEnumConverter<AdministrationType>(),
            new SnakeCaseEnumConverter<RandomizationConfidence>(),
            new SnakeCaseEnumConverter<DeviceCategory>(),
        },
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    /// <summary>Creates a client. Pass an empty <paramref name="apiKey"/> for keyless use, where only <see cref="VendorAsync"/> works.</summary>
    public MacadressClient(string apiKey = "", MacadressClientOptions? options = null)
    {
        options ??= new MacadressClientOptions();
        _apiKey = apiKey.Trim();
        _baseUrl = string.IsNullOrEmpty(options.BaseUrl) ? "https://api.macadress.com" : options.BaseUrl.TrimEnd('/');

        if (options.HttpClient is not null)
        {
            _http = options.HttpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _http = new HttpClient { Timeout = options.Timeout };
            _ownsHttpClient = true;
        }

        var version = typeof(MacadressClient).Assembly.GetName().Version;
        var versionText = version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd(
            options.UserAgent ?? $"macadress-csharp/{versionText} (+https://github.com/sapisos/macadress-csharp)");
    }

    /// <summary>The API root this client is configured with.</summary>
    public string BaseUrl => _baseUrl;

    public void Dispose()
    {
        if (_ownsHttpClient) _http.Dispose();
    }

    /// <summary>
    /// Returns the registered organization name for a MAC address as plain
    /// text. It needs no API key.
    /// </summary>
    /// <remarks>
    /// Returns an empty string when the address is valid but has no vendor
    /// to report: an unregistered prefix, a private block, or a locally
    /// administered (usually privacy-randomized) address. Use
    /// <see cref="LookupAsync"/> to tell those cases apart.
    /// </remarks>
    public async Task<string> VendorAsync(string mac, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/vendor/{EscapePathSegment(mac.Trim())}";
        using var response = await SendAsync(HttpMethod.Get, path, cancellationToken: cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return Encoding.UTF8.GetString(bytes).Trim();
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return "";
        }
        throw BuildApiException(response, bytes);
    }

    /// <summary>
    /// Returns the full analysis of one address. It needs an API key. A
    /// result is always returned, registered or not.
    /// </summary>
    public async Task<MacResult> LookupAsync(string mac, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/mac/{EscapePathSegment(mac.Trim())}";
        var bytes = await SendJsonRequestAsync(HttpMethod.Get, path, body: null, cancellationToken).ConfigureAwait(false);
        return HydrateMacResult<MacResult>(bytes);
    }

    /// <summary>
    /// Looks up up to <see cref="MaxBatchSize"/> addresses in one request.
    /// It needs an API key. Results come back in input order; check
    /// <see cref="BatchItem.Failed"/> on each item.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="macs"/> is empty or longer than <see cref="MaxBatchSize"/>.</exception>
    public async Task<List<BatchItem>> BatchAsync(IReadOnlyList<string> macs, CancellationToken cancellationToken = default)
    {
        if (macs.Count == 0)
        {
            throw new ArgumentException("macadress: Batch requires at least one address", nameof(macs));
        }
        if (macs.Count > MaxBatchSize)
        {
            throw new ArgumentException($"macadress: Batch accepts at most {MaxBatchSize} addresses, got {macs.Count}", nameof(macs));
        }

        var bytes = await SendJsonRequestAsync(HttpMethod.Post, "/v1/mac/batch", new { macs }, cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(bytes);
        var items = new List<BatchItem>();
        if (doc.RootElement.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in resultsEl.EnumerateArray())
            {
                var item = JsonSerializer.Deserialize<BatchItem>(el.GetRawText(), JsonOptions)!;
                item.Raw = el.Clone();
                if (el.TryGetProperty("device", out var deviceEl))
                {
                    item.Device.Raw = deviceEl.Clone();
                }
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>
    /// Searches the registered vendor/block directory by organization name
    /// and/or country. It needs an API key and counts as one call against
    /// the plan quota.
    /// </summary>
    public async Task<VendorSearchResult> SearchVendorsAsync(
        string? query = null,
        string? country = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(query)) parts.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrEmpty(country)) parts.Add($"country={Uri.EscapeDataString(country)}");
        if (limit is > 0) parts.Add($"limit={limit.Value}");
        var path = "/v1/vendors" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");

        var bytes = await SendJsonRequestAsync(HttpMethod.Get, path, body: null, cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<VendorSearchResult>(bytes, JsonOptions)!;
        using var doc = JsonDocument.Parse(bytes);
        result.Raw = doc.RootElement.Clone();
        return result;
    }

    /// <summary>
    /// Reports whether the API and its database are reachable. It is
    /// keyless and not counted against any quota. A transport failure
    /// (unreachable, timeout) returns false rather than throwing.
    /// </summary>
    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Get, "/v1/healthz", cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (MacadressTransportException)
        {
            return false;
        }
    }

    /// <summary>
    /// Performs a raw request against any endpoint (for example
    /// "/v1/healthz") and returns the response as-is. A non-2xx status is
    /// not treated as an error; only a transport failure throws.
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(method, path, content, cancellationToken);

    /// <summary>
    /// Percent-encodes a path segment the way Go's <c>url.PathEscape</c>
    /// does: unreserved characters, sub-delims, ":" and "@" stay literal
    /// (all valid unescaped in a path segment per RFC 3986), everything
    /// else - notably a space - is percent-encoded. <see cref="Uri.EscapeDataString"/>
    /// is not used here because it also escapes ":", which would turn a
    /// colon-separated MAC address unreadable in the request path.
    /// </summary>
    private static string EscapePathSegment(string s)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            var c = (char)b;
            if (IsUnescapedPathChar(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
    }

    private static bool IsUnescapedPathChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
        || c is '-' or '.' or '_' or '~' // unreserved
        or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=' // sub-delims
        or ':' or '@'; // pchar extras

    private static T HydrateMacResult<T>(byte[] bytes) where T : MacResult
    {
        var result = JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
        using var doc = JsonDocument.Parse(bytes);
        result.Raw = doc.RootElement.Clone();
        if (doc.RootElement.TryGetProperty("device", out var deviceEl))
        {
            result.Device.Raw = deviceEl.Clone();
        }
        return result;
    }

    private async Task<byte[]> SendJsonRequestAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        HttpContent? content = null;
        if (body is not null)
        {
            content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        using var response = await SendCoreAsync(method, path, content, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildApiException(response, bytes);
        }
        return bytes;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path) { Content = content };
        request.Headers.Accept.TryParseAdd("application/json");
        if (_apiKey.Length > 0)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
        try
        {
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new MacadressTransportException(ex);
        }
    }

    private static ApiException BuildApiException(HttpResponseMessage response, byte[] body)
    {
        var message = $"HTTP {(int)response.StatusCode}";
        string? requestId = response.Headers.TryGetValues("X-Request-Id", out var ids) ? ids.FirstOrDefault() : null;
        var retryAfter = response.Headers.RetryAfter?.Delta;
        IReadOnlyDictionary<string, object?>? bodyDict = null;

        var text = Encoding.UTF8.GetString(body).Trim();
        if (text.Length > 0 && (text[0] == '{' || text[0] == '['))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.Clone();
                    }
                    bodyDict = dict;

                    if (doc.RootElement.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.String)
                    {
                        message = errEl.GetString() ?? message;
                    }
                    if (requestId is null && doc.RootElement.TryGetProperty("request_id", out var ridEl) && ridEl.ValueKind == JsonValueKind.String)
                    {
                        requestId = ridEl.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Not a JSON body; fall through to the raw-text message below.
            }
        }
        else if (text.Length > 0)
        {
            message = text;
        }

        var kind = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ApiErrorKind.InvalidMac,
            HttpStatusCode.Unauthorized => ApiErrorKind.Auth,
            (HttpStatusCode)429 when message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0 => ApiErrorKind.Quota,
            (HttpStatusCode)429 => ApiErrorKind.RateLimited,
            _ => ApiErrorKind.Other,
        };

        return new ApiException(response.StatusCode, message, kind, requestId, retryAfter, bodyDict);
    }
}
