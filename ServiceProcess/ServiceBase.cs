using System;
using System.Collections.Generic;
using System.Globalization;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Jannesen.Service.Windows;

namespace Jannesen.Service.ServiceProcess
{
    public enum EventLogEntryType : short
    {
        Error           = 0x01,
        Warning         = 0x02,
        Information     = 0x04,
        SuccessAudit    = 0x08,
        FailureAudit    = 0x10
    }

    public abstract class ServiceBase: IDisposable
    {
        private struct LogEntry
        {
            public          DateTime    Timestamp;
            public          string      Source;
            public          string      Type;
            public          string      Message;
            public          object      Data;
        }

        public  const           string                          EventLogName = "Application";

        private     static      ServiceBase                     _serviceInstance;
        private                 string                          _serviceName;
        private                 bool                            _debuggerLogging;
        private                 bool                            _console;
        private                 bool                            _stopping;
        private                 bool                            _shutingdown;
        private                 bool                            _logTimestamp;
        private                 EventLog                        _eventLog;
        private     volatile    bool                            _debugLogActive;
        private                 string                          _debugLogDirectory;
        private                 List<LogEntry>                  _debugLogBuffer;
        private                 StreamWriter                    _debugLogStreamWriter;
        private                 int                             _debugLogDay;
        private                 Timer                           _debugLogFlushTimer;
        private                 object                          _debugLogFlushLock;
        private                 object                          _logLock;
        private                 Delegate                        __serviceControlCallback;
        private                 IntPtr                          _statusHandle;
        private                 NativeMethods.SERVICE_STATUS    _serviceStatus;
        private     static      object                          _lockErrorToString = new object();

        public                  bool                            RuningOnConsole
        {
            get {
                return _console;
            }
        }

        protected                                               ServiceBase()
        {
            _debuggerLogging = Debugger.IsAttached && Debugger.IsLogging();
            _serviceStatus.ServiceType              = NativeMethods.SERVICE_TYPE_WIN32_OWN_PROCESS;
            _serviceStatus.CurrentState             = 0;
            _serviceStatus.ControlsAccepted         = NativeMethods.SERVICE_ACCEPT_STOP | NativeMethods.SERVICE_ACCEPT_SHUTDOWN;
            _serviceStatus.Win32ExitCode            = 0;
            _serviceStatus.ServiceSpecificExitCode  = 0;
            _serviceStatus.CheckPoint               = 0;
            _serviceStatus.WaitHint                 = 0;
            _debugLogActive                         = false;
            _debugLogBuffer                         = null;
            _debugLogStreamWriter                   = null;
            _debugLogDay                            = int.MaxValue;
            _debugLogFlushTimer                     = null;
            _debugLogFlushLock                      = new object();
            _logLock                                = new object();
            _serviceInstance = this;
        }
                                                                ~ServiceBase()
        {
           Dispose(false);
        }
        public                  void                            Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected   virtual     void                            Dispose(Boolean disposing)
        {
            if (disposing) {
                lock(_debugLogFlushLock)
                    _debugLogStop();

                if (_eventLog != null)              _eventLog.Dispose();
            }
        }

        public      static      ServiceBase                     ServiceInstance
        {
            get {
                return _serviceInstance;
            }
        }
        public                  string                          ServiceName
        {
            get {
                return _serviceName;
            }
        }
        public                  bool                            Stopping
        {
            get {
                return _stopping;
            }
        }
        public                  bool                            Shutingdown
        {
            get {
                return _shutingdown;
            }
        }
        public                  bool                            LogDebugActive
        {
            get {
                return _debugLogActive;
            }
        }

