using System;
using System.Reflection;
using Microsoft.Reporting.WinForms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class fmNexusTests
    {
        [TestMethod]
        public void ReportHyperlinksEnabled_ReportContainsHyperlinks_EnablesReportProcessing()
        {
            Assert.IsTrue(global::sqlnexus.fmNexus.ReportHyperlinksEnabled);
        }

        [TestMethod]
        public void NullHyperlink_RequestsCancellation()
        {
            HyperlinkEventArgs args = CreateHyperlinkEventArgs(null);

            global::sqlnexus.fmNexus.CancelReportHyperlink(args);

            Assert.IsTrue(args.Cancel);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("a")]
        [DataRow("relative/path")]
        [DataRow(@"\\server\share\payload")]
        [DataRow("file:///C:/payload.cmd")]
        [DataRow("search-ms:query=payload")]
        [DataRow("shell:AppsFolder")]
        [DataRow("custom-protocol://payload")]
        [DataRow("CuStOm-PrOtOcOl://payload")]
        [DataRow("https://example.com/")]
        [DataRow("sqlnexus://C:%5Cpayload.dll/Type/Method")]
        public void AnyHyperlink_RequestsCancellation(string hyperlink)
        {
            HyperlinkEventArgs args = CreateHyperlinkEventArgs(hyperlink);

            global::sqlnexus.fmNexus.CancelReportHyperlink(args);

            Assert.IsTrue(args.Cancel);
        }

        [DataTestMethod]
        [DataRow("https://docs.microsoft.com/troubleshoot/windows-server/performance/slow-performance-when-using-power-plan")]
        [DataRow("https://learn.microsoft.com/sql/")]
        [DataRow("https://support.microsoft.com/topic/test")]
        [DataRow("https://TECHCOMMUNITY.MICROSOFT.COM/blog/test")]
        [DataRow("https://aka.ms/sqlnexus")]
        public void AllowedHttpsHyperlink_ValidationSucceeds(string hyperlink)
        {
            Uri allowedUri;

            bool result = global::sqlnexus.fmNexus.TryGetAllowedReportHyperlink(hyperlink, out allowedUri);

            Assert.IsTrue(result);
            Assert.IsNotNull(allowedUri);
            Assert.AreEqual(Uri.UriSchemeHttps, allowedUri.Scheme);
        }

        [TestMethod]
        public void NullHyperlink_ValidationFails()
        {
            Uri allowedUri;

            bool result = global::sqlnexus.fmNexus.TryGetAllowedReportHyperlink(null, out allowedUri);

            Assert.IsFalse(result);
            Assert.IsNull(allowedUri);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("a")]
        [DataRow("relative/path")]
        [DataRow(@"\\server\share\payload")]
        [DataRow("http://docs.microsoft.com/test")]
        [DataRow("file:///C:/payload.cmd")]
        [DataRow("search-ms:query=payload")]
        [DataRow("shell:AppsFolder")]
        [DataRow("custom-protocol://payload")]
        [DataRow("https://example.com/")]
        [DataRow("https://docs.microsoft.com.evil.example/test")]
        [DataRow("https://user@docs.microsoft.com/test")]
        [DataRow("sqlnexus://C:%5Cpayload.dll/Type/Method")]
        public void UnsafeOrUntrustedHyperlink_ValidationFails(string hyperlink)
        {
            Uri allowedUri;

            bool result = global::sqlnexus.fmNexus.TryGetAllowedReportHyperlink(hyperlink, out allowedUri);

            Assert.IsFalse(result);
            Assert.IsNull(allowedUri);
        }

        [TestMethod]
        public void LegacyReflectionDispatcher_IsNotPresent()
        {
            const BindingFlags privateInstanceOrStatic = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            MethodInfo executeMethod = typeof(global::sqlnexus.fmNexus).GetMethod("ExecuteMethod", privateInstanceOrStatic);
            MethodInfo tryMethodCall = typeof(global::sqlnexus.fmNexus).GetMethod("TryMethodCall", privateInstanceOrStatic);

            Assert.IsNull(executeMethod);
            Assert.IsNull(tryMethodCall);
        }

        private static HyperlinkEventArgs CreateHyperlinkEventArgs(string hyperlink)
        {
            return (HyperlinkEventArgs)Activator.CreateInstance(
                typeof(HyperlinkEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { hyperlink },
                null);
        }
    }
}
