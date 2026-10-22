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

## Custom XEL compatibility gate (issue #499 / F-002)

`sqlnexus/CustomXELImporterCompatibilityTests.cs` in the unit-test project contains
opt-in probes for comparing the installed XeLite reader with
`sys.fn_xe_file_target_read_file`. These are assessment tests, not a production reader
or a certification of full XML/schema compatibility. Passing the probes means that
the comparison completed; inspect their diagnostic summaries for unresolved gaps.

- `CustomXelLocalCompatibility` reads the supplied primary folder locally.
- `CustomXelCompatibility` also reads each exact file through a parameterized SQL
  query. It performs no database writes and does not run import/post-processing.
- Ordinary unit-test runs skip these probes. Small deterministic checks for probe
  XML parsing and value formatting still run without files or SQL Server.
- No capture content is committed or included in the summaries: only counts,
  event/field names, runtime types, schema names, and aggregate differences.

After building the test project with Visual Studio/MSBuild, run:

```powershell
$env:SQLNEXUS_RUN_CUSTOM_XEL_COMPATIBILITY = "1"
$env:SQLNEXUS_CUSTOM_XEL_PATH = "D:\Captures\Primary"
$env:SQLNEXUS_CUSTOM_XEL_SERVER = "localhost"
$env:SQLNEXUS_CUSTOM_XEL_DATABASE = "sqlnexus"

dotnet test .\TestingInfrastructure\UnitTests\SqlNexus.UnitTests\SqlNexus.UnitTests.csproj `
  --no-build --no-restore --filter "FullyQualifiedName~CustomXELImporterCompatibilityTests" `
  --logger "console;verbosity=detailed"
```

The SQL probe uses Windows authentication and encryption, with certificate validation
enabled by default. Only for an explicitly trusted local test server with a self-signed
certificate, opt in using `SQLNEXUS_CUSTOM_XEL_TRUST_SERVER_CERTIFICATE=1`. This setting
affects only the probe, not the application. Clear these process environment settings
after testing. The SQL Server service must be able to read the same fixture paths and
the principal needs the server-version-appropriate XE file-reading permissions.
Missing categories are reported as inconclusive, never as validated.

### Initial measurements (2026-10-21)

Against the supplied primary-folder fixtures, using XeLite `2024.2.5.1` and a local
SQL Server baseline:

| Source | Files | Events from each reader | Field/action names absent from either reader |
|---|---:|---:|---:|
| SQLDiag | 20 | 9,947 | 0 |
| System Health | 10 | 11,936 | 0 |
| AlwaysOn Health | 0 | Not validated | Not validated |

An additional AG capture supplied later the same day completed category coverage:

| Source | Additional files | Events from each reader | Field/action names absent from either reader |
|---|---:|---:|---:|
| SQLDiag | 10 | 4,007 | 0 |
| AlwaysOn Health | 10 | 153,219 | 0 |
| System Health | 10 | 27,241 | 0 |

All six probes (three local, three SQL comparisons) passed for the AG capture.
Across both captures, the assessment read 60 files and 206,350 events with each
reader. These are source counts, not rows written to any database.

The observed SQL reader schema is `module_guid` (`uniqueidentifier`), `package_guid`
(`uniqueidentifier`), `object_name` (`nvarchar(60)`), `event_data` (`nvarchar(max)`),
`file_name` (`nvarchar(260)`), `file_offset` (`bigint`), and `timestamp_utc`
(`datetime2(7)`). The existing importer creates its tables from that schema.

Compatibility issues that must be resolved before switching production readers:

1. XeLite's public `IXEvent` exposes field/action dictionaries, not original event XML.
   Its `ToString()` is diagnostic text, not XML. Reconstructing XML is a new serialization
   contract, not preservation of the original SQL representation.
2. SQL System Health XML contained 8,919 field/action display-text elements. XeLite
   exposes the underlying values but no separate public map-label metadata.
3. The dictionaries contain all observed field/action names, but simple invariant
   string formatting differs from SQL XML for 5,304 SQLDiag and 15,084 System Health
   values. These are lexical differences, not proof of value loss; structured XML,
   GUIDs, maps, and other representations need semantic comparison.
4. XeLite event-start byte offsets matched SQL `file_offset` in zero events. Do not
   relabel the former as the latter or promise SQL resume-offset compatibility.
5. Most timestamps differ by less than one millisecond; XeLite retains additional
   fractional precision. Truncating XeLite timestamps to milliseconds matched the
   SQL timestamp column for all 206,350 measured events. Timestamp precision needs
   an explicit compatibility policy.
