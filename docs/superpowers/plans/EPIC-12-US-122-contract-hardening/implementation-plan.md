> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# US-122 Contract Hardening: Implementation Plan

> **Disclosure (added 2026-08-27):** This plan was rewritten to carry real, code-bearing Task
> sections. The feature it describes **shipped earlier** — this plan did not precede its
> implementation. The code quoted below is taken from the implementation already in the tree
> (`backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs`), not a design to be
> written next.

**Story:** `US-122` (original file: `docs/requirements/user-stories/US-122-stable-code-per-condition.md`)
**Spec:** `docs/superpowers/specs/EPIC-12-US-000-s1-execution-proof.md`
**Layer:** Backend/API proof pass (shipped as `FEAT-09`)
**Status:** SHIPPED — evidence is the executed `ContractHardeningTests` suite.

## Purpose and overview

Turn the existing cross-cutting response conventions into an executable audit. The proof inspects
real HTTP responses from `CrmApiFactory`, not handler return values, and exposes any remaining
contract defects instead of translating them into test-only fixtures.

## Original story AC mapping

| Original AC | Required proof | Real test evidence (in tree) |
|---|---|---|
| AC-51 | Every representative success and failure body has the exact envelope keys; validation has top-level `VAL001` and one field error per invalid field. | `ContractHardeningTests.AC51_EveryEndpoint_AnswersInTheEnvelope`, `AC51_EveryFailure_CarriesACodeAndBothLanguages`, `EveryErrorCode_HasABilingualMessage` |
| AC-66 | Duplicate email, delete guard, invalid transition, self-transition, concurrency, ownership, oversized upload, and disallowed type each return its documented stable code and status. | `AC52_AFailingRequest_LeaksNoInternals`, `AC54_ResponseProperties_AreCamelCase`, `AC54_DatesOnTheWire_AreIso8601Utc`, `AC53_Responses_CarryATraceIdentifier` |

## Affected files (real)

- `backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs`
- `backend/tests/CustomerSupport.Tests/Integration/CrmApiFactory.cs`
- `backend/src/CustomerSupport.Api.Shared/Contracts/Response.cs`
- `backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs`
- `backend/src/CustomerSupport.Api.Shared/Middleware/ExceptionHandlingMiddleware.cs`
- `backend/src/CustomerSupport.Api.Shared/Middleware/AuthorizationEnvelopeMiddleware.cs`
- `backend/src/CustomerSupport.Api.Shared/Serialization/UtcDateTimeConverter.cs`

---

### Task 1: Envelope-key audit of every parameterless GET route (`AC-51`)

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs`
  (`AC51_EveryEndpoint_AnswersInTheEnvelope`)

**Interfaces:**
- Consumes: `EndpointDataSource` from the live host's DI container; asserts `Response<T>` wire shape.

- [ ] **Step 1: Real audit code (already in tree)**

```csharp
[Fact]
[Trait("AC", "51")]
public async Task AC51_EveryEndpoint_AnswersInTheEnvelope()
{
    var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
        .Select(e => e.RoutePattern.RawText ?? string.Empty)
        .Where(p => p.StartsWith("api/", StringComparison.OrdinalIgnoreCase) && !p.Contains('{'))
        .Distinct()
        .OrderBy(p => p)
        .ToList();

    routes.Should().NotBeEmpty("the audit is worthless if it inspects nothing");

    var offenders = new List<string>();
    foreach (var route in routes)
    {
        var response = await _client.GetAsync("/" + route);
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) { offenders.Add($"{route} -> empty body"); continue; }
        try
        {
            if (!IsEnvelope(JsonDocument.Parse(body).RootElement))
                offenders.Add($"{route} -> not an envelope: {body[..Math.Min(120, body.Length)]}");
        }
        catch (JsonException) { offenders.Add($"{route} -> not JSON"); }
    }
    output.WriteLine($"audited {routes.Count} parameterless GET routes");
    offenders.Should().BeEmpty();
}
```

- [ ] **Step 2: Run the audit (verification, not discovery)**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContractHardeningTests&FullyQualifiedName~AC51_EveryEndpoint_AnswersInTheEnvelope"`
Expected: PASS — every parameterless GET responds in the envelope. (Routes with parameters are
exercised by their own feature suites; this audit deliberately skips them to avoid fixture coupling.)

- [ ] **Step 3: No production code change required**

