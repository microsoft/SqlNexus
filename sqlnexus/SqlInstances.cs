using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace sqlnexus
{
    class SqlInstances
    {
        private Dictionary<string, string> m_AllInstances = new Dictionary<string, string>();
        private Dictionary<string, string> m_BlockedInstances = new Dictionary<string, string>();
        public string SQLVersion { get; set; }
        private String m_SelectedInstance;
        private string m_FileDir;
        public SqlInstances(String FileDir)
        {
            String logFile = FileDir + @"\##pssdiag.log";
            m_FileDir = FileDir;
            if (!File.Exists(logFile))
            {
                logFile = FileDir + @"\internal\##sqldiag.log";
            }

            Regex re = new Regex(@"Instance: (?<InstanceName>\S+)\s*");

            if (File.Exists(logFile))
            {
                StreamReader sr = File.OpenText(logFile);
                string pssdiagLog = sr.ReadToEnd();


                //substring is used to fix a bug where it grabs things like
                //2009/01/29 17:59:54.22 Error opening instance key . Instance: myinstance. Function result: 2. Message: The system cannot find the file specified. 


                if (pssdiagLog.ToUpper().IndexOf(@"INITIALIZATION STARTING") >= 1)  //some pssdiag may not even be initialized fully. so you don't see this message
                {

                    MatchCollection mc = re.Matches(pssdiagLog.Substring(0, pssdiagLog.ToUpper().IndexOf(@"INITIALIZATION STARTING")));

                    foreach (Match m in mc)
                    {
                        String InstanceName = m.Result("${InstanceName}");

                        String DisplayInstanceName;
                        String Pattern;

                        if (String.Compare(InstanceName, "(Default)", true) == 0)
                        {
                            DisplayInstanceName = "Default";
                            Pattern = "__";


                        }
                        else
                        {
                            DisplayInstanceName = InstanceName;
                            Pattern = "_" + InstanceName.Trim() + "_";

                        }

                        if (!m_AllInstances.ContainsKey(DisplayInstanceName))
                            m_AllInstances.Add(DisplayInstanceName, Pattern);

                    }// foreach
                }

            } //if 


        }
        public Int32 Count { get { return m_AllInstances.Count; } }
        public String InstanceToImport
        {
            get { return m_SelectedInstance; }
            set
            {
                if (!m_AllInstances.ContainsKey(value))
                    throw new ArgumentException("The instance " + value + " you specified doesn't exist. try again");
                m_SelectedInstance = value;
                m_BlockedInstances.Clear();
                foreach (String k in m_AllInstances.Keys)
                {
                    if (k.ToUpper() != m_SelectedInstance.ToUpper())
                        m_BlockedInstances.Add(k, m_AllInstances[k]);

                }
            }
        }

        public string SelectedTraceFileMask
        {
            get
            {
                if (Count <= 1)
                    return "*.trc";
                else
                    return "*" + SelectedInstanceMask + "*.trc";
            }
        }
        public string SelectedXEventFileMask
        {
            get
            {
                if (Count <= 1)
                    return "*pssdiag*.xel";
                else
                    return "*" + SelectedInstanceMask + "*pssdiag*.xel";
            }
        }
        private string SelectedInstanceMask
        {
            get
            {
                if (Count <= 1)
                    return "";
                return m_AllInstances[InstanceToImport];
            }
        }
        public bool Block(String fileName)
        {
            if (m_AllInstances.Count <= 1)
                return false;

            foreach (String key in m_BlockedInstances.Keys)
            {
                if (fileName.ToUpper().IndexOf(m_BlockedInstances[key].ToUpper()) >= 0)
                    return true;
            }
            return false;


        }
        public List<string> InstanceList()
        {
            List<string> InstanceListThatHasData = new List<string>();
            List<string> InstanceListThatHasNoData = new List<string>();
            foreach (String key in m_AllInstances.Keys)
            {
                
                string pattern =   "*" + m_AllInstances[key] + "*Perf*Stats*";
                
                if (Directory.GetFiles(m_FileDir, pattern).Length > 0)
                    InstanceListThatHasData.Add(key);
                else
                    InstanceListThatHasNoData.Add(key);
                
            }

            InstanceListThatHasData.Sort();
            InstanceListThatHasNoData.Sort();

            InstanceListThatHasData.AddRange (InstanceListThatHasNoData);
            return InstanceListThatHasData;
        }

    }
}
