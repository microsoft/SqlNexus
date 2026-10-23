# SqlNexus Testing Infrastructure

This folder contains the automated test scaffolding for SqlNexus.

```
TestingInfrastructure/
  IntegrationTests/
    SQLNexusPiiAudit.ps1        Database-wide PII scrubber integration audit
  UnitTests/
    SqlNexus.UnitTests/          MSTest project (targets net48)
      <MirrorOfSourceProject>/   test folders mirror the code under test
```

## Goals

- Provide a home for unit tests so new code changes can be covered going forward.
- Keep test scaffolding separate from the product source and the installer/release tree.
- Mirror the source layout under the test project so coverage gaps are visible.

## Conventions

- **Framework:** MSTest (`[TestClass]` / `[TestMethod]`). Works in Visual Studio Test Explorer
  and in CI via `dotnet test`.
- **Target:** `net48` (matches the .NET Framework 4.8 product projects).
- **Naming:** `<TypeUnderTest>Tests.cs`; method names read `Scenario_ExpectedResult`.
- **Structure:** Arrange / Act / Assert; one logical assertion focus per test.
- **Folders:** mirror the source project namespace (e.g. tests for `RowsetImportEngine` live under
  `SqlNexus.UnitTests/RowsetImportEngine/`).

## Referencing product code

The tests reference real production code via `ProjectReference` entries in
`SqlNexus.UnitTests.csproj` (currently `RowsetImportEngine`, `sqlnexus`, `TraceEventImporter`,
`ErrorLogImporter`, and `SqlNexus.McpServer`). To test additional product code:

1. Add a `ProjectReference` to the product project in `SqlNexus.UnitTests.csproj`.
2. If you need to test `internal` members, grant access from the product project with a
   **strong-name-qualified** `InternalsVisibleTo` (see below), then call directly into the
   product type from the test.

### Strong naming and `InternalsVisibleTo`

The product assemblies (`sqlnexus.exe`, the importers, and `SqlNexus.McpServer.exe`) are
**strong-named** with the shared key `sqlnexus/SqlNexus.snk`. A strong-named assembly can only
grant `InternalsVisibleTo` to a **strong-named** friend, so the test project is **also**
strong-named with the same key (`SignAssembly=true` +
`AssemblyOriginatorKeyFile=..\..\..\sqlnexus\SqlNexus.snk`).

Because of that, a bare `InternalsVisibleTo("SqlNexus.UnitTests")` **no longer works** for any
strong-named product assembly -- the grant must include the test project's public key:

```csharp
// In the product project's AssemblyInfo.cs (public key is the one from SqlNexus.snk):
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(
    "SqlNexus.UnitTests, PublicKey=0024000004800000...<full 320-hex-char key>...")]
```

The `sqlnexus`, `ErrorLogImporter`, and `SqlNexus.McpServer` projects already do this; copy the
exact `PublicKey=` value from any of their `AssemblyInfo.cs` files. Do **not** add an unqualified
`InternalsVisibleTo("SqlNexus.UnitTests")` -- it will silently fail to expose internals from a
strong-named assembly.

## Running tests

- **Visual Studio:** open Test Explorer and run.
- **Command line:** `dotnet test TestingInfrastructure/UnitTests/SqlNexus.UnitTests/SqlNexus.UnitTests.csproj`

> Note: the legacy WinForms `sqlnexus` project may not build under the `dotnet` SDK
> toolchain on all machines (non-string WinForms resources). If `dotnet test` fails to
> build it, build the solution in Visual Studio and run the tests from Test Explorer.

### ReadTrace overview metric regression tests

[`ReadTracePostProcessingScriptTests.cs`](UnitTests/SqlNexus.UnitTests/sqlnexus/ReadTracePostProcessingScriptTests.cs)
checks the deployed SQL script and its integrity hash without connecting to SQL Server.
`ReadTrace.spReporter_Overview_Counts` accepts only `StartingEvents`, `CompletedEvents`,
`TotalCPU`, `TotalDuration`, `TotalReads`, and `TotalWrites`. Null and unsupported metrics
raise an error before dynamic SQL is built. Complete dotted column identifiers are quoted
with `QUOTENAME`, preserving the baseline/comparison and matching/unmatched query branches.

