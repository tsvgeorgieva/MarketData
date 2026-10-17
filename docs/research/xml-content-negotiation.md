# XML content negotiation in ASP.NET Core 10

Issue: tsvgeorgieva/MarketData#11. Primary sources only (Microsoft Learn, dotnet/aspnetcore and dotnet/runtime `main`).
Items marked **UNVERIFIED** were not confirmed in a primary source and need a spike test.

## Short answer

- Controllers: `AddControllers().AddXmlSerializerFormatters()` gives JSON and XML selected by `Accept`. [1]
- Minimal APIs: no built-in content negotiation. `TypedResults.Ok` and friends write JSON only. XML needs a custom `IResult` (or `Results.Content`) plus your own `Accept` check. [4][5]
- Recommendation: use controllers for endpoints that must return JSON and XML, with `XmlSerializer` formatters and `ReturnHttpNotAcceptable = true`. If minimal APIs are required, build one reusable negotiating `IResult` (section 4).

## 1. Controllers

### Enabling XML
```csharp
builder.Services.AddControllers(o => o.ReturnHttpNotAcceptable = true)
    .AddXmlSerializerFormatters();          // or AddXmlDataContractSerializerFormatters()
```
- Microsoft Learn: "controller methods return the appropriate format based on the request's `Accept` header". [1]
- Both extension methods exist, with and without an `Action<MvcXmlOptions>` overload. [2]
- The setup class `Add`s (appends) the XML formatters to `MvcOptions.InputFormatters`/`OutputFormatters`, so the built-in JSON formatter stays first. It also maps the `xml` format to `application/xml` unless already mapped, and adds a `ProblemDetailsWrapperProviderFactory`. [3]
- XML output formatters default to media types `application/xml`, `text/xml` and `application/*+xml`, encodings UTF-8 and UTF-16. [6][7]

### Selection algorithm (`DefaultOutputFormatterSelector`) [8]
1. Parse `Accept`, sorted by quality.
2. If it contains `*/*` and `RespectBrowserAcceptHeader` is false (default), the list is cleared (treated as no header). [1][8]
3. With a non-empty list: first formatter able to write one of the accepted types, in quality order.
4. If nothing matched and `ReturnHttpNotAcceptable` is false, `Accept` is ignored and the first formatter that can write the object wins (JSON by default). [1][8]
5. Otherwise the selector returns `null`.

Consequences:
- No `Accept`, or `Accept: */*`, returns JSON (first registered formatter). [1][8]
- `Accept: application/xml` returns XML.
- `Accept: application/yaml` returns JSON unless `ReturnHttpNotAcceptable = true`. [1]

### 406 behaviour
- Docs: 406 is returned only when `MvcOptions.ReturnHttpNotAcceptable` is true. [1]
- Source: the selector only returns `null`; `ObjectResultExecutor` sets `Status406NotAcceptable` and, if an `IProblemDetailsService` is registered, writes a problem-details response; otherwise the body is empty. [8][9]
- Exact body for an unsatisfiable `Accept`: **UNVERIFIED**, test it.

### Special cases [1]
- `string` return values use `StringOutputFormatter` (text/plain), not XML. Return objects, not bare strings.
- `null` results return 204 via `HttpNoContentOutputFormatter`. Without it the XML formatter emits an empty element with `xsi:nil="true"`.
- `.xml` URL suffix / `[FormatFilter]` can force a format (the `xml` mapping above). [1][3]

## 2. XmlSerializer vs DataContractSerializer

| | `XmlSerializer` (`AddXmlSerializerFormatters`) | `DataContractSerializer` (`AddXmlDataContractSerializerFormatters`) |
|---|---|---|
| Members | Public read/write properties and fields; needs public parameterless ctor [10] | Public types with parameterless ctor, `[DataContract]` types, `[Serializable]` types [11]; non-public data possible [10] |
| Attributes | `XmlElement`, `XmlAttribute`, `XmlRoot`, `XmlArray` etc. [10] | Does not support `XmlElement`/`XmlAttribute` [11] |
| Read-only props | Skipped, except read-only collections [10] | Not covered by the sources read |
| Interfaces | Not covered by the sources read | Interface types treated as Object, or as collections for collection interfaces [11] |
| Unsupported type | Formatter catches the exception when building the serializer, logs a warning, `CanWriteType` returns false, so selection moves on (JSON or 406) [6] | Same pattern [7] |

