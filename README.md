# macadress-csharp

Official .NET client for the [macadress.com](https://macadress.com) MAC address and
OUI vendor lookup API.

- Vendor name, OUI, IEEE block, country, address type, EUI-64 / IPv6 link-local, randomization confidence, device guess
- Keyless vendor-name lookup, plus keyed single / batch / directory-search calls
- Typed results with a `Raw` (`JsonElement`) escape hatch, typed `ApiException` / `MacadressTransportException`
- `CancellationToken` on every call, `netstandard2.0` (works on .NET Framework 4.6.1+, .NET Core 2+, .NET 5+)

```csharp
using Macadress;

var mac = new MacadressClient("mk_live_xxx");

var name = await mac.VendorAsync("00:03:93:AB:12:34"); // "Apple, Inc."   (no API key required)
var res = await mac.LookupAsync("00:03:93:AB:12:34");  // res.Country == "US"
```

## Install

```bash
dotnet add package Macadress
```

## Getting a key

`VendorAsync` needs no key. Everything else does. A free key (1,000 lookups a day) is
instant at [macadress.com/signup](https://macadress.com/signup); see
[pricing](https://macadress.com/pricing) for more.

## Usage

### Create a client

```csharp
var mac = new MacadressClient("mk_live_xxx");

// keyless: only VendorAsync will work
var anon = new MacadressClient();

// options
var mac = new MacadressClient("mk_live_xxx", new MacadressClientOptions
{
    BaseUrl = "https://api.macadress.com", // change only for a self-hosted deployment
    Timeout = TimeSpan.FromSeconds(10),
});
```

`MacadressClient` is safe for concurrent use. Dispose it unless you supplied your own `HttpClient`.

### `VendorAsync` - name only, no key

Returns an empty string when the address is valid but has no vendor to report
(unregistered, private, or locally administered / randomized).

```csharp
await mac.VendorAsync("00:03:93:AB:12:34");   // "Apple, Inc."
await mac.VendorAsync("02:1a:2b:3c:4d:5e");   // ""
```

`:`, `-`, `.` and space grouping are all accepted, as is a bare 12-hex string.

### `LookupAsync` - full analysis

```csharp
var r = await mac.LookupAsync("3C:22:FB:12:34:56");

r.Organization;             // string?
r.VendorLookupReliable;     // bool  (false for a private block / LAA)
r.Oui;                      // "3C:22:FB"
r.MatchedPrefix;            // full matched block at its real width
r.BlockType;                // BlockType.MAL, etc.
r.Country;                  // "US"
r.AdministrationType;       // AdministrationType.UniversallyAdministered | LocallyAdministered
r.PotentiallyRandomized;    // bool
r.RandomizationConfidence;  // RandomizationConfidence.None | Possible | Likely
r.Eui64;                    // "3E:22:FB:FF:FE:12:34:56"
r.Ipv6LinkLocal;            // "fe80::3e22:fbff:fe12:3456"
r.Device.Category;          // DeviceCategory.Unknown (usually)
r.Explanation;               // plain-English summary
r.Meta.DatabaseVersion;     // "2026-08-30"
```

Any field the type does not cover is still reachable:

```csharp
r.TryGet("vendor_location.city", out var city); // (bool, JsonElement)
r.Raw;                                          // JsonElement, the decoded payload
```

### `BatchAsync` - up to 100 at once

Results come back in input order; check each item.

```csharp
var items = await mac.BatchAsync(new[] { "00:03:93:00:00:00", "3C:22:FB:00:00:00", "bad" });
foreach (var it in items)
{
    Console.WriteLine(it.Failed ? $"{it.Input} -> ERROR {it.Error}" : $"{it.Input} -> {it.Organization}");
}
```

`BatchAsync` throws `ArgumentException` without making a request if the list is empty
or longer than `MacadressClient.MaxBatchSize` (100).

### `SearchVendorsAsync` - the directory

```csharp
var res = await mac.SearchVendorsAsync("Cisco", country: "US", limit: 20);

res.Total; // total matches, ignoring the limit
foreach (var b in res.Blocks)
{
    Console.WriteLine($"{b.BlockType} {b.Organization} ({b.Country})");
}
```

### `HealthAsync`

```csharp
await mac.HealthAsync(); // bool; a transport failure is false, not an exception
```

## Errors

```csharp
try
{
    var r = await mac.LookupAsync(input);
}
catch (ApiException ex) when (ex.Kind == ApiErrorKind.RateLimited)
{
    await Task.Delay(ex.RetryAfter ?? TimeSpan.FromSeconds(1));
}
catch (ApiException ex) when (ex.Kind == ApiErrorKind.InvalidMac)
{
    // caller mistake, never billed
}
catch (MacadressTransportException)
{
    // never reached the API
}
```

| Kind / type | When |
|---|---|
| `ApiErrorKind.InvalidMac` | HTTP 400, the input did not parse |
| `ApiErrorKind.Auth` | HTTP 401, missing or invalid API key |
| `ApiErrorKind.RateLimited` | HTTP 429, per-minute rate exceeded |
| `ApiErrorKind.Quota` | HTTP 429, billing-cycle quota spent |
| `ApiException` | any non-2xx: carries `StatusCode`, `Message`, `RequestId`, `RetryAfter`, `Body` |
| `MacadressTransportException` | never reached the API: DNS, connection, TLS, timeout, canceled request |

## Development

```bash
dotnet build Macadress.slnx
dotnet test Macadress.slnx
```

## Links

- API reference: <https://macadress.com/docs>
- NuGet package: <https://www.nuget.org/packages/Macadress>
- Issues: <https://github.com/sapisos/macadress-csharp/issues>

## License

MIT, see [LICENSE](LICENSE). A product of [ApisOS FZE](https://apisos.com).
