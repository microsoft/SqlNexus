/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;

namespace LinuxPerfImporter.Model
{
    class LinuxOutFileNetwork : LinuxOutFile
    {
        // class constructor
        public LinuxOutFileNetwork(string mpStatFileName) :
            base(mpStatFileName)
        {
            Devices = GetNetDevices();
            Header = GetNetHeader();
            Metrics = GetNetMetrics();
        }

        // class properties
        private List<string> Devices { get; set; }

        // class methods
        private List<string> GetNetDevices()
        {
            int startingLine = 3;
            int deviceColumnNumber = 2;

            return new LinuxOutFileHelper().GetDevices(startingLine, FileContents, deviceColumnNumber);
        }

        private string GetNetHeader()
        {
            OutHeader outHeader = new OutHeader()
            {
                StartingColumn = 3,
                StartingRow = 2,
                FileContents = FileContents,
                Devices = Devices,
                ObjectName = "Network Interface"
            };

            return new LinuxOutFileHelper().GetHeader(outHeader);
        }

        private List<string> GetNetMetrics()
        {
            return new LinuxOutFileHelper().GetMetrics(Devices, FileContents);
        }
    }

}

/* EXAMPLE of Network file
Linux 3.10.0-327.28.3.el7.x86_64 (dl380g802-v02) 	01/11/2017 	_x86_64_	(8 CPU)

12:58:00 PM     IFACE   rxpck/s   txpck/s    rxkB/s    txkB/s   rxcmp/s   txcmp/s  rxmcst/s
12:58:01 PM      eth0      1.00      0.00      0.16      0.00      0.00      0.00      0.00
12:58:01 PM        lo      0.00      0.00      0.00      0.00      0.00      0.00      0.00

12:58:01 PM     IFACE   rxpck/s   txpck/s    rxkB/s    txkB/s   rxcmp/s   txcmp/s  rxmcst/s
12:58:02 PM      eth0      2.00      0.00      0.29      0.00      0.00      0.00      0.00
12:58:02 PM        lo      0.00      0.00      0.00      0.00      0.00      0.00      0.00

12:58:02 PM     IFACE   rxpck/s   txpck/s    rxkB/s    txkB/s   rxcmp/s   txcmp/s  rxmcst/s
12:58:03 PM      eth0      4.00      1.00      0.33      0.06      0.00      0.00      0.00
12:58:03 PM        lo      0.00      0.00      0.00      0.00      0.00      0.00      0.00
 */
