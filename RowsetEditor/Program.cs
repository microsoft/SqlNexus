using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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

            //INexusImporter fImp = new TextRowsetImporter();
            int count = 0;
            XmlDocument xDoc = new XmlDocument();
            String TextRowsetXMLFile = "..\\..\\..\\sqlnexus\\TextRowsets.xml";
            try
            {
                xDoc.Load(TextRowsetXMLFile);
            } catch (IOException ex)
            {
                OpenFileDialog f = new OpenFileDialog();
                f.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                f.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                f.FileName = "TextRowsets.xml";
                if (f.ShowDialog() == DialogResult.OK)
                {
                    TextRowsetXMLFile += f.FileName;
                    xDoc.Load(f.FileName);
                } else
                {
                    MessageBox.Show(ex.Message);
                    
                    return;
                }

            }
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
