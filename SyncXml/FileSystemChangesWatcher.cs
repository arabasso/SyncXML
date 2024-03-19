using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Caching;

namespace SyncXml
{
    public class FileSystemChangeEventArgs
        : FileSystemChangeEventArgs<FileSystemChangeFile>
    {
    }

    public class FileSystemChangeEventArgs<T> : 
        EventArgs
        where T : FileSystemChangeFile
    {
        public T File { get; set; }
    }

    public class FileSystemChangeFile
    {
        public string FullPath { get; set; }
        public object Tag { get; set; }
    }

    public class FileSystemChangesWatcher :
        FileSystemChangesWatcher<FileSystemChangeFile>
    {
        public FileSystemChangesWatcher(
            string path,
            string filter)
            : base(path, filter)
        {
        }
    }

    public class FileSystemChangesWatcher<T> :
        IDisposable
        where T : FileSystemChangeFile
    {
        private readonly MemoryCache _memoryCache;
        private readonly FileSystemWatcher _fileSystemWatcher;

        public int CacheTimeout { get; set; } = 1000;
        public string Path
        {
            get => _fileSystemWatcher.Path;
            set => _fileSystemWatcher.Path = value;
        }

        public string Filter
        {
            get => _fileSystemWatcher.Filter;
            set => _fileSystemWatcher.Filter = value;
        }

        public event EventHandler<FileSystemChangeEventArgs<T>> Changed;

        private readonly Dictionary<string, T>
            _watchedFiles = new Dictionary<string, T>();

        public FileSystemChangesWatcher(
            string path,
            string filter)
        {
            _memoryCache = CreateCache();
            _fileSystemWatcher = new FileSystemWatcher(path, filter);

            _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
            _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void FileSystemWatcherOnChanged(
            object sender,
            FileSystemEventArgs args)
        {
            AddOrGetCacheItem(args.FullPath);
        }

        public void Watch(
            T file)
        {
            _watchedFiles.Add(file.FullPath, file);
        }

        private void AddOrGetCacheItem(
            string fullPath)
        {
            if (_watchedFiles.TryGetValue(fullPath, out var file))
            {
                var cacheItemPolicy = new CacheItemPolicy
                {
                    RemovedCallback = RemovedCallback,
                    AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CacheTimeout)
                };

                _memoryCache.AddOrGetExisting(fullPath, file, cacheItemPolicy);
            }
        }

        private void RemovedCallback(
            CacheEntryRemovedArguments args)
        {
            if (args.RemovedReason != CacheEntryRemovedReason.Expired) return;

            var e = (T) args.CacheItem.Value;

            try
            {
                OnChanged(new FileSystemChangeEventArgs<T>
                {
                    File = e
                });
            }

            catch
            {
                AddOrGetCacheItem(e.FullPath);
            }
        }

        private MemoryCache CreateCache()
        {
            var assembly = typeof(CacheItemPolicy).Assembly;

            var type = assembly.GetType("System.Runtime.Caching.CacheExpires");

            if (type == null) return new MemoryCache("FastExpiringCache");

            var field = type.GetField("_tsPerBucket", BindingFlags.Static | BindingFlags.NonPublic);

            if (field == null || field.FieldType != typeof(TimeSpan)) return new MemoryCache("FastExpiringCache");

            var originalValue = (TimeSpan)field.GetValue(null);

            field.SetValue(null, TimeSpan.FromMilliseconds(1));

            var instance = new MemoryCache("FastExpiringCache");

            field.SetValue(null, originalValue);

            return instance;
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
            _fileSystemWatcher?.Dispose();
        }

        protected virtual void OnChanged(
            FileSystemChangeEventArgs<T> e)
        {
            Changed?.Invoke(this, e);
        }
    }
}
