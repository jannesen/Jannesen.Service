using System;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Jannesen.Service.Windows
{
    public abstract class ServiceHandle: IDisposable
    {
        internal            IntPtr                          _handle;

        protected                                           ServiceHandle()
        {
            _handle = IntPtr.Zero;
        }
                                                            ~ServiceHandle()
        {
                    Dispose(false);
        }
        public              void                            Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual   void                            Dispose(bool disposing)
        {
            if (_handle!=IntPtr.Zero) {
                NativeMethods.CloseServiceHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        protected           void                            assignHandle(IntPtr handle)
        {
            _handle = handle;
        }
    }

    public class ServiceManager: ServiceHandle
    {
        private             bool                            _fullAccess;

        public                                              ServiceManager(string machineName = null, bool fullAccess = false)
        {
            IntPtr  handle;
            UInt32  dr = fullAccess ? (NativeMethods.SC_MANAGER_ALL_ACCESS) : (NativeMethods.SC_MANAGER_CONNECT|NativeMethods.SC_MANAGER_ENUMERATE_SERVICE|NativeMethods.SC_MANAGER_QUERY_LOCK_STATUS|NativeMethods.STANDARD_RIGHTS_READ);

            if ((handle = NativeMethods.OpenSCManager(machineName, null, dr))==IntPtr.Zero)
                throw NativeMethods.NewSystemError("Can't open service control manager.");

            assignHandle(handle);
            _fullAccess = fullAccess;
        }

        public              Service                         CreateService(string serviceName, string displayName, string binaryPathName, string serviceStartName, string password)
        {
            IntPtr  serviceHandle = NativeMethods.CreateService(_handle,
                                                           serviceName,
                                                           displayName,
                                                           NativeMethods.SERVICE_ALL_ACCESS,
                                                           NativeMethods.SERVICE_TYPE_WIN32_OWN_PROCESS,
                                                           NativeMethods.SERVICE_DEMAND_START,
                                                           NativeMethods.SERVICE_ERROR_NORMAL,
                                                           binaryPathName,
                                                           null,
                                                           IntPtr.Zero,
                                                           null,
                                                           serviceStartName,
                                                           password);

            if (serviceHandle==IntPtr.Zero)
                throw NativeMethods.NewSystemError("Create service '" + serviceName + "' failed");

            return new Service(serviceName, serviceHandle);
        }
        public              Service                         OpenService(string serviceName)
        {
            UInt32  dr = _fullAccess ? (NativeMethods.SERVICE_ALL_ACCESS) : (NativeMethods.SERVICE_QUERY_STATUS|NativeMethods.SERVICE_START|NativeMethods.SERVICE_STOP|NativeMethods.STANDARD_RIGHTS_READ);

            IntPtr  serviceHandle = NativeMethods.OpenService(_handle, serviceName, dr);

            if (serviceHandle==IntPtr.Zero)
                throw NativeMethods.NewSystemError("Open service '" + serviceName + "' failed");

            return new Service(serviceName, serviceHandle);
        }
    }

    public class Service: ServiceHandle
    {
        private             string                          _serviceName;

        public                                              Service(string serviceName, IntPtr handle)
        {
            _serviceName = serviceName;
            assignHandle(handle);
        }

        public              void                            ChangeServiceConfig(string displayName, string binaryPathName, string serviceStartName, string password)
        {
            if (!NativeMethods.ChangeServiceConfig( _handle,
                                              NativeMethods.SERVICE_TYPE_WIN32_OWN_PROCESS,
                                              NativeMethods.SERVICE_DEMAND_START,
                                              NativeMethods.SERVICE_ERROR_NORMAL,
                                              binaryPathName,
                                              null,
                                              IntPtr.Zero,
                                              null,
                                              serviceStartName,
                                              password,
                                              displayName))
                throw NativeMethods.NewSystemError("Change service config '" + _serviceName + "' failed");
        }
        public              void                            DeleteService()
        {
            if (!NativeMethods.DeleteService(_handle))
                throw NativeMethods.NewSystemError("Delete service '" + _serviceName + "' failed");
        }
        public              void                            StartService()
        {
            if (!NativeMethods.StartService(_handle, 0, null))
                throw NativeMethods.NewSystemError("Start service '" + _serviceName + "' failed");
        }
        public              void                            StopService()
        {
            NativeMethods.SERVICE_STATUS    service_status = new NativeMethods.SERVICE_STATUS();

            if (!NativeMethods.ControlService(_handle, NativeMethods.SERVICE_CONTROL.STOP, ref service_status)) {
                UInt32      err = (UInt32)Marshal.GetLastWin32Error();

                if (err != 1062)
                    throw NativeMethods.NewSystemError("Stop service '" + _serviceName + "' failed", err);
            }
        }

        private             NativeMethods.SERVICE_STATUS    _queryServiceStatus()
        {
            NativeMethods.SERVICE_STATUS    service_status = new NativeMethods.SERVICE_STATUS();

            if (!NativeMethods.QueryServiceStatus(_handle, ref service_status))
                throw NativeMethods.NewSystemError("Query service status '" + _serviceName + "' failed");

            return service_status;
        }
    }
}
