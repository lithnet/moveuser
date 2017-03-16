using System.Security.Principal;

namespace Lithnet.Moveuser
{
    internal struct SidMapping
    {
        internal SecurityIdentifier SourceSid;
        internal SecurityIdentifier DestinationSid;
    }
}
