using System;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Lithnet.Moveuser
{
    internal class SystemManagement
    {
        private const int VerSuitePersonal = 0x200;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVersionEx(ref OsVersionInfoEx versionInfo);

        private static OsVersionInfoEx GetWindowsVersionDetails()
        {
            OsVersionInfoEx verInfo = new OsVersionInfoEx();
            verInfo.dwOSVersionInfoSize = Marshal.SizeOf(verInfo);

            if (!SystemManagement.GetVersionEx(ref verInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return verInfo;
        }

        internal static bool IsWinXp()
        {
            return Environment.OSVersion.Version.Major == 5 & Environment.OSVersion.Version.Minor == 1;
        }

        internal static bool IsWinXPorServer2003()
        {
            return Environment.OSVersion.Version.Major == 5 & Environment.OSVersion.Version.Minor >= 1;
        }

        internal static bool IsHomeEdition()
        {
            OsVersionInfoEx verInfo = GetWindowsVersionDetails();
            return verInfo.wSuiteMask == (verInfo.wSuiteMask | SystemManagement.VerSuitePersonal);
        }

        internal static bool IsWinVistaOrAbove()
        {
            return Environment.OSVersion.Version.Major >= 6;
        }

        internal static bool IsWinVistaSp1OrAbove()
        {
            if (Environment.OSVersion.Version.Major == 6)
            {
                if (Environment.OSVersion.Version.Minor < 1)
                {
                    return Environment.OSVersion.Version.Build >= 6001;
                }
                else
                {
                    return true;
                }
            }
            else if (Environment.OSVersion.Version.Major > 6)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static bool IsWin7OrAbove()
        {
            return Environment.OSVersion.Version.Major >= 6 & Environment.OSVersion.Version.Minor >= 1;
        }

        internal static void TerminateWithError(int code)
        {
            Environment.Exit(code);
        }

        internal static string GetActiveComputerName()
        {
            return Environment.MachineName;
        }
    }
}
