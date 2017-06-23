using System.ServiceProcess;
using System.Threading;

namespace SyncXml
{
    public partial class Service :
        ServiceBase
    {
        private readonly SynchronizationFile _synchronizationFile;
        private readonly Thread _thread;

        public Service(SynchronizationFile synchronizationFile)
        {
            _synchronizationFile = synchronizationFile;
            _thread = new Thread(synchronizationFile.Synchronize);

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _thread.Start();
        }

        protected override void OnStop()
        {
            _synchronizationFile.Stop();

            _thread.Interrupt();
        }
    }
}
