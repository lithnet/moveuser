using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lithnet.Moveuser
{
    internal class Program
    {
        private const int ErrorInvalidParameter = 0x57;

        private const int ErrorFileNotFound = 0x2;

        internal static bool UserProfileMigrationAddAclSideBySide { get; set; }

        internal static bool UserProfileMigrationReplaceExistingProfile { get; set; }

        internal static bool UserProfileMigrationAllowMoveUserFallback { get; set; }

        internal static PostMoveActions UserProfileMigrationPostMoveAction { get; set; } = PostMoveActions.Delete;

        internal static bool UserProfileMigrationReAclAllFixedDrives { get; set; }

        internal static List<string> UserProfileMigrationReAclIncludePaths { get; set; } = new List<string>();

        internal static List<string> UserProfileMigrationReAclExcludePaths { get; set; } = new List<string>();

        internal static MoveUserModes MoveUserMode { get; set; } = MoveUserModes.Native;

        internal static string SourceUserName { get; set; }

        internal static string DestinationUserName { get; set; }

        internal static string SourceUserSid { get; set; }

        internal static string DestinationUserSid { get; set; }

        internal static bool DisableNameResolutionFromSid { get; set; }

        internal static void Main(string[] args)
        {
            try
            {
                ShowHeader();
                ProcessSwitches(args);

                AccountMigrationJob am = ProcessIdentities();

                Logging.Log("Performing profile migration for {0} -> {1}", am.SourceUserObject.FqUserName, am.DestinationUserObject.FqUserName);

                UserProfileMigration.MoveUser(am, MoveUserMode);

                if (Program.UserProfileMigrationReAclAllFixedDrives | (Program.UserProfileMigrationReAclIncludePaths.Count > 0))
                {
                    if ((am.ProfileMigrationResult == ProfileMigrationStatus.Migrated) |
                        (am.ProfileMigrationResult == ProfileMigrationStatus.MigratedWithErrors) |
                        (am.ProfileMigrationResult == ProfileMigrationStatus.MigratedWithWarnings))
                    {
                        Logging.Log("Performing ReACL of file system (this may take some time)");
                        UserProfileMigration.ReAclFileSystem(am);
                        Logging.Log("ReACL complete");
                    }
                }

                Logging.Log("\n****************************\n", false, false);
                Logging.Log("Migration Result: " + am.ProfileMigrationResult.ToString(), false, false);
                if (!am.MigrationResultText.ToString().IsNullOrWhiteSpace())
                {
                    Logging.Log("Messages: " + am.MigrationResultText.ToString(), false, false);
                    Logging.Log("****************************", false, false);
                }
                else
                {
                    Logging.Log("\n****************************", false, false);
                }

            }
            catch (Exception ex)
            {
                Logging.Log(ex);
            }

        }

        private static AccountMigrationJob ProcessIdentities()
        {
            AccountMigrationJob am = new AccountMigrationJob();
            PrincipalObject sourceuser = new PrincipalObject();
            PrincipalObject destuser = new PrincipalObject();

            // Validate the Source User
            if (!SourceUserName.IsNullOrWhiteSpace())
            {
                sourceuser.FqUserName = SourceUserName;
                try
                {
                    sourceuser.SidPrincipal = PrincipalManagement.GetUserSid(sourceuser);
                }
                catch (System.Security.Principal.IdentityNotMappedException)
                {
                    Logging.Log("Error: Unable to determine SID for user " + sourceuser.FqUserName, false, false);
                    Environment.Exit(Program.ErrorInvalidParameter);
                }
            }
            else
            {
                if (!Program.SourceUserSid.IsNullOrWhiteSpace())
                {
                    sourceuser.Sid = Program.SourceUserSid;
                    try
                    {
                        System.Security.Principal.NTAccount ntaccount = (System.Security.Principal.NTAccount)sourceuser.SidPrincipal.Translate(typeof(System.Security.Principal.NTAccount));
                        if (!Program.DisableNameResolutionFromSid)
                            sourceuser.FqUserName = ntaccount.Value;
                    }
                    catch { }

                }
                else
                {
                    Logging.Log("The source user SID was not valid");
                    Environment.Exit(Program.ErrorInvalidParameter);
                }
            }

            //Validate the Destination User

            if (!DestinationUserName.IsNullOrWhiteSpace())
            {
                destuser.FqUserName = DestinationUserName;
                try
                {
                    destuser.SidPrincipal = PrincipalManagement.GetUserSid(destuser);
                }
                catch (System.Security.Principal.IdentityNotMappedException)
                {
                    Logging.Log("Error: Unable to determine SID for user " + destuser.FqUserName, false, false);
                    Environment.Exit(Program.ErrorInvalidParameter);
                }
            }
            else
            {
                if (!Program.DestinationUserSid.IsNullOrWhiteSpace())
                {
                    destuser.Sid = Program.DestinationUserSid;
                    try
                    {
                        System.Security.Principal.NTAccount ntaccount = (System.Security.Principal.NTAccount)destuser.SidPrincipal.Translate(typeof(System.Security.Principal.NTAccount));
                        if (!Program.DisableNameResolutionFromSid)
                            destuser.FqUserName = ntaccount.Value;
                    }
                    catch { }
                }
                else
                {
                    Logging.Log("The destination user SID was not valid");
                    Environment.Exit(Program.ErrorInvalidParameter);
                }
            }

            if (SystemManagement.IsWinXPorServer2003() && (MoveUserMode == MoveUserModes.Os))
            {
                if ((sourceuser.AccountName == string.Empty) || (destuser.AccountName == string.Empty))
                {
                    Logging.Log("Usernames must be provided when using 'OS' migration mode on Windows XP or Windows Server 2003. Use Native mode to migrate using only SIDs");
                    Environment.Exit(Program.ErrorInvalidParameter);
                }
            }

            am.SourceUserObject = sourceuser;
            am.DestinationUserObject = destuser;

            return am;

        }

        /*  Command Line Syntax
       *  
       *  lithnet.moveuser.exe        [ sourceusername | *souceuserSID ] [ destinationusername | *destinationuserSID ] [/postmoveaction:delete|disable|keep]
       *                              [/moveusermode: native|OS ] [ /replace ] [ /reaclfixeddrives ] [ /reaclincludefile:include.txt ]
       *                              [ /reaclexcludefile:exclude.txt ] [/log:logfile.log]
       * 
       */

        private static void ProcessSwitches(string[] switches)
        {
            if (switches.Length > 0)
                if ((switches[0] == "/?") | (switches[0] == "-?") | (switches[0] == "/h") | (switches[0] == "-h") | (switches[0].ToLower() == "/help") | (switches[0].ToLower() == "-help"))
                    ShowHelp();

            if (switches.Length < 2)
            {
                Logging.Log("Not enough command line arguments. At least a source and destination user name must be specified");
                Logging.Log("Use /? for help");
                Environment.Exit(Program.ErrorInvalidParameter);
            }

            if (PrincipalManagement.IsValidSidString(switches[0]))
            {
                Program.SourceUserSid = switches[0];
            }
            else
            {
                SourceUserName = switches[0];
            }

            if (PrincipalManagement.IsValidSidString(switches[1]))
            {
                Program.DestinationUserSid = switches[1];
            }
            else
            {
                DestinationUserName = switches[1];
            }

            if (switches.Length > 2)
            {
                for (int x = 2; x <= switches.Length - 1; x++)
                {
                    if (switches[x].ToLower() == "/replace")
                    {
                        UserProfileMigrationReplaceExistingProfile = true;
                    }else if (switches[x].ToLower() == "/noresolve")
                    {
                        Program.DisableNameResolutionFromSid = true;
                    }
                    else if (switches[x].ToLower().StartsWith("/debug"))
                    {
                        Logging.CurrentLogLevel = Logging.LogLevel.Debug;
                    }
                    else if (switches[x].ToLower().StartsWith("/log:"))
                    {
                    }
                    else if (switches[x].ToLower() == ("/postmoveaction:delete"))
                    {
                        UserProfileMigrationPostMoveAction = PostMoveActions.Delete;
                    }
                    else if (switches[x].ToLower() == ("/postmoveaction:disable"))
                    {
                        UserProfileMigrationPostMoveAction = PostMoveActions.Disable;
                    }
                    else if (switches[x].ToLower() == ("/postmoveaction:keep"))
                    {
                        UserProfileMigrationPostMoveAction = PostMoveActions.DoNothing;
                    }
                    else if (switches[x].ToLower() == ("/moveusermode:os"))
                    {
                        if (SystemManagement.IsWinVistaOrAbove() & !SystemManagement.IsWinVistaSp1OrAbove())
                        {
                            Logging.Log("In order to use OS move user mode on Windows Vista, at least SP1 must be installed. Use /moveusermode:native instead");
                            Environment.Exit(Program.ErrorInvalidParameter);
                        }
                        MoveUserMode = MoveUserModes.Os;
                    }
                    else if (switches[x].ToLower() == ("/moveusermode:native"))
                    {
                        MoveUserMode = MoveUserModes.Native;
                    }
                    else if (switches[x].ToLower() == ("/reaclfixeddrives"))
                    {
                        Program.UserProfileMigrationReAclAllFixedDrives = true;
                    }
                    else if (switches[x].StartsWith("/reaclincludefile:"))
                    {
                        ReadIncludeFile(switches[x]);
                    }
                    else if (switches[x].StartsWith("/reaclexcludefile:"))
                    {
                        ReadExcludeFile(switches[x]);
                    }
                    else if (switches[x].StartsWith("/reaclsidebyside"))
                    {
                        UserProfileMigrationAddAclSideBySide = true;
                    }
                    else
                    {
                        Logging.Log("Unknown command line option: " + switches[x]);
                        Logging.Log("Use /? for help");
                        Environment.Exit(Program.ErrorInvalidParameter);
                    }
                }
            }
        }

        private static void ShowHeader()
        {
            Assembly assem = Assembly.GetEntryAssembly();
            AssemblyName assemName = assem.GetName();
            Version ver = assemName.Version;
            Console.WriteLine();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(System.Windows.Forms.Application.ProductName + "                                                       " + ver.ToString(3));
            Console.WriteLine("Copyright © 2011 Lithnet");
            Console.WriteLine();
            Console.WriteLine("User profile migration tool for Windows");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine();
        }

        private static void ShowHelp()
        {
            string appname = System.Windows.Forms.Application.ProductName;

            Console.WriteLine("Syntax: ");
            Console.WriteLine();
            //Console.WriteLine("-------------------------------------------------------------------------------");
            //                 lithnet.moveuser.exe   [ SourceUserName | SourceUserSID ]
            Console.WriteLine(appname + ".exe   [SourceUserName | SourceUserSID] ");
            Console.WriteLine("                       [Destination User Name | Destination User SID]");
            Console.WriteLine("                       [/postmoveaction:delete|disable|keep]  [/replace]");
            Console.WriteLine("                       [/log:logfile.log] [/reaclfixeddrives]  ");
            Console.WriteLine("                       [/reaclincludefile:filename.txt] ");
            Console.WriteLine("                       [/reaclexcludefile:filename.txt] ");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(" SourceUserName        The username of the account whose profile to migrate");
            Console.WriteLine("                       Usernames can be in the format of .\\username, ");
            Console.WriteLine("                       computername\\username, username, or domain\\username");
            Console.WriteLine();
            Console.WriteLine(" SourceUserSID         The SID of the account whose profile to migrate");
            Console.WriteLine("                       The SID must be in SID string format (S-x-x-xx etc)");
            Console.WriteLine("                       Provide either the SID, or the username, do not provide");
            Console.WriteLine("                       both. Use the SID when the username may not be able to");
            Console.WriteLine("                       be translated (ie moving to/from another domain)");
            Console.WriteLine();
            Console.WriteLine(" DestinationUserName   The username of the account to migrate the profile to");
            Console.WriteLine("                       Usernames can be in the format of .\\username, ");
            Console.WriteLine("                       computername\\username, username, or domain\\username");
            Console.WriteLine();
            Console.WriteLine(" SourceUserSID         The SID of the account to migrate the profile to");
            Console.WriteLine("                       The SID must be in SID string format (S-x-x-xx etc)");
            Console.WriteLine("                       Provide either the SID, or the username, do not provide");
            Console.WriteLine("                       both. Use the SID when the username may not be able to");
            Console.WriteLine("                       be translated (ie moving to/from another domain)");
            Console.WriteLine();
            Console.WriteLine(" /postmoveaction:      Action to be taken no the source account after a");
            Console.WriteLine(" delete|keep|disable   successful migration. This only impacts local ");
            Console.WriteLine("                       accounts. Domain accounts are not modified in any way");
            Console.WriteLine("                       The default action is to delete the source account");
            Console.WriteLine();
            Console.WriteLine(" /replace              If the destination user already has a profile on the");
            Console.WriteLine("                       local computer, this switch will assign the user the");
            Console.WriteLine("                       new profile. The default action is to abort the");
            Console.WriteLine("                       migration if an existing profile is found");
            Console.WriteLine();
            Console.WriteLine(" /reaclfixeddrives     Permissions are update on the users profile folder");
            Console.WriteLine("                       automatically, however, if explicit permissions have");
            Console.WriteLine("                       been assigned elsewhere on the drive, this option");
            Console.WriteLine("                       will allow you to scan all files and folders on all");
            Console.WriteLine("                       fixed (non-removable and non-network) drives for ");
            Console.WriteLine("                       permissions assigned to the source user. This can ");
            Console.WriteLine("                       take a long time, depending on the number of files");
            Console.WriteLine("                       and folders on the drive. Use the /reaclexcludefile");
            Console.WriteLine("                       option to exclude locations (such as %windir%)");
            Console.WriteLine("                       from being scanned.");
            Console.WriteLine();
            Console.WriteLine(" /reaclincludefile:    Specifies the path to a text file containing a list");
            Console.WriteLine(" filename.txt          of folders on the computer that should be checked");
            Console.WriteLine("                       for permissions belonging to the source user. Each");
            Console.WriteLine("                       line of the text file should contain a unique parent");
            Console.WriteLine("                       path to scan. Each path is checked recusively, and");
            Console.WriteLine("                       subpaths can be excluded using the /reaclexcludefile");
            Console.WriteLine("                       option");
            Console.WriteLine();
            Console.WriteLine(" /reaclexcludefile:    Specifies the path to a text file containing a list");
            Console.WriteLine(" filename.txt          of folders on the computer that should be excluded");
            Console.WriteLine("                       for checks for permissions belonging to the source user.");
            Console.WriteLine("                       The text file should contain each path to exclude");
            Console.WriteLine("                       on a new line. This setting has no effect unless either");
            Console.WriteLine("                       /reaclfixeddrives or /reaclincludefile are used");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("The following examples take the profile for the local account for 'bob' ");
            Console.WriteLine("and migrate it to bob's account in the lithnet domain");
            Console.WriteLine();
            Console.WriteLine(appname + ".exe .\\bob lithnet\\bob");
            Console.WriteLine(appname + ".exe bob lithnet\\bob");
            Console.WriteLine(appname + ".exe BOBSPC\\bob lithnet\\bob");
            Console.WriteLine();
            Console.WriteLine("The following example migrates the local profile for 'jane' and migrates it");
            Console.WriteLine("to the SID of jane's domain account (the workstation may not yet be joined");
            Console.WriteLine("to the new domain)");
            Console.WriteLine();
            Console.WriteLine(appname + ".exe .\\jane S-1-5-21-2656768339-1635643127-14959812366-1038");
            Console.WriteLine();
            Console.WriteLine("Other examples");
            Console.WriteLine();
            Console.WriteLine(appname + ".exe .\\dave lithnet\\davidm /replace");
            Console.WriteLine(appname + ".exe john lithnet\\johnsmith /postmoveaction:keep");
            Console.WriteLine(appname + ".exe S-1-5-21-2656768339-1635643127-14959812366-3442 S-1-5-21-2656768339-1635643127-14959812366-1030");
            Console.WriteLine(appname + ".exe otherdomain\\john lithnet\\john");
            Console.WriteLine(appname + ".exe lithnet\\john .\\john");
            Environment.Exit(0);
        }

        private static void ReadIncludeFile(string cmdarg)
        {
            string file = cmdarg.Replace("/reaclincludefile:", string.Empty);

            if (!System.IO.File.Exists(file))
            {
                Logging.Log("Re-ACL include file specified could not be found: " + file);
                Environment.Exit(Program.ErrorFileNotFound);
            }
            System.IO.StreamReader sr = new System.IO.StreamReader(file);

            do
            {
                string path = sr.ReadLine();
                if (path == null)
                {
                    continue;
                }

                path = Environment.ExpandEnvironmentVariables(path);
                Program.UserProfileMigrationReAclIncludePaths.Add(path);
            } while (!sr.EndOfStream);

        }

        private static void ReadExcludeFile(string cmdarg)
        {
            string file = cmdarg.Replace("/reaclexcludefile:", string.Empty);

            if (!System.IO.File.Exists(file))
            {
                Logging.Log("Re-ACL exclude file specified could not be found: " + file);
                Environment.Exit(Program.ErrorFileNotFound);
            }
            System.IO.StreamReader sr = new System.IO.StreamReader(file);

            do
            {
                string path = sr.ReadLine();
                path = Environment.ExpandEnvironmentVariables(path);
                Program.UserProfileMigrationReAclExcludePaths.Add(path);
            } while (!sr.EndOfStream);
        }
    }
}
