using System;

namespace Lithnet.Moveuser
{
    [Flags]
    public enum AccountState
    {
        Unknown = 0,
        Active = 1,
        Disabled = 2,
        Deleted = 4
    }
}
