using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
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
        private FileSystemChangesWatcher _fileSystemWatcher;

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
            EventLog.WriteEntry($"SyncXml.{Name}", $"Synchronizing \"{Source}\" and \"{Destiny}\"", EventLogEntryType.Information);

            try
            {
                if (Sync(Destiny, Source))
                {
                    Execute();
                }
            }

            catch (Exception ex)
            {
                EventLog.WriteEntry($"SyncXml.{Name}", ex.Message, EventLogEntryType.Error);
            }

            _fileSystemWatcher = new FileSystemChangesWatcher(Path.GetDirectoryName(Source), Path.GetFileName(Source));

            _fileSystemWatcher.Watch(new FileSystemChangeFile
            {
                FullPath = Source
            });
            _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
        }

        private void FileSystemWatcherOnChanged(object sender, FileSystemChangeEventArgs<FileSystemChangeFile> e)
        {
            try
            {
                if (Sync(Source, Destiny))
                {
                    Execute();
                }
            }

            catch (Exception ex)
            {
                EventLog.WriteEntry($"SyncXml.{Name}", ex.Message, EventLogEntryType.Error);
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

        private bool Sync(string source, string destiny)
        {
            var sourceDocument = XDocument.Load(source);
            var destinyDocument = XDocument.Load(destiny);

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

            using (var stream = File.Create(destiny))
            {
                destinyDocument.Save(stream);
            }

            EventLog.WriteEntry($"SyncXml.{Name}", $"File \"{Destiny}\" was syncronized", EventLogEntryType.Information);

            return false;
        }

        public void Stop()
        {
            _fileSystemWatcher.Dispose();
        }
    }
}