        protected   abstract    void                            ParseArgs(string[] args);
        protected   abstract    void                            ServiceMain();
        protected   virtual     int                             ServiceStart()
        {
            ServiceControl.ServiceStart(_serviceName);
            return 0;
        }
        protected   virtual     int                             ServiceStop()
        {
            ServiceControl.ServiceStop(_serviceName);
            return 0;
        }
        protected   virtual     int                             ServiceInstall()
        {
            Installer installer = new Installer(InstallMode.Install);
            installer.Execute();
            ServiceInstaller(installer);

            return 0;
        }
        protected   virtual     int                             ServiceUninstall()
        {
            Installer installer = new Installer(InstallMode.Uninstall);
            ServiceInstaller(installer);
            installer.Execute();
            return 0;
        }
        protected   abstract    void                            ServiceInstaller(Installer installer);

        protected   abstract    void                            OnStop();
        protected   virtual     void                            OnShutdown()
        {
            OnStop();
        }

        public                  void                            EnableDebugLog(bool enable)
        {
            lock(_debugLogFlushLock) {
                if (enable) {
                    _debugLogDirectory = GetAppSettings("logdirectory") + @"\" + _serviceName;
                    if (!Directory.Exists(_debugLogDirectory))
                        Directory.CreateDirectory(_debugLogDirectory);

                    _debugLogOpen(DateTime.UtcNow);
                    _debugLogFlushTimer = new Timer(_debugLogFlush, null, 1000, Timeout.Infinite);

                    lock(_logLock) {
                        _debugLogBuffer = new List<LogEntry>(1024);
                        _debugLogActive = true;
                        _debugLogWrite(DateTime.UtcNow, null, "INFO", "DebugLogging enabled.", null);
                    }
                }
                else {
                    _debugLogActive = false;
                    _debugLogStop();
                }
            }
        }
        public                  void                            LogError(Exception error)
        {
            LogError(null, ErrorToString(error, true));
        }
        public                  void                            LogDebug(object source, string message)
        {
            LogDebug(source, "DBG", message);
        }
        public                  void                            LogDebug(object source, Exception error)
        {
            LogDebug(source, "DBG-ERROR", ErrorToString(error, false));
        }
        public                  void                            LogDebug(object source, string type, string message)
        {
            if (_debugLogActive) {
                lock(_logLock) {
                    _debugLogWrite(DateTime.UtcNow, SafeToString(source), type, message, null);
                }
            }
        }
        public                  void                            LogDebug(object source, string type, string message, object data)
        {
            if (_debugLogActive) {
                lock(_logLock) {
                    _debugLogWrite(DateTime.UtcNow, SafeToString(source), type, message, data);
                }
            }
        }
        public                  void                            LogInfo(object source, string message)
        {
            LogWrite(EventLogEntryType.Information, SafeToString(source), message);
        }
        public                  void                            LogWarning(object source, string message)
        {
            LogWrite(EventLogEntryType.Warning, SafeToString(source), message);
        }
        public                  void                            LogWarning(object source, Exception error)
        {
            LogWrite(EventLogEntryType.Warning, SafeToString(source), ErrorToString(error, false));
        }
        public                  void                            LogError(object source, string message)
        {
            LogWrite(EventLogEntryType.Error, SafeToString(source), message);
        }
        public                  void                            LogError(object source, Exception error)
        {
            LogWrite(EventLogEntryType.Error, SafeToString(source), ErrorToString(error, true));
        }
        public                  void                            LogWrite(EventLogEntryType type, string source, string message)
        {
            try {
                DateTime    timestamp = DateTime.UtcNow;
                string      srcmsg = (source != null) ? source + ": " + message : message;

                lock(_logLock) {
                    string stimestamp = _logTimestamp ? timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) : null;

                    if (_debuggerLogging)
                        Trace.WriteLine(stimestamp != null ? stimestamp + ": " + srcmsg : srcmsg);

                    if (_debugLogActive)
                        _debugLogWrite(timestamp, source, _mapEventTypeToString(type), message, null);

                    if (_console)
                        Console.WriteLine(stimestamp != null ? stimestamp + ": " + srcmsg : srcmsg);

                    if (_eventLog!=null)
                        _eventLog.WriteEntry(srcmsg, (System.Diagnostics.EventLogEntryType)type, 0);
                }
            }
            catch(Exception Err) {
                Trace.WriteLine("WriteLog failed: "+Err.Message);
            }
        }
        protected   virtual     void                            LogDataWriter(StreamWriter writer, object data)
        {
#pragma warning disable CA1062 // Validate arguments of public methods
            writer.Write(data.ToString());
#pragma warning restore CA1062
        }
        public      static      string                          GetAppSettings(string name)
        {
            string  s = ConfigurationManager.AppSettings[name];

            if (s == null)
                throw new Exception("Missing appSetting '" + name + "'.");

            return s;
        }
        public      static      string                          GetAppSettings(string name, string defaultValue)
        {
            string  s = ConfigurationManager.AppSettings[name];

            if (s == null)
                return defaultValue;

            return s;
        }

