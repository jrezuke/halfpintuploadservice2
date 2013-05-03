using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;


namespace hpUploadServiceConsole
{
    class Program
    {
        private static EventLog Logger = null;
        
        static void Main(string[] args)
        {
            if (!EventLog.SourceExists("Halfpint"))
            {
                EventLog.CreateEventSource(
                        "Halfpint", "Application");
            }
            Logger = new EventLog("Application") {Source = "Halfpint"};

            Logger.WriteEntry("Starting halfpint upload service", EventLogEntryType.Information);

            //create the archive directory if it doesn't exits
            if (!Directory.Exists(@"C:\HalfPintArchive"))
            {
                Directory.CreateDirectory(@"C:\HalfPintArchive");
                Logger.WriteEntry("Created the HalfPintArchive folder", EventLogEntryType.Information);
            }
            
            //archive old files (files with last modified older than 7 days)
            //Halfpint subject files
            var di = new DirectoryInfo(@"C:\Halfpint");
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                Logger.WriteEntry("The Halfpint folder does not exist", EventLogEntryType.Information);
                return;
            }

            FileInfo[] fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(@"C:\HalfPintArchive\" + fi.Name);
                    Logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }
            }

            //get the files from the copy directory
            di = new DirectoryInfo(@"C:\Halfpint\Copy");
            if (!di.Exists)
            {
                //this should get created by the CHECKS application
                //if it doesn't exist then there is no work to do so just exit
                Logger.WriteEntry("The Halfpint\\Copy folder does not exist", EventLogEntryType.Information);
                return;
            }


            fis = di.GetFiles();
            foreach (var fi in fis)
            {
                //if the file is older than 1 week then archive
                if (fi.LastWriteTime.CompareTo(DateTime.Today.AddDays(-7)) < 0)
                {
                    fi.MoveTo(@"C:\HalfPintArchive\" + fi.Name);
                    Logger.WriteEntry("Archived file: " + fi.Name, EventLogEntryType.Information);
                }

                if (fi.Name.IndexOf("copy") > -1)
                {
                    string institId = fi.Name.Substring(0, 2);

                    //formulate key
                    //add all the numbers in the file name

                    int key = 0;
                    foreach (var c in fi.Name)
                    {
                        if (char.IsNumber(c))
                            key += int.Parse(c.ToString());
                    }

                    key *= key;

                    int iInstitId = int.Parse(institId);


                    key = key * iInstitId;
                    var rnd = new Random();
                    int iRnd = rnd.Next(100000, 999999);

                    string sKey = iRnd.ToString() + key.ToString();

                    var nvc = new NameValueCollection();
                    nvc.Add("institID", institId);
                    nvc.Add("key", sKey);

                    UploadFile("https://halfpintstudy.org/hpProd/FileManager/ChecksUpload",
                         fi.FullName, "file", "application/msexcel", nvc);


                }

            }
            

        }

        private static void UploadFile(string url, string fullName, string paramName, string contentType, NameValueCollection nvc)
        {
            HttpRequestMessage rm = new HttpRequestMessage();
            
            using (var client = new HttpClient())
            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(new byte[100]);
                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "myFilename.txt"
                };

                var formData = new FormUrlEncodedContent(new[]
                                            {
                                                new KeyValuePair<string, string>("name", "ali"),
                                                new KeyValuePair<string, string>("title", "ostad")
                                            });


                //MultipartContent content = new MultipartContent();
                content.Add(formData);
                content.Add(fileContent);
                //var values = new[]
                //{
                //    new KeyValuePair<string, string>("Foo", "Bar"),
                //    new KeyValuePair<string, string>("More", "Less"),
                //};
                //foreach (var keyValuePair in values)
                //{
                //    content.Add(new StringContent(keyValuePair.Value), keyValuePair.Key);
                //}

                //var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(fullName));
                //fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                //{
                //    FileName = "Foo.txt"
                //};
                //content.Add(fileContent);

                //var requestUri = "https://halfpintstudy.org/hpUpload/api/upload";
                var requestUri = "http://localhost:1736/api/upload";
                var result = client.PostAsync(requestUri, content).Result;
            }
        }
    }
}
