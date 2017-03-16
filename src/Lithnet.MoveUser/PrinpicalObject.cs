using System;
using System.Security.Principal;

namespace Lithnet.Moveuser
{
    public class PrincipalObject
    {
        private string domain;
        private string sid;
        private string accountName;
        private SecurityIdentifier sidPrincipal;
        private string profilePath;

        public string Domain
        {
            get
            {
                return Helper.StringIsNullOrWhiteSpace(this.domain) ? "." : this.domain;
            }
            set
            {
                if (string.Equals(value, SystemManagement.GetActiveComputerName(), StringComparison.OrdinalIgnoreCase))
                {
                    this.domain = ".";
                }
                else if (string.IsNullOrEmpty(value))
                {
                    this.domain = ".";
                }
                else
                {
                    this.domain = value;
                }
            }
        }

        public string DomainOrComputerName
        {
            get
            {
                if (Helper.StringIsNullOrWhiteSpace(this.domain))
                {
                    return SystemManagement.GetActiveComputerName();
                }
                else if (this.domain == ".")
                {
                    return SystemManagement.GetActiveComputerName();
                }
                else
                {
                    return this.domain;
                }
            }
        }

        public string AccountName
        {
            get
            {
                return Helper.StringIsNullOrWhiteSpace(this.accountName) ? string.Empty : this.accountName;
            }
            set
            {
                this.accountName = value;
            }
        }

        public string FqUserName
        {
            get
            {
                if (!Helper.StringIsNullOrWhiteSpace(this.AccountName))
                {
                    return this.Domain + "\\" + this.AccountName;
                }

                return this.Sid.IsNullOrWhiteSpace() ? string.Empty : this.Sid;
            }
            set
            {
                this.AccountName = PrincipalObject.GetUsernameFromFqUsername(value);
                this.Domain = PrincipalObject.GetDomainFromFqUsername(value);
            }
        }

        public string FqUserNameExpanded
        {
            get
            {
                if (Helper.StringIsNullOrWhiteSpace(this.AccountName))
                {
                    if (this.Sid.IsNullOrWhiteSpace())
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return this.Sid;
                    }
                }

                return this.DomainOrComputerName + "\\" + this.AccountName;
            }
        }

        public string UserPrincipalName
        {
            get
            {
                return this.AccountName + "@" + this.Domain;
            }
            set
            {
                this.AccountName = PrincipalObject.GetUsernameFromFqUsername(value);
                this.Domain = PrincipalObject.GetDomainFromFqUsername(value);
            }
        }

        public string Sid
        {
            get
            {
                if (Helper.StringIsNullOrWhiteSpace(this.sid))
                {
                    if (this.sidPrincipal == null)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        this.sid = this.sidPrincipal.ToString();
                    }
                }

                return this.sid;
            }
            set
            {
                this.sid = value;
            }
        }

        public SecurityIdentifier SidPrincipal
        {
            get
            {
                if (this.sidPrincipal == null)
                {
                    if (Helper.StringIsNullOrWhiteSpace(this.sid))
                    {
                        return null;
                    }
                    else
                    {
                        this.sidPrincipal = new SecurityIdentifier(this.sid);
                    }
                }
                return this.sidPrincipal;
            }
            set
            {
                this.sidPrincipal = value;
            }
        }

        public PrincipalObject(string fqUserName)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
        }

        public PrincipalObject(string fqUserName, string userSid)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.Sid = userSid;
        }

        public PrincipalObject(string fqUserName, SecurityIdentifier userSid)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.SidPrincipal = userSid;
        }

        public PrincipalObject(string fqUserName, byte[] userSid)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.SidPrincipal = new SecurityIdentifier(userSid, 0);
        }

        public PrincipalObject(string fqUserName, string userSid, string userProfilePath)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.Sid = userSid;
            this.profilePath = userProfilePath;
        }

        public PrincipalObject(string fqUserName, SecurityIdentifier userSid, string userProfilePath)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.SidPrincipal = userSid;
            this.profilePath = userProfilePath;
        }

        public PrincipalObject(string fqUserName, byte[] userSid, string userProfilePath)
        {
            this.AccountName = PrincipalObject.GetUsernameFromFqUsername(fqUserName);
            this.Domain = PrincipalObject.GetDomainFromFqUsername(fqUserName);
            this.SidPrincipal = new SecurityIdentifier(userSid, 0);
            this.profilePath = userProfilePath;
        }

        public PrincipalObject()
        {
        }

        internal bool IsLocalUser()
        {
            if (this.accountName.IsNullOrWhiteSpace())
            {
                SecurityIdentifier machinesid = PrincipalManagement.GetMachineSid();
                return this.SidPrincipal.IsEqualDomainSid(machinesid);
            }
            else
            {
                return this.Domain == ".";
            }
        }

        internal bool IsDomainUser()
        {
            return this.Domain != ".";
        }

        private static string GetDomainFromFqUsername(string username)
        {
            int atIndex = username.IndexOf("@", StringComparison.Ordinal);
            int slashIndex = username.IndexOf("\\", StringComparison.Ordinal);

            if (atIndex == -1)
            {
                return slashIndex == -1 ? "." : username.Substring(0, slashIndex);
            }

            //username is in user@domain format
            string domain = username.Substring(atIndex + 1, username.Length - atIndex - 1);
            return (Helper.StringIsNullOrWhiteSpace(domain) ? "." : domain);
        }

        private static string GetUsernameFromFqUsername(string username)
        {
            int atIndex = username.IndexOf("@", StringComparison.Ordinal);
            int slashIndex = username.IndexOf("\\", StringComparison.Ordinal);

            if (atIndex != -1)
            {
                //username is in user@domain format
                return username.Substring(0, atIndex);
            }

            return slashIndex == -1 ? username : username.Substring(slashIndex + 1, username.Length - slashIndex - 1);
        }

        public string ProfilePath
        {
            get
            {
                if (Helper.StringIsNullOrWhiteSpace(this.profilePath))
                {
                    this.profilePath = PrincipalManagement.GetUserProfilePath(this);
                }
                return this.profilePath;
            }
            set
            {
                this.profilePath = value;
            }
        }
    }
}