        public                  int                             Run(string[] args)
        {
            try {
                if (args is null) throw new ArgumentNullException(nameof(args));

                _console     = _hasConsole();
                _serviceName = GetAppSettings("service-name");

                if (args.Length>0) {
                    switch(args[0]) {
                    case "start":           return ServiceStart();
                    case "stop":            return ServiceStop();
                    case "install":         return ServiceInstall();
                    case "uninstall":       return ServiceUninstall();

                    case "run": {
                            string[]    newArgs = new string[args.Length-1];

                            Array.Copy(args, 1, newArgs, 0, args.Length-1);

                            return ConsoleRun(newArgs);
                        }

                    default:
                        throw new Exception(AppDomain.CurrentDomain.FriendlyName+": Unknown option '"+args[0]+"'.");
                    }
                }
                else {
                    return ServiceRun();
                }
            }
            catch(Exception Err) {
                _logTimestamp = false;
                LogWrite(EventLogEntryType.Error, null, "TERMINATED: " + ErrorToString(Err, true));
                return -1;
            }
        }
        public                  int                             ConsoleRun(string[] args)
        {
            if (!_console) {
                _eventLog = new EventLog("Application") { Source = _serviceName };
                _eventLog.WriteEntry(AppDomain.CurrentDomain.FriendlyName+": Has no console.", System.Diagnostics.EventLogEntryType.Error, 0);
                return -1;
            }

            _stopping     = false;
            _shutingdown  = false;
            _logTimestamp = true;

            unsafe
            {
                __serviceControlCallback = new NativeMethods.ConsoleCtrlHandler(_consoleCtrlHandler);
                NativeMethods.SetConsoleCtrlHandler(__serviceControlCallback, true);
            }

            _serviceMain(args);

            return 0;
        }
        public                  int                             ServiceRun()
        {
            if (_console) {
                _writeConsole(AppDomain.CurrentDomain.FriendlyName+": Cannot run service in console.");
                return -1;
            }

            _stopping    = false;
            _shutingdown = false;
            _eventLog    = new EventLog("Application") { Source = _serviceName };

            if (Environment.OSVersion.Platform != PlatformID.Win32NT ||
                Environment.OSVersion.Version.Major < 5)
                throw new Exception("Can only run on W2K or higher.");

            unsafe
            {
                NativeMethods.SERVICE_TABLE_ENTRY[]     entryTable = new NativeMethods.SERVICE_TABLE_ENTRY[2];
                entryTable[0].name     = _serviceName;
                entryTable[0].callback = new NativeMethods.ServiceMainCallback(this._serviceMainCallback);

                if (!NativeMethods.StartServiceCtrlDispatcher(entryTable))
                    throw NativeMethods.NewSystemError("StartServiceCtrlDispatcher failed");
            }

            return 0;
        }

