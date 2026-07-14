# SQL Server Setup / Installation Health - Scenario Guide

## PURPOSE
Diagnose SQL Server **setup** and **installation** health from pre-collected SQL Nexus data: which SQL Server components are installed, and whether any Windows Installer **MSI/MSP** cached packages are missing — a condition that can block patching, repair, or uninstall operations.

---

## WHEN TO USE
- SQL Server **patching / cumulative update** fails or rolls back
- **Repair** or **uninstall** of an instance fails
- Setup complains about a **missing MSI or MSP** in the Windows Installer cache
- Verifying which SQL Server **components / features** are installed
- General "is the SQL Server installation healthy?" check

**Keywords**: setup, install, installation, installed, patch, patching, cumulative update, CU, service pack, MSI, MSP, Windows Installer cache, repair, uninstall, components, features.

---

## PRIMARY MCP TOOL

### `analyze_setup_health`
**Purpose**: One-stop SQL Server setup/installation health assessment.
**Use When**: Any of the symptoms above, or to confirm installed components.

Inspects these SQL Nexus tables when present (missing ones are reported under `tables_not_present`):

| Section | Table | What it tells you |
|---------|-------|-------------------|
| `installed_sql_programs` | `tbl_installed_programs` (filtered `name LIKE 'sql%'`) | Installed SQL Server related programs/components; each well-known component is flagged present/missing |
| `missing_msi_msp_packages` | `tbl_setup_missing_msi_msp_packages` | Missing Windows Installer MSI/MSP cached packages. **ANY row = a real problem.** |

**Output shape**:
- `issues_found` — pre-triaged problems with a severity rating
- `tables_not_present` — setup tables absent from this collection
- `sections` — full per-table data, including `known_component_status`

---

## ISSUE INTERPRETATION

| Finding | Severity | Meaning / Action |
|---------|----------|------------------|
| Missing MSI/MSP package(s) detected | High | `tbl_setup_missing_msi_msp_packages` has one or more rows. Missing cached installer files will block patching, repair, or uninstall and **must** be restored to the Windows Installer cache (`%WINDIR%\Installer`). Review the `*_MissingMsiMsp_Detailed.txt` output file collected by SQL LogScout for the exact packages and recovery steps. |
| Component present/missing (informational) | Info | `known_component_status` flags well-known components (Database Engine, SSAS, SSRS, SSIS, Full-Text, SSMS, Native Client, Browser, Machine Learning, PolyBase). Use to confirm expected features are installed. |

> **Key rule**: If `tbl_setup_missing_msi_msp_packages` contains **any** rows at all, treat it as a setup issue — there is no "safe" number of missing packages.

---

## INVESTIGATION FLOW

1. Call `analyze_setup_health`.
2. If both setup tables appear under `tables_not_present` ? this collection has **no setup data**. Tell the user the LogScout collection that captures installed programs / missing MSI-MSP is required.
3. Check `issues_found`:
   - **Missing MSI/MSP present** ? High priority. Direct the user to the `*_MissingMsiMsp_Detailed.txt` file collected by SQL LogScout for the full list and remediation guidance (typically restoring the cached `.msi`/`.msp` from the original media or a matching machine). Advise resolving this **before** attempting any patch, repair, or uninstall.
4. Review `known_component_status` in the `installed_sql_programs` section to confirm the expected components are installed and spot unexpected gaps.

---

## CROSS-CHECK QUERIES

If deeper detail is needed beyond the tool output, use `query_nexus_database`:

```sql
-- All installed SQL Server components/programs
IF OBJECT_ID('dbo.tbl_installed_programs') IS NOT NULL
    SELECT * FROM dbo.tbl_installed_programs WHERE name LIKE 'sql%' ORDER BY name;
```

```sql
-- Missing MSI/MSP packages — ANY row indicates a setup/patching problem
IF OBJECT_ID('dbo.tbl_setup_missing_msi_msp_packages') IS NOT NULL
    SELECT * FROM dbo.tbl_setup_missing_msi_msp_packages;
```

---

## RELATED DATA (optional context)
- `tbl_windows_hotfixes_installed` — Windows patches/hotfixes present (helps confirm patch level).
- `tbl_ServerProperties` / `tbl_XPMSVER` — SQL Server edition and build number, useful to match the correct MSI/MSP versions.

---

## DATA GAPS
If `analyze_setup_health` reports the setup tables under `tables_not_present`, the collection did not include installed-programs / missing-MSI-MSP data. Recommend re-running SQL LogScout with a scenario that captures setup diagnostics, then re-import into SQL Nexus. For missing MSI/MSP specifically, the `*_MissingMsiMsp_Detailed.txt` output file is the authoritative detail source.
