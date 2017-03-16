using System;

namespace Lithnet.Moveuser
{
    [Flags]
    internal enum RegistryRights : int
    {
        ReadKey = 131097,
        WriteKey = 131078
    }
}
