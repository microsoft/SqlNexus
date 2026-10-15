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
exist on the machine. When the server and database are already known — for example, a sibling
repository's integration tests that just imported freshly collected diagnostic data with SQL
Nexus — validate every MCP tool directly, without requiring registration:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1 -Server localhost -Database SqlNexus -All -NonInteractive
```

This resolves `SqlNexus.McpServer.exe` from `bin\Release` next to the script by default; pass
`-ExecutablePath` to point at a different build. `-Database2`, `-TrustedConnection`, and
`-ElevatedPrincipalPolicy` are supported the same way as the registered-configuration path.

### Skipping validation when MCP is not configured

Add `-SkipIfNotConfigured` to exit `0` (instead of failing) when the MCP server executable or
configuration cannot be found, rather than when validation itself fails. This is intended for
callers — such as a consistency/integration-tests harness in another repository — that want to
opportunistically run MCP tool validation when it is available, without failing the overall test
run when SqlNexus hasn't been registered or built there:

```powershell
.\SqlNexus.McpServer\Test-McpServer.ps1 -Server $ServerName -Database $SqlNexusDb `
  -All -NonInteractive -SkipIfNotConfigured
```

With `-SkipIfNotConfigured`, a missing `SqlNexus.McpServer.exe` (direct mode) or a missing/invalid
registered configuration (registered-configuration mode) prints a yellow "skipping MCP tool
validation" message and exits `0`. A real validation failure — the server and configuration were
found, but a tool returned a SQL or JSON-RPC error — still exits `1` and is never silently skipped.

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

Menu option `7` lets you change the elevated principal policy at any time — for example, after a
`Block` connection is rejected because the connected SQL principal is elevated — and re-run a tool
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