        public      static      string                          ErrorToString(Exception err, bool allowDumpStack)
        {
            try {
                string      Message      = null;
                bool        DumpStack    = false;
                string      StackTrace   = string.Empty;

                lock(_lockErrorToString) {
                    while (err!=null) {
                        string  Msg;

                        if (err.StackTrace!=null)
                            StackTrace = err.GetType().FullName+":\r\n"+err.StackTrace + (StackTrace.Length==0 ? "" : "\r\n"+StackTrace);

                        if (err is System.Reflection.TargetInvocationException) {
                            Msg = string.Empty;
                        }
                        else
                        if (err is NotImplementedException) {
                            Msg = "Not implemented: "+((NotImplementedException)err).Message;
                            DumpStack = true;
                        }
                        else {
                            if ((err is System.AppDomainUnloadedException ||
                                 err is System.ArgumentException ||
                                 err is System.ArithmeticException ||
                                 err is System.ArrayTypeMismatchException ||
                                 err is System.BadImageFormatException ||
                                 err is System.CannotUnloadAppDomainException ||
                                 err is System.ComponentModel.LicenseException ||
                                 err is System.ComponentModel.WarningException ||
                                 err is System.FormatException ||
                                 err is System.IndexOutOfRangeException ||
                                 err is System.InvalidCastException ||
                                 err is System.InvalidOperationException ||
                                 err is System.InvalidProgramException ||
                                 err is System.IO.InternalBufferOverflowException ||
                                 err is System.MemberAccessException ||
                                 err is System.MulticastNotSupportedException ||
                                 err is System.NotImplementedException ||
                                 err is System.NotSupportedException ||
                                 err is System.NullReferenceException ||
                                 err is System.OutOfMemoryException ||
                                 err is System.RankException ||
                                 err is System.Reflection.AmbiguousMatchException ||
                                 err is System.Reflection.ReflectionTypeLoadException ||
                                 err is System.Resources.MissingManifestResourceException ||
                                 err is System.Runtime.InteropServices.ExternalException ||
                                 err is System.Runtime.InteropServices.InvalidComObjectException ||
                                 err is System.Runtime.InteropServices.InvalidOleVariantTypeException ||
                                 err is System.Runtime.InteropServices.MarshalDirectiveException ||
                                 err is System.Runtime.InteropServices.SafeArrayRankMismatchException ||
                                 err is System.Runtime.InteropServices.SafeArrayTypeMismatchException ||
                                 err is System.Runtime.Remoting.RemotingException ||
                                 err is System.Runtime.Remoting.ServerException ||
                                 err is System.Runtime.Serialization.SerializationException ||
                                 err is System.Security.Cryptography.CryptographicException ||
                                 err is System.Security.Policy.PolicyException ||
                                 err is System.Security.SecurityException ||
                                 err is System.Security.VerificationException ||
                                 err is System.Security.XmlSyntaxException ||
                                 err is System.StackOverflowException ||
                                 err is System.Threading.SynchronizationLockException ||
                                 err is System.Threading.ThreadInterruptedException ||
                                 err is System.Threading.ThreadStateException ||
                                 err is System.TypeInitializationException ||
                                 err is System.TypeLoadException ||
                                 err is System.TypeUnloadedException ||
                                 err is System.UnauthorizedAccessException) &&
                                !(err is System.Runtime.InteropServices.ExternalException) &&
                                !(err is  System.Net.WebException))
                            {
                                Msg = "Exception "+err.GetType().FullName+". "+err.Message;
                                DumpStack = true;
                            }
                            else
                                Msg = err.Message;
                        }

                        if (Msg.Length>0) {
                            Msg = Msg.Replace("\r\n", " ");

                            if (Message!=null) {
                                if (!Message.EndsWith(".", StringComparison.Ordinal))
                                    Message += ".";

                                Message += " "+Msg;
                            }
                            else
                                Message = Msg;
                        }

                        err = err.InnerException;
                    }

                    if (DumpStack && allowDumpStack)
                        Message += "\r\nSTACKTRACE:\r\n"+StackTrace;
                }

                return Message;
            }
            catch(Exception Err2) {
                try {
                    Trace.WriteLine("ErrorToString failed: "+Err2.Message);
                }
                catch(Exception) {
                }

                return "ErrorToString failed.";
            }
        }
        public                  void                            ServiceRunning()
        {
            _serviceStatus.CurrentState = NativeMethods.SERVICE_RUNNING;
            _serviceStatus.CheckPoint   = 0;
            _serviceStatus.WaitHint     = 0;
            _setServiceStatus();
            LogInfo(null, "Started");
        }
        public                  void                            ServiceStopping(uint Timeout)
        {
            _serviceStatus.CurrentState = NativeMethods.SERVICE_STOP_PENDING;
            _serviceStatus.CheckPoint   = 0;
            _serviceStatus.WaitHint     = Timeout;
            _setServiceStatus();
        }

