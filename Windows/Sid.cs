using System;
using System.Runtime.InteropServices;

namespace Jannesen.Service.Windows
{
    public  struct  Sid: IEquatable<Sid>
    {
        private static  Sid                 _Null = new Sid();

        public      byte[]                  sid;

        public static Sid                   Nobody                              { get { return new Sid(0,0); } }
        public static Sid                   Everyone                            { get { return new Sid(1,0); } }
        public static Sid                   CreatorOwner                        { get { return new Sid(3,0); } }
        public static Sid                   CreatorGroup                        { get { return new Sid(3,1); } }
        public static Sid                   Dialup                              { get { return new Sid(5,1); } }
        public static Sid                   Network                             { get { return new Sid(5,2); } }
        public static Sid                   Batch                               { get { return new Sid(5,3); } }
        public static Sid                   Interactive                         { get { return new Sid(5,4); } }
        public static Sid                   Service                             { get { return new Sid(5,6); } }
        public static Sid                   Anonymous                           { get { return new Sid(5,7); } }
        public static Sid                   EnterpriseControllers               { get { return new Sid(5,9); } }
        public static Sid                   PrincipalSelf                       { get { return new Sid(5,10); } }
        public static Sid                   AuthenticatedUsers                  { get { return new Sid(5,11); } }
        public static Sid                   TerminalServerUsers                 { get { return new Sid(5,13); } }
        public static Sid                   LocalSystem                         { get { return new Sid(5,18); } }
        public static Sid                   Administrators                      { get { return new Sid(5,32,544); } }
        public static Sid                   Users                               { get { return new Sid(5,32,545); } }
        public static Sid                   Guests                              { get { return new Sid(5,32,546); } }
        public static Sid                   PowerUsers                          { get { return new Sid(5,32,547); } }
        public static Sid                   AccountOperators                    { get { return new Sid(5,32,548); } }
        public static Sid                   ServerOperators                     { get { return new Sid(5,32,549); } }
        public static Sid                   PrintOperators                      { get { return new Sid(5,32,550); } }
        public static Sid                   BackupOperators                     { get { return new Sid(5,32,551); } }
        public static Sid                   Administrator(Sid domain)           { return new Sid(5,domain,500); }
        public static Sid                   Guest(Sid domain)                   { return new Sid(5,domain,501); }
        public static Sid                   KRBTGT(Sid domain)                  { return new Sid(5,domain,502); }
        public static Sid                   DomainAdmins(Sid domain)            { return new Sid(5,domain,512); }
        public static Sid                   DomainUsers(Sid domain)             { return new Sid(5,domain,513); }
        public static Sid                   DomainGuests(Sid domain)            { return new Sid(5,domain,514); }
        public static Sid                   DomainComputers(Sid domain)         { return new Sid(5,domain,515); }
        public static Sid                   DomainControllers(Sid domain)       { return new Sid(5,domain,516); }
        public static Sid                   CertPublishers(Sid domain)          { return new Sid(5,domain,517); }
        public static Sid                   SchemaAdmins(Sid domain)            { return new Sid(5,domain,518); }
        public static Sid                   EnterpriseAdmins(Sid domain)        { return new Sid(5,domain,519); }
        public static Sid                   RASandIASServers(Sid domain)        { return new Sid(5,domain,553); }

