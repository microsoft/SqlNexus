**What is the quickest way to learn this tool?**

Go to the [[How to use SQL Nexus]] page and follow the quick steps there on 
data collection, importing, and analyzing results. You can also check out the 
[[How to Videos]] page for step-by-step video tutorials.

---

**What tool should I use to collect data for SQL Nexus?**

[SQL LogScout](https://github.com/microsoft/sql_logscout) is the recommended 
modern data collection tool for SQL Server on both Windows and Linux. 
[PSSDIAG/DiagManager](https://github.com/microsoft/diagmanager) is the legacy 
alternative for older environments.

---

**Can SQL Nexus import data collected from multiple instances?**

SQL Nexus imports data for one instance at a time. If the folder you specify 
contains data collected from multiple SQL Server instances, SQL Nexus will 
detect this and prompt you to select which instance to import. Once the import 
for one instance is complete, create a new database and repeat the import 
process to analyze a different instance.

---

**What version of SQL Server do I need to import data into?**

SQL Nexus requires a **SQL Server 2016 or later** instance as the target 
database where it imports and analyzes data. This does not need to be the 
same server you are troubleshooting — it is recommended to use a separate 
non-production instance.

---

**Why are my reports empty after import?**
The most common reasons are:
- The ReadTrace (RML Utilities) importer was disabled — check **Import → Options → Importers**
- The data folder path points to a file instead of a folder
- The wrong SQL Server instance was selected during import (for multi-instance collections)
- The collected data does not contain files matching the expected formats (`.out`, `.trc`, `.xel`, `.blg`)

See [[Top Issues]] for more detailed troubleshooting steps.

---

**Why does SQL Nexus say "database already contains Nexus data"?**

You are importing into a database that was previously used with SQL Nexus. 
Either enable **Drop existing tables** in **Import → Options**, or select 
**\<New Database\>** from the database dropdown on the toolbar to start fresh.

---

**Does SQL Nexus support SQL Server on Linux?**

Yes. SQL Nexus can import and analyze Linux performance data (IO stats, CPU, 
memory, networking, process stats) collected by SQL LogScout. Enable the 
**Linux Perf** importer under **Import → Options → Importers** before importing.

---

**I received an error — what do I do?**

Check the [[Top Issues]] page first. If your issue is not listed there, visit 
[GitHub Discussions](https://github.com/microsoft/SqlNexus/discussions) to 
search for answers or ask a question.