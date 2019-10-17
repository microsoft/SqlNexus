using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Reflection;
using System.IO;
namespace sqlnexus
{
    public class FileMgr
    {
        private List<Importer> m_Importers;
        private List<RawFile> m_RawFileList;

        

        public FileMgr()
        {
            m_Importers = new List<Importer>();

            m_RawFileList = new List<RawFile>();
            
            //throw new Exception("FileMgr Path " + Path.GetDirectoryName (Assembly.GetExecutingAssembly().Location));

            String xmlFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\appconfig.xml";
            //Console.WriteLine (xmlFile);
            //throw new Exception("file path: " + xmlFile);
            XPathDocument doc = new XPathDocument(xmlFile);

            XPathNavigator nav = doc.CreateNavigator();
            XPathNodeIterator iter = nav.Select("config/importers/importer");
            while (iter.MoveNext())
            {
                string name = iter.Current.GetAttribute("name", "");
                bool ude = Convert.ToBoolean(iter.Current.GetAttribute("ude", ""));
                Importer imp = new Importer();
                imp.Name = name;
                imp.ude = ude;
                XPathNodeIterator iter2 = iter.Current.Select("files/file");
                
                while (iter2.MoveNext())
                {

                    string filename = iter2.Current.GetAttribute("pattern", "");
                    bool include = Convert.ToBoolean(iter2.Current.GetAttribute("include", ""));

                    imp.FileList.Add(filename, include);

                }
                m_Importers.Add(imp);
                
            }


            XPathNavigator nav2 = doc.CreateNavigator();
            XPathNodeIterator iter3 = nav2.Select("config/importers/rawfile");
            while (iter3.MoveNext())
            {
                string tablename = iter3.Current.GetAttribute("tablename", "");
                string mask = iter3.Current.GetAttribute("mask", "");
                RawFile file = new RawFile(tablename, mask);
                m_RawFileList.Add(file);

            }



        }
        public List<RawFile> RawFileList
        {
            get
            {
                return m_RawFileList;
            }
        }
        public Importer this[string index]
        {
            get
            {
                foreach (Importer imp in m_Importers)
                {
                    if (index.ToUpper() == imp.Name.ToUpper())
                    {
                        return imp;
                    }
                }
                return null;
            }
        }
        public override string ToString()
        {
            string str = "";
            foreach (Importer imp in m_Importers)
            {
                str += imp.ToString() + Environment.NewLine;
            }
            return str;
        }


    }
    public class RawFile
    {
        public string TableName { get; set; }
        public string Mask { get; set; }
        public RawFile(string tablename, string mask)
        {
            TableName = tablename;
            Mask = mask;

        }
    }
    public class Importer
    {
        public string Name { get; set; }
        public bool ude { get; set; }
        public Dictionary<string, bool> FileList;
        public Importer()
        {
            Name = string.Empty;
            ude = false;
            FileList = new Dictionary<string, bool>();
        }
        public bool ExcludeFile(string fileName)
        {
            if (FileList.ContainsKey(fileName) && FileList[fileName] == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public override string ToString()
        {
            string str = "name: " + Name;
            str += "ude: " + ude;
            foreach (string key in FileList.Keys)
            {
                str += "file: " + key + " " + FileList[key];
            }
            return str;

            
        }
    }
}
