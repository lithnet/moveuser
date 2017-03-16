using System.Security.AccessControl;

namespace Lithnet.Moveuser
{
    internal enum MappedGenericRights
    {
        FileGenericExecute = FileSystemRights.ExecuteFile | FileSystemRights.ReadPermissions | FileSystemRights.ReadAttributes | FileSystemRights.Synchronize,
        FileGenericRead = FileSystemRights.ReadAttributes | FileSystemRights.ReadData | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize,
        FileGenericWrite = FileSystemRights.AppendData | FileSystemRights.WriteAttributes | FileSystemRights.WriteData | FileSystemRights.WriteExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize,
        FileGenericAll = FileSystemRights.FullControl
    }
}
