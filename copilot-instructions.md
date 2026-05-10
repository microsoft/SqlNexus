# GitHub Copilot Instructions — SqlNexus

## Project Overview
SqlNexus is a .NET Framework 4.8 Windows Forms desktop application for importing and analyzing SQL Server diagnostic data (PSSdiag, SQL LogScout, XEL, TRC, Perfmon, Linux perf). It consists of several projects:

- **sqlnexus** — Main WinForms host (UI, report viewer, theme engine, import orchestration)
- **NexusInterfaces** — Shared importer interfaces (`INexusImporter`, `INexusFileImporter`, etc.)
- **RowsetImportEngine** — Core T-SQL rowset importer
- **ReadTraceNexusImporter** — XEL/TRC trace importer (legacy)
- **PerfmonImporter** — Windows Performance Monitor (.blg/.csv) importer
- **LinuxPerfImporter** — Linux performance data importer
- **ErrorLogImporter** — SQL Server error log importer
- **BulkLoadEx** — Native bulk-load helper
- **TraceEventImporter** — New importer for XEL files using SQL Server's TraceEvent API XeLite (future replacement for ReadTraceNexusImporter)

## Language and Framework
- **C# 7.3**, **.NET Framework 4.8**
- **Windows Forms** for all UI
- **Microsoft.Reporting.WinForms** (ReportViewer) for RDLC report rendering
- **Microsoft.Data.SqlClient** for all SQL Server connectivity
- Use only APIs available in .NET Framework 4.8 — do **not** suggest .NET Core / .NET 5+ APIs

## Code Style
- Follow existing naming conventions: `PascalCase` for methods and properties, `camelCase` for local variables, `m_` prefix for private List fields where already established
- Match the indentation and brace style of the file being edited
- Do not add comments unless they match the style of existing comments or explain genuinely non-obvious logic
- Prefer minimal changes — only modify what is necessary to satisfy the request
- Do not introduce new NuGet packages without explicit instruction

## Architecture Conventions
- All importers implement `INexusImporter` (and optionally `INexusFileImporter`, `INexusProgressReporter`) from `NexusInterfaces`
- Importer ordering is controlled in `fmImport.OrderedImporterFiles()` — TraceEventImporter (150) must run after RowsetImportEngine (100)
- ReadTrace and TraceEventImporter are mutually exclusive — never enable both simultaneously
- The `CustomXELImporter` handles SQLDiag, AlwaysOn, and System Health XEL files independently of the plugin importers
- Report parameters are set via `ReportParameter` objects; `ContrastTheme` must be propagated to every report that declares it
- `ThemeManager` owns all color definitions for the three themes: **Default**, **Aquatic** (`#202020` background), and **Desert** (`#FFFAEF` background)
- The `TopToolStripPanel` layout is order-sensitive: `menuBarMain → toolbarService → toolbarReport → toolbarMain` (last added = topmost row)

## Security — CodeQL Standards
- **Never** construct SQL command text by concatenating user-supplied strings; use parameterized queries or stored procedures
- Database names passed to SQL commands must be bracket-escaped (`[dbname]`) before use — see `CodeQL [SM03934]` annotation pattern in `fmImport.cs`
- Do not log passwords, connection strings with credentials, or other secrets via `LogMessage`
- Use `ScriptIntegrityChecker.VerifyScript()` before executing any `.sql` or `.cmd` file on disk
- Validate all file paths before use; reject paths containing directory traversal sequences
- Do not use `Assembly.LoadFile` on untrusted paths without verification
- Prefer `Microsoft.Data.SqlClient` over `System.Data.SqlClient` for all new SQL connectivity code

