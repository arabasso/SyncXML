using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SyncXml
{
    public class SynchronizationConfigSection :
        ConfigurationSection
    {
        [ConfigurationProperty("Files")]
        public FilesCollection Files => ((FilesCollection)base["Files"]);
    }

    [ConfigurationCollection(typeof(SynchronizationFile))]
    public class FilesCollection :
        ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new SynchronizationFile();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SynchronizationFile)element).Name;
        }

        public SynchronizationFile this[int index] => (SynchronizationFile)BaseGet(index);
    }

    public class SynchronizationFile :
        ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get => (string)this["name"];
            set => this["name"] = value;
        }

        [ConfigurationProperty("xpath", IsRequired = true)]
        public string XPath
        {
            get => (string)this["xpath"];
            set => this["xpath"] = value;
        }

        [ConfigurationProperty("source", IsRequired = true)]
        public string Source
        {
            get => (string)this["source"];
            set => this["source"] = value;
        }

        [ConfigurationProperty("destiny", IsRequired = true)]
        public string Destiny
        {
            get => (string)this["destiny"];
            set => this["destiny"] = value;
        }

        [ConfigurationProperty("executable")]
        public string Executable
        {
            get => (string)this["executable"];
            set => this["executable"] = value;
        }

        [ConfigurationProperty("arguments")]
        public string Arguments
        {
            get => (string)this["arguments"];
            set => this["arguments"] = value;
        }

        public void Synchronize()
        {
            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(Source),
                Filter = Path.GetFileName(Source)
            };

            Run(watcher);
        }

        private void Run(FileSystemWatcher watcher)
        {
            while (true)
            {
                try
                {
                    watcher.WaitForChanged(WatcherChangeTypes.Changed);

                    if (Sync())
                    {
                        Execute();
                    }
                }

                catch (Exception ex)
                {
                    EventLog.WriteEntry(Name, ex.Message, EventLogEntryType.Error);
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

        private void Execute()
        {
            if (!File.Exists(Executable)) return;

            var process = new ProcessStartInfo(Executable, Arguments);

            Process.Start(process);
        }

        private bool Sync()
        {
            Thread.Sleep(250);

            for (var i = 0; IsFileLocked(Source) && i < 100; i++)
            {
                Thread.Sleep(100);
            }

            var sourceDocument = XDocument.Load(Source);
            var destinyDocument = XDocument.Load(Destiny);

            var sourceUsersNode = sourceDocument.XPathSelectElement(XPath);
            var destinyUsersNode = destinyDocument.XPathSelectElement(XPath);

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

            using (var stream = File.Create(Destiny))
            {
                destinyDocument.Save(stream);
            }

            return false;
        }
    }
}
