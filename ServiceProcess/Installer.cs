using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Jannesen.Service.Windows;

namespace Jannesen.Service.ServiceProcess
{
    public enum InstallMode
    {
        Install,
        Uninstall
    }

    public class Installer
    {
        public              InstallMode     InstallMode             { get ; private set ; }
        public              string          ServiceName             { get ; private set ; }
        public              string          ServiceDisplayName      { get ; private set ; }
        public              string          AccountName             { get ; private set ; }
        public              string          AccountPassword         { get ; private set ; }

        public              string          AccountFullName
        {
            get {
                if (AccountName.IndexOf('\\') > 0)
                    return AccountName;

                switch(AccountName.ToUpper())
                {
                case "NT SERVICE":          return "NT SERVICE\\" + ServiceName;
                case "LOCAL SERVICE":
                case "NETWORK SERVICE":
                case "LOCAL SYSTEM":        return "NT AUTHORITY\\" + AccountName.ToUpper();
                default:                    return System.Environment.MachineName + "\\" + AccountName;
                }
            }
        }
        public              NTAccount       AccountIdentity
        {
            get {
                return new NTAccount(AccountFullName);
            }
        }
        public              bool            predefinedAccount
        {
            get {
                switch(AccountName.ToUpper())
                {
                case "LOCAL SERVICE":
                case "NETWORK SERVICE":
                case "LOCAL SYSTEM":
                case "NT SERVICE":
                    return true;

                default:
                    return false;
                }
            }
        }
        public              string          ProgramExe
        {
            get {
                return System.Reflection.Assembly.GetEntryAssembly().Location;
            }
        }
        public              string          ProgramDirectory
        {
            get {
                return Path.GetDirectoryName(ProgramExe);
            }
        }
        public                              Installer(InstallMode installMode)
        {
            InstallMode        = installMode;
            ServiceName        = ServiceBase.GetAppSettings("service-name");
            ServiceDisplayName = ServiceBase.GetAppSettings("service-displayname",  ServiceName + " service");
            AccountName        = ServiceBase.GetAppSettings("service-account-name", "LOCAL SERVICE");
        }

        public              void            Execute(ServiceBase service)
        {
            if (InstallMode == InstallMode.Install) {
                InstallMode = InstallMode.Install;
                if (!predefinedAccount || AccountName.IndexOf('\\') > 0) {
                    ReadPassword();
                    AccountLSASetServiceLogonRigh();
                }

                CreateEventSource();
                ServiceControl.ServiceDefine(ServiceName, ServiceDisplayName, ProgramExe, AccountName, AccountPassword);
                SetStandardFileFolderRights();
                SetLogDirectoryRights();
            }
            else {
                SetStandardFileFolderRights();
                ServiceControl.ServiceRemove(ServiceName);
            }
        }

