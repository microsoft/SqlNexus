using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Security.Principal;
using System.Security.AccessControl;
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

//[assembly: System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.RequestMinimum, Name = "FullTrust")]
namespace NexusInterfaces
{
    
    public static class Util
    {
        static ILogger m_Logger;
        public static RuntimeEnv Env
        {
            get { return RuntimeEnv.Env; }
        }
        public static ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }

        }


        public static void OpenFile(String fileName, String ErrorMessage)
        {

            if (fileName == null || !File.Exists(fileName))
            {
                Util.Logger.LogMessage(String.Format("File {0} doesn't exist yet. {1}", fileName,ErrorMessage), MessageOptions.All);
                return;
            }
            ProcessStartInfo pi = new ProcessStartInfo("notepad.exe", fileName);
            Process.Start(pi);

        }
        public static bool IsRunningUnderWOW()
        {
            String procType = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            if (null != procType)
                return true;
            else
                return false;

        }
        public static String GetReadTracePath()
        {
            string ReadTracePath = null;
            String resultString = "empty";
            //HKEY_CLASSES_ROOT\CLSID\{8F930F9E-42A5-11D3-95F9-0080C71FC66E}
            String oracRegPath = @"CLSID\{8F930F9E-42A5-11D3-95F9-0080C71FC66E}\LocalServer32";
            String orca = null;
            Util.Logger.LogMessage ("RTD: searching registry path " + oracRegPath);
            if (IsRunningUnderWOW())
            {
                Util.Logger.LogMessage("Running under WOW");
                int result = RegistryEx.Get64StringValue(RegistryEx.HKCR, oracRegPath, null, ref resultString);

                if (result == RegistryEx.ERROR_SUCCESS)
                    orca = resultString;
                else
                    Util.Logger.LogMessage("RTD: unable to find 64 bit registry key");

            }
            else
            {
                orca = (string)Registry.GetValue(@"HKEY_CLASSES_ROOT\" + oracRegPath, null, null);
            }


            if (null != orca)
            {
                ReadTracePath = Path.GetDirectoryName(orca.Replace("\"", "").Replace("\0", "")); //Registry.GetValue always wrap string around ""
            }

            //hard code path
            if (string.IsNullOrEmpty(ReadTracePath))
            {
                ReadTracePath = @"C:\Program Files\Microsoft Corporation\RMLUtils";
            }
            Util.Logger.LogMessage("Readtrace path " + ReadTracePath);
            
            FileVersionInfo info ;
            if (File.Exists (ReadTracePath + @"\readtrace.exe"))
            {
                info= FileVersionInfo.GetVersionInfo(ReadTracePath + @"\readtrace.exe");
                Util.Logger.LogMessage ("readtrace.exe file version " + info.FileVersion);
            }
            else
                Util.Logger.LogMessage ("Warning: readtrace.exe doesn't exist in directory : " + ReadTracePath);

            if (File.Exists (ReadTracePath + @"\reporter.exe"))
            {
                info= FileVersionInfo.GetVersionInfo(ReadTracePath + @"\reporter.exe");
                Util.Logger.LogMessage ("readtrace.exe file version " + info.FileVersion);
            }
            else
                Util.Logger.LogMessage ("Warning: readtrace.exe doesn't exist in directory : " + ReadTracePath);



            
            return ReadTracePath;
        }


        public static String GetReadTraceExe()
        {
            return GetReadTracePath() + @"\readtrace.exe";
        }
      

        public static string ExpandEscapeStrings(string instr)
        {
            return instr.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
        }

        public static bool ConnectingtoCurrentMachine(string Server)
        {

            if ('.' == Server[0])  // '.' = local machine
            {
                return true;
            }
            if (0 == string.Compare("(local)", Server, true, CultureInfo.InvariantCulture))  // '(local)' = local machine
            {
                return true;
            }
            if (-1 != Server.IndexOf('\\'))  //has instance
            {
                string[] sargs = Server.Split('\\');
                if (0 == string.Compare(sargs[0], System.Environment.MachineName, true, CultureInfo.InvariantCulture)) //this mach
                {
                    return true; 
                }
                else  //Has machine name and doesn't match ours
                {
                    //LogMessage(sqlnexus.Properties.Resources.Msg_CantRegister, MessageOptions.Dialog);
                    return false;
                }
            }
            else  //No instance
            {
                if (0 == string.Compare(Server, System.Environment.MachineName, true, CultureInfo.InvariantCulture)) //this mach
                {
                    return true;
                }
                else
                {
                    //LogMessage(sqlnexus.Properties.Resources.Msg_CantRegister, MessageOptions.Dialog);
                    return false;
                }
            }
            
        }

        public static FileSecurity GetFileSecurity()
        {
            FileSecurity fss = new FileSecurity();
            //  Deny anonymous 
            try
            {
                fss.AddAccessRule(new FileSystemAccessRule(@"NT AUTHORITY\ANONYMOUS LOGON",
                                                            FileSystemRights.FullControl,
                                                            AccessControlType.Deny));
            }
            catch (System.Security.Principal.IdentityNotMappedException ex)
            { 
                //eat this because use can change names
            }
            try
            {
                //  Deny guests 
                fss.AddAccessRule(new FileSystemAccessRule(System.Security.Principal.WindowsBuiltInRole.Guest.ToString(),
                                                            FileSystemRights.FullControl,
                                                            AccessControlType.Deny));
            }
            catch (System.Security.Principal.IdentityNotMappedException ex)
            {
                //eat this because use can change names
            }


            try
            {
                //      Admins only can see that the file exists 
                fss.AddAccessRule(new FileSystemAccessRule(@"BuiltIn\Administrators",
                                                            FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadAttributes | FileSystemRights.Synchronize,
                                                            AccessControlType.Allow));
            }
            catch (System.Security.Principal.IdentityNotMappedException ex)
            {
                //eat this because use can change names
            }

            try
            {
                String strCurrentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

                // Current User full control 
                fss.AddAccessRule(new FileSystemAccessRule(strCurrentUser,
                                                           FileSystemRights.FullControl,
                                                                                                     AccessControlType.Allow));
            }
            catch (System.Security.Principal.IdentityNotMappedException ex)
            {
                //eat this because use can change names
            }

            return fss;
        }
    }

    
    public class RuntimeEnv
    {
        private static RuntimeEnv m_singleton = new RuntimeEnv();
        private Dictionary<string, string> m_Env;
        private Object m_lock;
        public static RuntimeEnv Env
        {
            get
            {
                return m_singleton;
            }
        }

        private RuntimeEnv()
        {
            m_lock = new object();
            m_Env = new Dictionary<string, string>();

            String procType = null; // Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            bool wow = false;
            if (null != procType)
                wow = true;
            this["RunningUnderWOW"] = wow.ToString();


            this["OSVersion"] = System.Environment.OSVersion.VersionString;
            this["CLRVersion"] = Environment.Version.ToString();
            this["%appdata%"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            this["%temp%"] = Environment.ExpandEnvironmentVariables("%temp%");
            this["NexusLogFile"] = Environment.ExpandEnvironmentVariables("%temp%") + @"\sqlnexus.000.log";

            ReadTraceLogFile = Path.GetTempPath() + @"RML\readtrace.log";

        }
        public String ReadTraceLogFile
        {
            get
            {
                return this["ReadTraceLogFile"];
            }
            set
            {
                this["ReadTraceLogFile"] = value;
            }
        }

        public String NexusLogFile
        {
            get
            {
                return this["NexusLogFile"];
            }
            set
            {
                this["NexusLogFile"] = value;
            }
        }

        

        public string this[String key]
        {
            get
            {
                if (m_Env.ContainsKey(key))
                    return m_Env[key];
                else
                    return null;

            }
            set
            {
                lock (m_lock)
                {
                    m_Env[key] = value;
                }

            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (String key in m_Env.Keys)
            {
                Int32 fixedLength = 30;
                String temp;
                if (key.Length >= fixedLength)
                    temp = key.Substring(0, fixedLength);
                else
                    temp = key.PadRight(fixedLength - key.Length);
                sb.AppendFormat("{0} {1} {2}", temp, m_Env[key], Environment.NewLine);
            }
            return sb.ToString();

        }
    }
    public class RegistryEx
    {
        public static int Get64StringValue(UIntPtr regKeyRoot, string keyName, string valueName, ref string stringResult)
        {
            UIntPtr hkey = UIntPtr.Zero;
            try
            {
                //int result = RegOpenKeyEx(regKeyRoot, keyName, 0, (KeyAccess.QueryValue), ref hkey);
                //if (ERROR_SUCCESS != result)
                 int result = RegOpenKeyEx(regKeyRoot, keyName, 0, (KeyAccess.QueryValue | KeyAccess.KEY_WOW64_64KEY), ref hkey);

                 if (ERROR_SUCCESS != result)
                 {
                     Util.Logger.LogMessage("RTD: unable to find key " + keyName + "\\" + valueName);
                     return result;
                 }

                byte[] bytes = null;
                uint length = 0;
                KeyType keyType = KeyType.None;

                result = RegQueryValueEx(hkey, valueName, IntPtr.Zero, ref keyType,
                    null, ref length);

                if (ERROR_SUCCESS != result)
                    return result;

                keyType = KeyType.None;
                bytes = new byte[length];

                result = RegQueryValueEx(hkey, valueName, IntPtr.Zero, ref keyType,
                    bytes, ref length);

                if (ERROR_SUCCESS != result)
                    return result;

                stringResult = Encoding.Unicode.GetString(bytes, 0, bytes.Length);

                return ERROR_SUCCESS;
            }
            finally
            {
                if (UIntPtr.Zero != hkey)
                {
                    RegCloseKey(hkey);
                }
            }
        }



        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyEx", SetLastError = true)]
        public static extern int RegOpenKeyEx
            (
                UIntPtr hkey,
                String lpSubKey,
                uint ulOptions,
                KeyAccess samDesired,
                ref UIntPtr phkResult
            );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueEx", SetLastError = true)]
        public static extern int RegQueryValueEx
        (
            UIntPtr hkey,
            String lpValueName,
            IntPtr lpReserved,
            ref KeyType lpType,
            byte[] lpData,
            ref uint lpcbData
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegCloseKey
        (
            UIntPtr hkey
        );





        public static UIntPtr HKCR = new UIntPtr(0x80000000);
        public static UIntPtr HKCU = new UIntPtr(0x80000001);
        public static UIntPtr HKLM = new UIntPtr(0x80000002);
        public static UIntPtr HKU = new UIntPtr(0x80000003);
        public const int ERROR_SUCCESS = 0;

        public enum KeyType : uint
        {
            None = 0,
            String = 1,
            Dword = 4,
        }


        public enum KeyAccess : uint
        {
            None = 0x0000,
            QueryValue = 0x0001,
            SetValue = 0x0002,
            CreateSubKey = 0x0004,
            EnumerateSubKeys = 0x0008,
            Notify = 0x0010,
            CreateLink = 0x0020,
            KEY_WOW64_32KEY = 0x200,
            KEY_WOW64_64KEY = 0x100


        }


    }
}
