using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NexusInterfaces;
using RowsetImportEngine;
using sqlnexus;
using System.Xml;

namespace RowsetEditor
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Hashtable KnownRowsets = new Hashtable();

            INexusImporter fImp = new TextRowsetImporter();
            int count = 0;
            XmlDocument xDoc = new XmlDocument();
            String TextRowsetXMLFile = "..\\..\\..\\sqlnexus\\TextRowsets.xml";
            xDoc.Load(TextRowsetXMLFile);
            XmlNode nodeKnownRowsets = xDoc.DocumentElement.ChildNodes.OfType<XmlNode>().Where(x => x.Name == "KnownRowsets").First();
            foreach (XmlNode locNode in nodeKnownRowsets.OfType<XmlNode>().Where(x => x.Name.Equals("Rowset")) )
            {
                String att = ((XmlElement)locNode).GetAttribute("name");
                try
                {
                    KnownRowsets.Add(att, locNode);

                } catch (Exception ex) 
                {
                    String msg = ex.Message;
                    count += 1;
                    att += "_DUP" + count.ToString("0000");
                    KnownRowsets.Add(att, locNode);
                }
                
            }
            
            fmRowsetEditor fm = new fmRowsetEditor();
            fm.setKnownRowsets(KnownRowsets, ref xDoc, TextRowsetXMLFile);

            fm.ShowDialog();

        }
    }
}
