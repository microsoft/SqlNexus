using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace sqlnexus
{

   
    /// <summary>
    /// used to handle importer options.  a user may want to save options for future usage
    /// </summary>
    public static class ImportOptions
    {
        static OptionDictionary m_options;
        static ImportOptions()
        {
            m_options = OptionDictionary.Parse(Properties.Settings.Default.ImporterOptions);
        }
        private static void Save()
        {
            Properties.Settings.Default.ImporterOptions = m_options.ToString();
            Properties.Settings.Default.Save();
        }
        public static bool IsEnabled(String option)
        {
            return m_options.ContainsKey(option) && m_options[option];
        }
        public static void Set(String option, bool Enable )
        {
            if (m_options.ContainsKey(option))
                m_options[option] = Enable;
            else
                m_options.Add(option, Enable);
            Save();
        }
        public static void Clear()
        {
            m_options.Clear();
            Save();
        }
    }


    public class ReportUtil

    {

            public static string GetReportNameSpace(XmlDocument doc)
            {


                return doc.DocumentElement.GetAttribute("xmlns");

            }
        
    }
    //todo: long term, this should be generalized
    public class OptionDictionary  : Dictionary<String, bool>
    {
        public static OptionDictionary  Parse(String xDocString)
        {
                
            XmlDocument xDoc = new XmlDocument();
            xDoc.LoadXml(xDocString);
            OptionDictionary options = new OptionDictionary();

            XPathNavigator nav = xDoc.CreateNavigator();
            XPathNodeIterator iter = nav.Select("root/item");
            while (iter.MoveNext())
            {
                String key =  iter.Current.GetAttribute("key", "");
                bool value =  bool.Parse( iter.Current.GetAttribute("value", ""));
                if (!options.ContainsKey(key)) //ignoring duplicates
                    options.Add(key, value);
            }
            return options;
        }

        public override string ToString()
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<root>");
            foreach (string key in this.Keys)
            {
                xml.AppendFormat("<item key='{0}' value='{1}'/>", key.ToString(), this[key].ToString());
            }
            xml.Append("</root>");
            return xml.ToString();
        }

    }

}
