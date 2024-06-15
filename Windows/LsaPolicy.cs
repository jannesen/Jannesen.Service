using System;
using System.Runtime.InteropServices;

namespace Jannesen.Service.Windows
{
    sealed class LsaPolicy : IDisposable
    {
        private IntPtr      _policyHandle = IntPtr.Zero;

        public unsafe                   LsaPolicy()
        {
            _lsaOpen(null);
        }

        public unsafe                   LsaPolicy(string systemName)
        {
            NativeMethods.LSA_UNICODE_STRING    lsasSystemName = new NativeMethods.LSA_UNICODE_STRING(systemName);

            try {
                _lsaOpen(&lsasSystemName);
            }
            finally {
                lsasSystemName.Dispose();
            }
        }
                                        ~LsaPolicy()
        {
                    _dispose();
        }
        public              void        Dispose()
        {
            _dispose();
            GC.SuppressFinalize(this);
        }
        private             void        _dispose()
        {
            if (_policyHandle!=IntPtr.Zero) {
                NativeMethods.LsaClose(_policyHandle);
                _policyHandle = IntPtr.Zero;
            }
        }

        public              void        Set(String accountName, string userRight)
        {
            Set(Sid.AccountSid(accountName), userRight);
        }
        public  unsafe      void        Set(Sid sid, string userRight)
        {
            IntPtr                              psid          = sid.AllocHGlobal();
            NativeMethods.LSA_UNICODE_STRING    lsasUserRight = new NativeMethods.LSA_UNICODE_STRING(userRight);

            try {
                UInt32 rtn = NativeMethods.LsaAddAccountRights(_policyHandle, psid, &lsasUserRight, 1);
                if (rtn!=0)
                    throw NativeMethods.NewSystemError("LsaAddAccountRights failed", NativeMethods.LsaNtStatusToWinError(rtn));
            }
            finally {
                lsasUserRight.Dispose();
                Marshal.FreeHGlobal(psid);
            }
        }
        public              void        Reset(String accountName, string userRight)
        {
            Reset(Sid.AccountSid(accountName), userRight);
        }
        public  unsafe      void        Reset(Sid sid, string userRight)
        {
            IntPtr                              psid          = sid.AllocHGlobal();
            NativeMethods.LSA_UNICODE_STRING    lsasUserRight = new NativeMethods.LSA_UNICODE_STRING(userRight);

            try {
                UInt32 rtn = NativeMethods.LsaRemoveAccountRights(_policyHandle, psid, false, &lsasUserRight, 1);
                if (rtn!=0)
                    throw NativeMethods.NewSystemError("LsaRemoveAccountRights failed", NativeMethods.LsaNtStatusToWinError(rtn));
            }
            finally {
                lsasUserRight.Dispose();
                Marshal.FreeHGlobal(psid);
            }
        }
        public              void        ResetAll(String accountName)
        {
            ResetAll(Sid.AccountSid(accountName));
        }
        public  unsafe      void        ResetAll(Sid sid)
        {
            IntPtr                      psid          = sid.AllocHGlobal();

            try {
                UInt32 rtn = NativeMethods.LsaRemoveAccountRights(_policyHandle, psid, true, null, 0);
                if (rtn!=0)
                    throw NativeMethods.NewSystemError("LsaRemoveAccountRights failed", NativeMethods.LsaNtStatusToWinError(rtn));
            }
            finally {
                Marshal.FreeHGlobal(psid);
            }
        }

        private unsafe      void        _lsaOpen(NativeMethods.LSA_UNICODE_STRING* lsasSystemName)
        {
            NativeMethods.LSA_OBJECT_ATTRIBUTES lsaAttr;

            lsaAttr.Length                      = 0;
            lsaAttr.RootDirectory               = IntPtr.Zero;
            lsaAttr.ObjectName                  = null;
            lsaAttr.Attributes                  = 0;
            lsaAttr.SecurityDescriptor          = null;
            lsaAttr.SecurityQualityOfService    = null;
            lsaAttr.Length                      = (UInt32)Marshal.SizeOf(typeof(NativeMethods.LSA_OBJECT_ATTRIBUTES));

            UInt32 rtn = NativeMethods.LsaOpenPolicy(lsasSystemName, ref lsaAttr, NativeMethods.POLICY_ACCESS.CreateAccount|NativeMethods.POLICY_ACCESS.LookupNames, out var hLSA);
            if (rtn!=0)
                throw NativeMethods.NewSystemError("LsaOpenPolicy failed", NativeMethods.LsaNtStatusToWinError(rtn));

            _policyHandle = hLSA;
        }
    }
}
