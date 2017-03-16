/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;

namespace LinuxPerfImporter.Model
{
    class LinuxOutFileMpStat : LinuxOutFile
    {
        // class constructor
        public LinuxOutFileMpStat(string mpStatFileName) :
            base(mpStatFileName)
        {
            Devices = GetMpStatDevices();
            Header = GetMpStatHeader();
            Metrics = GetMpStatMetrics();
        }

        // class properties
        private List<string> Devices { get; set; }

        // class methods
        // get the list of devices, in this case device is the cpu
        private List<string> GetMpStatDevices()
        {
            int startingLine = 3;
            int deviceColumnNumber = 2;

            return new LinuxOutFileHelper().GetDevices(startingLine, FileContents, deviceColumnNumber);
        }
        // get the header that will be written to the tsv file
        private string GetMpStatHeader()
        {
            OutHeader outHeader = new OutHeader()
            {
                StartingColumn = 3,
                StartingRow = 2,
                FileContents = FileContents,
                Devices = Devices,
                ObjectName = "Processor"
            };

            return new LinuxOutFileHelper().GetHeader(outHeader);
        }
        // generate the metrics that will get written to the tsv file
        private List<string> GetMpStatMetrics()
        {
            return new LinuxOutFileHelper().GetMetrics(Devices, FileContents);
        }
    }

}


/* EXAMPLE of MpStat_CPU file
Linux 3.10.0-327.28.3.el7.x86_64 (dl380g802-v02) 	01/11/2017 	_x86_64_	(8 CPU)

12:58:00 PM  CPU    %usr   %nice    %sys %iowait    %irq   %soft  %steal  %guest  %gnice   %idle
12:58:10 PM  all    0.48    0.00    0.36    0.03    0.00    0.01    0.00    0.00    0.00   99.12
12:58:10 PM    0    0.10    0.00    0.10    0.00    0.00    0.00    0.00    0.00    0.00   99.79
12:58:10 PM    1    2.40    0.00    0.70    0.10    0.00    0.00    0.00    0.00    0.00   96.80
12:58:10 PM    2    0.10    0.00    0.50    0.00    0.00    0.00    0.00    0.00    0.00   99.40
12:58:10 PM    3    0.80    0.00    0.20    0.00    0.00    0.00    0.00    0.00    0.00   99.00
12:58:10 PM    4    0.00    0.00    0.20    0.00    0.00    0.00    0.00    0.00    0.00   99.80
12:58:10 PM    5    0.10    0.00    0.20    0.00    0.00    0.00    0.00    0.00    0.00   99.70
12:58:10 PM    6    0.00    0.00    0.10    0.00    0.00    0.00    0.00    0.00    0.00   99.90
12:58:10 PM    7    0.20    0.00    0.90    0.00    0.00    0.00    0.00    0.00    0.00   98.90

12:58:10 PM  CPU    %usr   %nice    %sys %iowait    %irq   %soft  %steal  %guest  %gnice   %idle
12:58:20 PM  all    0.29    0.00    0.23    0.00    0.00    0.00    0.00    0.00    0.00   99.48
12:58:20 PM    0    0.00    0.00    0.10    0.00    0.00    0.00    0.00    0.00    0.00   99.90
12:58:20 PM    1    1.10    0.00    0.60    0.00    0.00    0.00    0.00    0.00    0.00   98.30
12:58:20 PM    2    0.20    0.00    0.40    0.00    0.00    0.00    0.00    0.00    0.00   99.40
12:58:20 PM    3    0.91    0.00    0.20    0.00    0.00    0.00    0.00    0.00    0.00   98.88
12:58:20 PM    4    0.00    0.00    0.10    0.00    0.00    0.00    0.00    0.00    0.00   99.90
12:58:20 PM    5    0.00    0.00    0.00    0.00    0.00    0.00    0.00    0.00    0.00  100.00
12:58:20 PM    6    0.10    0.00    0.00    0.00    0.00    0.00    0.00    0.00    0.00   99.90
12:58:20 PM    7    0.10    0.00    0.40    0.00    0.00    0.00    0.00    0.00    0.00   99.50
*/
