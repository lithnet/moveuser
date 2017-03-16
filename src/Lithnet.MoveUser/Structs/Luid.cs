using System.Runtime.InteropServices;

namespace Lithnet.Moveuser
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }
}
