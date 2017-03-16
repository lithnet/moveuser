using System.Text;

namespace Lithnet.Moveuser
{
    internal class AccountMigrationJob
    {
        public PrincipalObject SourceUserObject { get; set; } = new PrincipalObject();

        public PrincipalObject DestinationUserObject { get; set; } = new PrincipalObject();

        public ProfileMigrationStatus ProfileMigrationResult { get; set; }

        public StringBuilder MigrationResultText { get; set; } = new StringBuilder();
    }
}
