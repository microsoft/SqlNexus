/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LinuxPerfImporter.Utility;

namespace LinuxPerfImporter.Model
{
    class LinuxOutFilePidStat : LinuxOutFile
    {
        // class constructor
        public LinuxOutFilePidStat(string pidStatFileName) :
            base(pidStatFileName)
        {
            Processes = GetProcessMetrics();
            UniquePids = GetUniquePids();
            Header = GetPidStatHeader();
            Metrics = GetPidStatMetrics();
        }

        // class properties
        private string[] PidFilter { get; set; }
        private List<long> BlockCount = new List<long>();
        private List<PidProcessGroup> Processes = new List<PidProcessGroup>();
        private Dictionary<long, string> UniquePids;
        // number of metric that gets parsed.
        private int MetricCount = 16;

        // class methods
        // Reads each line where there are metrics, creates a process object and adds it to the objects collection
        public List<PidProcessGroup> GetProcessMetrics()
        {
            // we need to keep track of blocks of metrics so that we know which metrics to group together.
            bool blockIsFirst = true;

            PidProcessGroup pidProcessGroup = new PidProcessGroup();

            List<PidProcessGroup> processes = new List<PidProcessGroup>();

            DateTimeUtility utility = new DateTimeUtility();

            string emptyLinePattern = "^\\s*$";
            string splitPattern = "\\s+";

            Regex rgxEmptyLine = new Regex(emptyLinePattern);
            Regex rgxSplitLine = new Regex(splitPattern);

            // starting at the first line where # appears
            for (int i = 2; i <= FileContents.Count - 1;)
            {
                if (FileContents[i].StartsWith("#"))
                {
                    // if not the first block on first iteration, we add the complete metric group to the larger collection "processes"
                    if (!blockIsFirst)
                    {
                        processes.Add(pidProcessGroup);
                    }

                    // if it is first iteration, we need to check and flip it to false so that future iterations will add to larger collection "processes"
                    if (blockIsFirst)
                    {
                        blockIsFirst = false;
                    }

                    // takes the value of the current line and splits on whitespace
                    string[] thisProcessLine = rgxSplitLine.Split(FileContents[i + 1]);

                    // reads the line timestamp, converts it from ephoc/unix time
                    string thisTimeStamp = utility.FromUnixTime(Convert.ToInt32(thisProcessLine[1]), TimeZone);

                    // add the current line position to the block count array. We will use this to spin off multiple threads for processing the pid stat file
                    BlockCount.Add(i + 1);
                    i++;

                    // we create the process group which groups by timestamp
                    pidProcessGroup = new PidProcessGroup()
                    {
                        TimeStamp = thisTimeStamp
                    };
                }
                // just skip empty lines and increment to the next line
                else if (rgxEmptyLine.IsMatch(FileContents[i]))
                {
                    i++;
                }
                else
                {
                    // takes the value of the current line and splits on whitespace
                    string[] thisProcessLine = rgxSplitLine.Split(FileContents[i]);
                    // reads the line timestamp, converts it from ephoc/unix time
                    string thisTimeStamp = utility.FromUnixTime(Convert.ToInt32(thisProcessLine[1]), TimeZone);
                    // reads this line's pid
                    int thisPid = Convert.ToInt32(thisProcessLine[3]);
                    // reads this lines process name
                    string thisProcessName = thisProcessLine[thisProcessLine.Length - 1];
                    // declares a new array so that we can populate this array with metrics with array.copy
                    string[] theseMetrics = new string[MetricCount];

                    // copies the metrics from the current line to theseMetrics array. We need to do this since we split the line to get other metrics.
                    Array.Copy(thisProcessLine, 4, theseMetrics, 0, MetricCount);

                    // create a new process object and set its properties from the vairables we declared above
                    PidProcess process = new PidProcess()
                    {
                        TimeStamp = thisTimeStamp,
                        Pid = thisPid,
                        ProcessName = thisProcessName,
                        Metrics = theseMetrics
                    };

                    // we need to filter out what gets collected based on the PidFilter in the config file.
                    if (ConfigValues.PidStatFilter.Count() != -1 && ConfigValues.PidStatFilter.Contains(process.ProcessName))
                    {
                        // once we are done generating the process object, we add the object to the collection of processes
                        pidProcessGroup.GroupMetrics.Add(process);
                    }

                    // if there are no filters, we need to add everything
                    if (ConfigValues.PidStatFilter[0] == "" || ConfigValues.PidStatFilter[0] == "false")
                    {
                        // once we are done generating the process object, we add the object to the collection of processes
                        pidProcessGroup.GroupMetrics.Add(process);
                    }

                    i++;
                }
            }

            return processes;
        }

