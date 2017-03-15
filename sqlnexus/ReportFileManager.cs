using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Diagnostics;
public class ReportFileManager
{
    SortedDictionary<string, ReportFile> _ReportListByFileName =new  SortedDictionary<string, ReportFile>();
    SortedDictionary<string, ReportFile> _ReportListByDisplayName = new SortedDictionary<string, ReportFile>();

    public static bool NeedToSupplyParameter(string reportname)
    {
        if (reportname.ToLower().IndexOf("blocking and wait statistics_c") != -1 ||
            reportname.ToLower().IndexOf("bottleneck analysis_c") != -1
            )
            return true;
        else
            return false;
    }
    public ReportFileManager(string controlFile)
    {
        XPathDocument doc = new XPathDocument(controlFile);
        XPathNavigator nav =         doc.CreateNavigator();
        XPathNodeIterator iter = nav.Select("/reports/reportfile");
        while (iter.MoveNext())
        {
            string filename = iter.Current.GetAttribute("name", "");
            string displayname = iter.Current.GetAttribute("displayname", "");
            bool ismainreport = bool.Parse (iter.Current.GetAttribute("mainreport", ""));
            int seqno = int.Parse(iter.Current.GetAttribute("seqno", ""));
            ReportFile rptFile = new ReportFile(filename, displayname, ismainreport, seqno);
            _ReportListByDisplayName.Add(rptFile.ReportDisplayName.ToUpper(), rptFile);
            _ReportListByFileName.Add(rptFile.ReportFileName.ToUpper(), rptFile);

            
        }
    }

    public string GetFileNameByDisplayName (string displayname)  //for a given display name return report file name
    {
        if (string.IsNullOrEmpty(displayname) || !_ReportListByFileName.ContainsKey(displayname.ToUpper()))
                return null;
            else
            return _ReportListByFileName[displayname.ToUpper()].ReportFileName;
    }
    public string GetDisplayNameByFileName(string filename)
    {
        if (string.IsNullOrEmpty(filename) || !_ReportListByDisplayName.ContainsKey(filename.ToUpper()))
            return null;
        else
            return _ReportListByDisplayName[filename.ToUpper()].ReportFileName;


    }
    public List<ReportFile> MainReportList
    {
        get
        {
            List<ReportFile> reportlist = new List<ReportFile>();
            SortedList<int, ReportFile> sorted = new SortedList<int,ReportFile>();
            foreach (string key in _ReportListByFileName.Keys)
            {
                sorted.Add(_ReportListByFileName[key].SequenceNumber, _ReportListByFileName[key]);
            }
            foreach (int key in sorted.Keys)
            {
                reportlist.Add(sorted[key]);
            }
            return reportlist;
        }
    }
   


 }

public class ReportFile
{
    string _ReportFileName;
    string _ReportDisplayName;
    bool _IsMainReport;
    int _SequenceNumber;
    public ReportFile(string rFileName, string rDisplayName, bool mainreport, int seqno)
    {
        _ReportFileName = rFileName;
        _ReportDisplayName = rDisplayName;
        _IsMainReport = mainreport;
        _SequenceNumber = seqno;
    }
    public int SequenceNumber
    {
        get {return _SequenceNumber;}
    }

    public string ReportFileName
    {
        get { return _ReportFileName; }
    }
    public string ReportDisplayName
    {
        get { return _ReportDisplayName; }
    }
    public bool IsMainReport
    {
        get { return _IsMainReport; }
    }


}