        private                 bool                            _consoleCtrlHandler(UInt32 CtrlType)
        {
            switch(CtrlType) {
            case NativeMethods.CTRL_C_EVENT:
            case NativeMethods.CTRL_BREAK_EVENT:
                if (!_stopping)
                    _onStop();
                break;

            default:
                if (!_shutingdown)
                    _onShutdown();
                break;
            }

            return true;
        }

        private     unsafe      void                            _serviceMainCallback(int argCount, IntPtr argPointer)
        {
            try {
                __serviceControlCallback = new NativeMethods.ServiceControlCallbackEx(this._serviceControlCallbackEx);
                _statusHandle = NativeMethods.RegisterServiceCtrlHandlerEx(_serviceName, __serviceControlCallback, IntPtr.Zero);
                if (_statusHandle==IntPtr.Zero)
                    throw NativeMethods.NewSystemError("RegisterServiceCtrlHandlerEx failed");

                string[]    args = new string[argCount-1];

                for (int i = 1 ; i<argCount ; ++i)
                    args[i-1] = Marshal.PtrToStringUni(((IntPtr *)argPointer)[i]);

                _serviceMain(args);
            }
            catch(Exception Err) {
                LogError(new Exception("ServiceMainCallback failed", Err));
                _serviceStatus.Win32ExitCode = 1359;
                _serviceStatus.CurrentState  = NativeMethods.SERVICE_STOPPED;
                _serviceStatus.CheckPoint    = 0;
                _serviceStatus.WaitHint      = 0;
                _setServiceStatus();
            }

        }
        private     unsafe      UInt32                          _serviceControlCallbackEx(UInt32 Control, UInt32 EventType, IntPtr EventData, IntPtr EventContext)
        {
            switch(Control) {
            case NativeMethods.SERVICE_CONTROL_INTERROGATE:
                break;

            case NativeMethods.SERVICE_CONTROL_STOP:
                if (!_stopping) {
                    _onStop();
                    return NativeMethods.NO_ERROR;
                }
                break;

            case NativeMethods.SERVICE_CONTROL_SHUTDOWN:
                if (!_shutingdown) {
                    _onShutdown();
                    return NativeMethods.NO_ERROR;
                }
                break;

            default:
                return NativeMethods.ERROR_CALL_NOT_IMPLEMENTED;
            }

            _setServiceStatus();
            return NativeMethods.NO_ERROR;
        }

