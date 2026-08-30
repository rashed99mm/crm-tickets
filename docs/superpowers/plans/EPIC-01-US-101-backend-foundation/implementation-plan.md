# Backend Foundation Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The solution, the four-layer/three-host .NET 10 project set, the `AppDbContext` (identity + domain + append-only guard + soft-delete), the shared composition core, JWT authentication, and the error/response envelope contract — the substrate every later feature builds on (ADR-0008, ADR-0009).

**Architecture:** Clean Architecture. `Domain` depends on nothing; `Application` depends on `Domain`; `Infrastructure` depends on both; `Api.Shared` holds the composition core both hosts share; `InternalApi`/`ExternalApi` are hosts. Solution is `CustomerSupport.slnx` (was `CCE.Platform.slnx` before ADR-0009).

**Spec/reference:** `refrence/cce-platform` (the adopted CCE Platform) — see `docs/adr/0009-adopt-the-cce-platform-as-the-crm-baseline.md`. This foundation is *inherited*, not hand-written in this repo; the plan now records what the inherited code actually contains.

## Global constraints

- Dependency rule is inviolable: `Domain` references no other project; `Application` references no web/EF package. Verify in each `.csproj`.
- Build at 0 warnings (warnings-as-errors is on per `Directory.Build.props`).
- Both hosts call the *same* `AddPlatform*` extensions from `Api.Shared`; never duplicate pipeline wiring in a host.
- `Jwt:Key` (or `Jwt:Authority`) must be set or `AddPlatformAuthentication` throws and every request 500s (see `CLAUDE.md`).

## Task 1 — The project set (`AC-` n/a, foundation)

**Files:**
- `backend/src/CustomerSupport.Domain/CustomerSupport.Domain.csproj` (no `ProjectReference`, no persistence package)
- `backend/src/CustomerSupport.Application/CustomerSupport.Application.csproj`
- `backend/src/CustomerSupport.Infrastructure/CustomerSupport.Infrastructure.csproj`
- `backend/src/CustomerSupport.Api.Shared/CustomerSupport.Api.Shared.csproj`
- `backend/src/CustomerSupport.InternalApi/CustomerSupport.InternalApi.csproj`
- `backend/src/CustomerSupport.ExternalApi/CustomerSupport.ExternalApi.csproj`
- `backend/src/CustomerSupport.Shared.Contracts/CustomerSupport.Shared.Contracts.csproj`
- `backend/src/CustomerSupport.Migrator/CustomerSupport.Migrator.csproj`

**Interfaces:** none — structure only.

**Step 1 — Host csproj references the right layers**

```xml
<!-- backend/src/CustomerSupport.InternalApi/CustomerSupport.InternalApi.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\CustomerSupport.Api.Shared\CustomerSupport.Api.Shared.csproj" />
    <ProjectReference Include="..\CustomerSupport.Application\CustomerSupport.Application.csproj" />
    <ProjectReference Include="..\CustomerSupport.Infrastructure\CustomerSupport.Infrastructure.csproj" />
    <ProjectReference Include="..\CustomerSupport.Shared.Contracts\CustomerSupport.Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

`Domain.csproj` intentionally has **no** `ProjectReference` and **no** `Microsoft.EntityFrameworkCore` package — that is the mechanical check an assessor runs (ADR dependency rule).

- [ ] **Step 2: Run — solution builds**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: build succeeds, 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.*/CustomerSupport.*.csproj backend/CustomerSupport.slnx
git commit -m "chore(foundation): four-layer solution, three hosts, dependency rule enforced"
```

## Task 2 — `AppDbContext`: identity + domain, append-only guard, soft delete (`AC-49`, `AC-41`)

