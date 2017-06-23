using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SyncXml
{
    public partial class Service :
        ServiceBase
    {
        private readonly Thread _thread;
        private readonly EventLog _eventLog;
        private readonly FileSystemWatcher _watcher;
        private readonly string _source;
        private readonly string _destiny;
        private readonly string _xPath;
        private static string _executable;
        private static string _arguments;

        public Service()
        {
            InitializeComponent();

            _thread = new Thread(Run);

            _eventLog = new EventLog(ServiceName);

            _source = ConfigurationManager.AppSettings["Source"];
            _destiny = ConfigurationManager.AppSettings["Destiny"];
            _xPath = ConfigurationManager.AppSettings["XPath"];

            _executable = ConfigurationManager.AppSettings["Executable"];
            _arguments = ConfigurationManager.AppSettings["Arguments"];

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_source),
                Filter = Path.GetFileName(_source)
            };
        }

        protected override void OnStart(string[] args)
        {
            _thread.Start();
        }

        protected override void OnStop()
        {
            _thread.Abort();
        }

        private void Run()
        {
            while (true)
            {
                try
                {
                    _watcher.WaitForChanged(WatcherChangeTypes.Changed);

                    if (Sync())
                    {
                        Execute();
                    }
                }

                catch (Exception ex)
                {
                    EventLog.WriteEntry(ServiceName, ex.Message, EventLogEntryType.Error);
                }
            }
        }

        protected virtual bool IsFileLocked(string file)
        {
            FileStream stream = null;

            try
            {
                stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }

            catch (IOException)
            {
                return true;
            }

            finally
            {
                stream?.Close();
            }

            return false;
        }

        private static void Execute()
        {
            if (!File.Exists(_executable)) return;

            var process = new ProcessStartInfo(_executable, _arguments);

            Process.Start(process);
        }

        private bool Sync()
        {
            for (var i = 0; IsFileLocked(_source) && i < 10; i++)
            {
                Thread.Sleep(100);
            }

            var sourceDocument = XDocument.Load(_source);
            var destinyDocument = XDocument.Load(_destiny);

            var sourceUsersNode = sourceDocument.XPathSelectElement(_xPath);
            var destinyUsersNode = destinyDocument.XPathSelectElement(_xPath);

            if (XNode.DeepEquals(sourceUsersNode, destinyUsersNode)) return true;

            if (destinyUsersNode == null)
            {
                destinyDocument.Root?.Add(new XElement(sourceUsersNode));
            }

            else if (sourceUsersNode == null)
            {
                destinyUsersNode.Remove();
            }

            else
            {
                destinyUsersNode.ReplaceWith(new XElement(sourceUsersNode));
            }

            using (var stream = File.Create(_destiny))
            {
                destinyDocument.Save(stream);
            }
            return false;
        }

        private bool Running { get; set; } = true;
    }
}
