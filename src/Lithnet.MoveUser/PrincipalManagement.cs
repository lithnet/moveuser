using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace Lithnet.Moveuser
{
    internal class PrincipalManagement
    {
        internal static SecurityIdentifier GetObjectSidFromAd(string samAccountName, string domainDnsName, string domainUserAccount, string domainUserPassword)
        {

            PrincipalContext directoryContext = new PrincipalContext(ContextType.Domain, domainDnsName, domainUserAccount, domainUserPassword);

            Principal targetPrincipal = Principal.FindByIdentity(directoryContext, IdentityType.SamAccountName, samAccountName);

            if (targetPrincipal == null)
            {
                string errorMsg = "Cannot find SID for object " + samAccountName + " in domain " + domainDnsName;
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                return targetPrincipal.Sid;
            }
        }

        internal static bool TryGetUserSid(PrincipalObject user, ref SecurityIdentifier sid)
        {
            //Calls GetUserSID, and if the call fails, returns nothing
            try
            {
                sid = PrincipalManagement.GetUserSid(user);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static SecurityIdentifier GetUserSid(PrincipalObject user)
        {
            //Trys to retrieve a SID using the translation capabilities and knowledge of the local computer
            NTAccount x = new NTAccount(user.FqUserNameExpanded);
            return (SecurityIdentifier)x.Translate(typeof(SecurityIdentifier));
        }

        internal static List<PrincipalObject> GetUserGroupMemberhipOnLocalMachine(PrincipalObject user)
        {
            PrincipalContext principalContext;
            List<PrincipalObject> groupList = new List<PrincipalObject>();

            if (user.IsLocalUser())
            {
                principalContext = new PrincipalContext(ContextType.Machine, SystemManagement.GetActiveComputerName());
            }
            else
            {
                principalContext = new PrincipalContext(ContextType.Domain, user.Domain);
            }

            ////NOTE: This function will remove the domain component of the identity, and match based on username only
            UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(principalContext, IdentityType.SamAccountName, user.FqUserNameExpanded);
            if (userPrincipal == null)
            {
                string errorMsg = "Cannot find user " + user.FqUserNameExpanded + " on local computer";
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                PrincipalSearchResult<Principal> groups = userPrincipal.GetAuthorizationGroups();

                foreach (Principal group in groups)
                {
                    PrincipalObject groupObject = new PrincipalObject
                    {
                        AccountName = group.SamAccountName,
                        SidPrincipal = group.Sid
                    };

                    groupList.Add(groupObject);
                }

                return groupList;
            }
        }

        internal static List<PrincipalObject> GetUserGroupMemberhipOnLocalMachine(SecurityIdentifier sid)
        {
            PrincipalContext principalContext = new PrincipalContext(ContextType.Machine, SystemManagement.GetActiveComputerName());
            UserPrincipal userPrincipal = default(UserPrincipal);
            List<PrincipalObject> groupList = new List<PrincipalObject>();

            userPrincipal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Sid, sid.ToString());
            if (userPrincipal == null)
            {
                string errorMsg = $"Cannot find user {sid} on local computer";
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                PrincipalSearchResult<Principal> groups = userPrincipal.GetAuthorizationGroups();

                foreach (Principal g in groups)
                {
                    PrincipalObject groupObject = new PrincipalObject
                    {
                        AccountName = g.SamAccountName,
                        SidPrincipal = g.Sid
                    };

                    groupList.Add(groupObject);
                }

                return groupList;
            }
        }

        internal static List<PrincipalObject> GetMembersOfLocalGroup(string groupName)
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName);
            List<PrincipalObject> groupMembers = new List<PrincipalObject>();

            DirectoryEntry grp = localDirectory.Children.Find(groupName, "group");

            foreach (DirectoryEntry member in (IEnumerable)grp.Invoke("Members"))
            {
                PropertyCollection memberpropcoll = member.Properties;
                byte[] objectSid = (byte[])memberpropcoll["objectSid"].Value;

                if (objectSid == null)
                {
                    continue;
                }

                SecurityIdentifier sidPrincipal = new SecurityIdentifier(objectSid, 0);

                try
                {
                    NTAccount ntAccount = (NTAccount)sidPrincipal.Translate(typeof(NTAccount));
                    groupMembers.Add(new PrincipalObject(ntAccount.ToString(), objectSid));
                }
                catch
                {
                    groupMembers.Add(new PrincipalObject(string.Empty, objectSid));
                }
            }

            return groupMembers;
        }

        internal static List<string> GetUserGroupMembershipFromAd(string samAccountName, string domainNetBiosName, string domainDnsName, string domainSearchAccount, string domainSearchAccountPassword)
        {

            PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, domainDnsName, domainSearchAccount, domainSearchAccountPassword);
            List<string> groupList = new List<string>();

            UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(principalContext, IdentityType.SamAccountName, samAccountName);

            if (userPrincipal == null)
            {
                string errorMsg = "Cannot find user " + samAccountName + " in domain " + domainDnsName;
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                PrincipalSearchResult<Principal> groups = userPrincipal.GetAuthorizationGroups();

                foreach (Principal group in groups)
                {
                    groupList.Add(domainNetBiosName + "\\" + group.SamAccountName);
                }

                return groupList;
            }
        }

        internal static PrincipalObject GetUserObjectFromAd(string accountName, string domainNetBiosName, string domainDnsName, string domainSearchAccount, string domainSearchAccountPassword)
        {

            PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, domainDnsName, domainSearchAccount, domainSearchAccountPassword);

            PrincipalObject user = new PrincipalObject();

            UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(principalContext, IdentityType.SamAccountName, accountName);

            if (userPrincipal == null)
            {
                string errorMsg = "Cannot find user " + accountName + " in domain " + domainDnsName;
                throw new NoMatchingPrincipalException(errorMsg);

            }
            else
            {
                user.Sid = userPrincipal.Sid.ToString();
                user.AccountName = userPrincipal.SamAccountName;
                user.Domain = domainNetBiosName;

                return user;
            }
        }

        internal static List<PrincipalObject> GetUserGroupMembershipFromAd(PrincipalObject user)
        {

            PrincipalContext principalContext = new PrincipalContext(ContextType.Domain);
            UserPrincipal userPrincipal = default(UserPrincipal);
            List<PrincipalObject> groupList = new List<PrincipalObject>();

            userPrincipal = UserPrincipal.FindByIdentity(principalContext, IdentityType.SamAccountName, user.FqUserName);

            if (userPrincipal == null)
            {
                string errorMsg = "Cannot find user " + user.FqUserName;
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                foreach (GroupPrincipal groupprincipal in userPrincipal.GetAuthorizationGroups())
                {
                    PrincipalObject g = new PrincipalObject();
                    g.SidPrincipal = groupprincipal.Sid;
                    string groupName = string.Empty;
                    string groupDomainName = string.Empty;

                    SidNameUse sidNameType = SidNameUse.SidTypeNone;

                    try
                    {
                        Advapi32.GetNameFromSid(groupprincipal.Sid, ref groupName, ref groupDomainName, ref sidNameType);
                        g.FqUserName = groupDomainName + "\\" + groupName;
                    }
                    catch
                    {
                        // unable to translate sid to name
                    }

                    groupList.Add(g);
                }
            }

            return groupList;
        }

        internal static void ResetUserProfileRefCount(string userSid)
        {
            if (Helper.StringIsNullOrWhiteSpace(userSid))
            {
                return;
            }

            RegistryKey mRegKey = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + userSid, true);

            mRegKey?.SetValue("RefCount", 0);
        }

        internal static void DeleteLocalAccount(string userAccountName)
        {
            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);
            DirectoryEntry userDirectoryEntry = default(DirectoryEntry);

            userDirectoryEntry = directoryEntry.Children.Find(userAccountName, "user");
            directoryEntry.Children.Remove(userDirectoryEntry);
            directoryEntry.Close();
        }

        internal static void DeleteLocalAccount(PrincipalObject user)
        {
            string accountname = string.Empty;
            string machinename = Environment.MachineName;
            SidNameUse use = SidNameUse.SidTypeUser;

            if (!user.AccountName.IsNullOrWhiteSpace())
            {
                accountname = user.AccountName;
            }
            else
            {
                Advapi32.GetNameFromSid(user.SidPrincipal, ref accountname, ref machinename, ref use);
            }

            DeleteLocalAccount(accountname);
        }


        internal static void DisableLocalAccount(PrincipalObject user)
        {
            string accountname = string.Empty;
            string machinename = Environment.MachineName;
            SidNameUse use = SidNameUse.SidTypeUser;

            if (!user.AccountName.IsNullOrWhiteSpace())
            {
                accountname = user.AccountName;

            }
            else
            {
                Advapi32.GetNameFromSid(user.SidPrincipal, ref accountname, ref machinename, ref use);
            }

            DisableLocalAccount(accountname);
        }

        internal static void DisableLocalAccount(string userAccountName)
        {
            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);

            DirectoryEntry userDirectoryEntry = directoryEntry.Children.Find(userAccountName, "user");
            int val = (int)userDirectoryEntry.Properties["UserFlags"].Value;
            userDirectoryEntry.Properties["UserFlags"].Value = val | 0x2; //ADS_UF_ACCOUNTDISABLE;

            userDirectoryEntry.CommitChanges();

            directoryEntry.Close();
        }

        internal static PrincipalObject GetUserObjectFromLocalComputer(string userName)
        {
            PrincipalObject userObject = new PrincipalObject(userName);
            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);
            DirectoryEntry newUser = null;
            PrincipalObject newObject = new PrincipalObject();

            try
            {
                newUser = directoryEntry.Children.Find(userObject.AccountName, "user");
            }
            catch
            {
            }

            if (newUser == null)
            {
                string errorMsg = "The following account does not exist on the local computer: " + userObject.AccountName;
                throw new NoMatchingPrincipalException(errorMsg);
            }
            else
            {
                newObject.AccountName = newUser.Name;
                newObject.Domain = ".";

                PropertyCollection memberpropcoll = newUser.Properties;
                byte[] objectSid = (byte[])memberpropcoll["objectSid"].Value;

                newObject.Sid = new SecurityIdentifier(objectSid, 0).ToString();
                return newObject;
            }
        }

        internal static List<PrincipalObject> GetAccountsFromLocalComputer()
        {
            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);
            List<PrincipalObject> userList = new List<PrincipalObject>();

            foreach (DirectoryEntry user in directoryEntry.Children)
            {
                if (user.SchemaClassName.ToLower() == "user")
                {
                    userList.Add(new PrincipalObject(user.Name));
                }
            }

            return userList;
        }

        internal static bool IsUserAMemberOfBuiltInAdminsGroup(PrincipalObject user)
        {
            List<PrincipalObject> groupList = new List<PrincipalObject>();

            if (user.IsLocalUser())
            {
                groupList = GetUserGroupMemberhipOnLocalMachine(user);
            }
            else
            {
                groupList = PrincipalManagement.GetUserGroupMembershipFromAd(user);
            }

            foreach (PrincipalObject group in groupList)
            {
                if (group.SidPrincipal.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool DoesLocalUserExist(string userAccountName)
        {
            PrincipalObject userObject = new PrincipalObject(userAccountName);

            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);
            DirectoryEntry newUser = null;
            try
            {
                newUser = directoryEntry.Children.Find(userObject.AccountName, "user");
            }
            catch
            {
            }

            if (newUser == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        internal static bool DoesDomainUserExist(PrincipalObject user, string domainDnsName, string searchAccount, string searchAccountPassword)
        {
            PrincipalContext directoryContext = new PrincipalContext(ContextType.Domain, domainDnsName, searchAccount, searchAccountPassword);

            if (UserPrincipal.FindByIdentity(directoryContext, IdentityType.SamAccountName, user.AccountName) == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        internal static void AddUserToLocalGroup(string groupName, PrincipalObject user)
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName);
            DirectoryEntry userDirectory = default(DirectoryEntry);

            if (user.Domain == ".")
            {
                userDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName);
            }
            else
            {
                userDirectory = new DirectoryEntry("WinNT://" + user.Domain);
            }

            DirectoryEntry grp = default(DirectoryEntry);
            DirectoryEntry userDe = default(DirectoryEntry);

            userDe = userDirectory.Children.Find(user.AccountName, "user");

            PropertyCollection userDEpropcoll = userDe.Properties;
            byte[] userDesid = (byte[]) userDEpropcoll["objectSid"].Value;

            grp = localDirectory.Children.Find(groupName, "group");

            foreach (DirectoryEntry member in (IEnumerable) grp.Invoke("Members"))
            {
                PropertyCollection memberpropcoll = member.Properties;
                byte[] obVal = (byte[]) memberpropcoll["objectSid"].Value;

                if (new SecurityIdentifier(obVal, 0).Value == new SecurityIdentifier(userDesid, 0).Value)
                {
                    return;
                }
            }


            DirectoryEntry de = new DirectoryEntry(grp.NativeObject);
            de.Invoke("Add", new object[] {userDe.Path.ToString()});
        }

        internal static void AddSidToLocalGroup(string groupName, SecurityIdentifier userSid)
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName);

            DirectoryEntry grp = localDirectory.Children.Find(groupName, "group");

            try
            {
                string userPath = "WINNT://" + userSid.ToString();
                grp.Invoke("Add", new object[] { userPath });
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                if (ex.InnerException == null)
                {
                    throw;
                }

                COMException ce = ex.InnerException as COMException;

                // Const ERR_ALREADY_MEMBER
                if (ce?.ErrorCode == -2147023518)
                {
                    return;
                }

                throw;
            }
        }

        internal static void AddSidToLocalGroup(string groupName, string userSid)
        {
            PrincipalManagement.AddSidToLocalGroup(groupName, new SecurityIdentifier(userSid));
        }

        internal static void CreateLocalAccount(string username, string password, bool administrator)
        {
            DirectoryEntry directoryEntry = new DirectoryEntry("WinNT://" + Environment.MachineName);
            DirectoryEntry newUser = null;

            try
            {
                newUser = directoryEntry.Children.Find(username, "user");
            }
            catch
            {
            }

            if (newUser == null)
            {
                newUser = directoryEntry.Children.Add(username, "user");
                newUser.Invoke("SetPassword", new object[] { password });
                newUser.CommitChanges();
                newUser.Close();
            }
            directoryEntry.Close();

            string addToGroup = null;

            if (administrator)
                addToGroup = "Administrators";
            else
                addToGroup = "users";

            PrincipalManagement.AddUserToLocalGroup(addToGroup, new PrincipalObject(username));
        }

        internal static System.Management.ManagementObject GetWin32UserProfileObject(string sid)
        {
            System.Management.ManagementScope wmiScope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
            System.Management.ObjectQuery wmiQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_UserProfile WHERE SID='" + sid + "'");
            System.Management.ManagementObjectSearcher wmiSearcher = new System.Management.ManagementObjectSearcher(wmiScope, wmiQuery);

            foreach (System.Management.ManagementObject objMgmt in wmiSearcher.Get())
            {
                if (string.Equals(objMgmt.GetPropertyValue("SID").ToString(), sid, StringComparison.CurrentCultureIgnoreCase))
                {
                    return objMgmt;
                }
            }

            return null;
        }

        internal static List<PrincipalObject> GetListOfUsersWithLocalProfiles(bool excludeSystem)
        {
            List<PrincipalObject> profileList = new List<PrincipalObject>();

            // Get the list of profiles from the registry
            RegistryKey regKeyProfileList = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList", false);

            if (regKeyProfileList != null)
            {
                foreach (string regKeyNameLoopVariable in regKeyProfileList.GetSubKeyNames())
                {
                    string regKeyName = regKeyNameLoopVariable;
                    PrincipalObject userProfile = new PrincipalObject(string.Empty, string.Empty);
                    RegistryKey regKeyProfile = regKeyProfileList.OpenSubKey(regKeyName, false);
                    byte[] rawSid = (byte[])regKeyProfile.GetValue("SID", null);
                    userProfile.ProfilePath = (string)regKeyProfile.GetValue("ProfileImagePath", string.Empty);

                    if ((rawSid != null))
                    {
                        try
                        {
                            userProfile.Sid = new SecurityIdentifier(rawSid, 0).Value;
                            userProfile.FqUserName = new SecurityIdentifier(rawSid, 0).Translate(typeof(NTAccount)).ToString();
                        }
                        catch (IdentityNotMappedException)
                        {
                            //unknown account
                            goto NextProfile;
                        }

                    }
                    else
                    {
                        // If we don't have an embedded SID, then we
                        // use the key name instead
                        try
                        {
                            userProfile.FqUserName = new SecurityIdentifier(regKeyName).Translate(typeof(NTAccount)).ToString();
                            userProfile.Sid = regKeyName;
                        }
                        catch (IdentityNotMappedException)
                        {
                            //unknown account
                            goto NextProfile;
                        }
                        catch (Exception)
                        {
                            //An unexpected error occured translating the SID '{0}' into a windows identity
                            goto NextProfile;
                        }
                    }

                    if (!(excludeSystem & userProfile.Domain.ToLower() == ("nt authority")))
                    {
                        // add the UserID to our String Collection
                        if (!Directory.Exists(userProfile.ProfilePath))
                        {
                            goto NextProfile;
                        }

                        if (!File.Exists(Path.Combine(userProfile.ProfilePath, "ntuser.dat")))
                        {
                            if (!File.Exists(Path.Combine(userProfile.ProfilePath, "ntuser.man")))
                            {
                                goto NextProfile;
                            }
                        }

                        profileList.Add(userProfile);
                    }
                    NextProfile:
                    ;
                }
            }

            return profileList;
        }

        internal static bool DoesUserHaveLocalProfile(PrincipalObject user)
        {
            if (!Helper.StringIsNullOrWhiteSpace(user.Sid))
            {
                //using the SID is the quickest way
                RegistryKey profileListRegKey = null;
                RegistryKey userProfileKey = null;

                profileListRegKey = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList", false);

                try
                {
                    userProfileKey = profileListRegKey.OpenSubKey(user.Sid);
                }
                catch (Exception)
                {
                }

                if (userProfileKey != null)
                {
                    return true;
                }
            }
            else
            {
                //fall back to a manual scan

                foreach (PrincipalObject userProfile in GetListOfUsersWithLocalProfiles(true))
                {
                    if (string.Equals(userProfile.FqUserName, user.FqUserName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                        
                    }
                }
            }

            return false;
        }

        internal static string GetUserProfilePath(PrincipalObject user)
        {

            if (!Helper.StringIsNullOrWhiteSpace(user.Sid))
            {
                //using the SID is the quickest way
                RegistryKey profileListRegKey = null;
                RegistryKey userProfileKey = null;

                profileListRegKey = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList", false);

                try
                {
                    userProfileKey = profileListRegKey.OpenSubKey(user.Sid);
                }
                catch
                {
                }

                if (userProfileKey != null)
                {
                    return (string)userProfileKey.GetValue("ProfileImagePath", string.Empty);
                }
            }
            else
            {
                //fall back to a manual scan

                foreach (PrincipalObject userProfile in GetListOfUsersWithLocalProfiles(true))
                {
                    if (userProfile.FqUserName.ToLower() == user.FqUserName.ToLower())
                    {
                        return userProfile.ProfilePath;
                    }
                }
            }

            return string.Empty;
        }

        internal static bool IsUserProfileInUse(PrincipalObject user)
        {

            RegistryKey mReg = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList", false);

            if (mReg == null)
            {
                return false;
            }

            string[] profileList = mReg.GetSubKeyNames();

            foreach (string profileSid in profileList)
            {
                if (!string.Equals(profileSid, user.Sid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RegistryKey mSubKey = mReg.OpenSubKey(profileSid);

                return (int?) mSubKey?.GetValue("RefCount", 0) > 0;
            }

            return false;
        }

        internal static bool IsValidSidString(string sid)
        {
            try
            {
                SecurityIdentifier siDobject = new SecurityIdentifier(sid);
                if (siDobject.IsAccountSid())
                    return true;
            }
            catch
            {
            }

            return false;
        }

        internal static SecurityIdentifier GetMachineSid()
        {
            return Advapi32.GetSidFromName(Environment.MachineName, SidNameUse.SidTypeDomain);
        }
    }
}