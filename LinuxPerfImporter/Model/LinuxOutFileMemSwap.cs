/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;

namespace LinuxPerfImporter.Model
{
    class LinuxOutFileMemSwap : LinuxOutFile
    {
        // class constructor
        public LinuxOutFileMemSwap(string ioStatFileName) :
            base(ioStatFileName)
        {
            Header = GetMemSwapHeader();
            Metrics = GetMemSwapMetrics();
        }
        // class methods
        private string GetMemSwapHeader()
        {
            // creating the outheader object and passing in variables on where to start parsing specific strings
            OutHeader outHeader = new OutHeader()
            {
                StartingColumn = 2,
                StartingRow = 2,
                FileContents = FileContents,
                ObjectName = "Memory Swap"
            };

            return new LinuxOutFileHelper().GetHeader(outHeader);
        }

        // the get memory metrics method is in with the common methods sine we have to call this more than once.
        private List<string> GetMemSwapMetrics()
        {
            // int progressLine = 10;
            return new LinuxOutFileHelper().GetMemoryMetrics(FileContents);
        }

    }
}

/* EXAMPLE of MemSwap file
Linux 3.10.0-327.28.3.el7.x86_64 (dl380g802-v02) 	01/11/2017 	_x86_64_	(8 CPU)

12:58:00 PM kbswpfree kbswpused  %swpused  kbswpcad   %swpcad
12:58:10 PM    758480     81196      9.67      5132      6.32
12:58:20 PM    758480     81196      9.67      5132      6.32
12:58:30 PM    758480     81196      9.67      5132      6.32
12:58:40 PM    758480     81196      9.67      5132      6.32
12:58:50 PM    758480     81196      9.67      5132      6.32
12:59:00 PM    754224     85452     10.18      7056      8.26
12:59:10 PM    136636    703040     83.73    185144     26.33
12:59:20 PM         0    839676    100.00     11276      1.34
12:59:30 PM    183292    656384     78.17     14876      2.27
12:59:40 PM    290816    548860     65.37     10368      1.89
12:59:50 PM    290820    548856     65.37     10396      1.89
01:00:00 PM    290820    548856     65.37     10396      1.89
01:00:10 PM    291072    548604     65.34     10728      1.96
01:00:20 PM    291072    548604     65.34     10728      1.96
01:00:30 PM    291072    548604     65.34     10728      1.96
01:00:40 PM    291072    548604     65.34     10728      1.96
01:00:50 PM    291072    548604     65.34     10728      1.96
01:01:00 PM    291072    548604     65.34     10728      1.96
01:01:10 PM    291180    548496     65.32     10904      1.99
01:01:20 PM    291180    548496     65.32     10904      1.99
01:01:30 PM    291180    548496     65.32     10904      1.99
01:01:40 PM    291180    548496     65.32     10904      1.99
01:01:50 PM    291180    548496     65.32     10904      1.99
01:02:00 PM    291216    548460     65.32     11016      2.01
 */
