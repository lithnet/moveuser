using System.Runtime.InteropServices;

namespace Lithnet.Moveuser
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TokenPrivileges
    {
        public int PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray)]
        public LuidAndAttributes[] Privileges;
    }
}
