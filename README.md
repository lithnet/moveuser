![](https://lithnet.github.io/images/logo-ex-small.png)
# Lithnet MoveUser
#### A replacement for Microsoft's moveuser.exe and Win32_UserProfile.ChangeOwner

Lithnet.MoveUser is a command line tool that can be used to change the owner of a profile from one user to another. It is designed to be a replacement for Microsoft's moveuser.exe tool (used for Windows XP), originally included in the Windows Resource Kit, and the Win32_UserProfile.ChangeOwner WMI method, used for Windows Vista and above. 

The Lithnet.MoveUser tool provides the same functionality as the other tools, but overcomes some of the shortcomings of the Microsoft provided toolsets. It does not require any scripting knowledge, provides a consistent experience across Windows XP, Vista, and Windows 7, and provides detailed logging of progress and any errors encountered.

The tool will perform the following tasks

- Change the owner of the profile to the destination user, and update associated permissions
- Add the destination user to the same local groups that the source user was a member of
- If the source account is a local account, then it can either be deleted, disabled, or left as-is after a successful migration. By default it is deleted
- The source and destination usernames can either be provided in standard username format (domain\username, computer\username) or as a SID
- The tool can also scan areas outside of a users profile for permissions assigned to the source user, and update them to apply to the destination user instead.

Ensure you have at least Microsoft .NET framework 3.5 installed, then download the tool and run the following command for help

```
lithnet.moveuser.exe /?
```

Examples:
The following examples take the profile for the local account for 'bob' and migrate it to bob's account in the lithnet domain

```
lithnet.moveuser.exe .\bob lithnet\bob
lithnet.moveuser.exe bob lithnet\bob
lithnet.moveuser.exe BOBSPC\bob lithnet\bob
```

The following example migrates the local profile for 'jane' and migrates it to the SID of jane's domain account (the workstation may not yet be joined to the new domain)

```
lithnet.moveuser.exe .\jane S-1-5-21-2656768339-1635643127-14959812366-1038
```
Other examples

```
lithnet.moveuser.exe .\dave lithnet\davidm /replace
lithnet.moveuser.exe john lithnet\johnsmith /postmoveaction:keep
lithnet.moveuser.exe S-1-5-21-2656768339-1635643127-14959812366-3442 S-1-5-21-2
56768339-1635643127-14959812366-1030
lithnet.moveuser.exe otherdomain\john lithnet\john
lithnet.moveuser.exe lithnet\john .\john
```

### Download the module
Download the [current release](https://github.com/lithnet/moveuser/releases/)

## How can I contribute to the project?
* Found an issue and want us to fix it? [Log it](https://github.com/lithnet/moveuser/issues)
* Want to fix an issue yourself or add functionality? Clone the project and submit a pull request
* Make a [donation](https://lithnet.io/donate) and help us cover our costs

### Keep up to date
*   [Visit my blog](http://blog.lithiumblue.com)
*   [Follow me on twitter](https://twitter.com/RyanLNewington)![](http://twitter.com/favicon.ico)
