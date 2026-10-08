#nullable enable
using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlNexus.McpServer
{
    internal enum ElevatedPrincipalPolicy
    {
        Block,
        ImpersonateReader
    }

    internal sealed class SqlPrivilegeSnapshot
    {
        internal bool IsElevated { get; set; }
        internal bool HasSelect { get; set; }
        internal bool IsDatabaseReadOnly { get; set; }
        internal bool ReaderUserExists { get; set; }
        internal bool ReaderUserIsDataReader { get; set; }
        internal bool ReaderUserIsElevated { get; set; }
        internal bool ReaderUserIsLoginlessSqlUser { get; set; }
        internal bool ReaderUserHasUnexpectedRole { get; set; }
        internal bool ReaderUserHasUnexpectedDirectPermission { get; set; }
        internal bool ReaderUserOwnsSecurable { get; set; }
    }

    internal static class SqlSecurityContext
    {
        internal const string ReaderUserName = "SqlNexusMcpReader";

        internal static ElevatedPrincipalPolicy ParsePolicy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, "block", StringComparison.OrdinalIgnoreCase))
            {
                return ElevatedPrincipalPolicy.Block;
            }

            if (string.Equals(value, "impersonate-reader", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ImpersonateReader", StringComparison.OrdinalIgnoreCase))
            {
                return ElevatedPrincipalPolicy.ImpersonateReader;
            }

            throw new ArgumentException(
                "Elevated principal policy must be either 'block' or 'impersonate-reader'.");
        }

        internal static string ValidateSnapshot(SqlPrivilegeSnapshot snapshot, ElevatedPrincipalPolicy policy)
        {
            if (!snapshot.IsDatabaseReadOnly)
                return "The SQL Nexus database is not read-only. Complete a new SQL Nexus import or apply the documented database hardening procedure.";

            if (policy == ElevatedPrincipalPolicy.Block)
            {
                if (snapshot.IsElevated)
                    return "The connected SQL principal is elevated. Registration must use ElevatedPrincipalPolicy ImpersonateReader, or a non-elevated Windows principal.";

                if (!snapshot.HasSelect)
                    return "The connected SQL principal does not have permission to read the SQL Nexus database.";

                return string.Empty;
            }

            if (!snapshot.ReaderUserExists)
                return "The required loginless database user 'SqlNexusMcpReader' does not exist. Re-import or harden the database before starting the MCP server.";

            if (!snapshot.ReaderUserIsLoginlessSqlUser ||
                !snapshot.ReaderUserIsDataReader ||
                snapshot.ReaderUserIsElevated ||
                snapshot.ReaderUserHasUnexpectedRole ||
                snapshot.ReaderUserHasUnexpectedDirectPermission ||
                snapshot.ReaderUserOwnsSecurable)
                return "The loginless database user 'SqlNexusMcpReader' is not configured as a dedicated db_datareader principal.";

            return string.Empty;
        }

        internal static void ValidateDatabase(string connectionString, ElevatedPrincipalPolicy policy)
        {
            SqlPrivilegeSnapshot snapshot;
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(GetPrivilegeQuery(), connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Unable to determine SQL principal permissions.");

                    snapshot = new SqlPrivilegeSnapshot
                    {
                        IsElevated = reader.GetInt32(0) == 1,
                        HasSelect = reader.GetInt32(1) == 1,
                        IsDatabaseReadOnly = reader.GetInt32(2) == 1,
                        ReaderUserExists = reader.GetInt32(3) == 1,
                        ReaderUserIsDataReader = reader.GetInt32(4) == 1,
                        ReaderUserIsElevated = reader.GetInt32(5) == 1,
                        ReaderUserIsLoginlessSqlUser = reader.GetInt32(6) == 1,
                        ReaderUserHasUnexpectedRole = reader.GetInt32(7) == 1,
                        ReaderUserHasUnexpectedDirectPermission = reader.GetInt32(8) == 1,
                        ReaderUserOwnsSecurable = reader.GetInt32(9) == 1
                    };
                }
            }

            string validationError = ValidateSnapshot(snapshot, policy);
            if (!string.IsNullOrEmpty(validationError))
                throw new InvalidOperationException(validationError);

            if (policy == ElevatedPrincipalPolicy.ImpersonateReader)
            {
                using (SqlConnection testConnection = OpenConnection(connectionString, true))
                using (var command = new SqlCommand(@"
SELECT CASE WHEN USER_NAME() = N'SqlNexusMcpReader'
                  AND IS_ROLEMEMBER(N'db_datareader') = 1
                  AND IS_ROLEMEMBER(N'db_owner') = 0
                  AND IS_ROLEMEMBER(N'db_datawriter') = 0
                  AND IS_ROLEMEMBER(N'db_ddladmin') = 0
                  AND IS_ROLEMEMBER(N'db_securityadmin') = 0
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'SELECT'), 0) = 1
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 0
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 0
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'INSERT'), 0) = 0
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'UPDATE'), 0) = 0
                  AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'DELETE'), 0) = 0
             THEN 1 ELSE 0 END;", testConnection))
                {
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                        throw new InvalidOperationException("The restricted SqlNexusMcpReader execution context could not be verified.");
                }
            }
        }

        internal static SqlConnection OpenConnection(string connectionString, bool impersonateReader)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (impersonateReader)
                builder.Pooling = false;

            var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            if (impersonateReader)
            {
                try
                {
                    using (var command = new SqlCommand(
                        "EXECUTE AS USER = N'SqlNexusMcpReader' WITH NO REVERT;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            }

            return connection;
        }

        internal static string WithDatabase(string connectionString, string database)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }

        internal static string GetPrivilegeQuery()
        {
            return @"
SELECT
    CASE WHEN ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'securityadmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'serveradmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'setupadmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'processadmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'diskadmin'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'dbcreator'), 0) = 1
               OR ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_securityadmin'), 0) = 1
               OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0) = 1
               OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) = 1
               OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'INSERT'), 0) = 1
               OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'UPDATE'), 0) = 1
               OR ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'DELETE'), 0) = 1
         THEN 1 ELSE 0 END AS IsElevated,
    ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'SELECT'), 0) AS HasSelect,
    CASE WHEN DATABASEPROPERTYEX(DB_NAME(), N'Updateability') = N'READ_ONLY' THEN 1 ELSE 0 END AS IsDatabaseReadOnly,
    CASE WHEN DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader') IS NULL THEN 0 ELSE 1 END AS ReaderUserExists,
    ISNULL(IS_ROLEMEMBER(N'db_datareader', N'SqlNexusMcpReader'), 0) AS ReaderUserIsDataReader,
    CASE WHEN ISNULL(IS_ROLEMEMBER(N'db_owner', N'SqlNexusMcpReader'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_datawriter', N'SqlNexusMcpReader'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin', N'SqlNexusMcpReader'), 0) = 1
               OR ISNULL(IS_ROLEMEMBER(N'db_securityadmin', N'SqlNexusMcpReader'), 0) = 1
         THEN 1 ELSE 0 END AS ReaderUserIsElevated,
    CASE WHEN EXISTS
         (
             SELECT 1
             FROM sys.database_principals AS reader
             WHERE reader.name = N'SqlNexusMcpReader'
               AND reader.type = N'S'
               AND reader.authentication_type = 0
         ) THEN 1 ELSE 0 END AS ReaderUserIsLoginlessSqlUser,
    CASE WHEN EXISTS
         (
             SELECT 1
             FROM sys.database_role_members AS drm
             INNER JOIN sys.database_principals AS role
                 ON role.principal_id = drm.role_principal_id
             WHERE drm.member_principal_id = DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader')
               AND role.name <> N'db_datareader'
         ) THEN 1 ELSE 0 END AS ReaderUserHasUnexpectedRole,
    CASE WHEN EXISTS
         (
             SELECT 1
             FROM sys.database_permissions AS permission
             WHERE permission.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader')
               AND NOT (permission.class = 0 AND permission.permission_name = N'CONNECT')
         ) THEN 1 ELSE 0 END AS ReaderUserHasUnexpectedDirectPermission,
    CASE WHEN EXISTS
         (
             SELECT 1
             FROM sys.schemas AS schemaOwner
             WHERE schemaOwner.principal_id = DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader')
         ) OR EXISTS
         (
             SELECT 1
             FROM sys.objects AS objectOwner
             WHERE objectOwner.principal_id = DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader')
         ) OR EXISTS
         (
             SELECT 1
             FROM sys.database_principals AS principalOwner
             WHERE principalOwner.owning_principal_id = DATABASE_PRINCIPAL_ID(N'SqlNexusMcpReader')
         ) THEN 1 ELSE 0 END AS ReaderUserOwnsSecurable;";
        }
    }
}