        public              void            ReadPassword()
        {
            for (;;) {
                Console.Write("Password for " + AccountName + ": ");
                AccountPassword = Console.ReadLine();

                if (validatePassword())
                    return ;

                Console.WriteLine("Invalid password, try again");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         CreateEventSource()
        {
            try {
                Console.WriteLine("# Create event source: " + ServiceName);

                if (!System.Diagnostics.EventLog.SourceExists(ServiceName))
                    System.Diagnostics.EventLog.CreateEventSource(ServiceName, ServiceBase.EventLogName);
            }
            catch(Exception err) {
                err = new InstallerException("CreateEventSource('" + ServiceName + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         AccountLSASetServiceLogonRigh()
        {
            try {
                Console.WriteLine("# Set lsa policies SeServiceLogonRight on user: " + AccountName);

                using (LsaPolicy lsaPolicy = new LsaPolicy())
                    lsaPolicy.Set(AccountName, "SeServiceLogonRight");
            }
            catch(Exception err) {
                err = new InstallerException("AccountLSASetServiceLogonRigh('" + AccountName + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         AccountLSAResetAll()
        {
            try {
                Console.WriteLine("# remove lsa policies from user: " + AccountName);

                try {
                    using (LsaPolicy lsaPolicy = new LsaPolicy())
                        lsaPolicy.ResetAll(AccountName);
                }
                catch(Win32Exception err) {
                    if (err.NativeErrorCode != 1332)
                        throw;
                }
            }
            catch(Exception err) {
                err = new InstallerException("AccountLSAResetAll('" + AccountName + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         SetStandardFileFolderRights()
        {
            try {
                SetAclDirectory(ProgramDirectory, FileSystemRights.ReadAndExecute);

                string file = System.Configuration.ConfigurationManager.OpenExeConfiguration(ProgramExe).AppSettings.File;

                if (!string.IsNullOrEmpty(file))
                    SetAclFile(Path.Combine(ProgramDirectory, file), FileSystemRights.ReadAndExecute);
            }
            catch(Exception err) {
                err = new InstallerException("SetStandardFileFolderRights failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         SetLogDirectoryRights()
        {
            try {
                if (ServiceBase.GetAppSettings("service-debuglog", "0") == "1") {
                    string logDirectory = ServiceBase.GetAppSettings("logdirectory") + @"\" + ServiceName;

                    if (InstallMode == InstallMode.Install) {
                        try {
                            if (!Directory.Exists(logDirectory))
                                Directory.CreateDirectory(logDirectory);
                        }
                        catch(Exception err) {
                            throw new InstallerException("Create directory '" + logDirectory + "' failed.", err);
                        }
                    }

                    SetAclDirectory(logDirectory, FileSystemRights.Modify);
                }
            }
            catch(Exception err) {
                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         SetAclDirectory(string path, FileSystemRights rights)
        {
            try {
                Console.WriteLine((InstallMode == InstallMode.Install ? "# set acl on directory: " : "# remove acl on directory: ") + path);

                if (InstallMode == InstallMode.Install) {
                    CreatePath(path);

                    DirectorySecurity   acl  = Directory.GetAccessControl(path);
                    acl.SetAccessRule(new FileSystemAccessRule(AccountIdentity,
                                                               rights,
                                                               InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                               PropagationFlags.None,
                                                               AccessControlType.Allow));
                    Directory.SetAccessControl(path, acl);
                }
                else {
                    if (Directory.Exists(path)) {
                        DirectorySecurity   acl  = Directory.GetAccessControl(path);
                        acl.PurgeAccessRules(AccountIdentity);
                        Directory.SetAccessControl(path, acl);
                    }
                }
            }
            catch(Exception err) {
                if (err is DirectoryNotFoundException)
                    err = new DirectoryNotFoundException("Directory not found.");

                err = new InstallerException((InstallMode == InstallMode.Install ? "SetAclDirectory('" + path + "') failed." : "RemoveAclDirectory('" + path + "') failed."), err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         SetAclFile(string path, FileSystemRights rights)
        {
            try {
                Console.WriteLine((InstallMode == InstallMode.Install ? "# set acl on file: " : "# remove acl on file: ") + path);

                if (InstallMode == InstallMode.Install || File.Exists(path)) {
                    FileSecurity        acl = File.GetAccessControl(path);

                    if (InstallMode == InstallMode.Install) {
                        acl.SetAccessRule(new FileSystemAccessRule(AccountIdentity,
                                                                    rights,
                                                                    InheritanceFlags.None,
                                                                    PropagationFlags.None,
                                                                    AccessControlType.Allow));
                    }
                    else {
                        acl.PurgeAccessRules(AccountIdentity);
                    }

                    File.SetAccessControl(path, acl);
                }
            }
            catch(Exception err) {
                if (err is FileNotFoundException)
                    err = new FileNotFoundException("File not found.");

                err = new InstallerException((InstallMode == InstallMode.Install ? "SetAclFile('" + path + "') failed." : "RemoveAclFile('" + path + "') failed."), err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         DatabaseLoginUser()
        {
            try {
                string role = ServiceBase.GetAppSettings("service-database-role", null);

                if (role != null) {
                    DatabaseLoginUser(ServiceBase.GetAppSettings("database-server"), ServiceBase.GetAppSettings("database-name"), role);
                }
            }
            catch(Exception err) {
                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public              void            DatabaseLoginUser(string server, string database, string role)
        {
            try {
                if (AccountName.ToUpper() != "NT SERVICE")
                    return ;

                string accountName = "NT SERVICE\\" + ServiceName;

                using (SqlConnection sqlConnection = new SqlConnection("Server=" + server + ";Database=" + database + ";Current Language=us_english;Connection Reset=false;Connect Timeout=15;Pooling=No;Trusted_Connection=true"))
                {
                    sqlConnection.Open();

                    SqlCommand  sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text };

                    Console.WriteLine("# database drop user: " + accountName);
                    sqlCmd.CommandText = "IF EXISTS (SELECT * FROM sys.sysusers WHERE [name] = '" + accountName.Replace("'", "''") + "') DROP USER [" + accountName.Replace("]", "[]") + "]";
                    sqlCmd.ExecuteNonQuery();

                    Console.WriteLine("# database drop login: " + accountName);
                    sqlCmd.CommandText = "IF EXISTS (SELECT * FROM master.sys.server_principals WHERE [name] = '" + accountName.Replace("'", "''") + "') DROP LOGIN [" + accountName.Replace("]", "[]") + "]";
                    sqlCmd.ExecuteNonQuery();

                    if (InstallMode == InstallMode.Install) {
                        Console.WriteLine("# database create login: " + accountName);
                        sqlCmd.CommandText = "CREATE LOGIN [" + accountName.Replace("]", "[]") + "] FROM WINDOWS";
                        sqlCmd.ExecuteNonQuery();

                        Console.WriteLine("# database create user: " + accountName);
                        sqlCmd.CommandText = "CREATE USER [" + accountName.Replace("]", "[]") + "]";
                        sqlCmd.ExecuteNonQuery();

                        Console.WriteLine("# database add user to role: " + role);
                        sqlCmd.CommandText = "ALTER ROLE [" + role.Replace("]", "[]") + "] ADD MEMBER [" + accountName.Replace("]", "[]") + "]";
                        sqlCmd.ExecuteNonQuery();
                    }
                }
            }
            catch(Exception err) {
                err = new InstallerException("DatabaseLoginUser('" + role  + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         HttpUrlAcl(string binding)
        {
            try {
                Exec("netsh", "http delete urlacl url=" + binding, redirectOutput:true);

                if (InstallMode == InstallMode.Install)
                    Exec("netsh", "http add urlacl url=" + binding + " user=\"" + AccountFullName + "\"");
            }
            catch(Exception err) {
                err = new InstallerException("HttpUrlAcl('" + binding + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         FirewallRemoveRules()
        {
            try {
                PowerShell("$program = \"" + ProgramExe + "\"\r\n"  +
                            "foreach ($rule in @(Get-NetFirewallRule -All)) {\r\n" +
                            "    if (@(Get-NetFirewallApplicationFilter -AssociatedNetFirewallRule $rule).Where({($_.Program -eq $program)})) {\r\n" +
                            "        Write-Output (\"# drop firewall rule: \" + $rule.DisplayName)\r\n" +
                            "        Remove-NetFirewallRule -Name $rule.Name\r\n" +
                            "    }\r\n" +
                            "}");
            }
            catch(Exception err) {
                err = new InstallerException("FirewallRemoveRules failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2200:RethrowToPreserveStackDetails")]
        public void         FirewallAddRule(string name, string protocol, string port)
        {
            try {
                Console.WriteLine("# add firewall rule: " + name);
                PowerShell("$dummy=New-NetFirewallRule -DisplayName \"" + name + "\" -Direction Inbound -Enabled true -Protocol " + protocol + " -LocalPort " + port + " -Action Allow -Program \"" + ProgramExe + "\"");
            }
            catch(Exception err) {
                err = new InstallerException("FirewallAddRule('" + name + "') failed.", err);

                if (InstallMode == InstallMode.Install)
                    throw err;
                else
                    DisplayError(err);
            }
        }

        public              void            Exec(string filename, string arguments, bool redirectOutput=false)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName  = filename;
            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.RedirectStandardOutput = redirectOutput;
            proc.StartInfo.RedirectStandardError = false;
            proc.StartInfo.CreateNoWindow = false;
            Console.WriteLine("# " + filename + " " + arguments);
            proc.Start();

            if (redirectOutput)
                proc.StandardOutput.ReadToEnd();

            proc.WaitForExit();
        }
        public              void            PowerShell(string cmd)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName  = "powershell";
            proc.StartInfo.Arguments = "-ExecutionPolicy Bypass -Noprofile -encodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(cmd));
            proc.StartInfo.RedirectStandardOutput = false;
            proc.StartInfo.RedirectStandardError = false;
            proc.StartInfo.CreateNoWindow = false;
            proc.Start();
            proc.WaitForExit();
        }

        public              void            CreatePath(string path)
        {
            if (!Directory.Exists(path)) {
                CreatePath(Path.GetDirectoryName(path));

                Console.WriteLine("# createdirectory: " + path);
                Directory.CreateDirectory(path);
            }
        }

        public              bool            validatePassword()
        {
            string              domainName;
            string              userName;

            int     i = AccountName.IndexOf('\\');
            if (i >= 0) {
                domainName = AccountName.Substring(0, i);
                userName   = AccountName.Substring(i + 1);
            }
            else {
                domainName = null;
                userName   = AccountName;
            }

            using (System.DirectoryServices.AccountManagement.PrincipalContext pc = new System.DirectoryServices.AccountManagement.PrincipalContext(domainName != null ? System.DirectoryServices.AccountManagement.ContextType.Domain : System.DirectoryServices.AccountManagement.ContextType.Machine, domainName))
                return pc.ValidateCredentials(userName, AccountPassword);
        }

        public              void            DisplayError(Exception err)
        {
            Console.Write("ERROR:");
            while (err != null) {
                Console.Write(" " + err.Message);
                err = err.InnerException;
            }
            Console.WriteLine();
        }
    }

    [Serializable]
    public class InstallerException: Exception
    {
        public              InstallerException(string message) : base(message)
        {
        }
        public              InstallerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
