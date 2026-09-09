# SqlNexus Report Map

| Report Name | Analysis Area | Key Tables |
|------------|---------------|------------|
| Bottleneck Analysis | CPU utilization + top wait categories | CounterData, CounterDetails, tbl_ServerProperties, tbl_OS_WAIT_STATS |
| Blocking and Wait Statistics | Wait categories + blocking chains | tbl_OS_WAIT_STATS, tbl_REQUESTS |
| WaitDetails | Specific wait type drill-down over time | tbl_OS_WAIT_STATS, tbl_REQUESTS |
| Other Waits | Less common wait type details | tbl_OS_WAIT_STATS |
| Blocking Chain Detail | Individual blocking chain analysis | tbl_REQUESTS |
| Blocking Runtime Detail | Runtime details of blocking sessions | tbl_REQUESTS |
| AnalysisSummary | Best practices rules and auto-analysis | tbl_AnalysisSummary |
| ReadTrace_Main | Trace data overview | ReadTrace.tblBatches, ReadTrace.tblUniqueBatches |
| ReadTrace_UniqueBatchTopN | Top N unique queries by resource | ReadTrace.tblBatches, ReadTrace.tblUniqueBatches |
| Query Hash | Query hash analysis | tbl_QueryHashStats |
| Query_Store | Query Store data | Query Store DMVs |
| TopPlanAnalysis | Execution plan analysis | ReadTrace tables |
| Missing Indexes | Missing index recommendations | tbl_MissingIndexes |
| SysIndexes | Existing index usage | tbl_SysIndexes |
| Perfmon_CPU | CPU performance counters | CounterData, CounterDetails |
| Perfmon_IO | I/O performance counters | CounterData, CounterDetails |
| Perfmon_Memory | Memory performance counters | CounterData, CounterDetails |
| Memory Brokers | Memory broker diagnostics | tbl_MemoryBrokers |
| Memory Clerks | Memory clerk breakdown | tbl_MemoryClerks |
| Query Execution Memory | Memory grant analysis | tbl_dm_exec_query_memory_grants |
| ServerConfiguration | Server configuration settings | tbl_ServerProperties |
| DatabaseConfiguration | Database-level configuration | tbl_DatabaseConfig |
| Spinlock Stats | Spinlock contention | tbl_SpinlockStats |
| Tempdb_Space_Use | TempDB space usage | tbl_TempdbSpaceUsage |
| AlwaysOn_AGBasics | AG overview and health | AG-related DMVs |
| AlwaysOn_AGDetails | Detailed AG diagnostics | AG-related DMVs |
| Loaded Modules | Loaded DLLs in SQL Server | tbl_LoadedModules |
| Active Traces and XEvents | Active traces and XEvent sessions | tbl_ActiveTraces |
| SQLAssessmentAPI | SQL Assessment API results | tbl_SQLAssessment |
