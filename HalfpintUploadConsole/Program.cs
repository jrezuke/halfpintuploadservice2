using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Web;

namespace HalfpintUploadConsole
{
    class Program
    {
        private static EventLog _logger;
        
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Halfpint Upload Console");
            
            string computerName = Environment.MachineName;
            Console.WriteLine("MachineName: {0}", computerName);
            
            string arg = string.Empty;
            if (args.Length > 0)
            {
                arg = args[0];
                Console.WriteLine("Running with argument:" + arg);
            }

            //trap unhandled errors
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += MyHandler;

            //set up the event logging
            if (!EventLog.SourceExists("HalfpintUploadConsole"))
            {
                EventLog.CreateEventSource(
                        "HalfpintUploadConsole", "Application");
            }
            _logger = new EventLog("Application") { Source = "HalfpintUploadConsole" };

            //todo - this could be used to store the last date of the upload
            //string localDataPath = Path.Combine(System.Environment.SpecialFolder.LocalApplicationData.ToString(), "Halfpint");
            //if (!Directory.Exists(localDataPath))
            //    Directory.CreateDirectory(localDataPath);

            //var di = new DirectoryInfo(localDataPath);

            Console.WriteLine("checks upload");
            string siteCode = DoChecksUploads();
            //string siteCode = "01";

            if (!string.IsNullOrEmpty(siteCode))
            {
                if (arg == "novanet")
                {
                    Console.WriteLine("novanet upload");
                    DoNovanetUploads(siteCode, computerName);
                }
            }
            //Console.Read();
        }

        private static void DoNovanetUploads(string siteCode, string computerName)
        {
            Console.WriteLine("Starting novanet upload console");
            _logger.WriteEntry("Starting novanet upload console", EventLogEntryType.Information);

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

        private static void UploadNovaNetFile(string fullName, string siteCode, string computerName, string fileName)
        {
            Console.WriteLine("Upload NovaNet File: " + fileName);
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
                Console.WriteLine("Result: " + result.StatusCode);
            }
        }

        private static string DoChecksUploads()
        {
            string siteCode = string.Empty;
            _logger.WriteEntry("Starting checks upload console", EventLogEntryType.Information);

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
                    try
                    {
                        if (!File.Exists(Path.Combine(archiveFolder, fi.Name)))
                        {
                            fi.MoveTo(Path.Combine(archiveFolder, fi.Name));
                            _logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteEntry("***Error moving file name:" +fi.Name + " - error message:"  + ex.Message, EventLogEntryType.Error);
                    }
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

                    if (string.IsNullOrEmpty(siteCode))
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


                    key = key * iInstitId;
                    var rnd = new Random();
                    int iRnd = rnd.Next(100000, 999999);
                    string sKey = iRnd.ToString() + key.ToString();

                    UploadChecksFile(fi.FullName, siteCode, sKey, fi.Name);
                }

            }
            return siteCode;
        }

        private static void UploadChecksFile(string fullName, string siteCode, string key, string fileName)
        {
            Console.WriteLine("UploadChecksFile: " + fileName);
            _logger.WriteEntry("UploadChecksFile: " + fileName, EventLogEntryType.Information);
            
            var qsCollection = HttpUtility.ParseQueryString(string.Empty);
            qsCollection["siteCode"] = siteCode;
            qsCollection["key"] = key;
            qsCollection["fileName"] = fileName;
            var queryString = qsCollection.ToString();
            var client = new HttpClient();
            using (var content = new MultipartFormDataContent())
            {
                var filestream = File.Open(fullName, FileMode.Open);
                content.Add(new StreamContent(filestream), "file", fileName);

                var requestUri = "https://halfpintstudy.org/hpUpload/api/upload?" + queryString; 
                //var requestUri = "http://asus1/hpuploadapi/api/upload?" + queryString;
                //var requestUri = "http://joelaptop4/hpuploadapi/api/upload?" + queryString;
                var result = client.PostAsync(requestUri, content).Result;
                Console.WriteLine("Result: " + result.StatusCode);
            }
        }

        private static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;
            Console.WriteLine("Exception: " + e.Message);
            _logger.WriteEntry("Exception: " + e.Message, EventLogEntryType.Error);
            //Console.Read();
            Environment.Exit(10);
        }
    }
}