        public                              Sid(byte[] sid)
        {
            this.sid = sid;
        }
        public                              Sid(UInt64 IA, UInt32 SA1)
        {
            sid = new byte[2+6+4];

            sid[ 0] = (byte)(1);
            sid[ 1] = (byte)(1);
            sid[ 2] = (byte)(IA>>40);
            sid[ 3] = (byte)(IA>>32);
            sid[ 4] = (byte)(IA>>24);
            sid[ 5] = (byte)(IA>>16);
            sid[ 6] = (byte)(IA>> 8);
            sid[ 7] = (byte)(IA);
            sid[ 8] = (byte)(SA1);
            sid[ 9] = (byte)(SA1>> 8);
            sid[10] = (byte)(SA1>>16);
            sid[11] = (byte)(SA1>>24);
        }
        public                              Sid(UInt64 IA, UInt32 SA1, UInt32 SA2)
        {
            sid = new byte[2+6+4+4];

            sid[ 0] = (byte)(1);
            sid[ 1] = (byte)(2);
            sid[ 2] = (byte)(IA>>40);
            sid[ 3] = (byte)(IA>>32);
            sid[ 4] = (byte)(IA>>24);
            sid[ 5] = (byte)(IA>>16);
            sid[ 6] = (byte)(IA>> 8);
            sid[ 7] = (byte)(IA);

            sid[ 8] = (byte)(SA1);
            sid[ 9] = (byte)(SA1>> 8);
            sid[10] = (byte)(SA1>>16);
            sid[11] = (byte)(SA1>>24);
            sid[12] = (byte)(SA2);
            sid[13] = (byte)(SA2>> 8);
            sid[14] = (byte)(SA2>>16);
            sid[15] = (byte)(SA2>>24);
        }
        public                              Sid(UInt64 IA, Sid domain, UInt32 SA2)
        {
            throw new NotImplementedException("Not implemented Sid(UInt64 IA, Sid domain, UInt32 SA2)");
        }
        internal unsafe                     Sid(byte* sid)
        {
            if (sid!=null) {
                if (sid[1]>15)
                    throw new Exception("Invalid SID");

                int     length = 2+6+4*sid[1];

                this.sid = new byte[length];

                for (int i = 0 ; i<length; ++i)
                    this.sid[i] = sid[i];
            }
            else
                this.sid = null;
        }

        public                  bool        IsNull
        {
            get {
                return sid==null;
            }
        }

        public static           Sid         AccountSid(string accountName)
       {
            if (accountName is null) throw new ArgumentNullException(nameof(accountName));

            if (accountName.IndexOf('\\') < 0)
                return AccountSid(null, accountName);

            using (System.DirectoryServices.DirectoryEntry entry = new System.DirectoryServices.DirectoryEntry("WinNT://" + accountName.Replace('\\', '/'))) {
                if (entry.Properties["objectSid"].Count == 0)
                    throw new Exception("Unknown domain/username '" + accountName + "'.");

                return new Sid((byte[])entry.Properties["objectSid"].Value);
            }
        }
        public static unsafe    Sid         AccountSid(string systemName, string accountName)
        {
            if (accountName is null) throw new ArgumentNullException(nameof(accountName));

            switch(accountName) {
            case "Nobody":                  return Nobody;
            case "Everyone":                return Everyone;
            case "CreatorOwner":            return CreatorOwner;
            case "CreatorGroup":            return CreatorGroup;
            case "Dialup":                  return Dialup;
            case "Network":                 return Network;
            case "Batch":                   return Batch;
            case "Interactive":             return Interactive;
            case "Service":                 return Service;
            case "Anonymous":               return Anonymous;
            case "EnterpriseControllers":   return EnterpriseControllers;
            case "PrincipalSelf":           return PrincipalSelf;
            case "AuthenticatedUsers":      return AuthenticatedUsers;
            case "TerminalServerUsers":     return TerminalServerUsers;
            case "LocalSystem":             return LocalSystem;
            case "Administrators":          return Administrators;
            case "Users":                   return Users;
            case "Guests":                  return Guests;
            case "PowerUsers":              return PowerUsers;
            case "AccountOperators":        return AccountOperators;
            case "ServerOperators":         return ServerOperators;
            case "PrintOperators":          return PrintOperators;
            case "BackupOperators":         return BackupOperators;

            default:
                {
                    System.Text.StringBuilder   DomainName   = new System.Text.StringBuilder(256);
                    UInt32                      cbDomainName = (UInt32)DomainName.Capacity;
                    NativeMethods.SidNameUse    Use = (NativeMethods.SidNameUse)0;

                    UInt32      cbSid = 96;
                    byte*       sid = stackalloc byte[(int)cbSid];

                    if (!NativeMethods.LookupAccountName(systemName, accountName, sid, &cbSid, DomainName, &cbDomainName, &Use))
                        throw NativeMethods.NewSystemError("System call LookupAccountName(\""+(systemName!=null ? systemName : ".")+"\",\"" + accountName + "\") failed");

                    return new Sid(sid);
                }
            }
        }
        public static           Sid         Null
        {
            get {
                return _Null;
            }
        }

