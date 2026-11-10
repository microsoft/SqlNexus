#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Resolves the SQL authentication password. The password is never read from a JSON
    /// configuration file: it comes from the DPAPI-protected credential file, or (as a compatibility
    /// mechanism) from the SqlNexus__Password environment variable.
    /// </summary>
    internal static class SqlCredentialResolver
    {
        internal const string PasswordKey = "SqlNexus:Password";

        /// <summary>
        /// Fails closed when any loaded JSON configuration source contains a plaintext password, so a
        /// secret can never be silently picked up from (or left behind in) a configuration file.
        /// Environment variables are not JSON sources and are not affected.
        /// </summary>
        internal static void RejectPlaintextPasswordInJsonConfiguration(IConfigurationRoot configuration)
        {
            foreach (JsonConfigurationProvider provider in configuration.Providers.OfType<JsonConfigurationProvider>())
            {
                if (provider.TryGet(PasswordKey, out string? value) && !string.IsNullOrEmpty(value))
                {
                    string source = (provider.Source as JsonConfigurationSource)?.Path ?? "a JSON configuration file";
                    throw new InvalidOperationException(
                        "'" + source + "' contains " + PasswordKey + ". Plaintext passwords are not allowed in configuration files. " +
                        "Remove the value and store the password with: SqlNexus.McpServer.exe --set-credential --server <server> --user-id <user>");
                }
            }
        }

        /// <param name="environmentPassword">Value of SqlNexus__Password, if any (compatibility fallback).</param>
        internal static string ResolvePassword(
            string server,
            string? userId,
            string? environmentPassword,
            ICollection<string> warnings,
            string? baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("SQL authentication requires a user name. Supply --user-id (or SqlNexus:UserId).");

            if (!string.IsNullOrEmpty(environmentPassword))
            {
                warnings.Add("The SQL password was supplied through the SqlNexus__Password environment variable. Environment variables can be exposed through process inspection, diagnostic dumps, parent processes and logs; prefer a stored credential (SqlNexus.McpServer.exe --set-credential).");
                return environmentPassword!;
            }

            if (CredentialStore.TryLoad(server, userId, out string password, out string error, baseDirectory))
                return password;

            throw new InvalidOperationException(error);
        }
    }
}
