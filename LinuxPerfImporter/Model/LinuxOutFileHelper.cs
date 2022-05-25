/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LinuxPerfImporter.Utility;

namespace LinuxPerfImporter.Model
{
    // contains common methods used by the linuxoutfile[name] classes to get devices, metrics and/or headers.
    class LinuxOutFileHelper
    {
        public List<string> GetDevices(int startingLine, List<string> fileContents, int deviceColumnNumber)
        {
            List<string> devices = new List<string>();

            string emptyLinePattern = "^\\s*$";
            string splitPattern = "\\s+";

            Regex rgxEmptyLine = new Regex(emptyLinePattern);
            Regex rgxSplitLine = new Regex(splitPattern);

            // since the out files that have devices are block format and devices can be parsed in 1 block, we tell this method to only read the first block.
            int blockCount = 1;
            // we set the initial block count to 0
            int block = 0;
            // the starting line to start to parse the devices. passed in by the caller
            int lineNumber = startingLine;

            // loop through the first block
            while (block < blockCount)
            {
                // check to make sure we are not on an empty line. then get loop through ad get devices
                if (!rgxEmptyLine.IsMatch(fileContents[lineNumber]))
                {
                    string[] thisLineValues = rgxSplitLine.Split(fileContents[lineNumber]);
                    devices.Add(thisLineValues[deviceColumnNumber]);
                    lineNumber++;
                }
                // if we hit an empty line, we know that we have reached the end of the block
                else
                {
                    block++;
                }
            }

            return devices;
        }

        // common method to generate the header that gets written to the tsv file for perfmon
        public string GetHeader(OutHeader outHeader)
        {
            string splitPattern = "\\s+";
            Regex rgx = new Regex(splitPattern);
            string[] outHeaderSplit = rgx.Split(outHeader.FileContents[outHeader.StartingRow]);
            StringBuilder header = new StringBuilder();
            header.Append('"' + "(PDH-TSV 4.0) (Pacific Daylight Time)(420)" + '"' + "\t");

            // for the out files that have devices, we need to include the devices as part of the header
            if (outHeader.Devices != null)
            {
                foreach (string device in outHeader.Devices)
                {
                    for (int i = outHeader.StartingColumn; i < outHeaderSplit.Length - outHeader.TrimColumn; i++)
                    {
                        header.Append('"' + "\\\\" + ConfigValues.MachineName + "\\" + outHeader.ObjectName + "(" + device + ")\\" + outHeaderSplit[i] + '"' + "\t");
                    }
                }
            }

            // some out files do not have devices, like mem free or mem swap. we look for devices count and if there are none, create header w/o devices
            if (outHeader.Devices == null)
            {
                for (int i = outHeader.StartingColumn; i < outHeaderSplit.Length - outHeader.TrimColumn; i++)
                {
                    header.Append('"' + "\\\\" + ConfigValues.MachineName + "\\" + outHeader.ObjectName + "\\" + outHeaderSplit[i] + '"' + "\t");
                }
            }

            return header.ToString();
        }

        // since we need to get the metrics of swap and free memory, I am creating the get metric method in the common class.
        public List<string> GetMemoryMetrics(List<string> fileContents)
        {
            // Progress progress = new Progress();
            // progress.WriteTitle("Parsing Memory metrics",progressLine);

            List<string> metrics = new List<string>();

            string splitPattern = "\\s+";
            string datePattern = "(\\d{2})\\/(\\d{2})\\/(\\d{4})";

            Regex rgxSplitLine = new Regex(splitPattern);

            int lastTimeStampHour = -1;
            Regex rgxDate = new Regex(datePattern);
            string metricDateMatch = rgxDate.Match(fileContents[0]).ToString();

            for (int i = 3; i < fileContents.Count; i++)
            {
                // progress.WriteProgress(i, fileContents.Count,progressLine);

                StringBuilder thisMetricSample = new StringBuilder();
                string[] thisLineContents = rgxSplitLine.Split(fileContents[i]);

                DateTime timeStamp = DateTime.Parse(metricDateMatch + " " + thisLineContents[0] + " " + thisLineContents[1]);

                MetricTimeStamp metricTimeStamp = new DateTimeUtility().FormatMetricTimeStamp(timeStamp, lastTimeStampHour);

                if (metricTimeStamp.IncrementDay) { metricDateMatch = DateTime.Parse(metricTimeStamp.FormattedTimeStamp).ToString("MM/dd/yyyy"); }
                lastTimeStampHour = metricTimeStamp.LastTimeStampHour;

                thisMetricSample.Append('"' + metricTimeStamp.FormattedTimeStamp + '"' + "\t");

                for (int j = 2; j < thisLineContents.Length; j++)
                {
                    thisMetricSample.Append('"' + thisLineContents[j] + '"' + "\t");
                }
                metrics.Add(thisMetricSample.ToString());
            }
            return metrics;
        }

