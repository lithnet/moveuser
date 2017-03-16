using System;

namespace Lithnet.Moveuser
{
    [Flags]
    public enum ProfileMappingState
    {
        Unknown = 0,
        NotMapped = 1,
        ManuallyMapped = 2,
        AutoMapped = 4,
        Ignored = 8,
        NotRequired = 16,
        NotFoundInTargetDomain = 32
    }
}
