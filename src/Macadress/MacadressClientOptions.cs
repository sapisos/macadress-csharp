namespace Macadress;

/// <summary>Configures a <see cref="MacadressClient"/>.</summary>
public sealed class MacadressClientOptions
{
    /// <summary>The API root. Change only for a self-hosted deployment.</summary>
    public string BaseUrl { get; set; } = "https://api.macadress.com";

    /// <summary>
    /// Supplies the <see cref="HttpClient"/> to use. When set, the client
    /// does not own it and will not dispose it; <see cref="Timeout"/> is
    /// ignored since the supplied client's own timeout applies.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>The whole-request timeout, used only when <see cref="HttpClient"/> is not set.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Overrides the User-Agent header.</summary>
    public string? UserAgent { get; set; }
}