        // this is the common get metric shared between network and mpstat. since both files are similar formats, we place the get metrics in the common helper class
        public List<string> GetMetrics(List<string> devices, List<string> fileContents)
        {
            // Progress progress = new Progress();
            // progress.WriteTitle("Parsing MPStat/Network metrics", progressLine);

            List<string> metrics = new List<string>();

            string emptyLinePattern = "^\\s*$";
            string splitPattern = "\\s+";
            string datePattern = "(\\d{2})\\/(\\d{2})\\/(\\d{4})";

            Regex rgxEmptyLine = new Regex(emptyLinePattern);
            Regex rgxSplitLine = new Regex(splitPattern);
            Regex rgxDate = new Regex(datePattern);

            int deviceCount = devices.Count;

            int lastTimeStampHour = -1;
            string metricDateMatch = rgxDate.Match(fileContents[0]).ToString();

            // looping through each line in the contents of this out file
            for (int i = 1; i < fileContents.Count;)
            {
                // progress.WriteProgress(i, fileContents.Count, progressLine);

                StringBuilder thisMetricSample = new StringBuilder();

                // this file is in a block format and we use empty lines to determin when to start parsing the next metric
                if (rgxEmptyLine.IsMatch(fileContents[i]) && i < fileContents.Count - 1)
                {
                    // advances to the line of the next metric
                    i = i + 2;

                    // grabbing timestamp information for this current metric. we also provide the logic to roll over to the next day since the linux metric collection does not do this for us.
                    string[] thisLineContents = rgxSplitLine.Split(fileContents[i]);

                    DateTime timeStamp = DateTime.Parse(metricDateMatch + " " + thisLineContents[0] + " " + thisLineContents[1]);

                    MetricTimeStamp metricTimeStamp = new DateTimeUtility().FormatMetricTimeStamp(timeStamp, lastTimeStampHour);

                    if (metricTimeStamp.IncrementDay) { metricDateMatch = DateTime.Parse(metricTimeStamp.FormattedTimeStamp).ToString("MM/dd/yyyy"); }
                    lastTimeStampHour = metricTimeStamp.LastTimeStampHour;

                    thisMetricSample.Append('"' + metricTimeStamp.FormattedTimeStamp + '"' + "\t");
                }

                // this is where the metric data gets parsed and added to the collection
                for (int x = 1; x <= deviceCount; x++)
                {
                    string[] thisLineContents = rgxSplitLine.Split(fileContents[i]);

                    // read the contents of the split line, start at column 3 and read each string until the end of the array.
                    for (int j = 3; j < thisLineContents.Length; j++)
                    {
                        thisMetricSample.Append('"' + thisLineContents[j] + '"' + "\t");
                    }

                    // the logic of incrementing the for loop for the file contents file is with in the for statement since we need to do some more complicated parsing.
                    i++;
                }

                metrics.Add(thisMetricSample.ToString());
            }
            return metrics;
        }
    }

    // this is the object that we pass in to the GetHeader method in this helper class. this object contains instructions on where to start parsing data, data to parse and devices to include in the header.
    class OutHeader
    {
        public OutHeader()
        {
            // setting the TrimColumn to a default of 0. In the case where we need to 
            // get columns from startcolumn to end, but need to ommit the last column. 
            // Like in the pidstat case. We don't want to get the last column "command"
            TrimColumn = 0;
        }
        public int StartingColumn { get; set; }
        public int TrimColumn { get; set; }
        public int StartingRow { get; set; }
        public List<string> FileContents { get; set; }
        public List<string> Devices { get; set; }
        public string ObjectName { get; set; }
    }
}