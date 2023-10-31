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
            foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
            {
                // first node is the url ... have to go to nexted loc node 
                foreach (XmlNode locNode in node)
                {
                    // thereare a couple child nodes here so only take data from node named loc 
                    if (locNode.Name.Equals("Rowset"))
                    {
                        XmlElement e = (XmlElement)locNode;

                        String att = e.GetAttribute("name");
                        
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
                }
                
            }
            
            fmRowsetEditor fm = new fmRowsetEditor();
            fm.setKnownRowsets(KnownRowsets, ref xDoc, TextRowsetXMLFile);

            fm.ShowDialog();

        }
    }
}
