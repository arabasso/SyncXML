using System.Configuration;
using System.IO;
using System.ServiceProcess;

namespace SyncXml
{
    static class Program
    {
        static void Main()
        {
            //if (!File.Exists(ConfigurationManager.AppSettings["Source"]))
            //{
            //    return;
            //}

            var servicesToRun = new ServiceBase[]
            {
                new Service()
            };

            ServiceBase.Run(servicesToRun);
        }
    }
}