        // we need to get the unique pids and order them so that we can generate the header.
        // Since each block of results in the pidstat out file can contain new or removed pids
        // from previous blocks, we need to get this information up front before processing the results
        private Dictionary<long, string> GetUniquePids()
        {
            Dictionary<long, string> unique = new Dictionary<long, string>();

            unique = Processes.SelectMany(x => x.GroupMetrics).Select(y => new { y.Pid, y.ProcessName }).Distinct().OrderBy(pid => pid.Pid).ToDictionary(x => x.Pid, x => x.ProcessName);

            return unique;
        }

        // now that we have the unique pids, we can create the header
        private string GetPidStatHeader()
        {
            // creating the outheader object and passing in variables on where to start parsing specific strings
            OutHeader outHeader = new OutHeader()
            {
                StartingColumn = 4,
                TrimColumn = 1,
                StartingRow = 2,
                FileContents = FileContents,
                Devices = UniquePids.Select(x => x.Value + "#" + x.Key).ToList(),
                ObjectName = "Process"
            };

            return new LinuxOutFileHelper().GetHeader(outHeader);
        }

        // generating the useful data for the metrics section of the TSV file
        private List<string> GetPidStatMetrics()
        {
            // create the collection that each generated line will be placed in
            List<string> metrics = new List<string>();

            // loop through every group in processes
            foreach (PidProcessGroup timeStampGroup in Processes)
            {
                string timeStamp = timeStampGroup.TimeStamp;

                StringBuilder metric = new StringBuilder();

                metric.Append('"' + timeStamp + '"' + "\t");

                foreach (var pid in UniquePids)
                {
                    // we check and see it the current unique pid exists in this group of metrics. some metric groups wont have all pids.
                    var checkIfPidExists = timeStampGroup.GroupMetrics.Where(x => x.Pid == pid.Key).Select(x => x.Pid).FirstOrDefault();

                    // if the unique pid matches the pid from the current process, this will write thatpid's data, collected from the out file
                    if (checkIfPidExists != 0)
                    {
                        var tm = timeStampGroup.GroupMetrics.Where(x => x.Pid == pid.Key).Select(x => x.Metrics).First();

                        foreach (var m in tm)
                        {
                            metric.Append('"' + m + '"' + "\t");
                        }
                    }

                    // if the unique pid does not match the pid from the current process, we write 0.00
                    if (checkIfPidExists == 0)
                    {
                        for (int i = 0; i <= MetricCount - 1; i++)
                        {
                            metric.Append('"' + "0.00" + '"' + "\t");
                        }
                    }
                }

                metrics.Add(metric.ToString());
            }

            return metrics;
        }
    }

    // Process class used when creating a new process. These get added to the Processes collection.
    class PidProcess
    {
        public long Pid { get; set; }
        public string ProcessName { get; set; }
        public string TimeStamp { get; set; }
        public string[] Metrics { get; set; }
    }

    // creating a class to keep track of PID groups. each group consists of several PIDs aligned with that timestamp.
    class PidProcessGroup
    {
        public PidProcessGroup()
        {
            GroupMetrics = new List<PidProcess>();
        }

        public string TimeStamp { get; set; }
        public List<PidProcess> GroupMetrics { get; set; }
    }
}

