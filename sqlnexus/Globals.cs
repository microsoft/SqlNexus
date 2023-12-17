using System;
using System.Collections.Generic;
using System.Text;
using NexusInterfaces;
using System.Diagnostics;
using System.Windows.Forms;
using System.Globalization;
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Reporting.WinForms;
using Microsoft.Win32;
namespace sqlnexus
{

    public struct Globals
    {
        public static List<ReportViewer> ListOfReports = new List<ReportViewer>();
        public static RuntimeEnv Runtime = RuntimeEnv.Env;
        public static Queue<string> ReportsToRun = new Queue<string>();
        public static Queue<string> PathsToImport = new Queue<string>();
        public static Dictionary<string, string> UserSuppliedReportParameters = new Dictionary<string, string>();
        public static string ReportExportPath;
        public static bool ExitAfterProcessingReports = false;
        public static Boolean ConsoleMode = false;
        private  static string m_connectionString;
        public static CredentialManager credentialMgr = new CredentialManager();
        public static bool QuietNonInteractiveMode = false;
        public static bool ExceptionEncountered = false;
        public static bool IsNexusCoreImporterSuccessful = true;
        public static bool NoWindow = false;
        public static bool DropExistingDb = false;

        private static readonly string m_StartupPath = Application.StartupPath;
        private static readonly string m_AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\sqlnexus";

        public static string StartupPath { get { return m_StartupPath; } }
        public static string AppDataPath         { get { return m_AppDataPath; } }

        public static System.Windows.Forms.DialogResult HandleException(Exception ex, System.Windows.Forms.IWin32Window owner, ILogger logger)
        {
            try
            {
                ExceptionEncountered = true;
                if (null != logger)
                {
                    logger.LogMessage(sqlnexus.Properties.Resources.Error_Exception, new string[] { ex.Message, ex.Source, ex.StackTrace, ((null == ex.InnerException) ? "" : ex.InnerException.Message) }, MessageOptions.Silent, TraceEventType.Critical);
                    logger.LogMessage(sqlnexus.Properties.Resources.Error_ExceptionShort, new string[] { ex.Message }, MessageOptions.NoLog, TraceEventType.Critical);
                }
                if (!ConsoleMode)
                {
                   // Microsoft.SqlServer.MessageBox.ExceptionMessageBox dlg = new Microsoft.SqlServer.MessageBox.ExceptionMessageBox(ex);
                    //return dlg.Show(owner);
                    MessageBox.Show(ex.ToString());
                    return DialogResult.OK;
                }
                else
                {
                    return DialogResult.OK;
                }
            }
            catch (Exception e)  //If exception while logging exception, try to write it to the debug listener
            {
                string msg = string.Format(sqlnexus.Properties.Resources.Error_Exception, e.Message, e.Source, e.StackTrace, ((null == e.InnerException) ? "" : e.InnerException.Message));
                System.Diagnostics.Debug.WriteLine(msg);
                msg = string.Format(sqlnexus.Properties.Resources.Error_Exception, ex.Message, ex.Source, ex.StackTrace, ((null == ex.InnerException) ? "" : ex.InnerException.Message));
                System.Diagnostics.Debug.WriteLine(msg);
                return DialogResult.Cancel;
            }
        }

        public static bool IsPowerBiDeskTopInstalled()
        {
            string RegNameValue = "DisplayName";
            string AppDisplayName = "Microsoft Power BI Desktop (x64)";
            RegistryKey MainKey = Registry.LocalMachine;
            MainKey = MainKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            foreach (string SubKey in MainKey.GetSubKeyNames())
            {
                RegistryKey ChildKeys = MainKey.OpenSubKey(SubKey);
                string[] ValueNames = ChildKeys.GetValueNames();
                foreach (var ValueName in ValueNames)
                {
                    if(ValueName == RegNameValue)
                    {
                       string value= ChildKeys.GetValue(RegNameValue).ToString();
                        if (value == AppDisplayName)
                            return true;
                    }

                }
                
            }
            return false;


        }

        public static void LuanchPowerBI()
        {
            if (!IsPowerBiDeskTopInstalled())
            {
                MessageBox.Show("PowerBI Desktop is not installed.Please download and install PowerBI Desktop before using this feature!. https://www.microsoft.com/en-us/download/details.aspx?id=58494");
                                    
            }
            else
            {
                for (int index = Application.OpenForms.Count - 1; index >= 0; index--)
                {
                    if (Application.OpenForms[index].Name == "fmPBReports")
                    {
                        
                        Application.OpenForms[index].Focus();

                    }
                    else {
                        fmPBReports pbReports = new fmPBReports();
                        pbReports.Show();
                    }
                    
                }

          

            }

        }
        public static bool IsProgramInstalled (string ProgramDisplayName) // This method fails if we dont have a "DisplayName" RegSubkey in the Main Key
        {
            
            
            foreach (var item in Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall").GetSubKeyNames())
            {

                object programName = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + item).GetValue("DisplayName");

                Console.WriteLine(programName);

                if (string.Equals(programName, ProgramDisplayName))
                {
            
                    return true;
                }
            }
            
            return false;

        }
       
    }
}
