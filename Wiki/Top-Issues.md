## Issues related to running SQL Nexus

### 1. File Blocked after download

When downloading SQL Nexus from the internet, Windows may block the executable.

- Right-click **sqlnexus.exe** and select **Properties**
- If the file is blocked you will see an **Unblock** checkbox or button at the bottom — check it and click **OK**

### 2. "Windows Protected your PC" SmartScreen warning

When running SQL Nexus for the first time on Windows 10/11 you may see a SmartScreen warning.

- Click **More Info** and then select **Run Anyway**

### 3. ReadTrace (RML Utilities) not found or import produces no trace results

SQL Nexus requires RML Utilities (ReadTrace.exe) version **9.3.78 or later** to import `.trc` and `.xel` trace files.

- Download the latest RML Utilities from the [RML Utility page](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
- If ReadTrace is not found, the ReadTrace importer will be **disabled by default** in the Import Options
- Verify ReadTrace.exe is installed in the same directory as sqlnexus.exe or in a path SQL Nexus can discover

### 4. "Database already contains Nexus data" message at import

If you attempt to import into a database that was previously used with SQL Nexus, you will see this message.

- In the Import window click **Options** and check **Drop existing tables** before importing, **or**
- Select **\<New Database\>** from the database dropdown on the toolbar to import into a fresh database

### 5. Reports show no data after import

This typically happens when:

- **Wrong data path**: Ensure you provide the **folder path** where collected data resides, not an individual file name
- **Multiple SQL instances in data**: For a SQL LogScout **All Instances** collection, select the desired instance subfolder. SQL Nexus automatically scans its direct sibling named `SharedOutputFiles`; the Import page shows an **Also scanning:** banner when that folder is detected
- **ReadTrace importer was disabled**: Check Import → Options → Importers and verify the ReadTrace importer is enabled if you expect trace-based reports (Bottleneck Analysis, Top Queries, etc.)
- **No matching files found**: Verify the collection folder contains files matching the expected patterns (`.out`, `.trc`, `.xel`, `.blg`)

### 6. SQL Nexus requires a non-production SQL Server instance

SQL Nexus imports diagnostic data into a SQL Server database and runs analysis scripts against it.  
**Do not point SQL Nexus at a production SQL Server instance.** Use a dedicated developer or test instance.  
SQL Server **2016 or later** is required.

### 7. .NET Framework 4.8 is required

If SQL Nexus fails to launch, ensure [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) is installed on the machine.

### 8. Linux performance data not importing

Linux perf data (IO stats, CPU, memory, networking) collected via SQL LogScout requires the **Linux Perf Importer** to be enabled.

- In the Import window click **Options → Importers** and verify **Linux Perf** is enabled
- The option **"Import to SQL (Linux Perf)"** must be checked for data to be loaded into the database

### 9. XEL files (SQLDiag, Always On Health, System Health) not imported

These three XEL file types are **disabled by default** to avoid inflating database size.

- In the Import window click **Options → Importers** and enable the desired XEL import options before running the import
- The Custom XEL importer reads only the selected instance folder. If the files exist only in a sibling `SharedOutputFiles` folder, move the needed files into the selected folder and re-import. SQL Nexus reports this condition in the status and application log
- See [Detailed XEvents Tracing](https://github.com/Microsoft/SqlNexus/wiki/Detailed-XEvents-Tracing) for more details

### 10. Post-processing fails with a certificate trust or encryption error

SQL Nexus passes its connection encryption choices to the `sqlcmd` commands that run
post-processing scripts. This is especially relevant when connecting to SQL Server 2025, where
encrypted connections are expected by current client tools.

- Confirm that the SQL Server certificate is valid, trusted by the computer running SQL Nexus, and
	contains the SQL Server name used for the connection. Installing or trusting the correct
	certificate is the preferred solution.
- Verify that **Encrypt connection** is appropriate for the server. This setting causes
	post-processing to use the `sqlcmd -N` option.
- For a known server that intentionally uses a self-signed or otherwise untrusted certificate,
	enable **Trust server certificate**. This causes post-processing to use `sqlcmd -C` and bypasses
	certificate validation, so consider the security implications before enabling it.
- Review the SQL Nexus application log for the failing `sqlcmd` step, then rerun the import or
	post-processing operation after changing the setting.

### 11. SharedOutputFiles data is missing or skipped

SQL Nexus automatically scans `SharedOutputFiles` only when it is an existing direct sibling of
the selected SQL LogScout instance folder. If expected shared data is absent:

- Confirm the folder is named exactly `SharedOutputFiles` and is next to the selected instance
	folder. Look for the **Also scanning:** banner on the Import page
- Check the application log for the resolved primary and shared paths and for folder-enumeration
	errors, such as access denied or a path that is too long
- If the same file name appears in both folders, SQL Nexus imports the selected-folder copy. When
	the sizes differ, it reports that the shared copy was skipped; import `SharedOutputFiles`
	separately if it contains different capture data
- Perfmon and ReadTrace use the shared folder only as a fallback. If the selected folder already
	has matching files, shared matches are not also imported; this is recorded in the status and log

See [[How to use SQL Nexus#sql-logscout-all-instances-collections]] for the complete behavior.

### 12. Perfmon BLG import fails to create SQLNexusDSN or does not use encryption

SQL Nexus uses `relog.exe` to import `.BLG` files. Because `relog.exe` requires an ODBC data source,
SQL Nexus creates a user DSN named `SQLNexusDSN` once per Perfmon import. It dynamically discovers
the SQL Server ODBC drivers installed on the local machine and prefers the newest
`ODBC Driver NN for SQL Server` version.

If the Perfmon import reports that it could not create `SQLNexusDSN`:

- Review the application log for the driver-selection and DSN-creation messages.
- Install or repair a current Microsoft ODBC Driver for SQL Server, then rerun the import.
- Confirm that the Windows user running SQL Nexus can create a user DSN.
- Do not create a DSN named `Nexus`; current versions use `SQLNexusDSN` and keep the registration
  and the `relog.exe` command synchronized automatically.

The Perfmon DSN inherits the SQL Nexus **Encrypt connection** and **Trust server certificate**
settings. If only the legacy `SQL Server` ODBC driver is available, SQL Nexus can use it as a
last-resort fallback, but that driver may not enforce requested encryption. The application log
warns when this occurs. Install a modern Microsoft ODBC Driver for SQL Server when encrypted
Perfmon imports are required.

See [[Importer Options#relog-dsn-odbc-driver-selection-and-encryption]] for the complete selection
and fallback behavior.