/* EXAMPLE of PidStat file
Wed Jan 11 12:58:00 PST 2017
Linux 3.10.0-327.28.3.el7.x86_64 (dl380g802-v02) 	01/11/2017 	_x86_64_	(8 CPU)

#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ    RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s   cswch/s nvcswch/s  Command
 1484168290     0         3    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  ksoftirqd/0
 1484168290     0         7    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  migration/0
 1484168290     0        73    0.00    0.10    0.00    0.01     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00     36.92      0.00  rcu_sched
 1484168290     0        74    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.59      0.00  rcuos/0
 1484168290     0        75    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      8.96      0.00  rcuos/1
 1484168290     0        76    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      2.19      0.00  rcuos/2
 1484168290     0        77    0.00    0.10    0.00    0.01     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      4.78      0.00  rcuos/3
 1484168290     0        78    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.70      0.00  rcuos/4
 1484168290     0        79    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.09      0.00  rcuos/5
 1484168290     0        80    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.90      0.00  rcuos/6
 1484168290     0        81    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      6.27      0.00  rcuos/7
 1484168290     0       138    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/0
 1484168290     0       139    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/1
 1484168290     0       144    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/2
 1484168290     0       145    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  migration/2
 1484168290     0       146    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  ksoftirqd/2
 1484168290     0       149    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/3
 1484168290     0       151    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  ksoftirqd/3
 1484168290     0       154    0.00    0.00    0.00    0.00     4      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/4
 1484168290     0       159    0.00    0.00    0.00    0.00     5      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/5
 1484168290     0       164    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/6
 1484168290     0       169    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.30      0.00  watchdog/7
 1484168290     0       190    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  khugepaged
 1484168290     0       589    0.00    0.10    0.00    0.01     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00     20.10      0.00  xfsaild/dm-0
 1484168290     0       590    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.50      0.00  kworker/1:1H
 1484168290     0       896    0.00    0.00    0.00    0.00     5      0.60      0.00   19180    540   0.00      0.00      0.00      0.00      0.10      0.00  irqbalance
 1484168290     0       897    0.00    0.00    0.00    0.00     0      0.20      0.00  207992    492   0.00      0.00      0.00      0.00      0.40      0.00  abrt-watch-log
 1484168290   996       899    0.00    0.00    0.00    0.00     7      0.00      0.00    8532    340   0.00      0.00      0.00      0.00      0.10      0.00  lsmd
 1484168290     0       926    0.00    0.00    0.00    0.00     1      0.00      0.00  126332    672   0.00      0.00      0.00      0.00      0.10      0.00  crond
 1484168290     0       985    0.00    0.00    0.00    0.00     7      0.00      0.00  436912   3060   0.02      0.00      0.00      0.00      0.10      0.00  NetworkManager
 1484168290     0      1597    0.00    0.00    0.00    0.00     0      0.00      0.00   13464    480   0.00      0.00      0.00      0.00      4.38      0.00  hv_kvp_daemon
 1484168290     0      2149    0.00    0.00    0.00    0.00     5      0.00      0.00   93228    572   0.00      0.00      0.00      0.00      0.20      0.00  master
 1484168290     0      6368    0.90    0.20    0.00    0.14     3      0.00      0.00  200076   9028   0.06      0.00      0.00      0.00      0.10      0.00  iotop
 1484168290     0      7309    0.00    0.70    0.00    0.09     4     91.04      0.00  797552  12164   0.07      0.00      0.00      0.00      1.00      0.00  collectd
 1484168290     0      8570    0.00    0.00    0.00    0.00     5      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.60      0.00  kworker/5:2
 1484168290     0      8856    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  cifsd
 1484168290     0     10060    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.09      0.00  kworker/7:1
 1484168290     0     10871    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.50      0.00  kworker/1:0
 1484168290     0     11481    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      5.47      0.00  kworker/0:2
 1484168290     0     13303    0.00    0.00    0.00    0.00     4      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  kworker/4:1
 1484168290     0     17698    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  kworker/6:1
 1484168290   994     19176    2.29    1.09    0.00    0.43     2      0.50      0.00 5394308 2599300  15.99      0.00      0.80      0.00      0.00      0.00  sqlservr
 1484168290     0     21691    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  kworker/3:2
 1484168290     0     22640    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.50      0.00  kworker/2:0
 1484168290    89     23228    0.00    0.00    0.00    0.00     3      0.00      0.00   93332   3920   0.02      0.00      0.00      0.00      0.10      0.00  pickup
 1484168290     0     23563    0.00    0.00    0.00    0.00     3      1.19      0.00  107940    812   0.00      0.00      0.00      0.00      0.10      0.00  iostat
 1484168290     0     23564    0.00    0.00    0.00    0.00     7      1.29      0.00  107928    792   0.00      0.00      0.00      0.00      0.10      0.00  mpstat
 1484168290     0     23568    0.00    0.00    0.00    0.00     5      1.19      0.00  107972    732   0.00      0.00      0.00      0.00      0.10      0.00  sar
 1484168290     0     23569    0.00    0.00    0.00    0.00     1      1.69      0.00  107928    820   0.01      0.00      0.00      0.00      0.10      0.00  mpstat
 1484168290     0     23570    0.00    0.00    0.00    0.00     4      1.19      0.00  107972    732   0.00      0.00      0.00      0.00      0.10      0.00  sar
 1484168290     0     23572    0.00    0.00    0.00    0.00     7      1.19      0.00  107972    736   0.00      0.00      0.00      0.00      1.00      0.00  sar
 1484168290     0     23573    0.00    0.00    0.00    0.00     5      1.89      0.00  113152    948   0.01      0.00      0.00      0.00      0.10      0.00  sadc
 1484168290     0     23574    0.00    0.00    0.00    0.00     4      1.89      0.00  113152    948   0.01      0.00      0.00      0.00      0.10      0.00  sadc
 1484168290     0     23575    0.00    0.40    0.00    0.05     7     18.91      0.00  113152    952   0.01      0.00      0.00      0.00      1.00      0.00  sadc
 1484168290     0     23576    0.10    0.40    0.00    0.06     2     90.45      0.00  108376   1244   0.01      0.00      0.00      0.00      0.10      0.00  pidstat
 1484168290     0     23577    2.29    0.60    0.00    0.36     1    415.72      0.00  197728  11556   0.07    234.03      0.40      0.00      1.99      0.80  iotop
 1484168290     0     26123    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  kworker/u128:1

#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ    RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s   cswch/s nvcswch/s  Command
 1484168300     0         3    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  ksoftirqd/0
 1484168300     0        73    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00     26.60      0.00  rcu_sched
 1484168300     0        74    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.70      0.00  rcuos/0
 1484168300     0        75    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      4.70      0.00  rcuos/1
 1484168300     0        76    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.90      0.00  rcuos/2
 1484168300     0        77    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      3.80      0.00  rcuos/3
 1484168300     0        78    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.60      0.00  rcuos/4
 1484168300     0        79    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.00      0.00  rcuos/5
 1484168300     0        80    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  rcuos/6
 1484168300     0        81    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      4.00      0.00  rcuos/7
 1484168300     0       138    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/0
 1484168300     0       139    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/1
 1484168300     0       144    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/2
 1484168300     0       149    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/3
 1484168300     0       154    0.00    0.10    0.00    0.01     4      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/4
 1484168300     0       159    0.00    0.00    0.00    0.00     5      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/5
 1484168300     0       164    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/6
 1484168300     0       169    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  watchdog/7
 1484168300     0       190    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  khugepaged
 1484168300     0       589    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00     20.00      0.00  xfsaild/dm-0
 1484168300     0       771    0.00    0.00    0.00    0.00     5      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  xfsaild/sda1
 1484168300     0       889    0.00    0.00    0.00    0.00     2      0.00      0.00  203368    180   0.00      0.00      0.00      0.00      0.10      0.00  gssproxy
 1484168300     0       896    0.00    0.00    0.00    0.00     5      0.60      0.00   19180    540   0.00      0.00      0.00      0.00      0.10      0.00  irqbalance
 1484168300   996       899    0.00    0.00    0.00    0.00     7      0.00      0.00    8532    340   0.00      0.00      0.00      0.00      0.10      0.00  lsmd
 1484168300     0      1518    0.10    0.00    0.00    0.01     5      0.00      0.00  553048   1900   0.01      0.00      0.00      0.00      0.00      0.00  tuned
 1484168300     0      1597    0.00    0.10    0.00    0.01     0      0.00      0.00   13464    480   0.00      0.00      0.00      0.00      4.40      0.00  hv_kvp_daemon
 1484168300     0      6368    0.80    0.10    0.00    0.11     3      0.20      0.00  200076   9028   0.06      0.00      0.00      0.00      0.10      0.00  iotop
 1484168300     0      7309    0.30    0.50    0.00    0.10     4     91.50      0.00  797552  12164   0.07      0.00      0.00      0.00      1.00      0.00  collectd
 1484168300     0      7944    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.10      0.00  kworker/6:1H
 1484168300     0      8570    0.00    0.00    0.00    0.00     5      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.70      0.00  kworker/5:2
 1484168300     0      8856    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  cifsd
 1484168300     0     10060    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      1.40      0.00  kworker/7:1
 1484168300     0     10871    0.00    0.00    0.00    0.00     1      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  kworker/1:0
 1484168300     0     11481    0.00    0.00    0.00    0.00     0      0.00      0.00       0      0   0.00      0.00      0.00      0.00      5.50      0.00  kworker/0:2
 1484168300     0     13303    0.00    0.00    0.00    0.00     4      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.60      0.00  kworker/4:1
 1484168300     0     17698    0.00    0.00    0.00    0.00     6      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.60      0.00  kworker/6:1
 1484168300   994     19176    2.10    1.40    0.00    0.44     2      0.40      0.00 5394308 2599292  15.99      0.00      0.00      0.00      0.00      0.00  sqlservr
 1484168300     0     21691    0.00    0.00    0.00    0.00     3      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.40      0.00  kworker/3:2
 1484168300     0     22640    0.00    0.00    0.00    0.00     2      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  kworker/2:0
 1484168300     0     23563    0.00    0.00    0.00    0.00     3      0.30      0.00  107940    848   0.01      0.00      0.00      0.00      0.10      0.00  iostat
 1484168300     0     23564    0.00    0.00    0.00    0.00     7      0.20      0.00  107928    836   0.01      0.00      0.00      0.00      0.10      0.00  mpstat
 1484168300     0     23568    0.00    0.00    0.00    0.00     5      0.00      0.00  107972    732   0.00      0.00      0.00      0.00      0.10      0.00  sar
 1484168300     0     23569    0.00    0.00    0.00    0.00     1      0.50      0.00  107928    868   0.01      0.00      0.40      0.00      0.10      0.00  mpstat
 1484168300     0     23570    0.00    0.00    0.00    0.00     4      0.00      0.00  107972    732   0.00      0.00      0.00      0.00      0.10      0.00  sar
 1484168300     0     23572    0.00    0.10    0.00    0.01     7      0.00      0.00  107972    736   0.00      0.00      0.40      0.00      1.00      0.00  sar
 1484168300     0     23573    0.00    0.00    0.00    0.00     5      1.90      0.00  113152    948   0.01      0.00      0.00      0.00      0.10      0.00  sadc
 1484168300     0     23574    0.00    0.10    0.00    0.01     4      1.90      0.00  113152    948   0.01      0.00      0.00      0.00      0.10      0.00  sadc
 1484168300     0     23575    0.10    0.50    0.00    0.08     7     19.00      0.00  113152    952   0.01      0.00      0.00      0.00      1.00      0.00  sadc
 1484168300     0     23576    0.10    0.30    0.00    0.05     2     91.60      0.00  108376   1276   0.01      0.00      0.80      0.00      0.10      0.00  pidstat
 1484168300     0     23577    0.90    0.10    0.00    0.13     1      5.10      0.00  199852  11908   0.07      0.00      0.00      0.00      0.10      0.10  iotop
 1484168300     0     26123    0.00    0.00    0.00    0.00     7      0.00      0.00       0      0   0.00      0.00      0.00      0.00      0.20      0.00  kworker/u128:1
 */
