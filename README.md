# TestPOC — Snapshot testing POC (REST + GraphQL) with EF Core + Postgres Testcontainer

Hand-rolled snapshot testing pattern. Tests run against a **real PostgreSQL
database** spun up in a Docker container per test-run (Testcontainers.PostgreSql),
with EF Core managing the schema via a real migration.

Third-party libraries: HotChocolate (GraphQL server), Swashbuckle (OpenAPI),
EF Core + Npgsql (data), xUnit + `Microsoft.AspNetCore.Mvc.Testing` +
`Xunit.DependencyInjection` + `Testcontainers.PostgreSql` +
`FluentAssertions` (test stack). The snapshot helper itself is deliberately
hand-written to avoid a snapshot-library dependency (Verify / Snapshooter).

## Prerequisites

- **.NET 8 SDK**
- **Docker Desktop** running (Testcontainers spins up `postgres:16-alpine`)
- **`dotnet-ef` global tool** — needed only if you want to add new migrations:
  `dotnet tool install --global dotnet-ef`

### One-time host opt-in: keep containers alive across runs

Every Testcontainer used in this repo — `SharedPostgresContainerReference` (test Postgres),
`RabbitMqTestContainerReference` (test RabbitMQ), `SourceContainerReference`
(test-side source Postgres) and `SourceDatabaseInitializer` (dev-run
source Postgres) — starts with `WithReuse(true)`. Testcontainers only honors
that flag when the reuse feature is enabled in your user-home properties file
— otherwise the containers get cleaned up (Ryuk) at end of run and the next
run pays the 5-10s startup cost again.

Run this **once** in a Windows PowerShell (creates the file if missing, no-ops
if it already contains the line):

```powershell
Set-Content -Path "$env:USERPROFILE\.testcontainers.properties" -Value "testcontainers.reuse.enable=true"
```

After that, every subsequent `dotnet test` / `dotnet run` — with or without a
debugger attached — reuses the containers by name/labels (`testpoc-postgres`,
`testpoc-rabbitmq`, `postgres-source`) and starts in <1s.

## First-time setup

The initial EF migration must be generated once before you can build:

```powershell
cd C:\Work\2026\TestPOC\src\TestPOC.Api
dotnet ef migrations add InitialCreate --output-dir Migrations
```

This creates `Migrations/{timestamp}_InitialCreate.cs` +
`Migrations/{timestamp}_InitialCreate.Designer.cs` +
`Migrations/AppDbContextModelSnapshot.cs`. Commit them.

## Layout

```
TestPOC.sln
src/TestPOC.Api/
├── Program.cs                          — REST + GraphQL + DbContext registration + Migrate on startup
├── Controllers/ItemsController.cs      — REST: GET /api/items, GET /api/items/{id}, POST /api/items
├── GraphQl/
│   ├── Query.cs                        — items, item(id) (async, EF-backed)
│   └── Mutation.cs                     — createItem(input)
├── Dtos/ItemDto.cs, CreateItemRequest.cs
├── Services/ItemsService.cs, IItemsService.cs, ItemsErrors.cs
├── Results/OperationResult.cs, OperationError.cs, ErrorType.cs
├── Extensions/OperationResultExtensions.cs
└── appsettings.json                    — ConnectionStrings:Default (dev-only)

src/TestPOC.Api.Infrastructure/         — persistence + messaging (clean-arch infra layer)
├── Persistence/AppDbContext.cs         — DbSet<Item> + fluent config
├── Persistence/Entities/Item.cs, OutboxMessage.cs
├── Persistence/Migrations/             — hand-written InitialCreate + AddOutboxMessages
├── Messaging/IEventPublisher.cs, OutboxEventPublisher.cs, OutboxProcessor.cs, RabbitMqPublisher.cs
└── Messaging/Events/ItemCreatedEvent.cs

test/TestPOC.TestInfrastructure/        — project-agnostic reusable test building blocks
├── LazyAsyncStartup.cs                 — one-time async init guard (SemaphoreSlim)
├── Containers/SharedPostgresContainerReference.cs, RabbitMqTestContainerReference.cs, SourceContainerReference.cs
├── Snapshots/SnapshotHelper.cs, JsonTestHelpers.cs
├── IO/ProjectFileLookup.cs             — [CallerFilePath] path resolver
└── Data/CsvDataset.cs, CsvExportSpec.cs, CsvDatasetReader.cs, CsvDatasetWriter.cs, SeedDatasetLoader.cs

test/TestPOC.Api.Tests.Integration/     — project-specific facts and fixtures
├── AssemblyInfo.cs                     — [DisableTestParallelization = true] (shared DB)
├── Startup.cs                          — Xunit.DependencyInjection registration
├── TestInfrastructure/
│   ├── ApiTestFactory.cs               — WebApplicationFactory<Program>; overrides ConnectionStrings:Default
│   │                                     to point at the container. Exposes ResetDataToSeedAsync().
│   ├── TestJwtBuilder.cs               — signs a JWT against the API's expected key
│   ├── TestData/ValidItem.cs           — canonical valid test data constants
│   ├── TestDataManagement/SourceCsvRegenerator.cs   — skipped [Fact]s: regenerate CSVs from a real source DB
│   └── TestDatasets/items.csv          — checked-in seed rows loaded by ResetDataToSeedAsync
├── Items/
│   ├── Rest/
│   │   ├── EndpointTests.cs            — partial parent (implements IAsyncLifetime → resets DB per class)
│   │   ├── EndpointTests.Create.cs, .List.cs, .Get.cs
│   │   └── __snapshots__/*.json
│   └── GraphQl/
│       ├── GraphQlEndpointTests.cs     — partial parent + ExecuteAsync helpers
│       ├── GraphQlEndpointTests.Create.cs, .List.cs, .Get.cs
│       ├── Queries/*.graphql           — raw query text loaded via ProjectFileLookup
│       └── __snapshots__/*.json
└── Schema/                             (cross-cutting: openapi + graphql SDL snapshots)
    ├── SchemaTests.cs
    └── __snapshots__/
        ├── openapi.json
        └── schema.graphql
```

