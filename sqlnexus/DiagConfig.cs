using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace sqlnexus
{
    class DiagConfig
    {
        public string SQLVersion {get;set;}
        public DiagConfig(string FileDir)
        {
            String ConfigFileDir = FileDir;
            if (Directory.Exists (FileDir + @"\internal"))
                ConfigFileDir += @"\internal";


            String ConfigFile = ConfigFileDir + @"\##pssdiag.xml";
            

            if (!File.Exists(ConfigFile))
            {
                ConfigFile = ConfigFileDir + @"\##sqldiag.xml";
            }
            if (!File.Exists(ConfigFile))
            {
                SQLVersion = "8";
            }
            else
            {
                XPathDocument doc = new XPathDocument(ConfigFile);
                XPathNavigator nav = doc.CreateNavigator();
                XPathNavigator InstanceNode = nav.SelectSingleNode("//Instance");

                SQLVersion = InstanceNode.GetAttribute("ssver", "");
            }


            


        }
    }
}
