using System;
using System.Configuration;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace SyncXml
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();

                    var installer = new AssemblyInstaller(assembly, null)
                    {
                        UseNewContext = true
                    };

                    switch (args[0].ToLower())
                    {
                        case "/install":
                            installer.Install(null);
                            break;

                        case "/uninstall":
                            installer.Uninstall(null);
                            break;
                    }

                    installer.Commit(null);
                }

                catch
                {
                    // Ignore
                }

                return;
            }

            var synchronization = (SynchronizationConfigSection)ConfigurationManager.GetSection("Synchronization");

            var servicesToRun = synchronization
                .Files
                .OfType<SynchronizationFile>()
                .Select(s => new Service(s))
                .ToArray<ServiceBase>();

            ServiceBase.Run(servicesToRun);
        }
    }
}
