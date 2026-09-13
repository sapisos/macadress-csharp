using System.Net.Http;

namespace Macadress.Tests;

/// <summary>Intercepts requests in-process so tests don't need a real server or network access.</summary>
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }

    public static MacadressClient NewClient(string apiKey, Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeHandler handler)
    {
        handler = new FakeHandler(respond);
        var http = new HttpClient(handler);
        return new MacadressClient(apiKey, new MacadressClientOptions { BaseUrl = "http://fake.local", HttpClient = http });
    }
}
