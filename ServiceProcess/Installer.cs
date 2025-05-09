using System;
using System.ComponentModel;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Jannesen.Configuration.Settings;
using Jannesen.Service.Windows;
using Microsoft.Data.SqlClient;

#pragma warning disable CA1822 // CA1822: Mark members as static

namespace Jannesen.Service.ServiceProcess
{
    public enum InstallMode
    {
        Install,
        Uninstall
    }

    public class Installer
    {
        public              InstallMode         InstallMode             { get ; private set ; }
        public              string              ServiceName             { get ; private set ; }
        public              string              ServiceDisplayName      { get ; private set ; }
        public              string              AccountName             { get ; private set ; }
        public              string?             AccountPassword         { get ; private set ; }

        public              string              AccountFullName
        {
            get {
                if (AccountName.IndexOf('\\', StringComparison.Ordinal) > 0)
                    return AccountName;

                switch(AccountName.ToUpperInvariant()) {
                case "NT SERVICE":          return "NT SERVICE\\" + ServiceName;
                case "LOCAL SERVICE":
                case "NETWORK SERVICE":
                case "LOCAL SYSTEM":        return "NT AUTHORITY\\" + AccountName.ToUpperInvariant();
                default:                    return System.Environment.MachineName + "\\" + AccountName;
                }
            }
        }
        public              SecurityIdentifier? AccountSecurityIdentifier
        {
            get {
                try {
                    return (new NTAccount(AccountFullName)).Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;
                }
                catch(IdentityNotMappedException) {
                    return null;
                }
            }
        }
        public              bool                predefinedAccount
        {
            get {
                switch(AccountName.ToUpperInvariant()) {
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

        public                                  Installer(InstallMode installMode)
        {
            if (!(new WindowsPrincipal(WindowsIdentity.GetCurrent())) .IsInRole(WindowsBuiltInRole.Administrator)) {
                throw new System.Security.SecurityException("No administrator rights.");
            }

            InstallMode        = installMode;
            ServiceName        = AppSettings.GetSetting("service-name");
            ServiceDisplayName = AppSettings.GetSetting("service-displayname",  ServiceName + " service");
            AccountName        = AppSettings.GetSetting("service-account-name", "LOCAL SERVICE");
        }

        public              void                Execute()
        {
            if (InstallMode == InstallMode.Install) {
                InstallMode = InstallMode.Install;
                if (!predefinedAccount || AccountName.IndexOf('\\', StringComparison.Ordinal) > 0) {
                    ReadPassword();
                    AccountLSASetServiceLogonRigh();
                }

                CreateEventSource();
                ServiceControl.ServiceDefine(ServiceName, ServiceDisplayName, AppSettings.ProgramExe, AccountName, AccountPassword);
                SetStandardFileFolderRights();
                SetLogDirectoryRights();
            }
            else {
                SetStandardFileFolderRights();
                ServiceControl.ServiceRemove(ServiceName);
            }
        }

        public              void                ReadPassword()
        {
            for (;;) {
                Console.Write("Password for " + AccountName + ": ");
                AccountPassword = Console.ReadLine();

                if (validatePassword())
                    return ;

                Console.WriteLine("Invalid password, try again");
            }
        }

        public              void                CreateEventSource()
        {
            try {
                Console.WriteLine("# create event source: " + ServiceName);

                if (!System.Diagnostics.EventLog.SourceExists(ServiceName))
                    System.Diagnostics.EventLog.CreateEventSource(ServiceName, ServiceBase.EventLogName);
            }
            catch(Exception err) {
                Error(new InstallerException("CreateEventSource('" + ServiceName + "') failed.", err));
            }
        }

        public              void                AccountLSASetServiceLogonRigh()
        {
            try {
                Console.WriteLine("# Set lsa policies SeServiceLogonRight on user: " + AccountName);

                using (var lsaPolicy = new LsaPolicy())
                    lsaPolicy.Set(AccountName, "SeServiceLogonRight");
            }
            catch(Exception err) {
                Error(new InstallerException("AccountLSASetServiceLogonRigh('" + AccountName + "') failed.", err));
            }
        }
        public              void                AccountLSAResetAll()
        {
            try {
                Console.WriteLine("# remove lsa policies from user: " + AccountName);

                try {
                    using (var lsaPolicy = new LsaPolicy())
                        lsaPolicy.ResetAll(AccountName);
                }
                catch(Win32Exception err) {
                    if (err.NativeErrorCode != 1332)
                        throw;
                }
            }
            catch(Exception err) {
                Error(new InstallerException("AccountLSAResetAll('" + AccountName + "') failed.", err));
            }
        }

        public              void                SetStandardFileFolderRights()
        {
            try {
                SetAclDirectory(AppSettings.ProgramDirectory, FileSystemRights.ReadAndExecute);

                SetAclFile(AppSettings.ProgramExe, FileSystemRights.ReadAndExecute);

                foreach(var filename in AppSettings.ConfigFilenames) {
                    SetAclFile(filename, FileSystemRights.ReadAndExecute);
                }
            }
            catch(Exception err) {
                Error(new InstallerException("SetStandardFileFolderRights failed.", err));
            }
        }
        public              void                SetLogDirectoryRights()
        {
            try {
                if (AppSettings.GetSetting("service-debuglog", "0") == "1") {
                    var logDirectory = AppSettings.GetSetting("logdirectory") + @"\" + ServiceName;

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
                Error(err);
            }
        }
        public              void                SetAclDirectory(string path, FileSystemRights rights)
        {
            if (path != null) {
                try {
                    var si = AccountSecurityIdentifier;

                    if (InstallMode == InstallMode.Install) {
                        Console.WriteLine("# set acl on directory: " + path);
                        if (si == null) {
                            throw new IdentityNotMappedException("Can't map '" + AccountFullName + "' to SecurityIdentifier.");
                        }

                        CreatePath(path);

                        var dirInfo = new DirectoryInfo(path);
                        var acl     = dirInfo.GetAccessControl();
                        acl.SetAccessRule(new FileSystemAccessRule(si,
                                                                   rights,
                                                                   InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                                   PropagationFlags.None,
                                                                   AccessControlType.Allow));
                        dirInfo.SetAccessControl(acl);
                    }
                    else {
                        if (si != null && Directory.Exists(path)) {
                            Console.WriteLine("# remove acl on directory: " + path);
                            var dirInfo = new DirectoryInfo(path);
                            var acl     = dirInfo.GetAccessControl();
                            acl.PurgeAccessRules(si);
                            dirInfo.SetAccessControl(acl);
                        }
                    }
                }
                catch(Exception err) {
                    if (err is DirectoryNotFoundException)
                        err = new DirectoryNotFoundException("Directory not found.");

                    Error(new InstallerException((InstallMode == InstallMode.Install ? "SetAclDirectory('" + path + "') failed." : "RemoveAclDirectory('" + path + "') failed."), err));
                }
            }
        }
        public              void                SetAclFile(string? path, FileSystemRights rights)
        {
            if (path != null) {
                try {
                    var si = AccountSecurityIdentifier;


                    if (InstallMode == InstallMode.Install) {
                        Console.WriteLine("# set acl on file: " + path);
                        var fileInfo = new FileInfo(path);
                        var acl      = fileInfo.GetAccessControl();
                        if (si == null) {
                            throw new IdentityNotMappedException("Can't map '" + AccountFullName + "' to SecurityIdentifier.");
                        }

                        acl.SetAccessRule(new FileSystemAccessRule(si,
                                                                    rights,
                                                                    InheritanceFlags.None,
                                                                    PropagationFlags.None,
                                                                    AccessControlType.Allow));
                        fileInfo.SetAccessControl(acl);
                    }
                    else {
                        if (si != null && File.Exists(path)) {
                            Console.WriteLine("# remove acl on file: " + path);
                            var fileInfo = new FileInfo(path);
                            var acl      = fileInfo.GetAccessControl();
                            acl.PurgeAccessRules(si);
                            fileInfo.SetAccessControl(acl);
                        }
                    }
                }
                catch(Exception err) {
                    if (err is FileNotFoundException)
                        err = new FileNotFoundException("File not found.");

                    Error(new InstallerException((InstallMode == InstallMode.Install ? "SetAclFile('" + path + "') failed." : "RemoveAclFile('" + path + "') failed."), err));
                }
            }
        }

        public              void                DatabaseLoginUser()
        {
            try {
                var role = AppSettings.GetSetting("service-database-role", null);

                if (role != null) {
                    DatabaseLoginUser(AppSettings.GetSetting("database-server"), AppSettings.GetSetting("database-name"), role);
                }
            }
            catch(Exception err) {
                Error(err);
            }
        }
        public              void                DatabaseLoginUser(string server, string database, string role)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(role);

            try {
                if (string.Compare(AccountName, "NT SERVICE", StringComparison.OrdinalIgnoreCase) != 0)
                    return ;

                var accountName = "NT SERVICE\\" + ServiceName;

                using (var sqlConnection = new SqlConnection("Server="     + server           +
                                                             ";Database="  + database         +
                                                             ";Current Language=us_english"   +
                                                             ";Connect Timeout=15"            +
                                                             ";Connect Retry Count=0"         +
                                                             ";Integrated Security=true"      +
                                                             ";Trust Server Certificate=true")) {
                    sqlConnection.Open();

                    using (var sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text }) {
                        Console.WriteLine("# database drop user: " + accountName);
                        sqlCmd.CommandText = "IF EXISTS (SELECT * FROM sys.sysusers WHERE [name] = '" + accountName.Replace("'", "''", StringComparison.Ordinal) + "') DROP USER [" + accountName.Replace("]", "[]", StringComparison.Ordinal) + "]";
                        sqlCmd.ExecuteNonQuery();
                    }

                    using (var sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text }) {
                        Console.WriteLine("# database drop login: " + accountName);
                        sqlCmd.CommandText = "IF EXISTS (SELECT * FROM master.sys.server_principals WHERE [name] = '" + accountName.Replace("'", "''", StringComparison.Ordinal) + "') DROP LOGIN [" + accountName.Replace("]", "[]", StringComparison.Ordinal) + "]";
                        sqlCmd.ExecuteNonQuery();
                    }

                    if (InstallMode == InstallMode.Install) {
                        using (var sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text }) {
                            Console.WriteLine("# database create login: " + accountName);
                            sqlCmd.CommandText = "CREATE LOGIN [" + accountName.Replace("]", "[]", StringComparison.Ordinal) + "] FROM WINDOWS";
                            sqlCmd.ExecuteNonQuery();
                        }

                        using (var sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text }) {
                            Console.WriteLine("# database create user: " + accountName);
                            sqlCmd.CommandText = "CREATE USER [" + accountName.Replace("]", "[]", StringComparison.Ordinal) + "]";
                            sqlCmd.ExecuteNonQuery();
                        }

                        using (var sqlCmd = new SqlCommand() { Connection = sqlConnection, CommandType = System.Data.CommandType.Text }) {
                            Console.WriteLine("# database add user to role: " + role);
                            sqlCmd.CommandText = "ALTER ROLE [" + role.Replace("]", "[]", StringComparison.Ordinal) + "] ADD MEMBER [" + accountName.Replace("]", "[]", StringComparison.Ordinal) + "]";
                            sqlCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch(Exception err) {
                Error(new InstallerException("DatabaseLoginUser('" + role  + "') failed.", err));
            }
        }

        public              void                HttpUrlAcl(string binding)
        {
            ArgumentNullException.ThrowIfNull(binding);

            try {
                Exec("netsh", "http delete urlacl url=" + binding, redirectOutput:true);

                if (InstallMode == InstallMode.Install)
                    Exec("netsh", "http add urlacl url=" + binding + " user=\"" + AccountFullName + "\"");
            }
            catch(Exception err) {
                Error(new InstallerException("HttpUrlAcl('" + binding + "') failed.", err));
            }
        }

        public              void                FirewallRemoveRules()
        {
            try {
                PowerShell("$program = \"" + AppSettings.ProgramExe + "\"\r\n"  +
                            "foreach ($rule in @(Get-NetFirewallRule -All)) {\r\n" +
                            "    if (@(Get-NetFirewallApplicationFilter -AssociatedNetFirewallRule $rule).Where({($_.Program -eq $program)})) {\r\n" +
                            "        Write-Output (\"# drop firewall rule: \" + $rule.DisplayName)\r\n" +
                            "        Remove-NetFirewallRule -Name $rule.Name\r\n" +
                            "    }\r\n" +
                            "}");
            }
            catch(Exception err) {
                Error(new InstallerException("FirewallRemoveRules failed.", err));
            }
        }

        public              void                FirewallAddRule(string name, string protocol, string port)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(protocol);
            ArgumentNullException.ThrowIfNull(port);

            try {
                Console.WriteLine("# add firewall rule: " + name);
                PowerShell("$dummy=New-NetFirewallRule -DisplayName \"" + name + "\" -Direction Inbound -Enabled true -Protocol " + protocol + " -LocalPort " + port + " -Action Allow -Program \"" + AppSettings.ProgramExe + "\"");
            }
            catch(Exception err) {
                Error(new InstallerException("FirewallAddRule('" + name + "') failed.", err));
            }
        }

        public              void                Exec(string filename, string arguments, bool redirectOutput=false)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ArgumentNullException.ThrowIfNull(arguments);

            using (var proc = new System.Diagnostics.Process()) {
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
        }
        public              void                PowerShell(string cmd)
        {
            ArgumentNullException.ThrowIfNull(cmd);

            using(var proc = new System.Diagnostics.Process()) {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName  = "powershell";
                proc.StartInfo.Arguments = "-ExecutionPolicy Bypass -Noprofile -encodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(cmd));
                proc.StartInfo.RedirectStandardOutput = false;
                proc.StartInfo.RedirectStandardError = false;
                proc.StartInfo.CreateNoWindow = false;
                proc.Start();
                proc.WaitForExit();
            }
        }

        public              void                CreatePath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            if (!Directory.Exists(path)) {
                var p = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(p)) {
                    CreatePath(p);
                }

                Console.WriteLine("# createdirectory: " + path);
                Directory.CreateDirectory(path);
            }
        }

        public              bool                validatePassword()
        {
            string? domainName;
            string  userName;

            var i = AccountName.IndexOf('\\', StringComparison.Ordinal);
            if (i >= 0) {
                domainName = AccountName.Substring(0, i);
                userName   = AccountName.Substring(i + 1);
            }
            else {
                domainName = null;
                userName   = AccountName;
            }

            using (var pc = new System.DirectoryServices.AccountManagement.PrincipalContext(domainName != null ? System.DirectoryServices.AccountManagement.ContextType.Domain : System.DirectoryServices.AccountManagement.ContextType.Machine, domainName))
                return pc.ValidateCredentials(userName, AccountPassword);
        }

        public              void                Error(Exception err)
        {
            if (InstallMode == InstallMode.Install)
                throw err;
            else
                DisplayError(err);
        }
        public              void                DisplayError(Exception? err)
        {
            Console.Write("ERROR:");
            while (err != null) {
                Console.Write(" " + err.Message);
                err = err.InnerException;
            }
            Console.WriteLine();
        }
    }

    public class InstallerException: Exception
    {
        public              InstallerException(string message): base(message)
        {
        }
        public              InstallerException(string message, Exception innerException): base(message, innerException)
        {
        }
    }
}
