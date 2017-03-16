using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Lithnet.Moveuser
{
    internal class Advapi32
    {
        private const int TokenAdjustPrivileges = 0x20;
        private const int TokenQuery = 0x8;
        private const string SeRestoreName = "SeRestorePrivilege";
        private const string SeBackupName = "SeBackupPrivilege";
        private const string SeTakeOwnershipName = "SeTakeOwnershipPrivilege";
        private const int SePrivilegeEnabled = 0x2;
        private const int ErrorInsufficientBuffer = 122;
        private static bool takeOwnAcquired;

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetNamedSecurityInfo(string pObjectName, SeObjectType objectType, SecurityInformation securityInfo, IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetNamedSecurityInfo(string pObjectName, SeObjectType objectType, SecurityInformation securityInfo, ref IntPtr pSidOwner, ref IntPtr pSidGroup, ref IntPtr pDacl, ref IntPtr pSacl, ref IntPtr pSecurityDescriptor);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string stringSecurityDescriptor, uint stringSdRevision, ref IntPtr securityDescriptor, ref ulong securityDescriptorSize);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern bool ConvertStringSidToSid(string stringSid, ref IntPtr pSid);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref Luid lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, ref IntPtr tokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TokenPrivileges newState, int bufferLength, ref TokenPrivileges previousState, ref int returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupAccountName([In, MarshalAs(UnmanagedType.LPTStr)] string systemName, string lpAccountName, byte[] sid, ref int cbSid, StringBuilder referencedDomainName, ref int cchReferencedDomainName, ref SidNameUse peUse);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool LookupAccountSid([In, MarshalAs(UnmanagedType.LPTStr)] string systemName, byte[] sid, [Out, MarshalAs(UnmanagedType.LPTStr)] StringBuilder name, ref int cbName, StringBuilder referencedDomainName, ref int cbReferencedDomainName, ref SidNameUse use);

        private const uint HkeyClassesRoot = 0x80000000u;
        private const uint HkeyCurrentUser = 0x80000001u;
        private const uint HkeyLocalMachine = 0x80000002u;
        private const uint HkeyUsers = 0x80000003u;
        private const uint HkeyCurrentConfig = 0x80000005u;

        
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int ulOptions, int samDesired, ref IntPtr phkResult);

        [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegLoadKey(uint hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegUnLoadKey(uint hKey, string lpSubKey);

        internal static RegistryKey OpenWow64Key(RegistryKey parentKey, string subKeyName, bool writable, RegWow64Options options)
        {
            if (parentKey == null || Advapi32.GetRegistryKeyHandle(parentKey) == IntPtr.Zero)
            {
                return null;
            }

            int requestedrights = (int)RegistryRights.ReadKey;
            if (writable)
            {
                requestedrights = (int)RegistryRights.WriteKey;
            }

            IntPtr subKeyHandle = IntPtr.Zero;
            int result = RegOpenKeyEx(Advapi32.GetRegistryKeyHandle(parentKey), subKeyName, 0, requestedrights | (int)options, ref subKeyHandle);

            if (result != 0)
            {
                throw new Win32Exception(result);
            }

            RegistryKey subKey = Advapi32.PointerToRegistryKey(subKeyHandle, writable, false);
            return subKey;
        }

        private static IntPtr GetRegistryKeyHandle(RegistryKey registryKey)
        {
            Type keyType = typeof(RegistryKey);
            System.Reflection.FieldInfo fieldInfo = keyType.GetField("hkey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            SafeHandle handle = (SafeHandle)fieldInfo.GetValue(registryKey);
            IntPtr dangerousHandle = handle.DangerousGetHandle();
            return dangerousHandle;
        }

        private static RegistryKey PointerToRegistryKey(IntPtr hKey, bool writable, bool ownsHandle)
        {
            //Get the BindingFlags for private contructors
            System.Reflection.BindingFlags privateConstructors = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            //Get the Type for the SafeRegistryHandle
            Type safeRegistryHandleType = typeof(Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid).Assembly.GetType("Microsoft.Win32.SafeHandles.SafeRegistryHandle");
            //Get the array of types matching the args of the ctor we want
            Type[] safeRegistryHandleCtorTypes = new Type[] {
                typeof(IntPtr),
                typeof(bool)
            };
            //Get the constructorinfo for our object
            System.Reflection.ConstructorInfo safeRegistryHandleCtorInfo = safeRegistryHandleType.GetConstructor(privateConstructors, null, safeRegistryHandleCtorTypes, null);
            //Invoke the constructor, getting us a SafeRegistryHandle
            object safeHandle = safeRegistryHandleCtorInfo.Invoke(new object[] {
                hKey,
                ownsHandle
            });

            //Get the type of a RegistryKey
            Type registryKeyType = typeof(RegistryKey);
            //Get the array of types matching the args of the ctor we want
            Type[] registryKeyConstructorTypes = new Type[] {
                safeRegistryHandleType,
                typeof(bool)
            };
            //Get the constructorinfo for our object
            System.Reflection.ConstructorInfo registryKeyCtorInfo = registryKeyType.GetConstructor(privateConstructors, null, registryKeyConstructorTypes, null);
            //Invoke the constructor, getting us a RegistryKey
            RegistryKey resultKey = (RegistryKey)registryKeyCtorInfo.Invoke(new object[] {
                safeHandle,
                writable
            });
            //return the resulting key
            return resultKey;
        }

        internal static void LoadRegFileKey(string regFilePath, string keyName)
        {
            int retval = 0;

            Advapi32.AcquireBackupRestorePrivileges();
            retval = RegLoadKey(Advapi32.HkeyUsers, keyName, regFilePath);
            if (retval != 0)
            {
                throw new Win32Exception(retval);
            }
        }

        internal static void UnloadRegFile(string hkuKeyName)
        {
            int retval = 0;

            if (hkuKeyName.ToLower().StartsWith("hkey_users\\"))
            {
                hkuKeyName = hkuKeyName.Remove(0, "hkey_users\\".Length);
            }

            if (!(hkuKeyName.StartsWith("\\\\")))
            {
                hkuKeyName = hkuKeyName.Trim('\\');
            }
            else
            {
                hkuKeyName = hkuKeyName.TrimEnd('\\');
            }

            Advapi32.AcquireBackupRestorePrivileges();
            retval = RegUnLoadKey(Advapi32.HkeyUsers, hkuKeyName);
            if (retval != 0)
            {
                throw new Win32Exception(retval);
            }
        }

        internal static void ChangeOwner(string strPath, SeObjectType objectType, string sid)
        {
            IntPtr pNewOwner = IntPtr.Zero;
            int retval = 0;

            if (!ConvertStringSidToSid(sid, ref pNewOwner))
            {
                throw new System.Security.Principal.IdentityNotMappedException("Conversion of SID string '" + sid + "' to binary format failed");
            }

            if (!Advapi32.takeOwnAcquired)
            {
                AcquireTakeOwnershipPrivileges();
                Advapi32.takeOwnAcquired = true;
            }

            retval = SetNamedSecurityInfo(strPath, objectType, SecurityInformation.OwnerSecurityInformation, pNewOwner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (retval != 0)
            {
                if (retval == 5)
                {
                    throw new System.UnauthorizedAccessException("Access denied setting ownership on object " + strPath);
                }
                else
                {
                    throw new Win32Exception(retval);
                }
            }
        }

        internal static void ChangeOwnerandRecalculateDacl(string strPath, SeObjectType objectType, string sid)
        {
            IntPtr pNewOwner = IntPtr.Zero;
            int retval = 0;

            if (!ConvertStringSidToSid(sid, ref pNewOwner))
            {
                throw new System.Security.Principal.IdentityNotMappedException("Conversion of SID string '" + sid + "' to binary format failed");
            }

            IntPtr dacl = new IntPtr();
            IntPtr secInfo = new IntPtr();
            IntPtr ptrZero = IntPtr.Zero;

            retval = GetNamedSecurityInfo(strPath, objectType, SecurityInformation.DaclSecurityInformation, ref ptrZero, ref ptrZero, ref dacl, ref ptrZero, ref secInfo);

            if (retval != 0)
                throw new Win32Exception(retval);

            if (!Advapi32.takeOwnAcquired)
            {
                AcquireTakeOwnershipPrivileges();
                Advapi32.takeOwnAcquired = true;
            }

            retval = SetNamedSecurityInfo(strPath, objectType, SecurityInformation.OwnerSecurityInformation | SecurityInformation.DaclSecurityInformation, pNewOwner, IntPtr.Zero, dacl, IntPtr.Zero);
            if (retval != 0)
            {
                if (retval == 5)
                {
                    throw new System.UnauthorizedAccessException("Access denied setting ownership on object " + strPath);
                }
                else
                {
                    throw new Win32Exception(retval);
                }
            }
        }

        internal static void ReCaclulateDacl(string strPath, SeObjectType objectType)
        {
            IntPtr pNewOwner = IntPtr.Zero;
            int retval = 0;

            IntPtr dacl = new IntPtr();
            IntPtr secInfo = new IntPtr();
            IntPtr ptrZero = IntPtr.Zero;

            retval = GetNamedSecurityInfo(strPath, objectType, SecurityInformation.DaclSecurityInformation, ref ptrZero, ref ptrZero, ref dacl, ref ptrZero, ref secInfo);

            if (retval != 0)
                throw new Win32Exception(retval);

            retval = SetNamedSecurityInfo(strPath, objectType, SecurityInformation.DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, dacl, IntPtr.Zero);
            if (retval != 0)
            {
                if (retval == 5)
                {
                    throw new System.UnauthorizedAccessException("Access denied setting ACL on object " + strPath);
                }
                else
                {
                    throw new Win32Exception(retval);
                }
            }
        }

        internal static void AcquireTakeOwnershipPrivileges()
        {
            int lastWin32Error = 0;

            Luid luidTakeOwn = default(Luid);
            if (!LookupPrivilegeValue(null, Advapi32.SeTakeOwnershipName, ref luidTakeOwn))
            {
                lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception(lastWin32Error, "LookupPrivilegeValue failed with error " + lastWin32Error.ToString() + ".");
            }

            //Get the current process's token.
            IntPtr hProc = Process.GetCurrentProcess().Handle;
            IntPtr hToken = IntPtr.Zero;

            if (!OpenProcessToken(hProc, Advapi32.TokenAdjustPrivileges | Advapi32.TokenQuery, ref hToken))
            {
                lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception(lastWin32Error, "OpenProcessToken failed with error " + lastWin32Error.ToString() + ".");
            }

            try
            {
                //Set up a LUID_AND_ATTRIBUTES structure containing the Backup privilege, marked as enabled.
                LuidAndAttributes takeOwnAttr = new LuidAndAttributes();
                takeOwnAttr.Luid = luidTakeOwn;
                takeOwnAttr.Attributes = Advapi32.SePrivilegeEnabled;

                //Set up a TOKEN_PRIVILEGES structure containing the backup privilege.
                TokenPrivileges newState = new TokenPrivileges();
                newState.PrivilegeCount = 1;
                newState.Privileges = new LuidAndAttributes[] { takeOwnAttr };

                //Apply the TOKEN_PRIVILEGES structure to the current process's token.
                CallAdjustTokenPrivileges(hToken, newState);

            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        internal static void AcquireBackupRestorePrivileges()
        {
            int lastWin32Error = 0;

            //Get the LUID that corresponds to the Backup privilege, if it exists.
            Luid luidBackup = default(Luid);
            if (!LookupPrivilegeValue(null, Advapi32.SeBackupName, ref luidBackup))
            {
                lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception(lastWin32Error, "LookupPrivilegeValue failed with error " + lastWin32Error.ToString() + ".");
            }

            //Get the LUID that corresponds to the Restore privilege, if it exists.
            Luid luidRestore = default(Luid);
            if (!LookupPrivilegeValue(null, Advapi32.SeRestoreName, ref luidRestore))
            {
                lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception(lastWin32Error, "LookupPrivilegeValue failed with error " + lastWin32Error.ToString() + ".");
            }

            //Get the current process's token.
            IntPtr hProc = Process.GetCurrentProcess().Handle;
            IntPtr hToken = default(IntPtr);
            if (!OpenProcessToken(hProc, Advapi32.TokenAdjustPrivileges | Advapi32.TokenQuery, ref hToken))
            {
                lastWin32Error = Marshal.GetLastWin32Error();
                throw new Win32Exception(lastWin32Error, "OpenProcessToken failed with error " + lastWin32Error.ToString() + ".");
            }

            try
            {
                //Set up a LUID_AND_ATTRIBUTES structure containing the Backup privilege, marked as enabled.
                LuidAndAttributes backupAttr = new LuidAndAttributes();
                backupAttr.Luid = luidBackup;
                backupAttr.Attributes = Advapi32.SePrivilegeEnabled;

                //Set up a LUID_AND_ATTRIBUTES structure containing the Restore privilege, marked as enabled.
                LuidAndAttributes restoreAttr = new LuidAndAttributes();
                restoreAttr.Luid = luidRestore;
                restoreAttr.Attributes = Advapi32.SePrivilegeEnabled;

                //Set up a TOKEN_PRIVILEGES structure containing the backup privilege.
                TokenPrivileges newState = new TokenPrivileges();
                newState.PrivilegeCount = 1;
                newState.Privileges = new LuidAndAttributes[] { backupAttr };

                //Apply the TOKEN_PRIVILEGES structure to the current process's token.

                CallAdjustTokenPrivileges(hToken, newState);

                newState = new TokenPrivileges();
                newState.PrivilegeCount = 1;
                newState.Privileges = new LuidAndAttributes[] { restoreAttr };

                CallAdjustTokenPrivileges(hToken, newState);
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        private static void CallAdjustTokenPrivileges(IntPtr hToken, TokenPrivileges state)
        {
            int returnLength = 0;

            TokenPrivileges prevstate = new TokenPrivileges();
            prevstate.Privileges = new LuidAndAttributes[Convert.ToInt32(state.PrivilegeCount) + 1];

            if (!AdjustTokenPrivileges(hToken, false, ref state, Marshal.SizeOf(prevstate), ref prevstate, ref returnLength))
            {
                int lastwin32Error = Marshal.GetLastWin32Error();
                if (lastwin32Error != Advapi32.ErrorInsufficientBuffer)
                {
                    throw new Win32Exception(lastwin32Error);
                }

                if (!AdjustTokenPrivileges(hToken, false, ref state, returnLength, ref prevstate, ref returnLength))
                {
                    lastwin32Error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastwin32Error);
                }
            }
        }
        
        internal static void GetNameFromSid(System.Security.Principal.SecurityIdentifier sid, ref string accountName, ref string domain, ref SidNameUse use)
        {
            byte[] binarySid = new byte[sid.BinaryLength + 1];
            sid.GetBinaryForm(binarySid, 0);
            Advapi32.GetNameFromSid(binarySid, ref accountName, ref domain, ref use);
        }

        internal static System.Security.Principal.SecurityIdentifier GetSidFromName(string accountName, SidNameUse use)
        {
            int sidBuffer = 0;
            int domainbuffer = 0;
            StringBuilder retDomain = new StringBuilder();
            StringBuilder retAccountName = new StringBuilder();
            bool retval = false;
            byte[] sid = { };

            retval = LookupAccountName(null, accountName, null, ref sidBuffer, retDomain, ref domainbuffer, ref use);

            retDomain = new StringBuilder(domainbuffer);
            sid = new byte[sidBuffer + 1];

            if (!LookupAccountName(null, accountName, sid, ref sidBuffer, retDomain, ref domainbuffer, ref use))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else
            {
                return new System.Security.Principal.SecurityIdentifier(sid, 0);
            }
        }

        internal static void GetNameFromSid(string sid, ref string accountName, ref string domain, ref SidNameUse use)
        {
            System.Security.Principal.SecurityIdentifier siDprincipal = new System.Security.Principal.SecurityIdentifier(sid);
            byte[] binarySid = new byte[siDprincipal.BinaryLength + 1];
            siDprincipal.GetBinaryForm(binarySid, 0);
            Advapi32.GetNameFromSid(binarySid, ref accountName, ref domain, ref use);
        }

        internal static void GetNameFromSid(byte[] sid, ref string accountName, ref string domain, ref SidNameUse use)
        {
            int nameBuffer = 0;
            int domainbuffer = 0;
            StringBuilder retDomain = new StringBuilder();
            StringBuilder retAccountName = new StringBuilder();

            LookupAccountSid(null, sid, retAccountName, ref nameBuffer, retDomain, ref domainbuffer, ref use);

            retDomain = new StringBuilder(domainbuffer);
            retAccountName = new StringBuilder(nameBuffer);

            if (!LookupAccountSid(null, sid, retAccountName, ref nameBuffer, retDomain, ref domainbuffer, ref use))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else
            {
                accountName = retAccountName.ToString();
                domain = retDomain.ToString();
            }
        }
    }
}
