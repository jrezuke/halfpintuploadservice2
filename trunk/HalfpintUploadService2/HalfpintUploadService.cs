using System;
using System.Configuration;
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
        private EventLog _logger = null;
        
        public HalfpintUploadService()
        {
            InitializeComponent();
            if (!EventLog.SourceExists("HalfpintUploadService"))
            {
                EventLog.CreateEventSource(
                        "HalfpintUploadService", "Application");
            }
            _logger = new EventLog("Application");
            _logger.Source = "HalfpintUploadService";
        }

        protected override void OnStart(string[] args)
        {
            _logger.WriteEntry("OnStart", EventLogEntryType.Information);
            _timer = new Timer { Interval = 3600000, Enabled = true, AutoReset = true }; //3600000 1hour
            _timer.Start();
            _timer.Elapsed += TimerElapsed;
            _logger.WriteEntry("Timer started", EventLogEntryType.Information);
            StartAction();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            _logger.WriteEntry("Timer event", EventLogEntryType.Information);

            StartAction();

        }

        private void StartAction()
        {
            string computerName = Environment.MachineName;
            string siteCode = DoChecksUploads();

            if (DateTime.Now.Hour == 1)
            {
                if (!string.IsNullOrEmpty(siteCode))
                {
                    DoNovanetUploads(siteCode, computerName);
                }
            }
        }

        private void DoNovanetUploads(string siteCode, string computerName)
        {
            _logger.WriteEntry("Starting novanet upload service", EventLogEntryType.Information);

            //check if folder exists
            string novanetFolder = ConfigurationManager.AppSettings["NovaNetArchives"];
            if (!Directory.Exists(novanetFolder))
            {
                _logger.WriteEntry("NovaNet archives folder does not exist!", EventLogEntryType.Warning);
                return;
            }

            var di = new DirectoryInfo(novanetFolder);
            FileInfo[] fis = di.GetFiles();
            foreach (var fi in fis)
            {
                UploadNovaNetFile(fi.FullName, siteCode, computerName, fi.Name);
            }
        }

        private  void UploadNovaNetFile(string fullName, string siteCode, string computerName, string fileName)
        {
            _logger.WriteEntry("Upload NovaNet File: " + fileName, EventLogEntryType.Information);

            var qsCollection = HttpUtility.ParseQueryString(string.Empty);
            qsCollection["siteCode"] = siteCode;
            qsCollection["computerName"] = computerName;
            qsCollection["fileName"] = fileName;
            var queryString = qsCollection.ToString();

            var client = new HttpClient();
            using (var content = new MultipartFormDataContent())
            {
                var filestream = File.Open(fullName, FileMode.Open);
                content.Add(new StreamContent(filestream), "file", fileName);

                var requestUri = "https://halfpintstudy.org/hpUpload/api/NovanetUpload?" + queryString;
                //var requestUri = "http://asus1/hpuploadapi/api/NovanetUpload?" + queryString;
                //var requestUri = "http://joelaptop4/hpuploadapi/api/NovanetUpload?" + queryString;
                var result = client.PostAsync(requestUri, content).Result;
                
            }
        }

        private string DoChecksUploads()
        {
            string siteCode = string.Empty;
            _logger.WriteEntry("Starting checks upload service" , EventLogEntryType.Information);

            //create the archive directory if it doesn't exits
            string archiveFolder = ConfigurationManager.AppSettings["ChecksArchivePath"];
            if (!Directory.Exists(archiveFolder))
            {
                Directory.CreateDirectory(archiveFolder);
                _logger.WriteEntry("Created the HalfPintArchive folder", EventLogEntryType.Information);
            }

            //archive old files (files with last modified older than 7 days)
            //CHECKS files
            string checksFolder = ConfigurationManager.AppSettings["ChecksPath"];
            var di = new DirectoryInfo(checksFolder);
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                _logger.WriteEntry("The Halfpint folder does not exist", EventLogEntryType.Information);
                return siteCode;
            }

            FileInfo[] fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(Path.Combine(archiveFolder, fi.Name));
                    _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }
            }

            //get the files from the copy directory
            string checksCopyFolder = Path.Combine(checksFolder, "Copy");
            di = new DirectoryInfo(checksCopyFolder);
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                _logger.WriteEntry("The Halfpint\\Copy folder does not exist", EventLogEntryType.Information);
                return siteCode;
            }

            //arcive them first
            fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(Path.Combine(archiveFolder, fi.Name));
                    _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }

            }

            //now upload
            fis = di.GetFiles();
            foreach (var fi in fis)
            {
                if (fi.Name.IndexOf("copy") > -1 || fi.Name.IndexOf("Chart") > -1)
                {
                    //skip test files
                    if (fi.Name.StartsWith("T"))
                        continue;

                    siteCode = fi.Name.Substring(0, 2);

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


                    key = key*iInstitId;
                    var rnd = new Random();
                    int iRnd = rnd.Next(100000, 999999);
                    string sKey = iRnd.ToString() + key.ToString();

                    UploadFile(fi.FullName, siteCode, sKey, fi.Name);
                }

            }
            return siteCode;
        }

        private void UploadFile(string fullName, string siteCode, string key, string fileName)
        {
            _logger.WriteEntry("UploadFile: " + fileName, EventLogEntryType.Information);
            
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