6. The public API does not expose SQL's module/package GUIDs. XeLite's event UUID
   matched neither GUID for any measured event and must not be substituted for them.

There were no explicit references to the legacy destination tables in repository SQL
scripts or reports during the initial inventory. That does not establish compatibility
with external queries or dynamic consumers.

The AlwaysOn sample additionally contained 244,103 display-text elements and showed
the same offset/GUID gaps; it does not remove the need to resolve the XML contract.

### Reader decision and standard importer

The installed SQL Server 160 `Microsoft.SqlServer.XEvent.Linq` reader was also
evaluated through its public API and local SQLDiag/AlwaysOn sample files. It exposes
event metadata and can read the files, but `PublishedEvent` has no public whole-event
XML serialization method. `XMLData.XML` represents an individual XML value, not the
complete original event. Building an equivalent event document would still require
custom serialization and proof of XML, map-text, timestamp, and offset fidelity.
The installed reader also depends on SQL Server's XE components, rather than being
the application's existing standalone managed reader dependency.

**Decision:** retain the secured, parameterized SQL reader. This is not a claim that
a custom local serializer is impossible; it avoids adopting an unproven replacement
for the required full event representation. The reader choice does not prevent the
`INexusImporter` redesign.

`CustomXELImporterTests` covers the standard contract, primary-only discovery,
category grouping, canonical table mapping, adversarial parameter values, saved-option
migration, lifecycle reset, failures, cancellation, and long row counts.
`fmImportTests` covers accessible standard menu construction; selection evaluator
tests cover the existing `/M CustomXEL` token through the normal importer mapping.
These unit tests use temporary files and an injected reader, not live SQL Server.
The production reader stages and publishes each category inside one transaction,
preserves the SQL reader schema, and rejects incompatible append destinations.

### Isolated SQL import integration tests

`CustomXELImporterIntegrationTests` is separately opt-in. It **creates and drops
uniquely named `SqlNexus_CustomXelTests_<guid>` databases**; never enable it without
permission on the selected server. It uses Windows authentication, requires database
creation/deletion and XE-reading permissions, and does not use or modify the
compatibility probe's `SQLNEXUS_CUSTOM_XEL_DATABASE`.

It copies two small, nonempty XEL fixtures per category into a temporary sibling
folder with punctuation in its name, then deletes those copies. Original captures
remain unchanged. The SQL Server service must be able to read that location.

```powershell
$env:SQLNEXUS_RUN_CUSTOM_XEL_IMPORT_INTEGRATION = "1"
$env:SQLNEXUS_CUSTOM_XEL_PATH = "D:\Captures\Primary"
$env:SQLNEXUS_CUSTOM_XEL_SERVER = "localhost"
# Only for an explicitly trusted local test instance:
$env:SQLNEXUS_CUSTOM_XEL_TRUST_SERVER_CERTIFICATE = "1"

dotnet test .\TestingInfrastructure\UnitTests\SqlNexus.UnitTests\SqlNexus.UnitTests.csproj `
  --no-build --no-restore --filter "TestCategory=CustomXelImportIntegration"
```

All three categories are checked for full schema and stored-row equivalence against
a fresh, direct `SELECT * INTO` baseline (the legacy SQL reader's storage contract).
Text is compared as binary values plus byte lengths, and duplicate multiplicities
are included; XML is not normalized and comparisons are not reduced to row counts.
The baseline is materialized before comparison rather than re-evaluating the XE
function's timestamp column inside grouping/set expressions.

The tests also verify repeated replacement, append counts, rejection of legacy-only
and incompatible destinations, preservation of old data on a forced transactional
failure, and responsive cancellation while replacement is blocked by another
transaction. The four live cases passed against the supplied local fixtures.
Clear the opt-in environment settings after testing.

`TracePostProcessingPrerequisitesTests` additionally covers per-run scheduling for
both trace importers, cancellation/failure outcomes, and exclusion of unrelated
importers. Its opt-in SQL cases use the same isolated-database permission and
environment switch. They reproduce the missing-column failure with fresh and stale
trace schemas, verify general rowset cleanup without modifying trace tables, and
exercise UTC/local flags from server properties, server times, and trace misc-info.
They also check missing metadata, partially extended schemas, repeat execution, and
explicit failure on malformed flag data. Both post-processing scripts are deployed
and checked against their final SHA-256 allowlist values.

## Running MCP SQL integration validation

After registering the MCP server with `CopilotIntegration/Register-SqlNexusCopilotIntegration.ps1`,
validate every advertised MCP tool against the registered SQL Nexus database:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1 -UseRegisteredConfiguration -All -NonInteractive
```

