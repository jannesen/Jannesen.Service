using System;

namespace Jannesen.Service.ServiceProcess
{
    [Flags]
    public enum EventLogEntryType: short
    {
        Error           = 0x01,
        Warning         = 0x02,
        Information     = 0x04,
        SuccessAudit    = 0x08,
        FailureAudit    = 0x10
    }

    public interface IServiceLogger
    {
        bool    LogDebugActive                      { get; }
        void    LogError(Exception err);
        void    LogDebug(object source, string message);
        void    LogDebug(object source, Exception err);
        void    LogDebug(object source, string type, string message);
        void    LogDebug(object source, string type, string message, object data);
        void    LogInfo(object source, string message);
        void    LogWarning(object source, string message);
        void    LogWarning(object source, Exception err);
        void    LogError(object source, string message);
        void    LogError(object source, Exception err);
        void    LogWrite(EventLogEntryType type, string source, string message);
    }
}
