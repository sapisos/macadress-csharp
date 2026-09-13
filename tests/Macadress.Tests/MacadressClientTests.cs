using System.Net;
using System.Net.Http;
using System.Text;

namespace Macadress.Tests;

public class MacadressClientTests
{
    [Fact]
    public async Task Vendor_ReturnsTrimmedPlainText()
    {
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Apple, Inc.\n") },
            out var handler);

        var got = await client.VendorAsync("00:03:93:AB:12:34");

        Assert.Equal("Apple, Inc.", got);
        Assert.Equal("/v1/vendor/00:03:93:AB:12:34", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("Bearer mk_test", handler.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Vendor_NotFound_ReturnsEmptyString()
    {
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not registered") },
            out _);

        var got = await client.VendorAsync("02:1a:2b:3c:4d:5e");

        Assert.Equal("", got);
    }

    [Fact]
    public async Task Vendor_Keyless_SendsNoAuthorizationHeader()
    {
        using var client = FakeHandler.NewClient("", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Apple, Inc.") },
            out var handler);

        await client.VendorAsync("00:03:93:00:00:00");

        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task Vendor_EscapesSpacesKeepsSeparators()
    {
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("x") },
            out var handler);

        var cases = new Dictionary<string, string>
        {
            ["3C-22-FB-11-22-33"] = "/v1/vendor/3C-22-FB-11-22-33",
            ["aabb.ccdd.eeff"] = "/v1/vendor/aabb.ccdd.eeff",
            ["3c 22 fb 11 22 33"] = "/v1/vendor/3c%2022%20fb%2011%2022%2033",
            ["  00:03:93:AB:12:34 "] = "/v1/vendor/00:03:93:AB:12:34",
        };
        foreach (var (input, want) in cases)
        {
            await client.VendorAsync(input);
            Assert.Equal(want, handler.Requests[^1].RequestUri!.PathAndQuery);
        }
    }

    [Fact]
    public async Task Lookup_DecodesResultAndRaw()
    {
        const string body = """
        {
            "mac": "3C:22:FB:11:22:33",
            "valid": true,
            "registered": true,
            "organization": "Apple, Inc.",
            "oui": "3C:22:FB",
            "block_type": "MA-L",
            "country": "US",
            "prefix_length": 24,
            "vendor_lookup_reliable": true,
            "device": {"category": "computer", "confidence": "low"},
            "meta": {"request_id": "req_123", "database_version": "2026-09-01"},
            "future_field": "kept-in-raw"
        }
        """;
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            },
            out var handler);

        var result = await client.LookupAsync("3C:22:FB:11:22:33");

        Assert.True(result.Registered);
        Assert.Equal("Apple, Inc.", result.Organization);
        Assert.Equal(BlockType.MAL, result.BlockType);
        Assert.Equal(DeviceCategory.Computer, result.Device.Category);
        Assert.Equal("req_123", result.Meta.RequestId);
        Assert.Equal("/v1/mac/3C:22:FB:11:22:33", handler.Requests[0].RequestUri!.PathAndQuery);

        Assert.True(result.TryGet("future_field", out var futureField));
        Assert.Equal("kept-in-raw", futureField.GetString());
        Assert.True(result.TryGet("meta.database_version", out var dbVersion));
        Assert.Equal("2026-09-01", dbVersion.GetString());
    }

    [Fact]
    public async Task Batch_DecodesItemsInOrderAndFlagsFailures()
    {
        const string body = """
        {
            "count": 2,
            "results": [
                {"input": "3C:22:FB:11:22:33", "mac": "3C:22:FB:11:22:33", "valid": true, "registered": true, "organization": "Apple, Inc."},
                {"input": "not-a-mac", "error": "invalid MAC address"}
            ]
        }
        """;
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            },
            out _);

        var items = await client.BatchAsync(new[] { "3C:22:FB:11:22:33", "not-a-mac" });

        Assert.Equal(2, items.Count);
        Assert.False(items[0].Failed);
        Assert.Equal("Apple, Inc.", items[0].Organization);
        Assert.True(items[1].Failed);
        Assert.Equal("invalid MAC address", items[1].Error);
    }

    [Fact]
    public async Task Batch_RejectsEmptyOrOversizedInput()
    {
        using var client = FakeHandler.NewClient("mk_test", _ => new HttpResponseMessage(HttpStatusCode.OK), out _);

        await Assert.ThrowsAsync<ArgumentException>(() => client.BatchAsync(Array.Empty<string>()));

        var tooMany = Enumerable.Range(0, MacadressClient.MaxBatchSize + 1).Select(i => i.ToString()).ToArray();
        await Assert.ThrowsAsync<ArgumentException>(() => client.BatchAsync(tooMany));
    }

    [Fact]
    public async Task SearchVendors_BuildsQueryString()
    {
        const string body = """{"total": 1, "blocks": [{"prefix_int": 1, "mask_bits": 24, "block_type": "MA-L", "organization": "Apple, Inc.", "country": "US"}]}""";
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            },
            out var handler);

        var result = await client.SearchVendorsAsync("Apple", country: "US", limit: 5);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Blocks);
        Assert.Equal("Apple, Inc.", result.Blocks[0].Organization);
        Assert.Equal("/v1/vendors?query=Apple&country=US&limit=5", handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Health_ReturnsFalseOnTransportFailure()
    {
        using var client = FakeHandler.NewClient("mk_test", _ => throw new HttpRequestException("connection refused"), out _);

        Assert.False(await client.HealthAsync());
    }

    [Fact]
    public async Task Health_ReturnsTrueOn2xx()
    {
        using var client = FakeHandler.NewClient("mk_test", _ => new HttpResponseMessage(HttpStatusCode.OK), out _);

        Assert.True(await client.HealthAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ApiErrorKind.InvalidMac)]
    [InlineData(HttpStatusCode.Unauthorized, ApiErrorKind.Auth)]
    public async Task Lookup_MapsStatusCodeToErrorKind(HttpStatusCode status, ApiErrorKind wantKind)
    {
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"error": "bad request", "request_id": "req_err"}""", Encoding.UTF8, "application/json"),
            },
            out _);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LookupAsync("00:00:00:00:00:00"));

        Assert.Equal(wantKind, ex.Kind);
        Assert.Equal("req_err", ex.RequestId);
        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task Lookup_RateLimitVsQuota_DistinguishedByMessage()
    {
        using var client = FakeHandler.NewClient("mk_test", _ =>
            new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("""{"error": "monthly quota exceeded"}""", Encoding.UTF8, "application/json"),
            },
            out _);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LookupAsync("00:00:00:00:00:00"));

        Assert.Equal(ApiErrorKind.Quota, ex.Kind);
    }
}