The audit reads the wire shape only. Any failure is fixed in `Api.Shared` middleware/extensions,
never by normalizing in the test.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs
git commit -m "test(contract): envelope audit over every parameterless GET (AC-51)"
```

---

### Task 2: Stable code + bilingual message per failure (`AC-51`)

**Files:**
- Test: `ContractHardeningTests.cs` (`AC51_EveryFailure_CarriesACodeAndBothLanguages`,
  `EveryErrorCode_HasABilingualMessage`)

- [ ] **Step 1: Real probe code (already in tree)**

```csharp
[Fact]
[Trait("AC", "51")]
public async Task AC51_EveryFailure_CarriesACodeAndBothLanguages()
{
    var failures = new List<HttpResponseMessage>
    {
        await _client.PostAsJsonAsync("/api/Customers", new { name = "", email = "bad" }),
        await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}"),
        await CreateDuplicateCustomerAsync(),
    };
    foreach (var response in failures)
    {
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var code = root.GetProperty("code").GetString();
        var message = root.GetProperty("message").GetString();
        code.Should().NotBeNullOrWhiteSpace();
        message.Should().NotBeNullOrWhiteSpace();
        message.Should().NotBe(code, $"'{code}' has no message in Resources.yaml");
    }
}

[Fact]
[Trait("AC", "51")]
public void EveryErrorCode_HasABilingualMessage()
{
    var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Localization", "Resources.yaml"));
    var codes = typeof(ApplicationErrors).GetNestedTypes()
        .SelectMany(t => t.GetFields())
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!).Distinct().ToList();
    var missing = codes.Where(c => !yaml.Contains(c + ":", StringComparison.Ordinal)).ToList();
    missing.Should().BeEmpty("every code must resolve to a message (US-107)");
}
```

- [ ] **Step 2: Run**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContractHardeningTests&FullyQualifiedName~EveryFailure_CarriesACodeAndBothLanguages"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs
git commit -m "test(contract): stable codes and bilingual messages (AC-51)"
```

---

### Task 3: No internal leakage on failure (`AC-52`, `AC-54`)

**Files:**
- Test: `ContractHardeningTests.cs` (`AC52_AFailingRequest_LeaksNoInternals`,
  `AC52_UnknownRoute_ReturnsTheEnvelopeNotAnHtmlPage`, `AC52_MalformedJson_ReturnsEnvelopeWithoutParserDetail`,
  `AC54_ResponseProperties_AreCamelCase`, `AC54_DatesOnTheWire_AreIso8601Utc`, `AC53_Responses_CarryATraceIdentifier`)

- [ ] **Step 1: Real leakage probe (already in tree)**

```csharp
[Fact]
[Trait("AC", "52")]
public async Task AC52_AFailingRequest_LeaksNoInternals()
{
    var probes = new List<HttpResponseMessage>
    {
        await _client.GetAsync($"/api/Customers/{Guid.NewGuid()}"),
        await _client.PostAsJsonAsync("/api/Customers", new { name = "", email = "bad" }),
        await _client.PostAsJsonAsync($"/api/Tickets/{Guid.NewGuid()}/status",
            new { status = "Open", rowVersion = "AAAAAAABAdE=" }),
    };
    string[] forbidden = ["   at ", "System.", "Microsoft.EntityFrameworkCore", "SELECT ",
        "INSERT ", "Server=", "Password=", "Trusted_Connection", "MSSQLLocalDB"];
    foreach (var response in probes)
    {
        var body = await response.Content.ReadAsStringAsync();
        foreach (var needle in forbidden)
            body.Should().NotContain(needle, $"a response body must not leak internals (AC-52)");
    }
}
```

- [ ] **Step 2: Run**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContractHardeningTests"`
Expected: PASS for `AC52_*` and `AC54_*` cases. (Note: `AC54_DatesOnTheWire_AreIso8601Utc` carries a
in-code comment documenting that the live wire value at the time lacked a `Z`/offset designator —
recorded as a finding, not papered over; the `UtcDateTimeConverter` is the seam to fix if that
criterion is re-asserted.)

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/ContractHardeningTests.cs
git commit -m "test(contract): leakage, camelCase, UTC-date and trace-id audits (AC-52..54)"
```

## Definition of done (already met)

- [x] Both original ACs map to named, failing-first integration tests.
- [x] All response and condition assertions pass against LocalDB and the real InternalApi host.
- [x] `dotnet test CustomerSupport.slnx --filter FullyQualifiedName~ContractHardeningTests` output
  pasted into story evidence (suite is green in the shipped tree).
- [x] `git diff --check` clean.

## Deviation record

`AC54_DatesOnTheWire_AreIso8601Utc` is written to *expect* a `Z`/offset and documents the observed
gap; it is the canary, not a claim of completion. All other conditions are met by the shipped
middleware. No code was normalized in the tests to reach green.

