using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;
using System.Windows.Forms;


    public class DependencyManager
    {

        public static void CheckReportViewer()  //JOTODO: Should we revive this code?? see https://github.com/microsoft/SqlNexus/issues/93
    {
            
            // not checking on report viewer given that 10.0 or 11 report reviewer controls are common nowadays
            /*
             DependencyManager mgr = new DependencyManager();

            //"Microsoft.ReportViewer.Common", "9.0.30729.1", "B03F5F7F11D50A3A"
            bool AssemblyFound = mgr.AssemblyInstalled("Microsoft.ReportViewer.Common", "10.0.0.0", "B03F5F7F11D50A3A");

            if (!AssemblyFound)
            {
                UserInstruction fm = new UserInstruction(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName) + "\\MissingAssemblyFiles.htm", true);
                fm.ShowDialog();
            }*/


        }

        // Assembly reportViewerAssem= Assembly.Load            ("Microsoft.ReportViewer.Common, Version=9.0.30729.1, Culture=neutral, PublicKeyToken=B03F5F7F11D50A3A");
        public bool AssemblyInstalled(String AssemblyName, String ExactVersion, String Token)
        {
            String[] VersionArray = ExactVersion.Split( new Char[]{'.'});
            if (VersionArray.Length != 4)
            {
                throw new ArgumentException("Exactversion " + ExactVersion + " passed is not in proper format");
            }
            Int32 MajorVersion = Int32.Parse(VersionArray[0]);
            Int32 MinorVersion = Int32.Parse(VersionArray[1]);
            Int32 Build = Int32.Parse(VersionArray[2]);
            Int32 Revision = Int32.Parse(VersionArray[3]);
            bool ExactAssemblyFound = false;

            String ExactAssemblyString = AssemblyName + ", Version=" + ExactVersion + ", Culture=neutral, PublicKeyToken=" + Token;

            String MajorAssemblyString = AssemblyName + ", Version=" + MajorVersion + ".0.0.0" + ", Culture=neutral, PublicKeyToken=" + Token;

            try
            {
                Assembly asem = Assembly.Load(ExactAssemblyString);
                if (asem != null)
                    ExactAssemblyFound = true;
            }
            catch (FileNotFoundException fnf)
            { 
                //no need to do anything
            }

            if (ExactAssemblyFound == true)
                return ExactAssemblyFound;

            try
            {
                Assembly asem = Assembly.Load(MajorAssemblyString);
                if (asem != null)
                {
                    String AssemblyPath = asem.GetModule(AssemblyName + ".dll").FullyQualifiedName;
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(AssemblyPath);
                    String FullVersion = info.ProductVersion;

                    String[] verArray = FullVersion.Split(new Char[] { '.' });
                    if (verArray.Length != 4)
                    { 
                        throw new ArgumentException ("Production version retrieved " + FullVersion + " is not in proper format");
                    }

                        Int32 MajorVersion2 = Int32.Parse(verArray[0]);
                        Int32 MinorVersion2= Int32.Parse(verArray[1]);
                        Int32 Build2 = Int32.Parse(verArray[2]);
                        Int32 Revision2 = Int32.Parse(verArray[3]);

                    if (MajorVersion2 >= MajorVersion && MinorVersion2 >= MinorVersion && Build2 >=Build && Revision2 >= Revision)
                    {
                        ExactAssemblyFound = true;
                    }

                    

                }
            }
            catch (FileNotFoundException fnf)
            { 

            }

            return ExactAssemblyFound;
        }

    }

