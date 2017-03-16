using System.Runtime.InteropServices;

namespace Lithnet.Moveuser
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LuidAndAttributes
    {
        public Luid Luid;
        public int Attributes;
    }
}
