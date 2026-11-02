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
        public void ReportHyperlinksEnabled_DefaultConfiguration_DisablesHyperlinks()
        {
            Assert.IsFalse(global::sqlnexus.fmNexus.ReportHyperlinksEnabled);
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
