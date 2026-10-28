#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Builds the MCP server's SQL connection string. Defaults to an encrypted connection with full
    /// server-certificate validation (matching the desktop application). Trusting an unvalidated
    /// certificate requires two explicit settings so it can never be enabled by accident.
    /// </summary>
    internal static class SqlConnectionSecurity
    {
        internal static string BuildConnectionString(
            string server,
            string database,
            bool trustedConnection,
            string? userId,
            string? password,
            string? encrypt,
            string? trustServerCertificate,
            string? allowCertificateValidationBypass,
            string applicationName,
            ICollection<string> warnings)
        {
            bool encryptValue = ParseBool(encrypt, true, "Encrypt");
            bool trustValue = ParseBool(trustServerCertificate, false, "TrustServerCertificate");
            bool allowBypass = ParseBool(allowCertificateValidationBypass, false, "AllowCertificateValidationBypass");

            if (trustValue && !allowBypass)
            {
                throw new InvalidOperationException(
                    "SqlNexus:TrustServerCertificate=true disables SQL Server certificate validation and is refused unless " +
                    "SqlNexus:AllowCertificateValidationBypass=true is also set. Install a certificate the client trusts, or set both " +
                    "options only for a lab environment that you control.");
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = trustedConnection,
                Encrypt = encryptValue,
                TrustServerCertificate = trustValue,
                ApplicationName = applicationName,
                ConnectTimeout = 30
            };

            if (!trustedConnection)
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("SQL authentication requires SqlNexus:UserId and SqlNexus:Password. Supply the password through the SqlNexus__Password environment variable.");

                builder.UserID = userId;
                builder.Password = password;
            }

            if (!encryptValue)
            {
                warnings.Add("SQL connection encryption is DISABLED (SqlNexus:Encrypt=false). Queries, results and any SQL credentials can be observed or tampered with on the network.");
            }
            else if (trustValue)
            {
                warnings.Add("SQL Server certificate validation is DISABLED (SqlNexus:TrustServerCertificate=true with SqlNexus:AllowCertificateValidationBypass=true). An on-path attacker could impersonate the SQL Server. Use only in a lab environment.");
            }

            return builder.ConnectionString;
        }

        // Fail closed: an unrecognized value is rejected rather than silently falling back to a default.
        internal static bool ParseBool(string? value, bool defaultValue, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (bool.TryParse(value!.Trim(), out bool parsed))
                return parsed;

            throw new ArgumentException($"SqlNexus:{settingName} must be 'true' or 'false'.");
        }
    }
}
