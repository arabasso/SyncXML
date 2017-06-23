using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;

namespace SyncXml
{
    [RunInstaller(true)]
    public partial class ProjectInstaller :
        Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();

            BeforeInstall += ProjectBeforeInstall;
        }

        private void ProjectBeforeInstall(object sender, InstallEventArgs e)
        {
            Context.Parameters["assemblyPath"] = Context.Parameters["assemblyPath"];
        }
    }
}