The script reads the `sqlnexus_mcp` entry from the VS Code or Copilot CLI MCP configuration,
requests the authoritative tool catalog with `tools/list`, and invokes every tool with safe
representative arguments. It exits with a nonzero code when a tool returns a SQL or JSON-RPC
error. `compare_nexus_databases` is skipped unless a comparison database is registered.

The same validation is exposed in Test Explorer under the `SqlIntegration` category. It is
disabled by default so ordinary unit tests never depend on a live SQL Server. Enable it for the
current process before running that category:

```powershell
$env:SQLNEXUS_RUN_MCP_INTEGRATION_TESTS = "1"
dotnet test .\TestingInfrastructure\UnitTests\SqlNexus.UnitTests\SqlNexus.UnitTests.csproj `
  --filter "TestCategory=SqlIntegration"
```

Set `SQLNEXUS_MCP_CONFIG_PATH` to an explicit `mcp.json` or `mcp-config.json` path when both
registered client configurations differ or when running in automation.

### Validating directly against an explicit server/database

`-UseRegisteredConfiguration` requires MCP client registration (`mcp.json` / `mcp-config.json`) to
exist on the machine. When the server and database are already known � for example, a sibling
repository's integration tests that just imported freshly collected diagnostic data with SQL
Nexus � validate every MCP tool directly, without requiring registration:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1 -Server localhost -Database SqlNexus -All -NonInteractive
```

This resolves `SqlNexus.McpServer.exe` from `bin\Release` next to the script by default; pass
`-ExecutablePath` to point at a different build. `-Database2`, `-TrustedConnection`, and
`-ElevatedPrincipalPolicy` are supported the same way as the registered-configuration path.

### Skipping validation when MCP is not configured

Add `-SkipIfNotConfigured` to exit `0` (instead of failing) when the MCP server executable or
configuration cannot be found, rather than when validation itself fails. This is intended for
callers � such as a consistency/integration-tests harness in another repository � that want to
opportunistically run MCP tool validation when it is available, without failing the overall test
run when SqlNexus hasn't been registered or built there:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1 -Server $ServerName -Database $SqlNexusDb `
  -All -NonInteractive -SkipIfNotConfigured
```

With `-SkipIfNotConfigured`, a missing `SqlNexus.McpServer.exe` (direct mode) or a missing/invalid
registered configuration (registered-configuration mode) prints a yellow "skipping MCP tool
validation" message and exits `0`. A real validation failure � the server and configuration were
found, but a tool returned a SQL or JSON-RPC error � still exits `1` and is never silently skipped.

### Manual / interactive single-tool testing

`Test-McpServer.ps1` also supports running against an explicit server/database (instead of the
registered configuration) and testing one tool at a time:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1
.\SqlNexus.McpServer\Test-McpServer.ps1 -Tool top_cpu_queries -Server localhost -Database SqlNexus
.\SqlNexus.McpServer\Test-McpServer.ps1 -Tool analyze_waits -TrustedConnection false -UserId sqlnexus_reader -Password <password>
.\SqlNexus.McpServer\Test-McpServer.ps1 -Tool analyze_waits -ElevatedPrincipalPolicy ImpersonateReader
```

`-ElevatedPrincipalPolicy` (`Block` or `ImpersonateReader`) mirrors the policy used by
`Register-SqlNexusCopilotIntegration.ps1`. It defaults to `Block` (matching registration) unless
`-ElevatedPrincipalPolicy` is specified, a default is configured in `appsettings.json`, or it is
changed interactively. Running the script without `-Tool` prompts for server, database, and
elevated principal policy, then presents a menu to test individual tools or run the full
registered-configuration validation:

```
Select an option to test MCP Server functionality:
  1. Test Top CPU Queries
  2. Test Top Waits
  3. Test Top Queries by Duration
  4. Test List Nexus Tables
  5. Test Blocking Analysis
  6. Test Performance Summary
  7. Change Elevated Principal Policy (current: Block)
  8. Test All Tools Against Registered MCP Configuration
  9. Exit
```

Menu option `7` lets you change the elevated principal policy at any time � for example, after a
`Block` connection is rejected because the connected SQL principal is elevated � and re-run a tool
without restarting the script. Menu option `8` is equivalent to running
`-UseRegisteredConfiguration -All -NonInteractive` without leaving the interactive session; a
failure is reported inline and returns to the menu instead of exiting the script.

When `ImpersonateReader` is active, the script reports that MCP queries execute as the
non-revertible, loginless `SqlNexusMcpReader` database principal, matching the server's own
startup log message.

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
