using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.ServiceProcess;
using System.Timers;
using System.Web;

namespace HalfpintUploadService2
{
    partial class HalfpintUploadService : ServiceBase
    {
        private Timer _timer;
        private readonly EventLog _logger;
        
        public HalfpintUploadService()
        {
            InitializeComponent();
            if (!EventLog.SourceExists("Halfpint"))
            {
                EventLog.CreateEventSource(
                        "Halfpint", "Application");
            }
            _logger = new EventLog("Application") { Source = "Halfpint" };
        }

        protected override void OnStart(string[] args)
        {
            _logger.WriteEntry("OnStart", EventLogEntryType.Information);
            _timer = new Timer { Interval = 3600000, Enabled = true, AutoReset = true };
            _timer.Start();
            _timer.Elapsed += TimerElapsed;
            _logger.WriteEntry("Timer started", EventLogEntryType.Information);
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            _logger.WriteEntry("Timer event", EventLogEntryType.Information);

            //create the archive directory if it doesn't exits
            if (!Directory.Exists(@"C:\HalfPintArchive"))
            {
                Directory.CreateDirectory(@"C:\HalfPintArchive");
                _logger.WriteEntry("Created the HalfPintArchive folder", EventLogEntryType.Information);
            }

            //archive old files (files with last modified older than 7 days)
            //CHECKS files
            var di = new DirectoryInfo(@"C:\Halfpint");
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                _logger.WriteEntry("The Halfpint folder does not exist", EventLogEntryType.Information);
                return;
            }

            FileInfo[] fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(@"C:\HalfPintArchive\" + fi.Name);
                    _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }
            }

            //get the files from the copy directory
            di = new DirectoryInfo(@"C:\Halfpint\Copy");
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                _logger.WriteEntry("The Halfpint\\Copy folder does not exist", EventLogEntryType.Information);
                return;
            }

            //arcive them first
            fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(@"C:\HalfPintArchive\" + fi.Name);
                    _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }

            }

            //now upload
            fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(@"C:\HalfPintArchive\" + fi.Name);
                    _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }

                if (fi.Name.IndexOf("copy") > -1 || fi.Name.IndexOf("Chart") > -1)
                {
                    //skip test files
                    if (fi.Name.StartsWith("T"))
                        continue;

                    string siteCode = fi.Name.Substring(0, 2);

                    //formulate key
                    //add all the numbers in the file name
                    int key = 0;
                    foreach (var c in fi.Name)
                    {
                        if (char.IsNumber(c))
                            key += int.Parse(c.ToString());
                    }

                    key *= key;

                    int iInstitId = int.Parse(siteCode);


                    key = key * iInstitId;
                    var rnd = new Random();
                    int iRnd = rnd.Next(100000, 999999);
                    string sKey = iRnd.ToString() + key.ToString();

                    UploadFile("https://halfpintstudy.org/hpProd/FileManager/ChecksUpload",
                         fi.FullName, siteCode, sKey, fi.Name);
                }

            }
        }

        private void UploadFile(string url, string fullName, string siteCode, string key, string fileName)
        {
            _logger.WriteEntry("UploadFile: " + fileName, EventLogEntryType.Information);
            var rm = new HttpRequestMessage();

            var qsCollection = HttpUtility.ParseQueryString(string.Empty);
            qsCollection["siteCode"] = siteCode;
            qsCollection["key"] = key;
            qsCollection["fileName"] = fileName;
            var queryString = qsCollection.ToString();
            using (var client = new HttpClient())
            using (var content = new MultipartFormDataContent())
            {
                var filestream = File.Open(fullName, FileMode.Open);
                content.Add(new StreamContent(filestream), "file", fileName);

                var requestUri = "https://halfpintstudy.org/hpUpload/api/upload?" + queryString; 
                //var requestUri = "http://asus1/hpuploadapi/api/upload?" + queryString;
                //var requestUri = "http://joelaptop4/hpuploadapi/api/upload?" + queryString;
                var result = client.PostAsync(requestUri, content).Result;
            }
        }

        protected override void OnStop()
        {
            _logger.WriteEntry("OnStop", EventLogEntryType.Information);
        }
    }
}