        private                 void                            _serviceMain(string[] args)
        {
            try {
                _serviceStatus.CurrentState = NativeMethods.SERVICE_START_PENDING;
                _serviceStatus.CheckPoint   = 0;
                _serviceStatus.WaitHint     = 30*1000;
                _setServiceStatus();

                System.Threading.Thread.CurrentThread.Name           = "Service";
                System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                try {
                    ParseArgs(args);
                }
                catch(Exception Err) {
                    _logTimestamp = false;
                    LogError(null,
                                 (_console ? AppDomain.CurrentDomain.FriendlyName+": Command syntax error. "
                                           : "ParseArgs failed, Service argument error. ") + ErrorToString(Err, true));
                    _serviceStatus.Win32ExitCode = 87;
                    return;
                }

                try {
                    if (GetAppSettings("service-debuglog", "0") == "1")
                        EnableDebugLog(true);

                    ServiceMain();
                    LogInfo(null, "Stopped");
                }
                catch(Exception Err) {
                    LogError(new Exception("Service main failed", Err));
                    _serviceStatus.Win32ExitCode = 1359;
                    return;
                }
            }
            finally {
                _serviceStatus.CurrentState = NativeMethods.SERVICE_STOPPED;
                _serviceStatus.CheckPoint   = 0;
                _serviceStatus.WaitHint     = 0;
                _setServiceStatus();
            }
        }
        private                 void                            _setServiceStatus()
        {
            try {
#if DEBUG
//              WriteDebug("SetServiceStatus("+_ServiceStatus.CurrentState+","+_ServiceStatus.CheckPoint+","+_ServiceStatus.WaitHint+")");
#endif
                if (!_console) {
                    unsafe
                    {
                        if (_statusHandle!=IntPtr.Zero) {
                            fixed(NativeMethods.SERVICE_STATUS* pServiceStatus = &_serviceStatus)
                            {
                                if (!NativeMethods.SetServiceStatus(_statusHandle, pServiceStatus))
                                    throw NativeMethods.NewSystemError("SetServiceStatus failed");
                            }
                        }
                    }
                }
                else {
                    switch(_serviceStatus.CurrentState) {
                    case NativeMethods.SERVICE_RUNNING:
                        _writeConsole("[Press control-C to stop service]");
                        break;

                    case NativeMethods.SERVICE_STOPPED:
                        if (_serviceStatus.Win32ExitCode!=0)
                            LogDebug(null, "ExitCode="+_serviceStatus.Win32ExitCode);
                        break;
                    }
                }
            }
            catch(Exception Err) {
                LogError(new Exception("SetServiceStatus failed", Err));
            }
        }
        private                 void                            _onStop()
        {
            try {
                _stopping     = true;
                LogInfo(null, "Stopping");
                OnStop();
            }
            catch(Exception Err) {
                LogError(new Exception("Service.OnStop failed", Err));
            }

            if (_serviceStatus.CurrentState!=NativeMethods.SERVICE_STOP_PENDING) {
                LogError(new Exception("Internal error in Service.OnStop, Service is not stopping."));
                ServiceStopping(15*1000);
            }
        }
        private                 void                            _onShutdown()
        {
            try {
                _stopping     = true;
                _shutingdown  = true;
                LogInfo(null, "Shutingdown");
                OnShutdown();
            }
            catch(Exception Err) {
                LogError(new Exception("Service.OnShutdown failed", Err));
            }

            if (_serviceStatus.CurrentState!=NativeMethods.SERVICE_STOP_PENDING) {
                LogError(new Exception("Internal error in Service.OnShutdown, Service is not stopping."));
                ServiceStopping(15*1000);
            }
        }
        private                 void                            _writeConsole(string Message)
        {
            Console.WriteLine(Message);

            if (_debuggerLogging)
                Trace.WriteLine(Message);
        }
        private                 void                            _debugLogWrite(DateTime timestamp, string source, string type, string message, object data)
        {
            try {
                _debugLogBuffer.Add(new LogEntry() {
                                        Timestamp = timestamp,
                                        Source    = SafeToString(source),
                                        Type      = type,
                                        Message   = message,
                                        Data      = data
                                    });
                if (_debugLogBuffer.Count == 256)
                    _debugLogFlushTimer.Change(10, Timeout.Infinite);
            }
            catch(Exception err) {
                _debugLogActive = false;
                LogError("DebugLog", err);
            }
        }
        private                 void                            _debugLogFlush(object state)
        {
            List<LogEntry>      buffer;

            lock(_debugLogFlushLock) {
                try {
                    lock(_logLock) {
                        buffer = _debugLogBuffer;
                        _debugLogBuffer = _debugLogActive ? new List<LogEntry>(1024) : null;
                    }

                    if (buffer != null) {
                        foreach(var entry in buffer)
                            _debugLogWriteEntry(entry);

                        _debugLogStreamWriter.Flush();
                    }

                    if (_debugLogFlushTimer != null)
                        _debugLogFlushTimer.Change(1000, Timeout.Infinite);
                }
                catch(Exception err) {
                    _debugLogActive = false;
                    LogError("DebugLog", err);
                }
            }
        }
        private                 void                            _debugLogWriteEntry(LogEntry logEntry)
        {
            _debugLogOpen(logEntry.Timestamp);

            var streamWriter = _debugLogStreamWriter;

            streamWriter.Write(logEntry.Timestamp.ToString("MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            streamWriter.Write('\t');
            if (logEntry.Source != null)    streamWriter.Write(logEntry.Source);
            streamWriter.Write("\t");
            streamWriter.Write(logEntry.Type);
            streamWriter.Write("\t");

            int     b = 0;
            int     e = logEntry.Message.Length;
            int     p;

            while ((p = logEntry.Message.IndexOf("\r\n", b, StringComparison.Ordinal)) >= 0) {
                while (b < p)
                    streamWriter.Write(logEntry.Message[b++]);

                b += 2;

                if (b < logEntry.Message.Length)
                    streamWriter.Write('\xA6');
            }

            if (b < logEntry.Message.Length) {
                if (b == 0)
                    streamWriter.Write(logEntry.Message);
                else {
                    while (b < logEntry.Message.Length)
                        streamWriter.Write(logEntry.Message[b++]);
                }
            }

            if (logEntry.Data != null) {
                streamWriter.Write("\t");
                LogDataWriter(streamWriter, logEntry.Data);
            }

            _debugLogStreamWriter.Write('\n');
        }
        private                 void                            _debugLogOpen(DateTime timestamp)
        {
            var day = (int)(timestamp.Ticks / TimeSpan.TicksPerDay);

            if (_debugLogDay != day || _debugLogStreamWriter == null) {
                if (_debugLogStreamWriter != null) {
                    _debugLogStreamWriter.Close();
                    _debugLogStreamWriter = null;
                }

                _debugLogDay = day;

                var     datetime      = new DateTime(day * TimeSpan.TicksPerDay);
                string  baseFileName  = _debugLogDirectory + @"\" + datetime.ToString(@"yyyy\\MM\\", CultureInfo.InvariantCulture) + _serviceName + datetime.ToString(@"-yyyy-MM-dd", CultureInfo.InvariantCulture);
                string  directoryName = Path.GetDirectoryName(baseFileName);

                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);

                for (int seq = 1 ; seq < 100 ; ++ seq) {
                    string fileName = baseFileName + "-" + seq.ToString("D3", CultureInfo.InvariantCulture) + ".log";

                    if (!File.Exists(fileName)) {
                        _debugLogStreamWriter = new StreamWriter(fileName, false, System.Text.Encoding.UTF8, 0x10000);
                        return;
                    }
                }

                throw new Exception("Can't create new logfile, to many logfiles.");
            }
        }
        private                 void                            _debugLogStop()
        {
            _debugLogActive = false;

            if (_debugLogFlushTimer != null) {
                _debugLogFlushTimer.Dispose();
                _debugLogFlushTimer = null;
            }

            _debugLogFlush(null);

            if (_debugLogStreamWriter != null) {
                _debugLogStreamWriter.Close();
                _debugLogStreamWriter = null;
            }
        }

        private     static      bool                            _hasConsole()
        {
            switch ((int)NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE)) {
            case -1:
            case 0:
                return false;

            default:
                return true;
            }
        }
        private     static      string                          _mapEventTypeToString(EventLogEntryType type)
        {
            switch(type) {
            case EventLogEntryType.Error:           return "ERROR";
            case EventLogEntryType.Warning:         return "WARNING";
            case EventLogEntryType.Information:     return "INFO";
            default:                                return type.ToString();
            }
        }

        public      static      void                            SaveDispose(IDisposable disposable)
        {
            if (disposable != null) {
                try {
                        disposable.Dispose();
                }
                catch(Exception err) {
                    _serviceInstance.LogError(disposable, new Exception("Dispose failed.", err));
                }
            }
        }
        public      static      string                          SafeToString(object o)
        {
            if (o != null) {
                try {
                    return o.ToString();
                }
                catch(Exception err) {
                    return "[Error: " + err.Message + "]";
                }
            }

            return null;
        }
    }
}
