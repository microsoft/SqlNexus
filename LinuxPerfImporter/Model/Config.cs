/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LinuxPerfImporter.Utility;

namespace LinuxPerfImporter.Model
{
    // since this configuration file gives instructions throughout the program, it is created as a global class
    public static class ConfigValues
    {
        public static string WorkingDirectory { get; set; }
        public static string MachineName { get; set; }
        public static int TimeZone { get; set; }
        public static bool ImportIoStat { get; set; }
        public static bool ImportMpStat { get; set; }
        public static bool ImportMemFree { get; set; }
        public static bool ImportMemSwap { get; set; }
        public static bool ImportNetStats { get; set; }
        public static bool ImportPidStat { get; set; }
        public static string[] PidStatFilter { get; set; }
        public static bool ImportCombine { get; set; }
        public static int ProgressLine { get; set; }
    }
    class Config
    {
        // class constructor
        public Config()
        {
            GetMachineName();
            FileContents = GetFileContents();
            GetConfigVariables();
            GetTimeZone();
        }

        private List<string> FileContents { get; set; }

        // class functions
        private List<string> GetFileContents()
        {
            return new FileUtility().ReadFileByLine("pssdiagimport.conf");
        }
        private void GetConfigVariables()
        {
            TypeConversionUtility typeConversion = new TypeConversionUtility();

            // delimeter for parameter lines "parameter = value"
            char parameterDelimeter = '=';
            // delimeter for pidstat filter value "sqlservr,sqlcmd"
            char pidStatFilterDelimeter = ',';

            // itterate through the config file line by line.        
            foreach (string line in FileContents)
            {
                string[] splitValue = { };
                bool parameterValueBool = false;
                string parameterValueString;

                // checks to see if the line contains an "=" and does not begin with "#", which allows to comment out lines in the text file.
                if (line.Contains(parameterDelimeter.ToString()) && !line.StartsWith("#"))
                {
                    // take the value of the line and split it. we can then compare the values on each side of the "="
                    splitValue = line.Split(parameterDelimeter);
                    // the configuration file allows for values of true/false and yes/no. this will convert those to bool values.
                    string parameterValue = splitValue[1].ToLower();
                    parameterValueBool = typeConversion.ConvertTypeToBool(parameterValue);
                    // get parameter name, converts to lowercase for comparing strings and trims white space.
                    string parameter = splitValue[0].ToLower().Trim();
                    // check parameter name and when it matches, set the value of that parameter property.
                    switch (parameter)
                    {
                        case "machine_name":
                            parameterValueString = splitValue[1];
                            ConfigValues.MachineName = parameterValueString;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueString, "machine_name", "[Config]");
                            break;
                        case "import_iostat":
                            ConfigValues.ImportIoStat = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_iostat", "[Config]");
                            break;
                        case "import_mpstat":
                            ConfigValues.ImportMpStat = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_mpstat", "[Config]");
                            break;
                        case "import_memfree":
                            ConfigValues.ImportMemFree = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_memfree", "[Config]");
                            break;
                        case "import_memswap":
                            ConfigValues.ImportMemSwap = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_memswap", "[Config]");
                            break;
                        case "import_network_stats":
                            ConfigValues.ImportNetStats = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_network_stats", "[Config]");
                            break;
                        case "import_pidstat":
                            ConfigValues.ImportPidStat = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_pidstat", "[Config]");
                            break;
                        case "import_pidstat_filter":
                            // since pidstat_filter accepts comma separated, dynamic values, we need to remove spaces, capture this and turn it into an array.
                            string spacePattern = "\\s+";
                            string spaceReplacement = "";
                            Regex rgx = new Regex(spacePattern);
                            parameterValueString = rgx.Replace(splitValue[1].ToLower(), spaceReplacement);
                            string[] pidStatFilerSplitValue = parameterValueString.Split(pidStatFilterDelimeter);

                            ConfigValues.PidStatFilter = pidStatFilerSplitValue;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueString, "import_pidstat_filter", "[Config]");
                            break;
                        case "import_combine_perfmon_files":
                            ConfigValues.ImportCombine = parameterValueBool;
                            LinuxPerfImortGlobals.log.WriteLog(parameterValueBool.ToString(), "import_combine_perfmon_files", "[Config]");
                            break;
                        default:
                            break;
                    }
                }
                else if (line.StartsWith("#"))
                {
                    LinuxPerfImortGlobals.log.WriteLog("Ignoring line: " + line, "SetConfigVariables", "[Config]");
                }
            };
        }

        // gets and sets timezone
        private void GetTimeZone()
        {
            FileUtility fileUtility = new FileUtility();
            int tz = Convert.ToInt16(fileUtility.ReadFileByLine("*timezone.info")[0].Substring(0, 3));

            ConfigValues.TimeZone = tz;
        }

        private void GetMachineName()
        {
            string[] splitChars = new string[] { "_machineconfig.info" };

            LinuxPerfImortGlobals.log.WriteLog("Getting machine name ", "SetConfigVariables", "[Config]");

            string machineName = Directory.GetFiles(".\\", "*_machineconfig.info")[0];

            ConfigValues.MachineName = machineName.Substring(2, (machineName.Length - 21));
            
            LinuxPerfImortGlobals.log.WriteLog(ConfigValues.MachineName, "MachineName:GetMachineName", "[Config]");
        }
    }
}