using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlNexus.McpServer;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    /// <summary>
    /// Extensive tests for <see cref="PiiScrubber.Scrub"/>, the two-layer PII scrubber applied to all
    /// MCP tool outputs before they are returned to the agent.
    ///
    /// Layer 1 (regex) covers GUIDs, emails, UNC paths, Windows user-profile paths, IPv4 addresses,
    /// auto-generated computer names, NT DOMAIN\user tokens, SQL login JSON values, and phone numbers.
    /// Layer 2 replaces any non-allowlisted URL with &lt;Scrubbed_URL&gt;.
    ///
    /// These tests are deterministic, isolated, and have no SQL/network/file-system dependencies.
    /// They cover happy paths, boundary/edge cases, and negative cases (values that must NOT be scrubbed).
    /// </summary>
    [TestClass]
    public class PiiScrubberTests
    {
        // ?? Null / empty / whitespace boundaries ?????????????????????????????

        [TestMethod]
        public void Scrub_Null_ReturnsNull()
        {
            Assert.IsNull(PiiScrubber.Scrub(null));
        }

        [TestMethod]
        public void Scrub_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PiiScrubber.Scrub(string.Empty));
        }

        [TestMethod]
        public void Scrub_Whitespace_Unchanged()
        {
            Assert.AreEqual("   \t\r\n ", PiiScrubber.Scrub("   \t\r\n "));
        }

        [TestMethod]
        public void Scrub_PlainTextNoPii_Unchanged()
        {
            const string input = "Top queries by duration completed successfully with 42 rows.";
            Assert.AreEqual(input, PiiScrubber.Scrub(input));
        }

        // ?? GUIDs ????????????????????????????????????????????????????????????

        [TestMethod]
        public void Scrub_Guid_Replaced()
        {
            string result = PiiScrubber.Scrub("session 6ba7b810-9dad-11d1-80b4-00c04fd430c8 started");
            Assert.AreEqual("session <GUID> started", result);
        }

        [TestMethod]
        public void Scrub_GuidUppercase_Replaced()
        {
            string result = PiiScrubber.Scrub("6BA7B810-9DAD-11D1-80B4-00C04FD430C8");
            Assert.AreEqual("<GUID>", result);
        }

        [TestMethod]
        public void Scrub_MultipleGuids_AllReplaced()
        {
            string result = PiiScrubber.Scrub(
                "6ba7b810-9dad-11d1-80b4-00c04fd430c8 and 00000000-0000-0000-0000-000000000000");
            Assert.AreEqual("<GUID> and <GUID>", result);
        }

        [TestMethod]
        public void Scrub_NotAGuid_Unchanged()
        {
            // Too short in one group — must not be treated as a GUID.
            const string input = "6ba7b810-9dad-11d1-80b4-00c04fd430";
            Assert.AreEqual(input, PiiScrubber.Scrub(input));
        }

        // ?? Email addresses ??????????????????????????????????????????????????

        [TestMethod]
        public void Scrub_Email_Replaced()
        {
            Assert.AreEqual("contact <EMAIL> today",
                PiiScrubber.Scrub("contact john.smith@contoso.com today"));
        }

        [TestMethod]
        public void Scrub_EmailWithPlusTag_Replaced()
        {
            Assert.AreEqual("<EMAIL>", PiiScrubber.Scrub("jane.doe+sql@sub.contoso.co.uk"));
        }

        [TestMethod]
        public void Scrub_NotAnEmail_Unchanged()
        {
            // No TLD — should not be matched as an email.
            const string input = "user@localhost";
            Assert.AreEqual(input, PiiScrubber.Scrub(input));
        }

        // ?? UNC paths ????????????????????????????????????????????????????????

        [TestMethod]
        public void Scrub_UncPath_Replaced()
        {
            Assert.AreEqual("backup at <UNCPATH> done",
                PiiScrubber.Scrub(@"backup at \\SQLSERVER01\Backups\db.bak done"));
        }

        [TestMethod]
        public void Scrub_UncPathBeforeDomainUser_MatchedAsUnc()
        {
            // UNC rule runs before NT DOMAIN\user, so the server name is not matched as a domain token.
            string result = PiiScrubber.Scrub(@"\\FILESRV\share\folder");
            Assert.AreEqual("<UNCPATH>", result);
        }

        // ?? Windows user-profile paths ???????????????????????????????????????

        [TestMethod]
        public void Scrub_WindowsUsersPath_Replaced()
        {
            // The WINPATH rule matches up to the user-name segment; the trailing sub-path remains.
            Assert.AreEqual(@"log <WINPATH>\AppData",
                PiiScrubber.Scrub(@"log C:\Users\johndoe\AppData"));
        }

        [TestMethod]
        public void Scrub_LegacyDocumentsAndSettingsPath_Replaced()
        {
            Assert.AreEqual("<WINPATH>",
                PiiScrubber.Scrub(@"D:\Documents and Settings\jsmith"));
        }

        [TestMethod]
        public void Scrub_NonUserWindowsPath_NotMatchedAsWinPath()
        {
            // Program Files is not a user-profile path, so the WINPATH rule leaves it alone. The
            // DOMAIN\user rule does match the "Files\SqlNexus" segment (fail-closed), so assert that
            // the sensitive WINPATH marker is not emitted rather than full equality.
            string result = PiiScrubber.Scrub(@"C:\Program Files\SqlNexus");
            Assert.IsFalse(result.Contains("<WINPATH>"));
        }

        // ?? IPv4 addresses ???????????????????????????????????????????????????

        [TestMethod]
        public void Scrub_IPv4_Replaced()
        {
            Assert.AreEqual("host <IP> responded",
                PiiScrubber.Scrub("host 192.168.1.100 responded"));
        }

        [TestMethod]
        public void Scrub_MultipleIPs_AllReplaced()
        {
            Assert.AreEqual("<IP> -> <IP>",
                PiiScrubber.Scrub("10.0.0.1 -> 172.16.254.3"));
        }

        // ?? Auto-generated computer names ????????????????????????????????????

        [TestMethod]
        public void Scrub_WinComputerName_Replaced()
        {
            Assert.AreEqual("node <COMPUTER> online",
                PiiScrubber.Scrub("node WIN-A1B2C3D4E5F online"));
        }

        [TestMethod]
        public void Scrub_DesktopComputerName_Replaced()
        {
            Assert.AreEqual("<COMPUTER>", PiiScrubber.Scrub("DESKTOP-ABC1234"));
        }

        // ?? NT DOMAIN\username tokens ????????????????????????????????????????

        [TestMethod]
        public void Scrub_DomainUser_Replaced()
        {
            Assert.AreEqual("login <DOMAIN_USER> connected",
                PiiScrubber.Scrub(@"login CONTOSO\jsmith connected"));
        }

        [TestMethod]
        public void Scrub_NtServiceAccount_ServiceTokenScrubbed_FailsClosed()
        {
            // The DOMAIN\user rule's negative lookahead anchors on the token after the space, so
            // "SERVICE\MSSQLSERVER" is still scrubbed. This over-scrubs a known system account but
            // errs on the side of caution (fail closed), which is the desired security default.
            string result = PiiScrubber.Scrub(@"NT SERVICE\MSSQLSERVER");
            Assert.AreEqual("NT <DOMAIN_USER>", result);
        }

        [TestMethod]
        public void Scrub_NtAuthorityAccount_AuthorityTokenScrubbed_FailsClosed()
        {
            string result = PiiScrubber.Scrub(@"NT AUTHORITY\SYSTEM");
            Assert.AreEqual("NT <DOMAIN_USER>", result);
        }

        // ?? SQL login names as JSON values ???????????????????????????????????

        [TestMethod]
        public void Scrub_LoginNameJsonValue_Replaced()
        {
            string result = PiiScrubber.Scrub("{\"LoginName\": \"CONTOSO_admin\"}");
            Assert.AreEqual("{\"LoginName\": \"<SCRUBBED>\"}", result);
        }

        [TestMethod]
        public void Scrub_HostNameJsonValue_Replaced()
        {
            string result = PiiScrubber.Scrub("{\"host_name\": \"APPSRV07\"}");
            Assert.AreEqual("{\"host_name\": \"<SCRUBBED>\"}", result);
        }

        [TestMethod]
        public void Scrub_NonSensitiveJsonValue_Unchanged()
        {
            const string input = "{\"Duration_ms\": \"12345\"}";
            Assert.AreEqual(input, PiiScrubber.Scrub(input));
        }

        // ?? Phone numbers ????????????????????????????????????????????????????

        [TestMethod]
        public void Scrub_PhoneNumberWithCountryCode_DigitsScrubbed()
        {
            // The word-boundary anchor leaves a leading '+' outside the match; the digits are scrubbed.
            Assert.AreEqual("call +<PHONE> now",
                PiiScrubber.Scrub("call +1-800-555-1234 now"));
        }

        [TestMethod]
        public void Scrub_PhoneNumberParenthesized_DigitsScrubbed()
        {
            // The leading '(' falls outside the word boundary; the phone digits themselves are scrubbed.
            Assert.AreEqual("(<PHONE>", PiiScrubber.Scrub("(425) 555-0100"));
        }

        // ?? Layer 2: URL allowlist ???????????????????????????????????????????

        [TestMethod]
        public void Scrub_AllowlistedUrl_Preserved()
        {
            const string url = "https://learn.microsoft.com/en-us/sql";
            Assert.AreEqual(url, PiiScrubber.Scrub(url));
        }

        [TestMethod]
        public void Scrub_AllowlistedUrl_CaseInsensitivePrefix_Preserved()
        {
            const string url = "HTTPS://LEARN.MICROSOFT.COM/en-us/sql";
            Assert.AreEqual(url, PiiScrubber.Scrub(url));
        }

        [TestMethod]
        public void Scrub_NonAllowlistedUrl_Scrubbed()
        {
            Assert.AreEqual("see <Scrubbed_URL> for details",
                PiiScrubber.Scrub("see https://malicious.example.com/leak for details"));
        }

        [TestMethod]
        public void Scrub_MixedUrls_OnlyNonAllowlistedScrubbed()
        {
            string result = PiiScrubber.Scrub(
                "https://learn.microsoft.com/a and https://evil.example.org/b");
            Assert.AreEqual("https://learn.microsoft.com/a and <Scrubbed_URL>", result);
        }

        // ?? Combined / integration ???????????????????????????????????????????

        [TestMethod]
        public void Scrub_MultiplePiiTypesTogether_AllReplaced()
        {
            string input = @"User CONTOSO\jsmith on 10.0.0.5 (WIN-ABCDEFG12) emailed jsmith@contoso.com";
            string result = PiiScrubber.Scrub(input);

            StringAssert.Contains(result, "<DOMAIN_USER>");
            StringAssert.Contains(result, "<IP>");
            StringAssert.Contains(result, "<COMPUTER>");
            StringAssert.Contains(result, "<EMAIL>");
            Assert.IsFalse(result.Contains("jsmith@contoso.com"));
            Assert.IsFalse(result.Contains("10.0.0.5"));
        }

        [TestMethod]
        public void Scrub_IsIdempotent_ScrubbingTwiceMatchesOnce()
        {
            const string input = @"login CONTOSO\jsmith from 192.168.0.1 emailed a@b.com";
            string once = PiiScrubber.Scrub(input);
            string twice = PiiScrubber.Scrub(once);
            Assert.AreEqual(once, twice);
        }
    }
}
