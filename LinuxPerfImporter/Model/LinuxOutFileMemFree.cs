/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;

namespace LinuxPerfImporter.Model
{
    class LinuxOutFileMemFree : LinuxOutFile
    {
        // class constructor
        public LinuxOutFileMemFree(string ioStatFileName) :
            base(ioStatFileName)
        {
            Header = GetMemFreeHeader();
            Metrics = GetMemFreeMetrics();
        }
        // class methods
        private string GetMemFreeHeader()
        {
            // creating the outheader object and passing in variables on where to start parsing specific strings
            OutHeader outHeader = new OutHeader()
            {
                StartingColumn = 2,
                StartingRow = 2,
                FileContents = FileContents,
                ObjectName = "Memory Free"
            };

            return new LinuxOutFileHelper().GetHeader(outHeader);
        }

        // the get memory metrics method is in with the common methods sine we have to call this more than once.
        private List<string> GetMemFreeMetrics()
        {
            // int progressLine = 5;
            return new LinuxOutFileHelper().GetMemoryMetrics(FileContents);
        }

    }
}

/* EXAMPLE of MemFree file
Linux 3.10.0-327.28.3.el7.x86_64 (dl380g802-v02) 	01/11/2017 	_x86_64_	(8 CPU)

12:58:00 PM kbmemfree kbmemused  %memused kbbuffers  kbcached  kbcommit   %commit  kbactive   kbinact   kbdirty
12:58:10 PM   8105716   8155044     50.15    178288   1243044   9375936     54.83   6337552   1203136      1688
12:58:20 PM   8105608   8155152     50.15    178288   1243056   9375864     54.83   6337688   1203112       100
12:58:30 PM   7961136   8299624     51.04    178304   1243068   9379420     54.85   6478332   1203128       136
12:58:40 PM   5739620  10521140     64.70    178304   1243088  11782280     68.90   8696632   1203128        72
12:58:50 PM   2902104  13358656     82.15    178328   1243108  15499868     90.64  11529556   1203152       128
12:59:00 PM   1240400  15020360     92.37    148608   1099664  16773688     98.09  12828312   1563924        32
12:59:10 PM    131676  16129084     99.19      6124    269140  19978600    116.83  13690352   1824840         4
12:59:20 PM    131848  16128912     99.19      1948     46292  20004200    116.98  14656092   1057576        52
12:59:30 PM   2410952  13849808     85.17      1336     49096  20004720    116.98  11944580   1496040         0
12:59:40 PM  11321528   4939232     30.38      2756    710368   7126840     41.68   3947960    593104       696
12:59:50 PM  11317544   4943216     30.40      2772    712604   7135148     41.72   3954440    591424       776
01:00:00 PM  11126756   5134004     31.57      4208    712536   7139324     41.75   4134008    592708       108
01:00:10 PM  10764160   5496600     33.80      9268    719952   8195160     47.92   4451736    603984       176
01:00:20 PM  10405280   5855480     36.01     12760    720012   8202932     47.97   4779464    607548        52
01:00:30 PM  10048840   6211920     38.20     16448    719952   8210476     48.01   5111256    611132       100
01:00:40 PM   9848212   6412548     39.44     18616    721768   9263380     54.17   5293988    615116       120
01:00:50 PM   9845900   6414860     39.45     18616    721792   9267460     54.19   5296100    615116       132
01:01:00 PM   9845776   6414984     39.45     18616    721808   9267460     54.19   5296136    615120        44
01:01:10 PM   9842520   6418240     39.47     18616    722760   9267640     54.20   5297128    615344       100
01:01:20 PM   9842568   6418192     39.47     18616    722784   9251032     54.10   5296768    615348       124
01:01:30 PM   9842568   6418192     39.47     18616    722808   9251032     54.10   5296784    615352       128
01:01:40 PM   9840048   6420712     39.49     18616    723068   9242764     54.05   5298880    615592        72
01:01:50 PM   9839908   6420852     39.49     18624    723088   9244804     54.06   5298984    615604       124
01:02:00 PM   9832760   6428000     39.53     18624    728932   9246572     54.07   5299556    621280       120
 */
