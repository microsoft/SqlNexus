/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LinuxPerfImporter.Utility;

namespace LinuxPerfImporter.Model
{
    class ImportCombine
    {
        // private void CleanUp()
        // {
        //     Progress progress = new Progress();

        //     try
        //     {
        //         IEnumerable<string> listOfTsv = Directory.EnumerateFiles(".\\", "*.tsv", SearchOption.AllDirectories);

        //         foreach (string file in listOfTsv)
        //         {
        //             Directory.Delete(file);
        //             LinuxPerfImortGlobals.log.WriteLog("Removing: " + file, "ImportCombine:CleanUp", "[Info]");
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         LinuxPerfImortGlobals.log.WriteLog("Nothing to clean up (TSV)", "ImportCombine:CleanUp", "[Info]");
        //     }

        //     try
        //     {
        //         IEnumerable<string> listOfBlg = Directory.EnumerateFiles(".\\", "*.blg", SearchOption.AllDirectories);

        //         foreach (string file in listOfBlg)
        //         {
        //             Directory.Delete(file);
        //             LinuxPerfImortGlobals.log.WriteLog("Removing: " + file, "ImportCombine:CleanUp", "[Info]");
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         LinuxPerfImortGlobals.log.WriteLog("Nothing to clean up (BLG)", "ImportCombine:CleanUp", "[Info]");
        //     }
        // }
        public void CreateOutputDirectory()
        {
            if (!Directory.Exists("Single_BLG"))
            {
                Directory.CreateDirectory("Single_BLG");
            }
        }
        public void RelogConvertToBlg()
        {
            try
            {
                List<Process> relogCollection = new List<Process>();
                IEnumerable<string> listOfTsv = Directory.GetFiles(".\\", "*.tsv");
                
                foreach (string file in listOfTsv)
                {
                    string blgFileName = file.Replace("tsv", "blg");
                    string processCommand = "relog.exe ";
                    string processArgs = file + " -o .\\" + blgFileName + " -y";
                    LinuxPerfImortGlobals.log.WriteLog(processCommand + processArgs, "ImportCombine:RelogConvertToBlg", "[Info]");

                    relogCollection.Add(new Utility.ProcessUtility().StartProcess(processCommand, processArgs));
                }

                foreach (Process p in relogCollection)
                {
                    p.WaitForExit();
                    p.Dispose();
                }
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog(e.Message, "ImportCombine:RelogConvertToBlg", "[Error]");
            }

            RelogCombineToBlg();
        }
        private void RelogCombineToBlg()
        {
            try
            {
                string processCommand = "relog.exe ";
                string processArgs = ".\\*.blg -o .\\Single_BLG\\" + ConfigValues.MachineName + "_AllPerfmonMetrics.blg -y";
                LinuxPerfImortGlobals.log.WriteLog(processCommand + processArgs, "ImportCombine:RelogCombineToBlg", "[Info]");
                
                Process p = new Utility.ProcessUtility().StartProcess(processCommand, processArgs);
                p.WaitForExit();
                p.Dispose();
            }
            catch (Exception e)
            {
                LinuxPerfImortGlobals.log.WriteLog(e.Message, "ImportCombine:RelogCombineToBlg", "[Error]");
            }
        }
    }
}