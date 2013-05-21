using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace HalfpintUploadService2
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void serviceInstaller1_Committed(object sender, InstallEventArgs e)
        {
            ServiceController sc = new ServiceController("HalfpintUploadService");
            sc.Start();
        }
        
    }
}
