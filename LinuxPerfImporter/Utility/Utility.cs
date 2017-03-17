/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LinuxPerfImporter.Model;

namespace LinuxPerfImporter.Utility
{
    // utility class that contains common methods used by multiple classes and/or methods
    // contains some basic logging
    class DateTimeUtility
    {
        public string FromUnixTime(long unixTime, int timeZone = 0)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dt = new DateTime();
            // returns the converted time and adjusts it to the timezone
            dt = epoch.AddSeconds(unixTime).AddHours(timeZone);
            return DateTime24HourFormat(dt);
        }
        // converts time to 24 hours
        public string DateTime24HourFormat(DateTime dateToFormat)
        {
            return dateToFormat.ToString("MM/dd/yyyy HH:mm:ss");
        }

        // converts the time to 24 hours and since some of the out files do not increment 
        // the day when metrics roll over into a new day, we need to provide the logic to do so.
        public MetricTimeStamp FormatMetricTimeStamp(DateTime timeStamp, int lastMetricHour)
        {
            MetricTimeStamp metric = new MetricTimeStamp();

            int currentMetricHour = Convert.ToInt16(timeStamp.ToString("HH"));
            metric.LastTimeStampHour = currentMetricHour;

            if (currentMetricHour >= lastMetricHour)
            {
                metric.FormattedTimeStamp = timeStamp.ToString("MM/dd/yyyy HH:mm:ss");
                metric.IncrementDay = false;
            }
            else
            {
                metric.FormattedTimeStamp = timeStamp.AddDays(1).ToString("MM/dd/yyyy HH:mm:ss");
                metric.IncrementDay = true;
            }

            return metric;
        }
    }

    class FileUtility
    {
        // reads files line by line
        public List<string> ReadFileByLine(string file)
        {
            List<string> returnValue = new List<string>();
            try
            {
                FileStream fileStream = new FileStream(GetFullFilePath(file), FileMode.Open);
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    while (!streamReader.EndOfStream)
                    {
                        string line = streamReader.ReadLine();
                        returnValue.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog(file + ": " + e.Message, "ReadFileByLine", "[Error]");
                throw;
            }

            return returnValue;
        }
        // writes the converted tsv results to tsv file
        public void WriteTsvFileByLine(string file, string header, List<string> metrics)
        {
            try
            {
                FileStream fileStream = new FileStream(SetTsvFileName(file), FileMode.Create, FileAccess.Write);

                using (StreamWriter streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine(header);

                    foreach (string line in metrics)
                    {
                        streamWriter.WriteLine(line);
                    }
                }
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog(file + ": " + e.Message, "WriteTsvFileByLine", "[Error]");
                throw;
            }

        }
        // returns the full path to the file. we pass in a wildcard value and need this when opening a file
        public static string GetFullFilePath(string file)
        {
            string currentDirectory = ConfigValues.WorkingDirectory; // Directory.GetCurrentDirectory();

            try
            {
                string filePath = Directory.GetFiles(currentDirectory, file)[0];

                return filePath;
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog("Current working directory: " + ConfigValues.WorkingDirectory, "GetFileContents:Config", "[Debug]");
                LinuxPerfImortGlobals.log.WriteLog(file + ": " + e.Message, "GetFullFilePath", "[Error]");

                return "false";
                throw;
            }
        }
        // this changes the file name from the original .out filename to .tsv. the result of this is used to name the tsv file
        public static string SetTsvFileName(string file)
        {
            try
            {
                string currentFileName = GetFullFilePath(file);
                string tsvFileName = currentFileName.Replace(".perf", ".tsv");

                return tsvFileName;
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog(file + ": " + e.Message, "SetTsvFileName", "[Error]");
                throw;
            }
        }
    }

    class ProcessUtility
    {
        public Process StartProcess(string processCommand, string processArgs)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(processCommand, processArgs);
            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.Start();

            return process;
        }

    }
    // used to convert common true,yes,1/false,no,0 values to bool
    class TypeConversionUtility
    {
        public bool ConvertTypeToBool(string typeValue)
        {
            bool pv;
            switch (typeValue)
            {
                case "1":
                case "true":
                case "yes":
                    pv = true;
                    break;
                case "0":
                case "false":
                case "no":
                    pv = false;
                    break;
                default:
                    pv = false;
                    break;
            }

            return pv;
        }
    }

    class MetricTimeStamp
    {
        public MetricTimeStamp()
        {
            IncrementDay = false;
        }
        public bool IncrementDay { get; set; }
        public string FormattedTimeStamp { get; set; }
        public int LastTimeStampHour { get; set; }
    }
}