Guidance: prefer `XmlSerializer` for DTO-shaped responses; attributes give control over element names and namespaces, and output is XSD-typed. [10] Default namespaces emitted by `DataContractSerializer`: **UNVERIFIED** in sources read; confirm in a spike.

Learn security note: `XmlSerializer` generates and compiles serialization code at runtime; never serialize unknown or untrusted types. [10]

## 3. decimal, DateTimeOffset, collections

- `decimal`: primitive in `XmlSerializer`, mapped to `xsd:decimal`. [12] Also a primitive for `DataContractSerializer`. [11] Culture-invariant text: **UNVERIFIED**, assert in a test.
- `DateTimeOffset`: registered in `XmlSerializer` as a non-XSD primitive mapped to `xsd:dateTime`. [12] For `DataContractSerializer` it is a primitive in XML form but "is not a known type by default" (matters when typed as `object`/polymorphic). [11] Exact output text (ISO 8601 with offset expected): verify in a test.
- Collections: `XmlSerializer` serializes arrays and `IEnumerable`/`ICollection` classes; an `IEnumerable` class needs a public `Add` method. [10] The formatters wrap enumerable return types via `EnumerableWrapperProviderFactory` in `WrapperProviderFactories`, and buffer `IAsyncEnumerable` into a materialised collection first. [6][7] Root element name for a bare `IEnumerable<T>`: **UNVERIFIED**; prefer a named response wrapper (`PricesResponse { List<PriceDto> Items }`) with `[XmlRoot]`/`[XmlArray]`/`[XmlArrayItem]` for stable XML.
- DTO constraints for XmlSerializer: public class, public parameterless ctor, public get/set. [10] Positional records without a parameterless ctor do not qualify; `init` setters: **UNVERIFIED**. Consider separate XML-facing DTOs.

## 4. Minimal APIs

- The Learn minimal API responses page covers `Results`/`TypedResults` for JSON, text, file, stream and status results and does not describe content negotiation or XML. [4]
- Source: `HttpResultsHelper.WriteResultAsJsonAsync` serializes with System.Text.Json via `Response.WriteAsJsonAsync`; no formatter lookup. [5]
- So `Accept: application/xml` against `TypedResults.Ok(dto)` still returns JSON. `AddXmlSerializerFormatters` only registers into `MvcOptions`. [3][5]

Pattern if minimal APIs are used (design suggestion, not from a single doc):
```csharp
public sealed class Negotiated<T>(T value) : IResult
{
    public Task ExecuteAsync(HttpContext ctx)
    {
        var accept = ctx.Request.GetTypedHeaders().Accept;
        // pick by quality: application/xml|text/xml -> XmlSerializer
        //                  application/json|*/*|none -> JSON
        //                  else -> 406
    }
}
```
Keep it in one shared helper so every endpoint behaves identically.

## 5. Pitfalls checklist

1. Without `ReturnHttpNotAcceptable = true`, unsupported `Accept` values silently return JSON. [1][8]
2. `*/*` (curl, browsers) is ignored by default and yields JSON; `RespectBrowserAcceptHeader` changes that. [1]
3. XML formatters are appended after JSON, so XML is never the default. [3]
4. Return objects, not `string`; strings go to `StringOutputFormatter`. [1]
5. Missing parameterless ctor, non-public or read-only members cause missing data or `CanWriteType` false and a silent JSON fallback (warning log only). [6][10]
6. `AddXml*Formatters` also registers the XML input formatter. [3]
7. Minimal API endpoints bypass all of this.
8. Test with real `Accept` headers via `WebApplicationFactory`: assert content type, 406, and exact `decimal`/`DateTimeOffset` text.

## Sources
1. https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting?view=aspnetcore-10.0
2. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Formatters.Xml/src/DependencyInjection/MvcXmlMvcBuilderExtensions.cs
3. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Formatters.Xml/src/DependencyInjection/XmlSerializerMvcOptionsSetup.cs
4. https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0
5. https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Results/src/HttpResultsHelper.cs
6. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Formatters.Xml/src/XmlSerializerOutputFormatter.cs
7. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Formatters.Xml/src/XmlDataContractSerializerOutputFormatter.cs
8. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/DefaultOutputFormatterSelector.cs
9. https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ObjectResultExecutor.cs
10. https://learn.microsoft.com/en-us/dotnet/standard/serialization/introducing-xml-serialization
11. https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/types-supported-by-the-data-contract-serializer
12. https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.Xml/src/System/Xml/Serialization/Types.cs (primitive table: decimal, DateTimeOffset)
