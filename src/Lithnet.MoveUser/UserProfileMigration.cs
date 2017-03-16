using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Security.AccessControl;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace Lithnet.Moveuser
{
    internal class UserProfileMigration
    {
        private static Dictionary<string, SecurityIdentifier> sidDictionary = new Dictionary<string, SecurityIdentifier>();

        internal static void MoveUser(AccountMigrationJob accountMigrationJob, MoveUserModes moveUserMode)
        {
            if (SystemManagement.IsWinVistaSp1OrAbove())
            {
                if (Helper.StringIsNullOrWhiteSpace(accountMigrationJob.DestinationUserObject.Sid))
                {
                    throw new MoveUserException("SID for username " + accountMigrationJob.DestinationUserObject.FqUserName + " was not found in the target directory");
                }

                if (Helper.StringIsNullOrWhiteSpace(accountMigrationJob.SourceUserObject.Sid))
                {
                    throw new MoveUserException("Cached SID lookup for user " + accountMigrationJob.SourceUserObject.FqUserName + " failed");
                }

                try
                {
                    switch (moveUserMode)
                    {
                        case MoveUserModes.Native:
                            MoveUserNative(accountMigrationJob);
                            break;
                        case MoveUserModes.Os:
                            UserProfileMigration.MoveUserOswmi(accountMigrationJob);
                            break;
                    }

                }
                catch (Exception ex)
                {
                    Logging.Log("An unexpected error occured trying to perform the MoveUser operation");
                    Logging.Log(ex.Message);
                    accountMigrationJob.MigrationResultText.AppendLine("An unexpected error occured trying to perform the MoveUser operation");
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                }

            }
            else if (SystemManagement.IsWinXPorServer2003())
            {
                try
                {
                    switch (moveUserMode)
                    {
                        case MoveUserModes.Native:
                            MoveUserNative(accountMigrationJob);
                            break;
                        case MoveUserModes.Os:
                            UserProfileMigration.MoveUserXpos(accountMigrationJob);
                            break;
                    }

                }
                catch (Exception ex)
                {
                    Logging.Log("An unexpected error occured trying to perform the MoveUser operation");
                    Logging.Log(ex.ToString());
                    accountMigrationJob.MigrationResultText.AppendLine("An unexpected error occured trying to perform the MoveUser operation");
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                }
            }

        }

        private static void MoveUserXpos(AccountMigrationJob accountMigrationJob)
        {
            PrincipalObject oldUser = accountMigrationJob.SourceUserObject;
            PrincipalObject newUser = accountMigrationJob.DestinationUserObject;
            bool keepSource = false;
            
            if (!Program.UserProfileMigrationReplaceExistingProfile)
            {
                if (PrincipalManagement.DoesUserHaveLocalProfile(accountMigrationJob.DestinationUserObject))
                {
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.NotMigrated;
                    accountMigrationJob.MigrationResultText.AppendLine("The user " + newUser.FqUserName + " already has a profile on this system, and the configuration file specified to not replace existing profiles");
                    //** moveuser.exe error 1316
                    Logging.Log(accountMigrationJob.MigrationResultText.ToString());
                    return;
                }
            }

            int retval = Profmap.ReMapUserProfileXp(null, oldUser.FqUserNameExpanded, newUser.FqUserNameExpanded, keepSource);

            //Debug.WriteLine(New System.ComponentModel.Win32Exception(1332).Message) 'No mapping between account names and security IDs was done
            //Debug.WriteLine(New System.ComponentModel.Win32Exception(1317).Message) 'The specified account does not exist

            if (retval == 1332)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("Profile migration could not complete. The specified source user does not exist (error 1332)");
                Logging.Log(accountMigrationJob.MigrationResultText.ToString());
                return;
            }
            else if (retval != 0)
            {
                string exMsg = new System.ComponentModel.Win32Exception(retval).Message;
                Logging.Log(("ReMapUserProfileXP from " + oldUser.FqUserNameExpanded + " to " + newUser.FqUserNameExpanded + " failed with error " + retval + ": " + exMsg));

                if (Program.UserProfileMigrationAllowMoveUserFallback)
                {
                    Logging.Log("Falling back to MoveUserNative");
                    accountMigrationJob.MigrationResultText.AppendLine("Migration fallback to MoveUserNative");

                    MoveUserNative(accountMigrationJob);
                    return;
                }
                else
                {
                    Logging.Log("Fall back to MoveUserDirect not enabled");
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                    accountMigrationJob.MigrationResultText.AppendLine("Mapping failed with error " + retval + ": " + exMsg);
                    return;
                }

            }
            else if (retval == 0)
            {
                Logging.Log("ReMapUserProfileXP completed successfully for " + oldUser.FqUserNameExpanded + " to " + newUser.FqUserNameExpanded);
            }

            if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Delete)
            {
                //		AccountMigration.SourceAccountStatus = AccountState.Deleted;
            }

            if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Disable)
            {
                try
                {
                    if (oldUser.IsLocalUser())
                    {
                        PrincipalManagement.DisableLocalAccount(oldUser.AccountName);
                        //AccountMigration.SourceAccountStatus = AccountState.Disabled;
                    }
                }
                catch (Exception ex)
                {
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                    accountMigrationJob.MigrationResultText.AppendLine("Account could not be disabled. " + ex.Message);
                    return;
                }
            }

            if (accountMigrationJob.ProfileMigrationResult == ProfileMigrationStatus.PendingMoveToTempProfile)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MovedToTempProfile;
            }
            else
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Migrated;
            }
        }

        private static void MoveUserOswmi(AccountMigrationJob accountMigrationJob)
        {
            accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.NotMigrated;

            System.Management.ManagementObject wmiProfile = PrincipalManagement.GetWin32UserProfileObject(accountMigrationJob.SourceUserObject.Sid);

            if (wmiProfile == null)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("The user profile " + accountMigrationJob.SourceUserObject.FqUserName + " (" + accountMigrationJob.SourceUserObject.Sid + ") was not found on this system");
                throw new ArgumentException(accountMigrationJob.MigrationResultText.ToString());
            }

            try
            {
                Logging.Log("Calling WMI ChangeOwner...");
                wmiProfile.InvokeMethod("ChangeOwner", new object[]
                {
                    accountMigrationJob.DestinationUserObject.Sid,
                    (Program.UserProfileMigrationReplaceExistingProfile ? 1 : 0)
                });
                Logging.Log("ChangeOwner for profile " + accountMigrationJob.SourceUserObject.FqUserName + " to user " + accountMigrationJob.DestinationUserObject.FqUserName + " completed sucessfully", Logging.LogLevel.Debug);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // ERROR_ALREADY_EXISTS 
                if (ex.ErrorCode == -2147024713)
                {
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.NotMigrated;
                    accountMigrationJob.MigrationResultText.AppendLine("The user " + accountMigrationJob.DestinationUserObject.FqUserName + " already has a profile on this system, and the configuration file specified to not replace existing profiles");
                    return;
                }
                else
                {
                    System.ComponentModel.Win32Exception win32Ex = new System.ComponentModel.Win32Exception(ex.ErrorCode);
                    Logging.Log(win32Ex.Message);
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                    accountMigrationJob.MigrationResultText.AppendLine("An unexpected error occured (" + ex.ErrorCode + "): " + win32Ex.Message);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.Log(ex.Message);
                string errMsg = ex.Message;
                if (Helper.StringIsNullOrWhiteSpace(errMsg))
                    errMsg = ex.GetType().ToString();
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("An unexpected error occured: " + errMsg);
                return;
            }

            try
            {
                Logging.Log("Copying local group membership...");
                CopyLocalGroupMembership(accountMigrationJob.SourceUserObject, accountMigrationJob.DestinationUserObject);
            }
            catch (Exception ex)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithErrors;
                accountMigrationJob.MigrationResultText.AppendLine("Error copying group membership\n" + ex.Message);
                throw new MoveUserException(accountMigrationJob.MigrationResultText.ToString(), ex);
            }

            try
            {
                Logging.Log("Performing post-migration action...");
                PerformPostMoveAction(accountMigrationJob);
            }
            catch (Exception ex)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Delete)
                {
                    accountMigrationJob.MigrationResultText.AppendLine("Error deleting account " + ex.Message);
                }
                else if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Disable)
                {
                    accountMigrationJob.MigrationResultText.AppendLine("Error disabling account " + ex.Message);
                }
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                return;
            }

            Logging.Log("Complete");
            accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Migrated;


        }


        #region "MoveUserNative"

        internal static void MoveUserNative(AccountMigrationJob accountMigrationJob)
        {
            RegistryKey mountedUserKey;
            RegistryKey mountedUserClassesKey;
            string userRegKeyName;
            string userClassesRegKeyName = string.Empty;

            PrincipalObject oldUser = accountMigrationJob.SourceUserObject;
            PrincipalObject newUser = accountMigrationJob.DestinationUserObject;

            if (!PrincipalManagement.DoesUserHaveLocalProfile(oldUser))
            {
                Logging.Log("Error: The source user " + oldUser.FqUserName + " does not have a profile on this system");
                accountMigrationJob.MigrationResultText.AppendLine("The user does not have a profile on this system");
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.NotMigrated;
                return;
            }

            if (!Program.UserProfileMigrationReplaceExistingProfile)
            {
                if (PrincipalManagement.DoesUserHaveLocalProfile(accountMigrationJob.DestinationUserObject))
                {
                    Logging.Log("User " + accountMigrationJob.DestinationUserObject.FqUserName + "  already has a profile on this system");
                    accountMigrationJob.MigrationResultText.AppendLine("The destination user already has a profile on this system");
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.NotMigrated;
                    return;
                }
            }

            try
            {
                Logging.Log("Opening user registry hives...");

                mountedUserKey = MountUserHive(accountMigrationJob);
                userRegKeyName = mountedUserKey.Name;
                mountedUserClassesKey = MountUserClassesHive(accountMigrationJob);
                if ((mountedUserClassesKey != null))
                {
                    userClassesRegKeyName = mountedUserClassesKey.Name;

                }

            }
            catch (Exception ex)
            {
                Logging.Log("Unable to mount registry keys for " + oldUser.FqUserName);
                Logging.Log(ex.Message);
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("Unable to load registry key");
                return;
            }

            try
            {
                Logging.Log("Updating registry permissions...");
                UserProfileMigration.ReAclUserRegistry(mountedUserKey, oldUser.SidPrincipal, newUser.SidPrincipal);
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to reset all permissions for " + newUser.FqUserName + "on registry key " + userRegKeyName);
                Logging.Log(ex.Message);
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                accountMigrationJob.MigrationResultText.AppendLine("Could not replace ACLs on all registry keys");
            }

            try
            {
                if ((mountedUserClassesKey != null))
                {
                    UserProfileMigration.ReAclUserRegistry(mountedUserClassesKey, oldUser.SidPrincipal, newUser.SidPrincipal);
                }
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to reset all permissions for " + newUser.FqUserName + " on registry key " + userClassesRegKeyName);
                Logging.Log(ex.Message);
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                accountMigrationJob.MigrationResultText.AppendLine("Could not replace ACLs on all registry keys");
            }


            try
            {
                Logging.Log("Updating file system permissions...");
                UserProfileMigration.ReAclPath(accountMigrationJob.SourceUserObject.ProfilePath, oldUser.SidPrincipal, newUser.SidPrincipal);
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to reset all permissions for " + newUser.FqUserName + " on folder " + accountMigrationJob.SourceUserObject.ProfilePath);
                Logging.Log(ex.Message);
                accountMigrationJob.MigrationResultText.AppendLine("Unable to reset all permissions on profile folder");
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
            }

            try
            {
                Logging.Log("Updating profile pointer...");
                RenameSourceUserProfileKey(accountMigrationJob);
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to rename the source user profile registry key");
                Logging.Log(ex.Message);
                accountMigrationJob.MigrationResultText.AppendLine("Unable to rename the source user profile registry key");
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                return;
            }

            try
            {
                try
                {
                    mountedUserKey.Close();
                }
                catch
                {
                }

                RegistryManagement.UnloadRegFile(userRegKeyName);
            }
            catch (Exception ex)
            {
                Logging.Log("Unable to unload registry key " + userRegKeyName + " - " + ex.Message);
            }

            try
            {
                try
                {
                    if (mountedUserClassesKey != null)
                    {
                        mountedUserClassesKey.Close();
                        mountedUserClassesKey = null;
                    }
                }
                catch
                {
                }

                if (mountedUserClassesKey != null)
                {
                    RegistryManagement.UnloadRegFile(userClassesRegKeyName);
                }

            }
            catch (Exception ex)
            {
                Logging.Log("Unable to unload registry key " + userClassesRegKeyName + " - " + ex.Message);
            }

            try
            {
                Logging.Log("Copying group membership...");
                CopyLocalGroupMembership(oldUser, newUser);
            }
            catch (Exception ex)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithErrors;
                accountMigrationJob.MigrationResultText.AppendLine("Error copying group membership");
                accountMigrationJob.MigrationResultText.AppendLine(ex.Message);
                return;
            }

            try
            {
                Logging.Log("Performing post-migration action...");
                PerformPostMoveAction(accountMigrationJob);
            }
            catch (Exception ex)
            {
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;

                if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Delete)
                {
                    accountMigrationJob.MigrationResultText.AppendLine("Error deleting account " + ex.Message);
                }
                else if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Disable)
                {
                    accountMigrationJob.MigrationResultText.AppendLine("Error disabling account " + ex.Message);
                }

                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.MigratedWithWarnings;
                return;
            }

            accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Migrated;
            Logging.Log("Complete");
        }


        private static void PerformPostMoveAction(AccountMigrationJob accountMigrationJob)
        {

            if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Delete)
            {
                if (accountMigrationJob.SourceUserObject.IsLocalUser())
                {
                    PrincipalManagement.DeleteLocalAccount(accountMigrationJob.SourceUserObject);
                }
            }
            else if (Program.UserProfileMigrationPostMoveAction == PostMoveActions.Disable)
            {
                if (accountMigrationJob.SourceUserObject.IsLocalUser())
                {
                    PrincipalManagement.DisableLocalAccount(accountMigrationJob.SourceUserObject);
                }
            }
        }

        private static RegistryKey MountUserHive(AccountMigrationJob accountMigrationJob)
        {
            RegistryKey mountedKey = null;
            PrincipalObject oldUser = accountMigrationJob.SourceUserObject;

            string regKeyName = oldUser.Sid;
            string regPath = Path.Combine(accountMigrationJob.SourceUserObject.ProfilePath, "ntuser.dat");

            try
            {
                mountedKey = RegistryManagement.Hku64BitRegistryView.OpenSubKey(oldUser.Sid, true);
                if ((mountedKey != null))
                {
                    Logging.Log("Registry hive for user " + oldUser.FqUserName + " was already loaded", Logging.LogLevel.Debug);
                }
                else
                {
                    Logging.Log("Registry hive for user " + oldUser.FqUserName + " was not already loaded", Logging.LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logging.Log("An error occured while trying to check if registry hive for user " + oldUser.FqUserName + "  was already loaded");
                Logging.Log(ex.Message);
            }

            if (mountedKey == null)
            {
                //Registry hive isnt loaded and needs to be mounted manually
                if (!File.Exists(regPath))
                {
                    regPath = Path.Combine(accountMigrationJob.SourceUserObject.ProfilePath, "ntuser.man");
                    if (!File.Exists(regPath))
                    {
                        //User does not have a standard or mandatory registry profile
                        Logging.Log("Could not find ntuser.dat or ntuser.man for account " + oldUser.FqUserName + ". Cannot migrate profile");
                        accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                        accountMigrationJob.MigrationResultText.AppendLine("Registry file could not be found");
                        throw new FileNotFoundException("Could not find ntuser.dat or ntuser.man for account " + oldUser.FqUserName);
                    }
                }

                try
                {
                    RegistryManagement.LoadRegFileKey(regPath, regKeyName);
                }
                catch (Exception ex)
                {
                    Logging.Log("An unexpected error occured while trying to mount the file " + regPath + " to the registry mount HKU\\" + regKeyName);
                    Logging.Log(ex.Message);
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                    accountMigrationJob.MigrationResultText.AppendLine("Registry file could not be mounted");
                    throw;
                }

                try
                {
                    mountedKey = RegistryManagement.Hku64BitRegistryView.OpenSubKey(regKeyName, true);
                }
                catch (Exception ex)
                {
                    Logging.Log("An unexpected error occured while trying to open the mounted registry key HKU\\" + regKeyName);
                    Logging.Log(ex.Message);
                    accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                    accountMigrationJob.MigrationResultText.AppendLine("Registry file could not be opened");
                    throw;
                }
            }

            return mountedKey;
        }

        internal static RegistryKey MountUserClassesHive(AccountMigrationJob accountMigrationJob)
        {
            RegistryKey mountedKey = null;
            PrincipalObject oldUser = accountMigrationJob.SourceUserObject;
            string regKeyName = oldUser.Sid + "_Classes";
            string regPath = null;

            if (SystemManagement.IsWinVistaOrAbove())
            {
                regPath = Path.Combine(accountMigrationJob.SourceUserObject.ProfilePath, "AppData\\Local\\Microsoft\\Windows\\Usrclass.dat");
            }
            else
            {
                regPath = Path.Combine(accountMigrationJob.SourceUserObject.ProfilePath, "Local Settings\\Application Data\\Microsoft\\Windows\\Usrclass.dat");
            }

            try
            {
                mountedKey = RegistryManagement.Hku64BitRegistryView.OpenSubKey(regKeyName);
                if ((mountedKey != null))
                {
                    Logging.Log("Classes registry hive for user " + oldUser.FqUserName + " was already loaded", Logging.LogLevel.Debug);
                    return mountedKey;
                }
            }
            catch (Exception)
            {
                //Registry hive for user is not loaded
            }


            //Registry hive isnt loaded and needs to be mounted manually
            if (!File.Exists(regPath))
            {
                //User does not have a standard or mandatory registry profile
                Logging.Log("Could not find usrclass.dat for account " + oldUser.FqUserName);
                //AccountMigration.ProfileMigrationResult = ProfileMigrationStatus.Failed
                accountMigrationJob.MigrationResultText.AppendLine("Note: User Classes registry file could not be found");
                //Throw New FileNotFoundException("Could not find usrclass.dat for account " & OldUser.FQUserName)
                return null;
            }

            try
            {
                RegistryManagement.LoadRegFileKey(regPath, regKeyName);
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to mount the file " + regPath + " to the registry mount HKU\\" + regKeyName);
                Logging.Log(ex.Message);
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("Classes registry file could not be mounted");
                throw;
            }

            try
            {
                mountedKey = RegistryManagement.Hku64BitRegistryView.OpenSubKey(regKeyName, true);
            }
            catch (System.Security.SecurityException)
            {
                Logging.Log("Access denied opening mounted registry key: " + regKeyName);
                accountMigrationJob.MigrationResultText.AppendLine("User classes registry file could not be opened");
                return null;
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to open the mounted registry key HKU\\" + regKeyName);
                Logging.Log(ex.Message);
                accountMigrationJob.ProfileMigrationResult = ProfileMigrationStatus.Failed;
                accountMigrationJob.MigrationResultText.AppendLine("Registry file could not be opened");
                throw;
            }

            return mountedKey;

        }

        private static void RenameSourceUserProfileKey(AccountMigrationJob accountMigrationJob)
        {
            RegistryKey profileListRegKey = null;

            profileListRegKey = RegistryManagement.Hklm64BitRegistryView.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList", true);

            //Remove any existing profile entries for the destination user
            try
            {
                profileListRegKey.DeleteSubKeyTree(accountMigrationJob.DestinationUserObject.Sid);
                Logging.Log("Removed existing profile entry for user " + accountMigrationJob.DestinationUserObject.FqUserName, Logging.LogLevel.Debug);
            }
            catch
            {
                //User didnt have an existing key
            }

            //Call the API to copy the profile registry key
            RegistryManagement.CopyKey(profileListRegKey, accountMigrationJob.SourceUserObject.Sid, accountMigrationJob.DestinationUserObject.Sid);
            Logging.Log("Copied profile entry for user " + accountMigrationJob.SourceUserObject.FqUserName + " to user " + accountMigrationJob.DestinationUserObject.FqUserName, Logging.LogLevel.Debug);

            //Replace the SID value in the profile registry key
            RegistryKey destinationUserKey = profileListRegKey.OpenSubKey(accountMigrationJob.DestinationUserObject.Sid, true);
            byte[] desinationBinarySid = new byte[accountMigrationJob.DestinationUserObject.SidPrincipal.BinaryLength + 1];
            accountMigrationJob.DestinationUserObject.SidPrincipal.GetBinaryForm(desinationBinarySid, 0);
            destinationUserKey.SetValue("Sid", desinationBinarySid, RegistryValueKind.Binary);
            destinationUserKey.Close();

            //Delete the old users registry key
            try
            {
                if (!Program.UserProfileMigrationAddAclSideBySide)
                {
                    profileListRegKey.DeleteSubKey(accountMigrationJob.SourceUserObject.Sid);
                }
            }
            catch (Exception)
            {
                Logging.Log("Unable to delete the source registry key for user " + accountMigrationJob.SourceUserObject.FqUserName + " with SID " + accountMigrationJob.SourceUserObject.Sid);
            }

        }

        internal static void ReAclUserRegistry(RegistryKey regKey, SecurityIdentifier sourceUser, SecurityIdentifier destinationUser)
        {

            UserProfileMigration.TryRegistryReplaceAcl(regKey, sourceUser, destinationUser);

            foreach (string subKeyName in regKey.GetSubKeyNames())
            {
                try
                {
                    RegistryKey subKey = regKey.OpenSubKey(subKeyName, true);
                    UserProfileMigration.ReAclUserRegistry(subKey, sourceUser, destinationUser);
                    subKey.Close();
                }
                catch (System.Security.SecurityException)
                {
                    if (!subKeyName.ToLower().Contains("protected"))
                    {
                        if (!regKey.Name.ToLower().Contains("protected"))
                        {
                            //Software\Microsoft\SystemCertificates\Root\ProtectedRoots
                            //Software\Microsoft\Protected Storage System Provider
                            //only SYSTEM has rights to these keys
                            Logging.Log("Access denied error when trying to open registry key " + regKey.Name + "\\" + subKeyName);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logging.Log("An unexpected error occured while trying to Re-ACL registry key " + regKey.Name + "\\" + subKeyName);
                    Logging.Log(ex.Message);
                }
            }


        }

        internal static void ReAclPath(string path, SecurityIdentifier sourceSid, SecurityIdentifier destinationSid)
        {
            List<SidMapping> sidMappingList = new List<SidMapping>();
            SidMapping sidMapping = new SidMapping
            {
                SourceSid = sourceSid,
                DestinationSid = destinationSid
            };

            sidMappingList.Add(sidMapping);

            UserProfileMigration.ReAclPath(path, sidMappingList);

        }

        internal static void ReAclPath(string path, List<SidMapping> sidMappingList)
        {
            if (UserProfileMigration.IsPathInReAclExcludeList(path))
            {
                Logging.Log("Path was in re-ACL exclude list: " + path);
                return;
            }

            try
            {
                UserProfileMigration.TryPathReplaceAcl(path, sidMappingList);
            }
            catch (FileNotFoundException)
            {
                Logging.Log("Re-ACL path was not found: " + path);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                Logging.Log("Re-ACL path was not found: " + path);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                Logging.Log("Access to path was denied: " + path);
                return;
            }

            try
            {
                if (IsPathReparsePoint(path))
                    return;

                if (Directory.Exists(path))
                {
                    foreach (string subPath in Directory.GetFileSystemEntries(path))
                    {
                        try
                        {
                            UserProfileMigration.ReAclPath(subPath, sidMappingList);
                        }
                        catch (System.UnauthorizedAccessException)
                        {
                            Logging.Log("Access to path was denied: " + subPath);
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("An unexpected error occured while trying to Re-ACL path " + subPath);
                            Logging.Log(ex.Message);
                        }
                    }
                }
            }
            catch (PathTooLongException)
            {
                Logging.Log("The following path could not be processed for ACL replacement as it exceeds MAX_PATH: " + path);
            }
            catch (System.UnauthorizedAccessException)
            {
                Logging.Log("Access to path was denied: " + path);
            }

        }

        internal static bool TryRegistryReplaceAcl(RegistryKey key, SecurityIdentifier sourcePrincipal, SecurityIdentifier destinationPrincipal)
        {
            try
            {
                RegistrySecurity registrySecurity = key.GetAccessControl();

                foreach (RegistryAccessRule ruleLoopVariable in registrySecurity.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    RegistryAccessRule rule = ruleLoopVariable;
                    if (rule.IdentityReference == sourcePrincipal)
                    {
                        try
                        {
                            RegistryAccessRule newRegistryAccessRule = new RegistryAccessRule(destinationPrincipal, rule.RegistryRights, rule.InheritanceFlags, rule.PropagationFlags, rule.AccessControlType);
                            registrySecurity.AddAccessRule(newRegistryAccessRule);

                            if (!Program.UserProfileMigrationAddAclSideBySide)
                            {
                                registrySecurity.RemoveAccessRule(rule);
                            }

                            key.SetAccessControl(registrySecurity);
                            Logging.Log("Replaced ACL on key " + key.Name, Logging.LogLevel.Debug);
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("An unexpected error occured while trying to replace the ACL on registry key " + key.Name);
                            Logging.Log(ex.Message);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to replace the ACL on registry key " + key.Name);
                Logging.Log(ex.Message);
                return false;
            }

            return true;
        }

        internal static bool IsFile(string path)
        {
            FileAttributes fileAttr = File.GetAttributes(path);

            if (Helper.EnumHasFlag((int)FileAttributes.Directory, fileAttr))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        internal static bool TryPathReplaceAcl(string path, List<SidMapping> sidMapping)
        {
            bool file = false;

            try
            {
                if (path.Length > 259)
                {
                    Logging.Log("The following path could not be processed as it exceeds MAX_PATH: " + path);
                    return false;
                }
                FileSystemSecurity accessControlFileAcl = null;
                AuthorizationRuleCollection accessRules = null;
                try
                {
                    FileAttributes fileAttr = File.GetAttributes(path);

                    if (Helper.EnumHasFlag((int)FileAttributes.Directory, fileAttr))
                    {
                        accessControlFileAcl = Directory.GetAccessControl(path, AccessControlSections.All);
                    }
                    else
                    {
                        accessControlFileAcl = File.GetAccessControl(path, AccessControlSections.All);
                        file = true;
                    }

                    accessRules = accessControlFileAcl.GetAccessRules(true, false, typeof(SecurityIdentifier));
                }
                catch (InvalidOperationException sioex)
                {
                    if (sioex.Message.Contains("error code 32"))
                    {
                        Logging.Log("Could not open " + path + " due to a sharing violation", Logging.LogLevel.Debug);
                        return false;
                    }
                    else
                        throw;
                }
                catch (UnauthorizedAccessException)
                {
                    Logging.Log("Access denied error reading access rules for path: " + path, Logging.LogLevel.Debug);
                    return false;
                }
                catch (Exception ex)
                {
                    Logging.Log("Unexpected error reading access rules for path: " + path);
                    Logging.Log(ex.Message);
                    return false;
                }

                IdentityReference fileOwner = null;
                try
                {
                    fileOwner = accessControlFileAcl.GetOwner(typeof(SecurityIdentifier));
                }
                catch (UnauthorizedAccessException)
                {
                    Logging.Log("Access denied error reading owner for path: " + path, Logging.LogLevel.Debug);
                    return false;
                }
                catch (Exception ex)
                {
                    Logging.Log("Unexpected error reading owner for path: " + path);
                    Logging.Log(ex.Message);
                    return false;
                }


                foreach (SidMapping mapping in sidMapping)
                {
                    if (fileOwner != null && mapping.SourceSid == fileOwner)
                    {
                        try
                        {
                            Advapi32.ChangeOwnerandRecalculateDacl(path, SeObjectType.SeFileObject, mapping.DestinationSid.ToString());

                            Logging.Log("Replaced owner from '" + mapping.SourceSid.ToString() + "' to '" + mapping.DestinationSid.ToString() + "' on path: " + path, Logging.LogLevel.Debug);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Logging.Log("Access denied replacing owner from '" + mapping.SourceSid.ToString() + "' to '" + mapping.DestinationSid.ToString() + "' on path: " + path);
                            return false;
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("Unexpected error replacing owner from '" + mapping.SourceSid.ToString() + "' to '" + mapping.DestinationSid.ToString() + "' on path: " + path);
                            Logging.Log(ex.Message);
                            return false;
                        }
                    }


                    foreach (FileSystemAccessRule rule in accessRules)
                    {
                        if (rule.IdentityReference != mapping.SourceSid)
                        {
                            continue;
                        }

                        try
                        {
                            FileSystemRights mappedRights = UserProfileMigration.MapGenericRightsToFileSystemRights(rule.FileSystemRights);
                            FileSystemAccessRule newFileAccessRule = new FileSystemAccessRule(mapping.DestinationSid, mappedRights, rule.InheritanceFlags, rule.PropagationFlags, rule.AccessControlType);
                            accessControlFileAcl.AddAccessRule(newFileAccessRule);

                            if (!Program.UserProfileMigrationAddAclSideBySide)
                            {
                                accessControlFileAcl.RemoveAccessRule(rule);
                            }

                            if (file)
                            {
                                File.SetAccessControl(path, (FileSecurity)accessControlFileAcl);
                            }
                            else
                            {
                                Directory.SetAccessControl(path, (DirectorySecurity)accessControlFileAcl);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Logging.Log("Access denied replacing ACL on path: " + path);
                            return false;
                        }
                        catch (Exception ex)
                        {
                            Logging.Log("An unexpected error occured while trying to replace the ACL on the path " + path);
                            Logging.Log(ex.Message);
                            return false;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logging.Log("Access denied replacing ACL on path: " + path);
                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to replace the ACL on the path " + path);
                Logging.Log(ex.Message);
                return false;
            }

            return true;
        }

        private static bool IsPathReparsePoint(string path)
        {
            FileAttributes pathAttr = File.GetAttributes(path);
            return Helper.EnumHasFlag((int)FileAttributes.ReparsePoint, pathAttr);
        }

        internal static FileSystemRights MapGenericRightsToFileSystemRights(FileSystemRights originalRights)
        {
            FileSystemRights mappedRights = (FileSystemRights)0;

            if (((uint)originalRights & (uint)GenericRights.GenericExecute) == (uint)GenericRights.GenericExecute)
            {
                mappedRights = (FileSystemRights)((uint)mappedRights | (uint)MappedGenericRights.FileGenericExecute);
            }
            if (((uint)originalRights & (uint)GenericRights.GenericRead) == (uint)GenericRights.GenericRead)
            {
                mappedRights = (FileSystemRights)((uint)mappedRights | (uint)MappedGenericRights.FileGenericRead);
            }
            if (((uint)originalRights & (uint)GenericRights.GenericWrite) == (uint)GenericRights.GenericWrite)
            {
                mappedRights = (FileSystemRights)((uint)mappedRights | (uint)MappedGenericRights.FileGenericWrite);
            }
            if (((uint)originalRights & (uint)GenericRights.GenericAll) == (uint)GenericRights.GenericAll)
            {
                mappedRights = (FileSystemRights)((uint)mappedRights | (uint)MappedGenericRights.FileGenericAll);
            }

            //Map the generic permissions to specific permissions
            mappedRights = originalRights | mappedRights;

            //Now get rid of the generic permissions so that our value is recognised as a FileSystemRights Enum value 
            mappedRights = (FileSystemRights)((uint)mappedRights << 8);
            mappedRights = (FileSystemRights)((uint)mappedRights >> 8);

            return mappedRights;
        }

        #endregion

        internal static void ReAclFileSystem(AccountMigrationJob accountMigrationJob)
        {
            List<SidMapping> sidMappingList = new List<SidMapping>();
            List<string> reAclPathList = new List<string>();

            SidMapping sidMapping = new SidMapping
            {
                SourceSid = accountMigrationJob.SourceUserObject.SidPrincipal,
                DestinationSid = accountMigrationJob.DestinationUserObject.SidPrincipal
            };

            sidMappingList.Add(sidMapping);

            if (sidMappingList.Count == 0)
            {
                return;
            }

            if (Program.UserProfileMigrationReAclAllFixedDrives)
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Fixed)
                    {
                        Logging.Log("Added path to re-ACL list: " + drive.RootDirectory.FullName);
                        reAclPathList.Add(drive.RootDirectory.FullName.ToLower());
                    }
                }
            }

            foreach (string path in Program.UserProfileMigrationReAclIncludePaths)
            {
                if (reAclPathList.Contains(path.ToLower()))
                {
                    continue;
                }

                if (File.Exists(path) | Directory.Exists(path))
                {
                    Logging.Log("Added path to re-ACL list:" + path);
                    reAclPathList.Add(path);
                }
                else
                {
                    Logging.Log("Path was not found on local computer: " + path);
                }
            }

            try
            {
                Advapi32.AcquireBackupRestorePrivileges();
            }
            catch (Exception ex)
            {
                Logging.Log("An unexpected error occured while trying to obtain privilges needed to set file ownership");
                Logging.Log(ex.Message);
                return;
            }

            foreach (string path in reAclPathList)
            {
                Logging.Log("Attempting to re-ACL path with " + sidMappingList.Count + " SID mappings: " + path);
                UserProfileMigration.ReAclPath(path, sidMappingList);
            }
        }

        internal static GroupCopyResults CopyLocalGroupMembership(PrincipalObject sourceUser, PrincipalObject destinationUser)
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + SystemManagement.GetActiveComputerName());
            GroupCopyResults groupCopyResults = new GroupCopyResults();

            if (Helper.StringIsNullOrWhiteSpace(sourceUser.Sid))
            {
                string errorMsg = "SID for source user " + sourceUser.FqUserName + " was not found";
                throw new MoveUserException(errorMsg);
            }

            if (Helper.StringIsNullOrWhiteSpace(destinationUser.Sid))
            {
                string errorMsg = "SID for destination user " + destinationUser.FqUserName + " was not found";
                throw new MoveUserException(errorMsg);
            }

            Logging.Log("Performing group copy from " + sourceUser.FqUserName + " to " + destinationUser.FqUserName, Logging.LogLevel.Debug);


            foreach (DirectoryEntry localgroup in localDirectory.Children)
            {
                if (localgroup.SchemaClassName.ToLower() != "group")
                {
                    continue;
                }

                foreach (object member in (IEnumerable)localgroup.Invoke("members"))
                {
                    DirectoryEntry entry = new DirectoryEntry(member);
                    //System.DirectoryServices.PropertyCollection memberpropcoll = entry.Properties ["objectSid"]

                    byte[] objectSid = (byte[])entry.Properties["objectSid"].Value;
                    //byte[] ObjectSID = (byte[])memberpropcoll["objectSid"].Value;

                    if (new SecurityIdentifier(objectSid, 0).Value.ToLower() != sourceUser.Sid.ToLower())
                    {
                        continue;
                    }

                    //Source User is a member
                    groupCopyResults.SourceGroups.Add(localgroup.Name);
                    try
                    {
                        PrincipalManagement.AddSidToLocalGroup(localgroup.Name, destinationUser.Sid);
                        groupCopyResults.AddedToGroups.Add(localgroup.Name);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log("Error adding " + destinationUser.FqUserName + " to group " + localgroup.Name);
                        Logging.Log(ex.Message);
                        groupCopyResults.NotAddedToGroups.Add(localgroup.Name);
                    }
                }
            }

            if (groupCopyResults.SourceGroups.Count > 0)
            {
                Logging.Log("Source user '" + sourceUser.FqUserName + "' was a member of: '" + string.Join(",", groupCopyResults.SourceGroups.ToArray()) + "'", Logging.LogLevel.Debug);
            }

            if (groupCopyResults.AddedToGroups.Count > 0)
            {
                Logging.Log("Destination user '" + destinationUser.FqUserName + "' added to groups: '" + string.Join(",", groupCopyResults.AddedToGroups.ToArray()) + "'", Logging.LogLevel.Debug);
            }

            if (groupCopyResults.NotAddedToGroups.Count > 0)
            {
                Logging.Log("Destination user '" + sourceUser.FqUserName + "' could not be added to groups: '" + string.Join(",", groupCopyResults.NotAddedToGroups.ToArray()) + "'");
                throw new MoveUserException("The user could not be added to the following groups: '" + string.Join(",", groupCopyResults.NotAddedToGroups.ToArray()) + "'. Check the log file for details");
            }

            return groupCopyResults;
        }
        
        internal static SecurityIdentifier CrackSid(PrincipalObject userObject)
        {
            SecurityIdentifier sid = null;

            if (UserProfileMigration.sidDictionary.ContainsKey(userObject.FqUserName.ToLower()))
            {
                return UserProfileMigration.sidDictionary[userObject.FqUserName.ToLower()];
            }

            sid = PrincipalManagement.GetUserSid(userObject);

            if (sid != null)
            {
                UserProfileMigration.sidDictionary.Add(userObject.FqUserName.ToLower(), sid);
            }

            return sid;
        }

        internal static bool TryCrackSid(PrincipalObject userObject, ref SecurityIdentifier sid)
        {
            try
            {
                sid = UserProfileMigration.CrackSid(userObject);
                return sid != null;
            }
            catch (IdentityNotMappedException)
            {
                Logging.Log("Could not determine SID for user " + userObject.FqUserName);
                return false;
            }
            catch (Exception ex)
            {
                Logging.Log("An unknown error occured determining SID for user " + userObject.FqUserName);
                Logging.Log(ex.Message);
                return false;
            }
        }

        private static bool IsPathInReAclExcludeList(string path)
        {
            foreach (string excludePath in Program.UserProfileMigrationReAclExcludePaths)
            {
                if (string.Equals(excludePath, path, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
