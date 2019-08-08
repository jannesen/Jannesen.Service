using System;
using System.ComponentModel;
using Jannesen.Service.Windows;

namespace Jannesen.Service.ServiceProcess
{
    public static class ServiceControl
    {
        public      static      void            ServiceDefine(string serviceName, string serviceDisplayName, string binaryPathName, string accountName, string accountPassword)
        {
            if (serviceName is null) throw new ArgumentNullException(nameof(serviceName));
            if (serviceDisplayName is null) throw new ArgumentNullException(nameof(serviceDisplayName));
            if (binaryPathName is null) throw new ArgumentNullException(nameof(binaryPathName));
            if (accountName is null) throw new ArgumentNullException(nameof(accountName));

            Console.WriteLine("# create/update service: " + serviceName);

            if (accountName.IndexOf('\\') < 0) {
                switch(accountName.ToUpperInvariant()) {
                case "NT SERVICE":
                    accountName     = "NT SERVICE\\" + serviceName;
                    accountPassword = null;
                    break;

                case "LOCAL SERVICE":
                case "NETWORK SERVICE":
                case "LOCAL SYSTEM":
                    accountName     = "NT AUTHORITY\\" + accountName.ToUpperInvariant();
                    accountPassword = null;
                    break;

                default:
                    accountName = ".\\" + accountName;
                    break;
                }
            }

            using (ServiceManager serviceManager = new ServiceManager(null, true)) {
                bool    fcreate = false;

                try {
                    using (var service = serviceManager.OpenService(serviceName))
                    {
                        service.ChangeServiceConfig(serviceDisplayName, binaryPathName, accountName, accountPassword);
                    }
                }
                catch(Win32Exception err) {
                    if (err.NativeErrorCode != 1060)
                        throw;

                    fcreate = true;
                }

                if (fcreate) {
                    using (serviceManager.CreateService(serviceName, serviceDisplayName, binaryPathName, accountName, accountPassword))
                        {}
                }
            }
        }
        public      static      void            ServiceRemove(string serviceName)
        {
            if (serviceName is null) throw new ArgumentNullException(nameof(serviceName));

            Console.WriteLine("# remove service: " + serviceName);

            using (ServiceManager serviceManager = new ServiceManager(null, true)) {
                try {
                    using (var service = serviceManager.OpenService(serviceName))
                        service.DeleteService();
                }
                catch(Win32Exception err) {
                    if (err.NativeErrorCode != 1060)
                        throw;
                }
            }
        }
        public      static      void            ServiceStart(string serviceName)
        {
            if (serviceName is null) throw new ArgumentNullException(nameof(serviceName));

            using (ServiceManager serviceManager = new ServiceManager()) {
                using (var service = serviceManager.OpenService(serviceName)) {
                    service.StartService();
                }
            }
        }
        public      static      void            ServiceStop(string serviceName)
        {
            if (serviceName is null) throw new ArgumentNullException(nameof(serviceName));

            using (ServiceManager serviceManager = new ServiceManager()) {
                using (var service = serviceManager.OpenService(serviceName)) {
                    service.StopService();
                }
            }
        }
    }
}
