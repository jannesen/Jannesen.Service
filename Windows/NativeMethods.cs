using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Jannesen.Service.Windows
{
    /// <summary>
    /// Summary description for WinApi.
    /// </summary>
    class NativeMethods
    {
        [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          IntPtr  GetStdHandle(UInt32 StdHandle);
        [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    SetConsoleCtrlHandler(Delegate HandlerRoutine, bool Add);
        [DllImport("Kernel32", CharSet=CharSet.Unicode)]
        internal static extern unsafe   UInt32  FormatMessageW(UInt32 Flags,void* lpSource,UInt32 MessageId,UInt32 LanguageId,char** lpBuffer,UInt32 nSize,void* Arguments);
        [DllImport("kernel32")]
        internal static extern          IntPtr  LocalFree(IntPtr hMem);

        [DllImport("netapi32.dll", CharSet=CharSet.Unicode)]
        internal static extern          UInt32  NetUserAdd(string servername, UInt32 level, ref USER_INFO_2 userinfo, out UInt32 parm_err);
        [DllImport("netapi32.dll", CharSet=CharSet.Unicode)]
        internal static extern          UInt32  NetUserDel(string servername, string username);
        [DllImport("netapi32.dll", CharSet=CharSet.Unicode)]
        internal static extern          UInt32  NetUserSetInfo(string servername, string username, UInt32 level, ref USER_INFO_2 userinfo, out UInt32 parm_err);
        [DllImport("netapi32.dll", CharSet=CharSet.Unicode)]
        internal static extern          UInt32  NetUserSetInfo(string servername, string username, UInt32 level, ref USER_INFO_1003 userinfo, out UInt32 parm_err);
        [DllImport("netapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          UInt32  NetLocalGroupAddMembers(string servername, string groupname, UInt32 level, ref LOCALGROUP_MEMBERS_INFO_3 buf, UInt32 totalentries);
        [DllImport("advapi32.dll")]
        internal static unsafe extern   UInt32  LsaOpenPolicy(LSA_UNICODE_STRING* systemName,ref LSA_OBJECT_ATTRIBUTES objectAttributes, POLICY_ACCESS desiredAccess, out IntPtr policyHandle);
        [DllImport("advapi32.dll")]
        internal static unsafe extern   UInt32  LsaClose(IntPtr policyHandle);
        [DllImport("advapi32.dll")]
        internal static unsafe extern   UInt32  LsaAddAccountRights(IntPtr policyHandle, IntPtr accountSid,LSA_UNICODE_STRING* userRights,UInt32 CountOfRights);
        [DllImport("advapi32.dll")]
        internal static unsafe extern   UInt32  LsaRemoveAccountRights(IntPtr policyHandle, IntPtr accountSid,[MarshalAs(UnmanagedType.U1)] bool AllRights,LSA_UNICODE_STRING* userRights,UInt32 CountOfRights);
        [DllImport("advapi32.dll")]
        internal static unsafe extern   UInt32  LsaNtStatusToWinError(UInt32 Status);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static unsafe extern   bool    LookupAccountSid(string SystemName,byte* pSid,System.Text.StringBuilder Name,UInt32* pcbName,System.Text.StringBuilder DomainName,UInt32* pcbDomainName,SidNameUse* pUse);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static unsafe extern   bool    LookupAccountName(string SystemName,string AccountName,byte* pSid,UInt32* pcbSid,System.Text.StringBuilder DomainName,UInt32* pcbDomainName,SidNameUse* pUse);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          IntPtr  OpenSCManager(string machineName, string databaseName, UInt32 access);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          IntPtr  OpenService(IntPtr databaseHandle, string serviceName, UInt32 access);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          IntPtr  CreateService(IntPtr databaseHandle, string serviceName, string displayName, UInt32 access, UInt32 serviceType, UInt32 startType, UInt32 errorControl, string binaryPath, string loadOrderGroup, IntPtr pTagId, string dependencies, string servicesStartName, string password);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    DeleteService(IntPtr serviceHandle);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    ChangeServiceConfig(IntPtr hService, UInt32 nServiceType, UInt32 nStartType, UInt32 nErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword, string lpDisplayName);
        [DllImport("advapi32", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);
        [DllImport("advapi32", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    ControlService(IntPtr hService, SERVICE_CONTROL dwControl, ref SERVICE_STATUS lpServiceStatus);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError=true)]
        public static extern            bool    QueryServiceStatus(IntPtr hService,ref SERVICE_STATUS dwServiceStatus);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    CloseServiceHandle(IntPtr handle);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          bool    StartServiceCtrlDispatcher(SERVICE_TABLE_ENTRY[] entryTable);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern          IntPtr  RegisterServiceCtrlHandlerEx(string serviceName, Delegate callback, IntPtr userData);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        internal static extern unsafe   bool    SetServiceStatus(IntPtr serviceStatusHandle, SERVICE_STATUS* status);


        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        public struct SERVICE_TABLE_ENTRY
        {
            public string   name;
            public Delegate callback;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            public UInt32   ServiceType;
            public UInt32   CurrentState;
            public UInt32   ControlsAccepted;
            public UInt32   Win32ExitCode;
            public UInt32   ServiceSpecificExitCode;
            public UInt32   CheckPoint;
            public UInt32   WaitHint;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct USER_INFO_2
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string name;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string password;
            public UInt32 password_age;
            public UInt32 priv;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string home_dir;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string comment;
            public UInt32 flags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string script_path;
            public UInt32 auth_flags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string full_name;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string usr_comment;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string parms;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string workstations;
            public UInt32 last_logon;
            public UInt32 last_logoff;
            public UInt32 acct_expires;
            public UInt32 max_storage;
            public UInt32 units_per_week;
            public IntPtr logon_hours;
            public UInt32 bad_pw_count;
            public UInt32 num_logons;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string logon_server;
            public int country_code;
            public int code_page;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct USER_INFO_1003
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string password;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LOCALGROUP_MEMBERS_INFO_3
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string domainandname;
        }
        [StructLayout(LayoutKind.Sequential)]
        public  unsafe  struct LSA_UNICODE_STRING: IDisposable
        {
            public  UInt16  Length;
            public  UInt16  MaximumLength;
            public  IntPtr  Buffer;

            public              LSA_UNICODE_STRING(string s)
            {
                Length        = (UInt16)(s.Length * 2);
                MaximumLength = (UInt16)((s.Length + 1) * 2);
                Buffer        = Marshal.StringToHGlobalUni(s);
            }
            public  void        Dispose()
            {
                Marshal.FreeHGlobal(Buffer);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public  unsafe  struct LSA_OBJECT_ATTRIBUTES
        {
            public  UInt32              Length;
            public  IntPtr              RootDirectory;
            public  LSA_UNICODE_STRING* ObjectName;
            public  UInt32              Attributes;
            public  void*               SecurityDescriptor;
            public  void*               SecurityQualityOfService;
        }

        public  enum    POLICY_ACCESS   : uint
        {
            ViewLocalInformation            = 0x00000001,
            ViewAuditInformation            = 0x00000002,
            GetPrivateInformation           = 0x00000004,
            TrustAdmin                      = 0x00000008,
            CreateAccount                   = 0x00000010,
            CreateSecret                    = 0x00000020,
            CreatePrivilege                 = 0x00000040,
            SetDefaultQuotaLimits           = 0x00000080,
            SetAuditRequirements            = 0x00000100,
            AuditLogAdmin                   = 0x00000200,
            ServerAdmin                     = 0x00000400,
            LookupNames                     = 0x00000800,
            Notification                    = 0x00001000,
            AllAccess                       = AceMask.StandardRightsRequired|ViewLocalInformation|ViewAuditInformation|GetPrivateInformation|TrustAdmin|CreateAccount|CreateSecret|CreatePrivilege|SetDefaultQuotaLimits|SetAuditRequirements|AuditLogAdmin|ServerAdmin|LookupNames,
            Read                            = AceMask.StandardRightsRead|ViewAuditInformation|GetPrivateInformation,
            Write                           = AceMask.StandardRightsWrite|TrustAdmin|CreateAccount|CreateSecret|CreatePrivilege|SetDefaultQuotaLimits|SetAuditRequirements|AuditLogAdmin|ServerAdmin,
            Execute                         = AceMask.StandardRightsExecute|ViewLocalInformation|LookupNames,
        }
        public  enum    AceMask                 : uint /*DWORD*/
        {
            None                            = 0,
            FileAllAccess                   = StandardRightsRequired|Synchronize|0x1ff,
            FileGenericRead                 = StandardRightsRead|FileReadData|FileReadAttributes|FileReadEa|Synchronize,
            FileGenericWrite                = StandardRightsWrite|FileWriteData|FileWriteAttributes|FileWriteEa|FileAppendData|Synchronize,
            FileGenericExecute              = StandardRightsExecute|FileReadAttributes|FileExecute|Synchronize,
            FileListDirectory               = 0x00000001,   // directory
            FileReadData                    = 0x00000001,   // file & pipe
            FileAddFile                     = 0x00000002,   // directory
            FileWriteData                   = 0x00000002,   // file & pipe
            FileCreatePipeInstance          = 0x00000004,   // named pipe
            FileAddSubdirectory             = 0x00000004,   // directory
            FileAppendData                  = 0x00000004,   // file
            FileReadEa                      = 0x00000008,   // file & directory
            FileWriteEa                     = 0x00000010,   // file & directory
            FileExecute                     = 0x00000020,   // file
            FileTraverse                    = 0x00000020,   // directory
            FileDeleteChild                 = 0x00000040,   // directory
            FileReadAttributes              = 0x00000080,
            FileWriteAttributes             = 0x00000100,
            Delete                          = 0x00010000,
            ReadControl                     = 0x00020000,
            WriteDac                        = 0x00040000,
            WriteOwner                      = 0x00080000,
            Synchronize                     = 0x00100000,
            StandardRightsRequired          = Delete | ReadControl | WriteDac | WriteOwner,
            StandardRightsRead              = ReadControl,
            StandardRightsWrite             = ReadControl,
            StandardRightsExecute           = ReadControl,
            AccessSystemSecurity            = 0x01000000,
            MaximumAllowed                  = 0x02000000,
            GenericAll                      = 0x10000000,
            GenericExecute                  = 0x20000000,
            GenericWrite                    = 0x40000000,
            GenericRead                     = 0x80000000,
        }
        public  enum    SidNameUse              : uint /*DWORD*/
        {
            SidTypeUser = 1,
            SidTypeGroup,
            SidTypeDomain,
            SidTypeAlias,
            SidTypeWellKnownGroup,
            SidTypeDeletedAccount,
            SidTypeInvalid,
            SidTypeUnknown,
            SidTypeComputer
        }
        [Flags]
        public enum SERVICE_CONTROL : uint
        {
            STOP                    = 0x00000001,
            PAUSE                   = 0x00000002,
            CONTINUE                = 0x00000003,
            INTERROGATE             = 0x00000004,
            SHUTDOWN                = 0x00000005,
            PARAMCHANGE             = 0x00000006,
            NETBINDADD              = 0x00000007,
            NETBINDREMOVE           = 0x00000008,
            NETBINDENABLE           = 0x00000009,
            NETBINDDISABLE          = 0x0000000A,
            DEVICEEVENT             = 0x0000000B,
            HARDWAREPROFILECHANGE   = 0x0000000C,
            POWEREVENT              = 0x0000000D,
            SESSIONCHANGE           = 0x0000000E
        }

        // Constant
        public  const   UInt32  SERVICE_STOPPED                             = 0x00000001;
        public  const   UInt32  SERVICE_START_PENDING                       = 0x00000002;
        public  const   UInt32  SERVICE_STOP_PENDING                        = 0x00000003;
        public  const   UInt32  SERVICE_RUNNING                             = 0x00000004;
        public  const   UInt32  SERVICE_CONTINUE_PENDING                    = 0x00000005;
        public  const   UInt32  SERVICE_PAUSE_PENDING                       = 0x00000006;
        public  const   UInt32  SERVICE_PAUSED                              = 0x00000007;
        public  const   UInt32  SERVICE_CONTROL_STOP                        = 0x00000001;
        public  const   UInt32  SERVICE_CONTROL_PAUSE                       = 0x00000002;
        public  const   UInt32  SERVICE_CONTROL_CONTINUE                    = 0x00000003;
        public  const   UInt32  SERVICE_CONTROL_INTERROGATE                 = 0x00000004;
        public  const   UInt32  SERVICE_CONTROL_SHUTDOWN                    = 0x00000005;
        public  const   UInt32  SERVICE_CONTROL_PARAMCHANGE                 = 0x00000006;
        public  const   UInt32  SERVICE_CONTROL_NETBINDADD                  = 0x00000007;
        public  const   UInt32  SERVICE_CONTROL_NETBINDREMOVE               = 0x00000008;
        public  const   UInt32  SERVICE_CONTROL_NETBINDENABLE               = 0x00000009;
        public  const   UInt32  SERVICE_CONTROL_NETBINDDISABLE              = 0x0000000A;
        public  const   UInt32  SERVICE_CONTROL_DEVICEEVENT                 = 0x0000000B;
        public  const   UInt32  SERVICE_CONTROL_HARDWAREPROFILECHANGE       = 0x0000000C;
        public  const   UInt32  SERVICE_CONTROL_POWEREVENT                  = 0x0000000D;
        public  const   UInt32  SERVICE_CONTROL_SESSIONCHANGE               = 0x0000000E;
        public  const   UInt32  SERVICE_ACCEPT_STOP                         = 0x00000001;
        public  const   UInt32  SERVICE_ACCEPT_PAUSE_CONTINUE               = 0x00000002;
        public  const   UInt32  SERVICE_ACCEPT_SHUTDOWN                     = 0x00000004;
        public  const   UInt32  SERVICE_ACCEPT_PARAMCHANGE                  = 0x00000008;
        public  const   UInt32  SERVICE_ACCEPT_NETBINDCHANGE                = 0x00000010;
        public  const   UInt32  SERVICE_ACCEPT_HARDWAREPROFILECHANGE        = 0x00000020;
        public  const   UInt32  SERVICE_ACCEPT_POWEREVENT                   = 0x00000040;
        public  const   UInt32  SERVICE_ACCEPT_SESSIONCHANGE                = 0x00000080;
        public  const   UInt32  CTRL_C_EVENT                                = 0;
        public  const   UInt32  CTRL_BREAK_EVENT                            = 1;
        public  const   UInt32  CTRL_CLOSE_EVENT                            = 2;
        public  const   UInt32  CTRL_LOGOFF_EVENT                           = 5;
        public  const   UInt32  CTRL_SHUTDOWN_EVENT                         = 6;
        public  const   UInt32  SC_MANAGER_CONNECT                          = 0x00000001;
        public  const   UInt32  SC_MANAGER_ENUMERATE_SERVICE                = 0x00000004;
        public  const   UInt32  SC_MANAGER_QUERY_LOCK_STATUS                = 0x00000010;
        public  const   UInt32  SC_MANAGER_ALL_ACCESS                       = 0x000F003F;
        public  const   UInt32  SERVICE_ALL_ACCESS                          = 0xF01FF;
        public  const   UInt32  SERVICE_INTERROGATE                         = 0x00080;
        public  const   UInt32  SERVICE_QUERY_CONFIG                        = 0x00001;
        public  const   UInt32  SERVICE_QUERY_STATUS                        = 0x00004;
        public  const   UInt32  SERVICE_START                               = 0x00010;
        public  const   UInt32  SERVICE_STOP                                = 0x00020;
        public  const   UInt32  STANDARD_RIGHTS_READ                        = 0x00020000;
        public  const   UInt32  DELETE                                      = 0x00010000;
        public  const   UInt32  SERVICE_TYPE_WIN32_OWN_PROCESS              = 0x10;
        public  const   UInt32  SERVICE_TYPE_WIN32_SHARE_PROCESS            = 0x20;
        public  const   UInt32  SERVICE_BOOT_START                          = 0x00000000;
        public  const   UInt32  SERVICE_SYSTEM_START                        = 0x00000001;
        public  const   UInt32  SERVICE_AUTO_START                          = 0x00000002;
        public  const   UInt32  SERVICE_DEMAND_START                        = 0x00000003;
        public  const   UInt32  SERVICE_DISABLED                            = 0x00000004;
        public  const   UInt32  SERVICE_ERROR_IGNORE                        = 0x00000000;
        public  const   UInt32  SERVICE_ERROR_NORMAL                        = 0x00000001;
        public  const   UInt32  SERVICE_ERROR_SEVERE                        = 0x00000002;
        public  const   UInt32  SERVICE_ERROR_CRITICAL                      = 0x00000003;
        public  const   UInt32  NO_ERROR                                    = 0;
        public  const   UInt32  ERROR_CALL_NOT_IMPLEMENTED                  = 120;
        public  const   UInt32  STD_OUTPUT_HANDLE                           = unchecked((UInt32)(-11));
        public  const   UInt32  FORMAT_MESSAGE_ALLOCATE_BUFFER              = 0x00000100;
        public  const   UInt32  FORMAT_MESSAGE_FROM_SYSTEM                  = 0x00001000;
        public  const   UInt32  USER_PRIV_GUEST                             = 0;
        public  const   UInt32  USER_PRIV_USER                              = 1;
        public  const   UInt32  USER_PRIV_ADMIN                             = 2;
        public  const   UInt32  UF_NORMAL_ACCOUNT                           = 0x0000200;
        public  const   UInt32  UF_PASSWD_CANT_CHANGE                       = 0x0000040;
        public  const   UInt32  UF_DONT_EXPIRE_PASSWD                       = 0x0010000;
        public  const   UInt32  UF_MNS_LOGON_ACCOUNT                        = 0x0020000;
        public  const   UInt32  UF_SMARTCARD_REQUIRED                       = 0x0040000;
        public  const   UInt32  UF_TRUSTED_FOR_DELEGATION                   = 0x0080000;
        public  const   UInt32  UF_NOT_DELEGATED                            = 0x0100000;
        public  const   UInt32  UF_USE_DES_KEY_ONLY                         = 0x0200000;
        public  const   UInt32  UF_DONT_REQUIRE_PREAUTH                     = 0x0400000;
        public  const   UInt32  UF_PASSWORD_EXPIRED                         = 0x0800000;
        public  const   UInt32  UF_TRUSTED_TO_AUTHENTICATE_FOR_DELEGATION   = 0x1000000;
        public  const   UInt32  UF_NO_AUTH_DATA_REQUIRED                    = 0x2000000;
        public  const   UInt32  UF_PARTIAL_SECRETS_ACCOUNT                  = 0x4000000;
        public  const   UInt32  UF_USE_AES_KEYS                             = 0x8000000;

        // Callbacks
        public delegate void    ServiceMainCallback(int argCount, IntPtr argPointer);
        public delegate UInt32  ServiceControlCallbackEx(UInt32 control, UInt32 eventType, IntPtr eventData, IntPtr eventContext);
        public delegate bool    ConsoleCtrlHandler(UInt32 CtrlType);

        // Helper function
        public static   Win32Exception      NewSystemError(string message)
        {
            return NewSystemError(message, (UInt32)Marshal.GetLastWin32Error());
        }
        public static   Win32Exception      NewSystemError(string Message,UInt32 win32Error)
        {
            return new Win32Exception((int)win32Error, Message+": "+Win32ErrorCodeToString(win32Error));
        }
        public static   string              Win32ErrorCodeToString(UInt32 Win32Error)
        {
            unsafe
            {
                char*   pMsgText;

                FormatMessageW(NativeMethods.FORMAT_MESSAGE_ALLOCATE_BUFFER|NativeMethods.FORMAT_MESSAGE_FROM_SYSTEM,
                               null,
                               Win32Error,
                               0x409,
                               &pMsgText,
                               0,
                               null);

                if (pMsgText!=null) {
                    string  MsgText = new string(pMsgText);
                    NativeMethods.LocalFree((IntPtr)pMsgText);
                    return "["+Win32Error+"] " + MsgText.Trim();
                }
                else
                    return "["+Win32Error+"]";
            }
        }
    }
}