**Files:**
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/CustomerSupport.Domain/Entities/IBaseEntity.cs`, `BaseEntity.cs`, `IAppendOnlyEntity.cs`

**Interfaces:** `IAppendOnlyEntity` — implemented by `TicketHistory`, `TicketMessage`, `SLAEvent`, `ContentVersion`, `ContentView`, `ContentTicketLink`. The guard rejects `Modified`/`Deleted` on any such row at `SaveChangesAsync`.

**Step 1 — DbSets and the save guard (real excerpt)**

```csharp
// backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<SLAPolicy> SLAPolicies => Set<SLAPolicy>();
    // ... Content, Notifications, PlatformSettings, ExternalApiConfigurations

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        GuardAppendOnlyHistory();                       // AC-49
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)   entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Deleted)  // soft delete
            {
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
                entry.State = EntityState.Modified;
            }
        }
        return await base.SaveChangesAsync(ct);
    }

    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<IAppendOnlyEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only: row ...");
        }
    }
}
```

The sequence behind `TKT-nnnnnn` lives here too: `modelBuilder.HasSequence<long>("TicketReferenceSequence").StartsAt(1000)`. A sequence, not `MAX(Reference)+1`, because the latter races under concurrent inserts and the unique index would turn the race into a 500.

- [ ] **Step 2: Run — inherited domain/handler tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AppendOnly"`
Expected: the ADR-0010 append-only tests pass against a real LocalDB (not InMemory, which would skip the guard).

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs \
        backend/src/CustomerSupport.Domain/Entities/IAppendOnlyEntity.cs
git commit -m "feat(foundation): AppDbContext with append-only guard and soft delete (AC-49)"
```

## Task 3 — Shared composition core and the two hosts (`BASE-5`..`BASE-8`)

**Files:**
- `backend/src/CustomerSupport.Api.Shared/Extensions/{Authentication,Authorization,WebApplication,ServiceCollection}Extensions.cs`
- `backend/src/CustomerSupport.InternalApi/Program.cs`
- `backend/src/CustomerSupport.ExternalApi/Program.cs`

**Interfaces:** `AddPlatformAuthentication`, `AddPlatformAuthorization`, `AddPlatformPersistence`, `AddPlatformWebApi`, `UsePlatformPipeline`, `MapPlatformEndpoints`, `UsePlatformDataSeedingAsync`.

**Step 1 — InternalApi Program (real)**

```csharp
// backend/src/CustomerSupport.InternalApi/Program.cs
using CustomerSupport.Api.Shared.Extensions;
var builder = WebApplication.CreateBuilder(args);
builder.Host.AddPlatformLogging(builder.Configuration);
builder.Services
    .AddPlatformOpenApi()
    .AddPlatformApiVersioning()
    .AddPlatformPersistence(builder.Configuration)
    .AddPlatformInfrastructureServices(builder.Configuration, "CustomerSupport.InternalApi")
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformAuthorization()
    .AddPlatformWebApi(builder.Configuration, builder.Environment);
