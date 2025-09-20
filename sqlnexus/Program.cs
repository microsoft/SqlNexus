using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Globalization;
using NexusInterfaces;
using Microsoft.Data.SqlClient;
//App can be run as a console or GUI app.  Passing parameters causes console mode.
//Sample cmd line for running as a console app
//  "/Cserver='.\ss2k5_rtm';Trusted_Connection=true;database='sqlnexus';Application Name=' ';Pooling=false;Packet Size=4096;multipleactiveresultsets=false" "/XD:\_data\src\Nexus\sqlnexus\sqlnexus\bin\Debug\Reports\Profiler Trace Analysis_M.rdlc"

//%NexusEXE% -S%SQLSERVER% -d%NEXUSDB% -E -I"%TESTFILEROOT%\NexusTestFiles\multi_instance\output"  -X -O"%TESTFILEROOT%\NexusTestFiles\multi_instance\output\nexuslog"

namespace sqlnexus
{
    internal class UnhandledExceptionHandler
    {
        public void OnThreadException(object sender, ThreadExceptionEventArgs t)
        {
            DialogResult result = DialogResult.Cancel;
            Globals.ExceptionEncountered = true;
            try
            {
                Globals.HandleException(t.Exception, null, null);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                try
                {
                    result = this.ShowThreadExceptionDialog(t.Exception);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    try
                    {
                        if (ex.Message.Contains("Could not load file or assembly"))
                        { 

                        }
                        else
                            MessageBox.Show("Fatal Error", "Fatal Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Stop);
                    }
                    finally
                    {
                        Application.Exit(); 
                    }
                }
            }

            // Exits the program when the user clicks Abort.
            if (result == DialogResult.Abort)
                Application.Exit();
        }

        // Creates the error message and displays it.
        private DialogResult ShowThreadExceptionDialog(Exception e)
        {
            string errorMsg = "An error occurred please contact the adminstrator with the following information:\n\n";
            errorMsg = errorMsg + e.Message + "\n\nStack Trace:\n" + e.StackTrace;
            return MessageBox.Show(errorMsg, "Application Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Stop);
        }
    }

    enum ProgramExitCodes
    {
        UserCancel = -1,
        Normal = 0,
        Exception = 2
    }

    static class Program
    {
        [DllImport("KERNEL32.DLL", EntryPoint = "FreeConsole", SetLastError = true,
           CharSet = CharSet.Unicode, ExactSpelling = true,
           CallingConvention = CallingConvention.StdCall)]
        public static extern bool FreeConsole();

        public static void ShowUsage()
        {
            // logger isn't hooked up to log file at this point and we're definitely running from the command line...
            //Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Msg_Nexus));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Summary));
            Console.WriteLine("");
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Server));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Database));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_WindowsAuth));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_UserName));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Password));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_ConnectStr));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_InputPath));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_RunReport));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_OutputPath));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_ExitAfterProcessing));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Parameter));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Usage_Quiet));
            Console.WriteLine(Util.ExpandEscapeStrings(sqlnexus.Properties.Resources.Drop_Existing_Database));
        }

        /// <summary>
        /// Process sqlnexus.exe command line parameters. 
        /// </summary>
        /// <remarks>DOES NOT support spaces between an arg switch and its parameter.  For example, 
        /// "/Rreport" is valid, "/R report" is not valid. Both '-' and '/' are supported as 
        /// switch starting characters.</remarks>
        private static bool ProcessCmdLine(string[] args, ILogger logger)
        {
            // TODO: print out command line params as they are processed
            // TODO: exit if -? passed
            //Special case usage info
            if ((1 == args.Length) && (("/?" == args[0]) || ("-?" == args[0]) || ("--help" == args[0]) ))
            {
                ShowUsage();
                return false;
            }

            //logger.LogMessage(sqlnexus.Properties.Resources.Msg_ProcessParams);
            Console.WriteLine (sqlnexus.Properties.Resources.Msg_ProcessParams);
            Console.WriteLine("");

            //Loop through the cmd line args
            //DOES NOT support spaces between the arg switches and their parameters.  I.e.,
            // "/Rreport" is valid, "/R report" is not
            foreach (string arg in args)
            {
                if ((('/' != arg[0]) && ('-' != arg[0])) || (arg.Length < 2))
                {
                    Console.WriteLine(sqlnexus.Properties.Resources.Msg_InvalidSwitch + arg);
                    return false;
                }

                // Some switches require a string to immediately follow the switch
                char switchChar = arg.ToUpper(CultureInfo.InvariantCulture)[1];
                if (('C'==switchChar) || ('S'==switchChar) || ('U'==switchChar) || ('P'==switchChar) || ('R'==switchChar)
                    || ('O' == switchChar) || ('I' == switchChar) || ('V' == switchChar) || ('D' == switchChar))
                {
                    if (arg.Length < 3)
                    {
                        Console.WriteLine(sqlnexus.Properties.Resources.Msg_InvalidSwitch + arg);
                        Console.WriteLine(sqlnexus.Properties.Resources.Error_CmdLineNoSpaces);
                        return false;
                    }
                }

                string arg_slash_validation = arg.Replace("/","");

                if (arg.Length - arg_slash_validation.Length > 1)
                {
                    Console.WriteLine(sqlnexus.Properties.Resources.Msg_InvalidSwitch + arg.Substring(0,2));
                    Console.WriteLine("Possible reason: An extra backslash exists at the end of " + arg.Substring(0, 2) + " parameter in your command");
                    return false;
                }

                switch (switchChar)
                {
                    case 'C':
                        {
                            Console.WriteLine(@"Command Line Arg (/C): ConnectionStr=(connection string)");
                            Globals.credentialMgr.ConnectionString = arg.Substring(2);
                            break;
                        }
                    case 'D':
                        {
                            string dbName = arg.Substring(2);
                            Console.WriteLine(@"Command Line Arg (/D): Database=" + dbName);

                            if (!System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^(?!(master|tempdb|msdb|model)$)[#$A-Za-z0-9_-]{1,128}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                Console.WriteLine("Error: Database name must contain only letters, numbers, underscores, hyphens, #, $, and cannot be 'master', 'tempdb', 'model', or 'msdb'. Length must be 1-128 characters.");
                                return false;
                            }
                         

                            Globals.credentialMgr.Database = dbName;
                            break;
                        }
                    case 'S':
                        {
                            Console.WriteLine(@"Command Line Arg (/S): Server=" + arg.Substring(2));
                            Globals.credentialMgr.Server = arg.Substring(2);
                            break;
                        }
                    case 'E':
                        {
                            Console.WriteLine(@"Command Line Arg (/E): UseWindowsAuth");
                            Globals.credentialMgr.WindowsAuth = true;
                            break;
                        }
                    case 'Q':
                        {
                            Console.WriteLine(@"Command Line Arg (/Q): Minimize windows in console mode");
                            Globals.NoWindow = true;
                            break;
                        }
                    case 'U':
                        {
                            Console.WriteLine(@"Command Line Arg (/U): UserID=" + arg.Substring(2));
                            Globals.credentialMgr.WindowsAuth = false;
                            Globals.credentialMgr.User = arg.Substring(2);
                            break;
                        }
                    case 'P':
                        {
                            Console.WriteLine(@"Command Line Arg (/P): Password=******");
                            Globals.credentialMgr.Password = arg.Substring(2);
                            break;
                        }
                    case 'R':
                        {
                            Console.WriteLine(@"Command Line Arg (/R): Report=" + arg.Substring(2));
                            Globals.ReportsToRun.Enqueue(arg.Substring(2));
                            break;
                        }
                    case 'X':
                        {
                            Console.WriteLine(@"Command Line Arg (/X): ExitAfterProcessing");
                            Globals.ExitAfterProcessingReports = true;
                            break;
                        }
                    case 'O':
                        {
                            Console.WriteLine(@"Command Line Arg (/O): OutputPath=" + arg.Substring(2));
                            Globals.ReportExportPath = arg.Substring(2).Trim().Replace("\"","");
                            // Path is assumed to be terminated by a backslash
                            if (@"\" != Globals.ReportExportPath.Substring(Globals.ReportExportPath.Length - 1))
                                Globals.ReportExportPath += @"\";
                            break;
                        }
                    case 'I':
                        {
                            Console.WriteLine(@"Command Line Arg (/I): InputPath=" + arg.Substring(2));
                            
                            String ipath = arg.Substring(2).Replace("\"", "").Trim();
                            if (ipath.EndsWith(@"\"))
                                ipath =  ipath.Substring(0, ipath.Length-1);

                            Globals.PathsToImport.Enqueue(ipath);
                            Globals.QuietNonInteractiveMode = true; 
                            break;
                        }
                    case 'V':
                        {
                            Console.WriteLine(@"Command Line Arg (/V): Parameter " + arg.Substring(2));
                            string tmpStr = arg.Substring(2);
                            string param = tmpStr.Substring(0, tmpStr.IndexOf('='));
                            string val = tmpStr.Substring(tmpStr.IndexOf('=')+1);
                            Globals.UserSuppliedReportParameters.Add(param, val);
                            break;
                        }
                    case 'N':
                        {
                            Console.WriteLine(@"Command Line Arg (/N)" + arg.Substring(2));
                            Globals.DropExistingDb = true;
                            break;
                        }
                    default:
                        {
                            Console.WriteLine(sqlnexus.Properties.Resources.Usage_UnknownArg + arg);
                            return false;
                        }
                }

                
            }
            //create a database

            if (!string.IsNullOrEmpty(Globals.credentialMgr.Database))
            {
                String currentDb = Globals.credentialMgr.Database;
                String CreateDB = string.Format(SQLScripts.CreateDB, Globals.credentialMgr.Database);
                Console.WriteLine("Creating Database" + CreateDB);
                //String connstring = string.Format("Data Source={0};Initial Catalog=master;Integrated Security=SSPI", Globals.credentialMgr.Server);
                //SqlConnection conn = new SqlConnection(connstring);

                //set the db to 'master' to be able to create a new Nexus db
                Globals.credentialMgr.Database = "master";
                SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString);
                conn.Open();
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = CreateDB; // CodeQL [SM03934] the DB name has been validated but even previously the db name was wrapped in brackets, so no SQL injection possible here

                cmd.ExecuteNonQuery();

                //reset the Nexus db name that the user selected
                Globals.credentialMgr.Database = currentDb;
            }

            return true;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
           
            try
            {   

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                //we ship ReportViewer as nuget package now. so Dependency check is no longer needed
                //DependencyManager.CheckReportViewer();

                //initialize the main form
                fmNexus fmN = new fmNexus();
            
                

                if (0 != args.Length)
                {
                    Globals.ConsoleMode = true;
                    if (!ProcessCmdLine(args, fmN))
                    {
                        return (int)ProgramExitCodes.UserCancel;
                    }
                }
                else
                {
                    FreeConsole();
                }
                Application.Run(fmN);
            }
            
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Exception encountered in Main(): [{0}]", ex.Message));
                Console.WriteLine(string.Format("{0}", ex.StackTrace));
            }

            return (int)(Globals.IsNexusCoreImporterSuccessful ? ProgramExitCodes.Normal : ProgramExitCodes.Exception);
        }
    }
}


//JOTODO: Once import is complete and person closes Import screen, switch to PerfMain RDL automatically
