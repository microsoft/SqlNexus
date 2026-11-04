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
            EncryptionMode mode = ParseEncryptionMode(encrypt);
            bool trustValue = ParseBool(trustServerCertificate, false, "TrustServerCertificate");
            bool allowBypass = ParseBool(allowCertificateValidationBypass, false, "AllowCertificateValidationBypass");

            if (trustValue && !allowBypass)
            {
                throw new InvalidOperationException(
                    "SqlNexus:TrustServerCertificate=true disables SQL Server certificate validation and is refused unless " +
                    "SqlNexus:AllowCertificateValidationBypass=true is also set. Install a certificate the client trusts, or set both " +
                    "options only for a lab environment that you control.");
            }

            if (trustValue && mode == EncryptionMode.Strict)
            {
                throw new InvalidOperationException(
                    "SqlNexus:Encrypt=Strict always requires certificate validation and cannot be combined with " +
                    "SqlNexus:TrustServerCertificate=true. Use SqlNexus:Encrypt=Mandatory for a lab server with an untrusted certificate.");
            }

            if (trustValue && mode == EncryptionMode.Optional)
            {
                throw new InvalidOperationException(
                    "SqlNexus:Encrypt=Optional cannot be combined with SqlNexus:TrustServerCertificate=true. " +
                    "Use SqlNexus:Encrypt=Mandatory for an encrypted connection to a lab server with an untrusted certificate.");
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = trustedConnection,
                Encrypt = ToEncryptOption(mode),
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

            if (mode == EncryptionMode.Optional)
            {
                warnings.Add("SQL connection encryption is NOT REQUIRED by the client (SqlNexus:Encrypt=Optional). The connection is encrypted only if the SQL Server requires it; otherwise queries, results and any SQL credentials can be observed or tampered with on the network.");
            }
            else if (trustValue)
            {
                warnings.Add("SQL Server certificate validation is DISABLED (SqlNexus:TrustServerCertificate=true with SqlNexus:AllowCertificateValidationBypass=true). An on-path attacker could impersonate the SQL Server. Use only in a lab environment.");
            }

            return builder.ConnectionString;
        }

        internal enum EncryptionMode
        {
            Optional,
            Mandatory,
            Strict
        }

        // Accepts Optional|Mandatory|Strict; the legacy booleans map true -> Mandatory and false -> Optional.
        // Fail closed: an unrecognized value is rejected and an empty value defaults to Mandatory.
        internal static EncryptionMode ParseEncryptionMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return EncryptionMode.Mandatory;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "optional":
                case "false":
                    return EncryptionMode.Optional;
                case "mandatory":
                case "true":
                    return EncryptionMode.Mandatory;
                case "strict":
                    return EncryptionMode.Strict;
                default:
                    throw new ArgumentException("SqlNexus:Encrypt must be 'Optional', 'Mandatory' or 'Strict'.");
            }
        }

        private static SqlConnectionEncryptOption ToEncryptOption(EncryptionMode mode)
        {
            switch (mode)
            {
                case EncryptionMode.Optional:
                    return SqlConnectionEncryptOption.Optional;
                case EncryptionMode.Strict:
                    return SqlConnectionEncryptOption.Strict;
                default:
                    return SqlConnectionEncryptOption.Mandatory;
            }
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