var app = builder.Build();
app.UsePlatformPipeline();
app.MapPlatformEndpoints();
await app.UsePlatformDataSeedingAsync();
app.Run();
public partial class Program;
```

`ExternalApi/Program.cs` is the same core minus `UsePlatformDataSeedingAsync` (seeding is an administrative act; the customer host stays narrow). Both hosts share the composition core — that is ADR-0008's shape.

- [ ] **Step 2: Run — both hosts serve OpenAPI**

Run: `dotnet run --project backend/src/CustomerSupport.InternalApi --urls http://localhost:5074` then `curl http://localhost:5074/openapi/v1.json`
Expected: 200 with 35 documented operations; `ExternalApi` serves 2 (`/api/knowledge-base/articles`, `/ask`).

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Api.Shared/Extensions/*.cs \
        backend/src/CustomerSupport.InternalApi/Program.cs \
        backend/src/CustomerSupport.ExternalApi/Program.cs
git commit -m "feat(foundation): shared composition core, two hosts (BASE-5..BASE-8)"
```

## Task 4 — JWT authentication extension (`FEAT-02` substrate)

**Files:**
- `backend/src/CustomerSupport.Api.Shared/Extensions/AuthenticationExtensions.cs`
- `backend/src/CustomerSupport.Api.Shared/Configuration/JwtOptions.cs`

**Interfaces:** `IServiceCollection.AddPlatformAuthentication(this IServiceCollection, IConfiguration)`.

**Step 1 — Real extension (keyed or authority-based)**

```csharp
// backend/src/CustomerSupport.Api.Shared/Extensions/AuthenticationExtensions.cs
public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
    var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
            {
                options.Authority = jwtOptions.Authority;
                options.Audience = jwtOptions.Audience;
            }
            else if (!string.IsNullOrWhiteSpace(jwtOptions.Key) && jwtOptions.Key != "CHANGE_IN_PRODUCTION")
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer, ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
            }
            else
            {
                throw new InvalidOperationException(
                    "JWT authentication is enabled but no valid Jwt configuration was found. Set Jwt:Authority or Jwt:Key.");
            }
        });
    return services;
}
```

This is the exact failure the `CLAUDE.md` note describes: omit `Jwt:Key` and every request — including `/openapi/v1.json` — returns 500, because the exception middleware sits first in the pipeline and converts the startup throw into the standard envelope.

- [ ] **Step 2: Run — host boots only with a key**

Run: `dotnet run --project backend/src/CustomerSupport.InternalApi` with no `Jwt:Key`
Expected: boot fails / all requests 500 until `Jwt:Key` is supplied (verified via console sink override from `CLAUDE.md`).

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Api.Shared/Extensions/AuthenticationExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Configuration/JwtOptions.cs
git commit -m "feat(foundation): AddPlatformAuthentication (key or authority)"
```

## Task 5 — Error/response envelope contract (`AC-51`..`AC-54`)

**Files:**
- `backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs` (`ToActionResult`, `MapFailureStatusCode`)
- `backend/src/CustomerSupport.Application/Messages/{SystemCode,SystemCodeMap}.cs`
- `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`
- `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`

**Interfaces:** `Response<T>` envelope `{ success, code, message, data, errors[], traceId, timestamp }` — matches the frontend's `ApiEnvelope<T>` (`frontend/projects/common/src/lib/api/api-response.ts`).

**Step 1 — Every new failure code is registered in three places**

```csharp
// ApplicationErrors.cs — domain key
public static class Ticket
{
    public const string CUSTOMER_NOT_FOUND = "TICKET_CUSTOMER_NOT_FOUND";
    public const string CATEGORY_NOT_FOUND = "TICKET_CATEGORY_NOT_FOUND";
    public const string CREATED = "TICKET_CREATED";
}
// SystemCode.cs — stable ERRxxx
public const string ERR012 = "ERR012"; // customer has tickets (delete guard)
// SystemCodeMap.cs — DomainKey -> ERRxxx
// ResponseExtensions.MapFailureStatusCode — 404/409 switch arms
```

`SystemCodeMap.Resolve(ApplicationErrors.Ticket.CUSTOMER_NOT_FOUND)` is how a handler turns a domain key into the wire `code`; an unregistered key silently falls back to `400` — the recurring lesson of `FEAT-16`/`FEAT-19`.

- [ ] **Step 2: Run — `EveryErrorCode_HasABilingualMessage` guard**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EveryErrorCode"`
Expected: PASS — every code has an ar/en pair in `Resources.yaml`.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Application/Messages/*.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml
git commit -m "feat(foundation): response envelope + bilin/gual error catalogue"
```

## Self-review

Coverage: project set → Task 1; `AppDbContext` → Task 2; composition core/hosts → Task 3; JWT → Task 4; envelope → Task 5.

**Discrepancy found (prose vs real code):** the earlier foundation narrative described a hand-built envelope, message catalogue and `S1` entities built from scratch. The real foundation is the *inherited CCE Platform* (ADR-0009): `Response<T>`, the bilingual catalogue, identity/auth, auditing, messaging and the migration history all arrived pre-built. What this repo added on top was renaming (`CCE.*` → `CustomerSupport.*`), splitting into two hosts, and the ticket workflow. The hand-built pieces named in the old prose do not exist in `src/` — they were discarded at adoption.
