using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;

namespace HalfpintUploadService
{
    public partial class HalfpintUploadService : ServiceBase
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
            _logger = new EventLog("Application") {Source = "Halfpint"};
        }

        protected override void OnStart(string[] args)
        {
            _logger.WriteEntry("OnStart", EventLogEntryType.Information);
            _timer = new Timer {Interval = 30000, Enabled = true, AutoReset = true};
            _timer.Start();
            _timer.Elapsed += _timer_Elapsed;
            _logger.WriteEntry("Timer started", EventLogEntryType.Information);
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger.WriteEntry("Timer event", EventLogEntryType.Information);

            //create the archive directory if it doesn't exits
            if (!Directory.Exists(@"C:\HalfPintArchive"))
            {
                Directory.CreateDirectory(@"C:\HalfPintArchive");
                _logger.WriteEntry("Created the HalfPintArchive folder", EventLogEntryType.Information);
            }

        }

        protected override void OnStop()
        {
        }
    }
}