## Test infrastructure flow

1. `Xunit.DependencyInjection` boots the test host from `TestInfrastructure/Startup.cs`.
   Registers `SharedPostgresContainerReference.Instance` + `ApiTestFactory` as singletons.
2. First test class instantiation → xUnit's `IClassFixture<ApiTestFactory>` →
   `ApiTestFactory.InitializeAsync`:
   - Awaits `SharedPostgresContainerReference.Instance.EnsureInitializedAsync` (starts Postgres
     container the first time only; subsequent calls no-op).
   - Calls `CreateClient()` which builds the ASP.NET host. `Program.cs` reads
     `ConnectionStrings:Default` (overridden to the container's conn string in
     `ConfigureWebHost`) and calls `db.Database.Migrate()` — creates the schema
     and applies the InitialCreate migration's `InsertData` seed.
3. Each test class's own `InitializeAsync` calls `Factory.ResetDataToSeedAsync()`:
   `TRUNCATE items RESTART IDENTITY CASCADE` + `SeedDatasetLoader.LoadItemsAsync(db)`
   which reads `TestInfrastructure/TestDatasets/items.csv` and inserts the rows.
   This gives every test class a clean, deterministic baseline regardless of
   what mutation tests inserted previously.

   **Migration owns schema, tests own data.** The initial
   EF migration is schema-only — no `HasData`, no `InsertData`. Test data lives
   exclusively in `TestInfrastructure/TestDatasets/*.csv` and is loaded by the
   test infrastructure. To regenerate the CSV from a real source database, use
   `SourceCsvRegenerator`: fill in `ExportDatabase` in `appsettings.Test.json`,
   remove the `Skip` from the relevant `[Fact]`, run once, commit the
   regenerated CSV, restore the `Skip`.

   Production deploys will therefore boot with an EMPTY items table. If a real
   project needs baseline reference data in prod, add a separate seed
   mechanism (idempotent SQL script, or a startup hosted service that reads
   its own dedicated CSV) — never re-couple it to the test datasets.
4. Tests execute against the container-backed API. Snapshots capture the wire
   responses (with volatile fields like `id`/`createdAt` stripped).

Test parallelism is disabled at the assembly level (`AssemblyInfo.cs`) — a single
container serves every test class, and parallel Migrate() runs / concurrent
data mutations would race.

## Snapshot pattern (unchanged from earlier iterations)

1. First run of a test: the `__snapshots__/*.json` (or `.graphql`) file does not
   exist yet. `SnapshotHelper` writes the actual value and passes. Commit the file.
2. Subsequent runs: actual vs snapshot.
   - Match → pass.
   - Mismatch → actual is written to a sibling `__MISMATCH__/` directory and the
     test throws `XunitException` **with a line-diff of what was added / removed**
     and both file paths (Ctrl+click friendly in VS / Rider).
     - Intentional change → overwrite snapshot with mismatch content, delete the
       `__MISMATCH__` file, commit both together.
     - Unintentional change → fix the code.

## Naming conventions

- Test method: `Should_<Behavior>_When_<Condition>`
- Snapshot file: `<Operation>.<TestMethodName>.json` (auto-derived by
  `SnapshotHelper.ValidateJson` from `[CallerFilePath]`; the operation is the
  last dot-segment of the caller file's base name, e.g.
  `EndpointTests.Create.cs` → `Create`)
