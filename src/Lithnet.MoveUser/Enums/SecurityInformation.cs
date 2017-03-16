using System;

namespace Lithnet.Moveuser
{
    [Flags]
    public enum SecurityInformation : uint
    {
        OwnerSecurityInformation = 0x1,
        GroupSecurityInformation = 0x2,
        DaclSecurityInformation = 0x4,
        SaclSecurityInformation = 0x8,
        UnprotectedSaclSecurityInformation = 0x10000000,
        UnprotectedDaclSecurityInformation = 0x20000000,
        ProtectedSaclSecurityInformation = 0x40000000,
        ProtectedDaclSecurityInformation = 0x80000000u
    }
}
