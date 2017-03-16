using System;

namespace Lithnet.Moveuser
{
    [Flags]
    public enum ProfileMigrationStatus
    {
        Unknown = 0,
        NotMigrated = 1,
        Failed = 2,
        Migrated = 4,
        MigratedWithErrors = 8,
        MigratedWithWarnings = 16,
        Ignored = 32,
        ProfileInUse = 64,
        PendingMigration = 128,
        PendingMoveToTempProfile = 256,
        MovedToTempProfile = 512
    }
}
