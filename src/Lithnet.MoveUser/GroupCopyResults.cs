using System.Collections.Generic;

namespace Lithnet.Moveuser
{
    internal class GroupCopyResults
    {
        internal List<string> SourceGroups { get; set; } = new List<string>();

        internal List<string> AddedToGroups { get; set; } = new List<string>();

        internal List<string> NotAddedToGroups { get; set; } = new List<string>();
    }
}