        public                  IntPtr      AllocHGlobal()
        {
            IntPtr ptr = Marshal.AllocHGlobal(96);
            Marshal.Copy(sid, 0, ptr, sid.Length);
            return ptr;
        }

        public                  string      AccountName()
        {
            return AccountName(null);
        }
        public unsafe           string      AccountName(string systemName)
        {
            if (sid!=null) {
                System.Text.StringBuilder   Name         = new System.Text.StringBuilder(256);
                System.Text.StringBuilder   DomainName   = new System.Text.StringBuilder(256);
                UInt32                      cbName       = (UInt32)Name.Capacity;
                UInt32                      cbDomainName = (UInt32)DomainName.Capacity;
                NativeMethods.SidNameUse            Use          = (NativeMethods.SidNameUse)0;

                fixed(byte* psid = sid)
                {
                    if (!NativeMethods.LookupAccountSid(systemName, psid, Name, &cbName, DomainName, &cbDomainName, &Use))
                        throw NativeMethods.NewSystemError("System call LookupAccountSid failed");
                }

                Name.Length       = (int)cbName;

                if (cbDomainName>0) {
                    DomainName.Length = (int)cbDomainName;

                    return DomainName.ToString()+'\\'+Name.ToString();
                }
                else
                    return Name.ToString();
            }
            else
                return string.Empty;
        }

        public override         string      ToString()
        {
            System.Text.StringBuilder   rtn = new System.Text.StringBuilder(128);

            rtn.Append('S');
            rtn.Append('-');
            rtn.Append(sid[0]);
            rtn.Append('-');

            rtn.Append( (((UInt64)( (((UInt32)sid[2])<<8)
                                    |((UInt32)sid[3])
                                  )) <<32
                         ) |
                        (UInt64)( (((UInt32)sid[4])<<24)
                                 |(((UInt32)sid[5])<<16)
                                 |(((UInt32)sid[6])<<8)
                                  |((UInt32)sid[7])
                                )
                      );

            for (int i = 8 ; i < sid.Length ; i+=4) {
                rtn.Append('-');
                rtn.Append( (((UInt32)sid[i+3])<<24)
                           |(((UInt32)sid[i+2])<<16)
                           |(((UInt32)sid[i+1])<<8)
                           | ((UInt32)sid[i+0]));
            }

            return rtn.ToString();
        }

        public static           bool        operator==(Sid sid1,Sid sid2)
        {
            if (sid1.sid==null && sid2.sid==null)
                return true;

            if (sid1.sid!=null && sid2.sid!=null) {
                if (sid1.sid.Length==sid2.sid.Length) {
                    for (int i = 0 ; i<sid1.sid.Length ; ++i) {
                        if (sid1.sid[i]!=sid2.sid[i])
                            return false;
                    }

                    return true;
                }
            }

            return false;
        }
        public static           bool        operator!=(Sid sid1,Sid sid2)
        {
            return !(sid1==sid2);
        }
        public override         bool        Equals(object o)
        {
            if (o is Sid)
                return this==(Sid)o;

            return false;
        }
        public                  bool        Equals(Sid o)
        {
            return this == o;
        }
        public override         int         GetHashCode()
        {
            return (sid!=null) ? sid.GetHashCode() : 0;
        }

        private                 char        _IntToHex(int i)
        {
            if (i<10)
                return (char)('0'+i);
            else
                return (char)('A'+(i-10));
        }
    }
}
