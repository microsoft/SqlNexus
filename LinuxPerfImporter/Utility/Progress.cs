/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;

namespace LinuxPerfImporter.Utility
{
    public static class ProgressConfig
    {
        public static int progressLine = 0;
        public static bool ClearScreen = true;
    }
    class Progress
    {
        public Progress()
        {
            if (ProgressConfig.ClearScreen)
            {
                Console.Clear();
                ProgressConfig.ClearScreen = false;
            }
        }
        public void WriteTitle(string title, int progressLine)
        {
            Console.SetCursorPosition(1, progressLine);
            Console.WriteLine(title);
        }
        public void WriteProgress(int value, int total, int progressLine)
        {
            double percent = (Convert.ToDouble(value) / Convert.ToDouble(total) * 100);
            Console.SetCursorPosition(1, progressLine + 2);
            Console.Write(String.Format("{0:F2}", percent) + "%");
        }
    }
}