## Accessibility — WCAG 2.1 Standards
- Every interactive control must have a meaningful `AccessibleName` and `AccessibleDescription`
- Use `AccessibleTextBox` (the project's custom subclass) instead of plain `TextBox` for all user-editable fields — it implements the UIA Text pattern required for screen readers
- Disabled `Label` controls must not rely on WinForms' default disabled rendering (which ignores `ForeColor`) — keep labels `Enabled = true` and set a muted `ForeColor` via `ThemeManager.CurrentThemeName` to simulate the disabled appearance
- All chart axes, titles, and legend text in RDLC reports must have explicit `<Color>` set to `=Variables!ReportTextColor.Value` so they remain readable in all contrast themes
- The "No Data Available" warning banner uses a `Gold` background — its text must always be `Black` regardless of theme
- `ToolStripLabel` and `ToolStripComboBox` items must have `AccessibleName` set
- Keyboard navigation must work for all menus and toolbars; submenus must remain open when the user toggles a checkbox option (`ToolStripDropDownCloseReason.ItemClicked` should be cancelled)
- Link labels must always use `LinkBehavior.AlwaysUnderline` for WCAG 1.4.1 compliance
- When Windows High Contrast mode is active (`SystemInformation.HighContrast`), defer to `SystemColors` rather than theme colors — see `ThemeManager.ApplyHighContrastTheme()`

## RDLC Report Theming
- Every report must declare a `ContrastTheme` parameter with valid values `None`, `Aquatic`, `Desert`
- Define exactly 11 theme variables: `ReportTextColor`, `BodyBackgroundColor`, `TitleColor`, `TableHeadingColor`, `TableHeadingFontColor`, `ChartColor`, `ChartSecondaryColor`, `ChartGradientStyle`, `TableShowCell`, `TableHidcell`, `URILinkFontColor`
- Replace all hardcoded colors in `<Style>` blocks with the appropriate variable reference
- Header `<BackgroundColor>` → `=Variables!TableHeadingColor.Value`
- Body `<BackgroundColor>` → `=Variables!BodyBackgroundColor.Value`
- Drillthrough/hyperlink `<Color>` → `=Variables!URILinkFontColor.Value`
- Chart area and chart background → `=Variables!ChartColor.Value`
- All `<TextRun>` font-styled text without an explicit color → `=Variables!ReportTextColor.Value`

## Toolbar and Settings Persistence
- Toolbar visibility (`ShowStandardToolbar`, `ShowReportToolbar`, `ShowDataCollectionToolbar`) must be explicitly saved in `fmNexus_FormClosing` — DataBindings alone are not reliable
- `ShowHideUIElements()` must explicitly set `toolbarMain.Visible` from the persisted setting to guard against DataBinding desync during `InitializeComponent`
- `SelectLoadReport()` must read `Properties.Settings.Default.ShowReportToolbar` as the source of truth when setting `toolbarReport.Visible` — never blindly set it to `true`
- When iterating `TopToolStripPanel` rows, use `ToolStripPanel.Join(toolStrip, rowIndex)` to restore toolbar row position after database changes

## TextRowsets.xml — Rowset Definition Standards
`TextRowsets.xml` (and its custom extension `TextRowsetsCustom.xml`) define how the `RowsetImportEngine` maps sections of SQL Server diagnostic text output files into SQL tables. Follow these standards when adding or modifying rowset definitions:

### Structure
- The root element is `<TextImport>` containing a single `<KnownRowsets>` block
- Each rowset is a `<Rowset>` element with mandatory attributes: `name`, `enabled`, `identifier`, and `type`
- Column definitions go inside `<KnownColumns>` — columns not listed are imported as `VarCharColumn` by default

### Naming
- Table names (`name` attribute) must be prefixed with `tbl_` 
- Column names must match exactly the column headers that appear in the source diagnostic text output — including spaces if present (e.g. `"Wait Time"`)


### Identifiers
- The `identifier` attribute is the exact string the engine uses to detect the start of this rowset in the text file — it must be unique across all rowsets in the file
- Use the actual header line from the diagnostic output script (e.g. `"-- sysperfinfo"`, `"-- sys.dm_os_memory_health_history --"`) — do not paraphrase it
- If the identifier could appear in multiple unrelated contexts, make it more specific

### Column Types
Use the most appropriate type from `RowsetImportEngine` — do not default everything to `VarCharColumn`:

| Type | Use for |
|---|---|
| `DateTimeColumn` | Any `runtime`, timestamp, or date/time column |
| `BigIntColumn` | `rownum`, large integer counters |
| `IntColumn` | SPIDs, counts, flags |
| `FloatColumn` | Decimal metrics (wait time, CPU %) |
| `DecimalColumn` | Precise decimal values |
| `VarCharColumn` | Short string identifiers, names |
| `NVarCharColumn` | Unicode text, query text, messages |
| `VarBinaryColumn` | Binary handles (e.g. `query_hash`) |

### Required Columns
- In some cases may include `rownum` typed as `BigIntColumn` with `valuetoken="ROWNUMBER"` as the first column
- In some cases include `runtime` typed as `DateTimeColumn` with `valuetoken="RUNTIME"` as the second column — this is used by most reports to filter by time range

### Value Tokens and Define Tokens
- `valuetoken` columns are populated by the engine from context (e.g. `ROWNUMBER`, `RUNTIME`, `SCRIPTNAME`, `USERNAME`, `IMPORTDATE`, `INPUTFILENAME`) — do not try to parse these from the text file
- `definetoken` columns extract a value from the identifier/header line itself and store it for use by `valuetoken` columns later in the same rowset

### Enabled Flag
- Set `enabled="true"` for all rowsets that should be active by default
- Set `enabled="false"` only for rowsets that are experimental, deprecated, or conditionally loaded — always add a comment explaining why


### Security
- The `name` attribute becomes a SQL table name — it must not be constructed from user input and must only contain alphanumeric characters and underscores
- Column `length` attributes are used to size `VARCHAR`/`NVARCHAR` columns — always specify an explicit `length` for string columns; omitting it defaults to a short platform-defined length that may truncate data

## Exception Handling
- Never leave a `catch` block empty — a silent catch hides bugs and makes failures impossible to diagnose
- Every `catch` block must do at least one of: log the exception via `MainForm.LogMessage(...)` or `Util.Logger.LogMessage(...)`, rethrow, or return a meaningful failure value
- Prefer `catch (Exception ex)` with a named variable over bare `catch {}` or `catch (Exception)` with no variable — the exception message, source, and stack trace should be accessible for logging
- Use `Globals.HandleException(ex, this, MainForm)` for unhandled exceptions in UI event handlers — it logs to both the silent log and the dialog, and sets `ExceptionEncountered = true`
- For expected, recoverable failures (e.g. file not found, SQL connection refused) catch the most specific exception type (`SqlException`, `IOException`, `UnauthorizedAccessException`) rather than the base `Exception`
- Do not swallow `SqlException` silently — always log `sqlex.Message` and, where appropriate, surface it to the user via `MessageOptions.Dialog`
- When an inner try/catch exists solely to protect a logging call (as in `Globals.HandleException`), fall back to `System.Diagnostics.Debug.WriteLine` so the failure is at minimum visible in the debugger output window
- Do not use exceptions for control flow — validate inputs before calling methods rather than relying on catching the resulting exception