These are source-contract tests, not a T-SQL execution engine. They cover the allowlist,
null/error/return guard, malformed and injection-shaped input examples, length boundaries,
identifier quoting, all four query branches for each supported metric, and script integrity.
The runtime test below executes the procedure against fixture rows. Execution permissions
for application users remain a separate deployment check.

### Running the ReadTrace overview SQL runtime test

[`IntegrationTests/ReadTraceOverviewCounts.sql`](IntegrationTests/ReadTraceOverviewCounts.sql)
performs **39 runtime checks** against a copy of the installed procedure:

- Exact `B`, `C`, `BO`, and `CO` totals for every supported metric.
- A `BIGINT` aggregate of 6,000,000,000.
- Thirteen invalid inputs, including null, empty/unknown names, bracket-breaking expressions,
  statement/comment delimiters, and 127/128/129-character inputs.
- Four rows with null aggregate values for every metric when the table is empty.
- The same thirteen rejections with the fixture table absent, proving validation occurs
  before dynamic SQL accesses it.

In SSMS, select a SqlNexus database where the updated procedure is already installed, open
the script, and execute it normally (SQLCMD mode is not required). With Windows authentication:

```powershell
sqlcmd -S localhost -d YourSqlNexusDatabase -E -b -i .\TestingInfrastructure\IntegrationTests\ReadTraceOverviewCounts.sql
```

Use the SQL Server instance and SqlNexus database you intend to test. Updating the file in
the repository alone does not update an already-installed stored procedure.

The login needs `VIEW DEFINITION` on the source procedure and permission to create/drop
databases. Run outside any existing transaction. The script copies only the installed
procedure definition, never customer rows, into a unique `SqlNexusOverviewTest_<GUID>`
database with synthetic fixture data. It does not alter the source procedure or tables.
The fixture contains only the columns used by this procedure; it is not a full import schema.
Cleanup is attempted on both success and failure. If execution is cancelled or the connection
is lost, the printed test database name identifies the database to remove manually.

Success ends with `PASS: 39 runtime checks completed; disposable test database removed.`
Any unexpected result raises an error; `sqlcmd -b` returns a nonzero exit code. The pre-fix
procedure fails the invalid-input checks rather than producing a false pass.

## Running the PII scrubber integration audit

[`IntegrationTests/SQLNexusPiiAudit.ps1`](IntegrationTests/SQLNexusPiiAudit.ps1) scans SQL Server
user tables, processes PII-capable column values through the production MCP `PiiScrubber`, and
reports patterns that remain after scrubbing.

### Prerequisites

- Run on Windows with PowerShell and access to the target SQL Server through Windows integrated
  authentication.
- Build `SqlNexus.McpServer` in Release first. By default, the script loads
  `SqlNexus.McpServer/bin/Release/SqlNexus.McpServer.exe` relative to the repository. Use
  `-AssemblyPath` to select a different build.
- The login must be able to read table metadata and the user tables being audited.

From the repository root, audit the default `localhost` / `sqlnexus` database:

```powershell
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1
```

Select another server or database:

```powershell
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1 `
  -Server localhost `
  -Database sqlnexus
```

Exclude specific tables and skip every table containing more than one million rows:

```powershell
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1 `
  -Database sqlnexus_ManagedTrc `
  -ExcludeTable dbo.Counters,dbo.CounterData `
  -MaximumTableRows 1000000 `
  -Verbose
```

`-ExcludeTable` accepts either `table` or `schema.table` names and is case-insensitive.
`-MaximumTableRows 0`, the default, disables the row-count limit. Tables exceeding a configured
limit are not queried and are listed in the `LargeTablesSkipped` result property.

The script writes JSON to the pipeline. Redirect it to retain a report:

```powershell
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1 `
  -Database sqlnexus `
  -MaximumTableRows 1000000 |
  Set-Content -LiteralPath .\pii-audit-report.json -Encoding utf8
```

The report does not contain raw source values. Findings contain table and column locations,
counts, value lengths, generalized value shapes, and truncated SHA-256 fingerprints. Review
`TableErrors` for tables that could not be audited and treat residual detector matches as leads
requiring classification; SQL text, timestamps, and numeric metrics can produce false positives.

## Solution membership

The test project is included in `sqlnexus.sln`. The product build is unaffected because
the test project is separate and only references product assemblies.

## Guiding principles

All tests and the code they cover must honor the security, accessibility, and privacy
requirements described in [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md).
