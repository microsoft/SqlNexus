--This script can be used to validate that tables exist 
--Not all tables will exist in every import because they depend on what data was collected by tools like PSSDIAG or SQLLogScout
--But this is a base to start with 



select top 5 * from tbl_IMPORTEDFILES
go
select top 5 * from tbl_SCRIPT_ENVIRONMENT_DETAILS
go
select top 5 * from tbl_SYSPERFINFO
go
select top 5 * from tbl_SYSPERFINFO
go
select top 5 * from tbl_SQL_CPU_HEALTH													
go
select top 5 * from tbl_RUNTIMES	
go
select top 5 * from tbl_SPINLOCKSTATS
go
select top 5 * from tbl_MEMORYSTATUS_BUF_DISTRIBUTION									
go
select top 5 * from tbl_MEMORYSTATUS_BUF_COUNTS
go
select top 5 * from tbl_MEMORYSTATUS_PROC_CACHE
go
select top 5 * from tbl_MEMORYSTATUS_DYNAMIC_MEM_MGR
go
select top 5 * from tbl_MEMORYSTATUS_GLOBAL_MEM_OBJ
go
select top 5 * from tbl_MEMORYSTATUS_QUERY_MEM_OBJ
go
select top 5 * from tbl_MEMORYSTATUS_OPTIMIZATION_QUEUE
go
select top 5 * from tbl_ERRORLOGS_RAW
go
select top 5 * from tbl_SPCONFIGURE
go
select top 5 * from tbl_loaded_modules
go
select top 5 * from tbl_dm_os_loaded_modules_non_microsoft
go
select top 5 * from tbl_dm_os_loaded_modules
go
select top 5 * from tbl_Query_Execution_Memory
go
select top 5 * from tbl_Query_Execution_Memory_MemScript
go
select top 5 * from tbl_dm_os_ring_buffers_mem
go
select top 5 * from tbl_dm_os_memory_objects
go
select top 5 * from tbl_dm_os_memory_pools
go
select top 5 * from tbl_sysaltfiles
go
select top 5 * from tbl_StartupParameters
go
select top 5 * from tbl_SPHELPDB
go
select top 5 * from tbl_XPMSVER
go
select top 5 * from tbl_REQUESTS
go
select top 5 * from tbl_NOTABLEACTIVEQUERIES
go
select top 5 * from tbl_HEADBLOCKERSUMMARY
go
select top 5 * from tbl_OS_WAIT_STATS
go
select top 5 * from tbl_SYSOBJECTS
go
select top 5 * from tbl_DM_OS_MEMORY_CLERKS
go
select top 5 * from tbl_DM_OS_MEMORY_CACHE_COUNTERS
go
select top 5 * from tbl_DM_OS_MEMORY_CACHE_CLOCK_HANDS
go
select top 5 * from tbl_DM_OS_MEMORY_CACHE_HASH_TABLES
go
select top 5 * from tblErrorlog
go
select top 5 * from tbl_FileStats
go
select top 5 * from tbl_DM_OS_MEMORY_CACHE_ENTRIES
go
select top 5 * from tbl_DM_EXEC_CACHED_PLANS
go
select top 5 * from tbl_DM_EXEC_QUERY_STATS
go
select top 5 * from tbl_DM_OS_MEMORY_OBJECTS
go
select top 5 * from tbl_DM_EXEC_CONNECTIONS
go
select top 5 * from tbl_MissingIndexes
go
select top 5 * from tbl_dm_db_stats_properties_for_master
go
select top 5 * from tbl_SYSINDEXES
go
select top 5 * from tbl_OS_WAIT_STATS
go
select top 5 * from tbl_RESOURCE_STATS
go
select top 5 * from tbl_RESOURCE_USAGE
go
select top 5 * from tbl_DB_CONN_STATS
go
select top 5 * from tbl_EVENT_LOG
go
select top 5 * from tbl_sp_configure
go
select top 5 * from tbl_dm_os_memory_brokers
go
select top 5 * from tbl_dm_exec_query_memory_grants
go
select top 5 * from tbl_dm_exec_query_resource_semaphores
go
select top 5 * from tbl_TopN_QueryPlanStats
go
select top 5 * from tbl_workingset_trimming
go
select top 5 * from tbl_PowerPlan
go
select top 5 * from tbl_ThreadStats
go
select top 5 * from tbl_LockSummary
go
select top 5 * from tbl_ServerProperties
go
select top 5 * from tbl_Sys_Configurations
go
select top 5 * from tbl_StartupParameters
go
select top 5 * from tbl_DatabaseFiles
go
select top 5 * from tbl_SysDatabases
go
select top 5 * from tbl_TraceFlags
go
select top 5 * from tbl_XEvents
go
select top 5 * from tbl_availability_groups
go
select top 5 * from tbl_dm_hadr_availability_replica_states
go
select top 5 * from tbl_availability_replicas
go
select top 5 * from tbl_dm_os_sys_info
go
select top 5 * from tbl_dm_os_nodes
go
select top 5 * from tbl_Thread_Stats_Snapshot
go
select top 5 * from tbl_dm_os_schedulers_snapshot
go
select top 5 * from tbl_Thread_Stats
go
select top 5 * from tbl_System_Requests
go
select top 5 * from tbl_sysperfinfo
go
select top 5 * from tbl_dm_os_latch_stats
go
select top 5 * from tbl_PlanCache_Stats
go
select top 5 * from tbl_dm_db_file_space_usage
go
select top 5 * from tbl_dm_exec_cursors
go
select top 5 * from tbl_profiler_trace_summary
go
select top 5 * from tbl_dm_os_ring_buffers_conn
go
select top 5 * from tbl_ring_buffer_temp
go
select top 5 * from tbl_trace_event_details
go
select top 5 * from tbl_dm_os_memory_nodes
go
select top 5 * from tbl_resource_governor_configuration
go
select top 5 * from tbl_resource_governor_workload_groups
go
select top 5 * from tbl_resource_governor_resource_pools
go
select top 5 * from tbl_dbm_partner_time
go
select top 5 * from tbl_dbm_timeout_connections
go
select top 5 * from tbl_dbm_perf_control
go
select top 5 * from tbl_dbm_perf_connection
go
select top 5 * from tbl_dbm_perf_executions
go
select top 5 * from tbl_database_mirroring
go
select top 5 * from tbl_dm_db_mirroring_connections
go
select top 5 * from tbl_DiagInfo
go
select top 5 * from tbl_dm_db_stats_properties
go
select top 5 * from tbl_DisabledIndexes
go
select top 5 * from tbl_QDS_Query_Stats
go
select top 5 * from tbl_query_store_runtime_stats_interval
go
select top 5 * from tbl_query_store_runtime_stats
go
select top 5 * from tbl_query_store_runtime_stats_interval
go
select top 5 * from tbl_query_store_query
go
select top 5 * from tbl_query_store_query_text
go
select top 5 * from tbl_query_store_plan
go
select top 5 * from tbl_dm_xtp_gc_stats
go
select top 5 * from tbl_xtp_gc_queue_stats
go
select top 5 * from tbl_db_xtp_table_memory_stats
go
select top 5 * from tbl_xtp_system_memory_consumers
go
select top 5 * from tbl_dm_os_performance_counters
go
select top 5 * from tbl_high_cpu_queries
go
select top 5 * from tbl_server_times
go
select top 5 * from tbl_database_options
go
select top 5 * from tbl_db_TDE_Info
go
select top 5 * from tbl_server_audit_status
go
select top 5 * from tbl_Top10_CPU_Consuming_Procedures
go
select top 5 * from tbl_Top10_CPU_Consuming_Triggers
go
select top 5 * from tbl_Hist_Top10_CPU_Queries_ByQueryHash
go
select top 5 * from tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash
go
select top 5 * from tbl_Hist_Top10_ElapsedTime_Queries_ByQueryHash
go
select top 5 * from tbl_Hist_Top10_CPU_Queries_by_Planhash_and_Queryhash
go
select top 5 * from tbl_Hist_Top10_LogicalReads_Queries_by_Planhash_and_Queryhash
go
select top 5 * from tbl_Hist_Top10_ElapsedTime_Queries_by_Planhash_and_Queryhash
go
select top 5 * from tbl_hadron_replica_info
go
select top 5 * from tbl_availability_groups
go
select top 5 * from tbl_hadr_cluster
go
select top 5 * from tbl_hadr_cluster_members
go
select top 5 * from tbl_hadr_cluster_networks
go
select top 5 * from tbl_availability_replicas
go
select top 5 * from tbl_ActiveProcesses_OS
go
select top 5 * from tbl_SystemInformation
go
select top 5 * from tbl_ActiveProcesses_with_ModulesLoaded
go
select top 5 * from tbl_transaction_perfmon_counters
go
select top 5 * from tbl_tempdb_space_usage_by_file
go
select top 5 * from tbl_dm_db_file_space_usage_summary
go
select top 5 * from tbl_dm_db_session_space_usage
go
select top 5 * from tbl_dm_db_task_space_usage
go
select top 5 * from tbl_open_transactions
go
select top 5 * from tbl_tempdb_usage_by_object
go
select top 5 * from tbl_tempdb_waits
go
select top 5 * from tbl_dm_tran_aborted_transactions
go
select top 5 * from tbl_dm_tran_persistent_version_store_stats
go