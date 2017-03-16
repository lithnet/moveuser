using System.Runtime.InteropServices;

namespace Lithnet.Moveuser
{
    internal static class Profmap
    {
        // http://msdn.microsoft.com/en-us/library/cc843962(v=WS.10).aspx

        private const int RemapProfileKeeplocalaccount = 0x4;

        [DllImport("profmap.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool RemapAndMoveUser([MarshalAs(UnmanagedType.LPWStr)]string pComputer, int dwFlags, [MarshalAs(UnmanagedType.LPWStr)]string pCurrentUser, [MarshalAs(UnmanagedType.LPWStr)]string pNewUser);

        internal static int ReMapUserProfileXp(string computer, string currentUser, string newUser, bool keepCurrentAccount)
        {
            int flags = 0;

            if (keepCurrentAccount)
            {
                flags = Profmap.RemapProfileKeeplocalaccount;
            }

            if (RemapAndMoveUser(null, flags, currentUser, newUser))
            {
                return 0;
            }
            else
            {
                return Marshal.GetLastWin32Error();
            }
        }
    }
}