- Test class decomposition: partial parent (`EndpointTests`,
  `GraphQlEndpointTests`) + per-operation nested classes
  (`EndpointTests.List.cs` declares `class List : EndpointTests`). xUnit
  reports them as `EndpointTests+List`, `GraphQlEndpointTests+Create`, etc.
  Type names are deliberately not the generic `MutationTests` / `QueryTests`
  pair so they do not collide with other REST/GraphQL test suites in the
  ecosystem.

## Test data

- `TestInfrastructure/TestData/ValidItem.cs` holds every field of a canonical
  valid item as `const` (matches `SeedData.Widget` — see `src/TestPOC.Api/Domain/SeedData.cs`).
- Per-operation nested classes define a private `GenerateInput()` /
  `GenerateVariables()` factory with those constants as default parameter values.
- Tests override one field at a time to exercise edge cases.

## Running

```powershell
cd C:\Work\2026\TestPOC
dotnet restore
dotnet build
dotnet test
```

Docker Desktop must be running. First `dotnet test` takes ~5-10s longer than
subsequent runs while the Postgres container image pulls + starts. Container
is reused across test classes but stops when the test process exits.

### Common commands

Run everything, HTML log written into the test project's `TestResults/`:

```powershell
dotnet test --logger "html;logfilename=testResults.html" --configuration Debug
```

Run a single test method by fully-qualified name (fastest, exact match):

```powershell
dotnet test --filter "FullyQualifiedName~RegenerateItemsCsv"
```

Filter shortcuts for common scopes:

```powershell
# every fact in one class
dotnet test --filter "FullyQualifiedName~EndpointTests+Create"

# every SourceCsvRegenerator fact (skip-marked ones stay skipped)
dotnet test --filter "FullyQualifiedName~SourceCsvRegenerator"

# every REST endpoint fact
dotnet test --filter "FullyQualifiedName~Items.Rest.EndpointTests"

# every GraphQL endpoint fact
dotnet test --filter "FullyQualifiedName~Items.GraphQl.GraphQlEndpointTests"
```

`--filter` uses xUnit's expression syntax: `~` is substring-match against the
test's fully-qualified name, `=` is exact. Nested test classes appear as
`ParentType+NestedType.MethodName` (e.g. `EndpointTests+Create.Should_Return400_When_NameIsMissing`).

## Dev-only "source" database (for the CSV regeneration demo)

`test/…/TestInfrastructure/SourceContainerReference.cs` brings up a second
Postgres container on host port **32774** (`testpoc_source` / `sourceuser` /
`sourcepass`) — a "prod-like" source DB used exclusively by
`SourceCsvRegenerator`. On first init the source container is migrated and seeded
with 30 dummy `items` rows (`Bogus`-generated with a fixed `Randomizer(12345)`
seed, so runs are deterministic).

**Why this exists.** `SourceCsvRegenerator` (in the integration test project) has
two `[Fact(Skip = "…")]` methods that regenerate `TestDatasets/items.csv`
from a real source database. Without a source DB those facts are impossible
to demonstrate end-to-end. This container is the demo — nothing outside the
integration-test project references it.

End-to-end CSV regeneration:

```powershell
# 1. Bring the container up by running any test (or an SourceCsvRegenerator fact
#    directly, since it injects SourceContainerReference and awaits it in
#    InitializeAsync).
cd C:\Work\2026\TestPOC
dotnet test --filter "FullyQualifiedName~ExportDataBySql" --configuration Debug

# 2. To regenerate items.csv, unskip one of the SourceCsvRegenerator facts (edit
#    the Skip argument out), point TablesToExport at "items" (or add an
#    CsvExportSpec to sqlToExport with FilterQueries.RecentItems), re-run. The
#    CSV is written to test/…/TestDatasets/items.csv.

# 3. Restore the Skip and commit the regenerated CSV.
```

No `appsettings.Test.json` connection-string wiring needed — the fact reads
the container's own connection string directly.

## Adding a new domain

1. Copy `test/TestPOC.Api.Tests.Integration/Items/` to `test/.../Products/`.
2. Rename `Items` → `Products` in namespaces + class refs.
3. Add `ValidProduct.cs` to `TestInfrastructure/TestData/`.
4. Add a `Product` entity + `DbSet<Product>` + `HasData(...)` in `AppDbContext`.
5. Extend `SeedData.cs` with `Product` fixtures + include them in the seed
   returned by a new `AllProducts()` (or extend `AllItems()`, name it more
   broadly).
6. `dotnet ef migrations add AddProducts` — generates a follow-up migration.
7. Run tests; snapshots auto-scaffold on first run.

## Why not Verify / Snapshooter?

Deliberate: keep the snapshot infrastructure inside the repo so we own it and
never fear an abandoned package upgrade. The `SnapshotHelper` is ~200 lines,
fully readable, and can be extended for new formats without waiting for a
third-party PR.

`Xunit.DependencyInjection` and `Testcontainers.PostgreSql` are stack
essentials, both widely used — but neither is the "snapshot library" concern.
