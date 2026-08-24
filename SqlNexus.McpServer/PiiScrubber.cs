using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Two-layer PII scrubber applied to all MCP tool outputs before returning to the agent.
    /// Pure C# — zero external dependencies, no Python, no NuGet packages beyond the base framework.
    ///
    /// Layer 1 — Regex: covers all structured PII realistically present in SQL Server diagnostic data:
    ///   GUIDs, IPv4 addresses, auto-generated computer names, email addresses,
    ///   Windows file paths containing usernames (C:\Users\..., C:\Documents and Settings\...),
    ///   UNC paths (\\server\share), NT DOMAIN\username tokens, SQL login names
    ///   appearing as JSON field values, and phone numbers.
    ///
    /// Layer 2 — URL allowlist: non-approved URLs are replaced with &lt;Scrubbed_URL&gt;.
    /// </summary>
    public static class PiiScrubber
    {
        // ── Layer 1: Regex rules (applied in order) ─────────────────────────────
        //
        // Ordering matters: more specific patterns first to avoid partial matches
        // being consumed by broader ones (e.g. UNC paths before NT domain tokens).
        private static readonly (Regex Pattern, string Replacement)[] s_regexRules =
        {
            // ── GUIDs  e.g. 6ba7b810-9dad-11d1-80b4-00c04fd430c8 ─────────────
            (new Regex(
                @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
             "<GUID>"),

            // ── Email addresses  e.g. john.smith@contoso.com ─────────────────
            (new Regex(
                @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
                RegexOptions.Compiled),
             "<EMAIL>"),

            // ── UNC paths  e.g. \\SQLSERVER01\Backups\db.bak ─────────────────
            // Must come before NT DOMAIN\user to avoid the server name being
            // matched as a domain token first.
            (new Regex(
                @"(?<![A-Za-z0-9_.:\-])\\{2,4}[A-Za-z0-9._\-]{2,}\\{1,2}[^\s""'<>,;]+",
                RegexOptions.Compiled),
             "<UNCPATH>"),

            // ── Windows user profile paths  e.g. C:\Users\johndoe\AppData ────
            // Covers both modern (Users) and legacy (Documents and Settings) paths.
            (new Regex(
                @"[A-Za-z]:\\{1,2}(?:[Uu]sers|[Dd]ocuments and [Ss]ettings)\\{1,2}[^\\""'\s,;>]+",
                RegexOptions.Compiled),
             "<WINPATH>"),

            // ── IPv4 addresses  e.g. 192.168.1.100 ───────────────────────────
            (new Regex(
                @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
                RegexOptions.Compiled),
             "<IP>"),

            // ── IPv6 addresses ────────────────────────────────────────────────
            (new Regex(
                @"(?<![0-9A-Fa-f:])[0-9A-Fa-f:]*:[0-9A-Fa-f:.]*(?![0-9A-Fa-f:])",
                RegexOptions.Compiled),
             "<IPV6>"),

            // ── MAC addresses  e.g. 00-15-5D-01-02-03 ───────────────────────
            (new Regex(
                @"\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b",
                RegexOptions.Compiled),
             "<MAC>"),

            // ── Auto-generated Windows computer names  WIN-*, DESKTOP-* ──────
            (new Regex(
                @"\b(win-|desktop-)[a-z0-9]{7,15}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
             "<COMPUTER>"),

            // ── NT DOMAIN\username tokens  e.g. CONTOSO\jsmith ───────────────
            // Matches DOMAIN (2-20 chars) \ username (2-20 chars).
            // Excludes paths already replaced above and known SQL Server system
            // accounts (NT SERVICE\, NT AUTHORITY\).
            (new Regex(
                @"\b(?!NT SERVICE\\|NT AUTHORITY\\)[A-Za-z0-9_\-]{2,64}\\{1,2}[A-Za-z0-9._\-]{2,64}\b",
                RegexOptions.Compiled),
             "<DOMAIN_USER>"),

            // ── SQL login names appearing as JSON values ──────────────────────
            // Targets the pattern: "LoginName": "somevalue" or "login_name": "somevalue"
            // in the JSON output from ReadTrace.tblConnections and tbl_REQUESTS.
            (new Regex(
                            @"(?<=""(?:LoginName|login_name|NTUserName|nt_user_name|HostName|host_name|User Name|UserName|user_name|ImportedBy|imported_by|InstalledBy|installed_by|ServerName|server_name|ReplicaServerName|replica_server_name|MachineName|machine_name|ComputerName|computer_name|OwnerSid|owner_sid|PrincipalSid|principal_sid)""\s*:\s*"")[^""]+(?="")",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
             "<SCRUBBED>"),

            // ── Phone numbers  e.g. +1-800-555-1234, (425) 555-0100 ──────────
            (new Regex(
                @"\b(\+?1[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}\b",
                RegexOptions.Compiled),
             "<PHONE>"),
        };

        // ── Layer 2: URL allowlist ──────────────────────────────────────────────
        private static readonly Regex s_urlPattern =
            new Regex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] s_urlAllowlist =
        {
            "https://stackoverflow.com/questions",
            "https://techcommunity.microsoft.com",
            "https://learn.microsoft.com/",
            "https://kb.databricks.com/en_us/azure",
            "https://issues.apache.org/jira/projects",
            "https://www.databricks.com/blog",
            "https://spark.apache.org/documentation",
            "https://blog.fabric.microsoft.com/en-us/blog",
            "https://powerbi.microsoft.com/en-us/blog",
            "https://blog.crossjoin.co.uk",
            "https://www.sqlbi.com/articles",
            "https://pbidax.wordpress.com/author/jwang8888",
            "https://dax.tips",
            "https://www.microsoft.com",
        };

        // ── Public entry point ──────────────────────────────────────────────────

        /// <summary>
        /// Scrubs PII from <paramref name="text"/> and returns the sanitized result.
        /// All processing is in-process — no external dependencies.
        /// </summary>
        public static string Scrub(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = ApplyRegex(text);         // Layer 1
            text = ApplyUrlAllowlist(text);  // Layer 2

            return text;
        }

        // ── Layer implementations ───────────────────────────────────────────────

        private static string ApplyRegex(string text)
        {
            foreach (var (pattern, replacement) in s_regexRules)
            {
                if (replacement == "<IP>")
                    text = pattern.Replace(text, ReplaceIpv4Address);
                else if (replacement == "<IPV6>")
                    text = pattern.Replace(text, ReplaceIpv6Address);
                else
                    text = pattern.Replace(text, replacement);
            }
            return text;
        }

        private static string ReplaceIpv4Address(Match match)
        {
            IPAddress address;
            if (IPAddress.TryParse(match.Value, out address) &&
                address.AddressFamily == AddressFamily.InterNetwork)
            {
                return "<IP>";
            }

            return match.Value;
        }

        private static string ReplaceIpv6Address(Match match)
        {
            string candidate = match.Value;
            IPAddress address;
            if ((candidate.Contains("::") || candidate.Split(':').Length >= 5) &&
                IPAddress.TryParse(candidate, out address) &&
                address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return "<IPV6>";
            }

            return candidate;
        }

        private static string ApplyUrlAllowlist(string text)
        {
            return s_urlPattern.Replace(text, match =>
            {
                string url = match.Value;
                Uri candidate;
                if (!Uri.TryCreate(url, UriKind.Absolute, out candidate))
                    return "<Scrubbed_URL>";

                foreach (string allowed in s_urlAllowlist)
                {
                    Uri allowedUri;
                    if (Uri.TryCreate(allowed, UriKind.Absolute, out allowedUri) &&
                        string.Equals(candidate.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                        IsAllowedPath(candidate.AbsolutePath, allowedUri.AbsolutePath))
                    {
                        return url;
                    }
                }
                return "<Scrubbed_URL>";
            });
        }

        private static bool IsAllowedPath(string candidatePath, string allowedPath)
        {
            if (allowedPath == "/")
                return true;

            string normalizedAllowedPath = allowedPath.TrimEnd('/');
            return string.Equals(candidatePath, normalizedAllowedPath, StringComparison.OrdinalIgnoreCase) ||
                candidatePath.StartsWith(normalizedAllowedPath + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
