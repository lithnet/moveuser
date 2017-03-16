using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lithnet.Moveuser
{
    public class RegistryManagement
    {
        private static RegistryKey hklm64BitRegistryView;

        private static RegistryKey hku64BitRegistryView;

        private static RegistryKey hkcu64BitRegistryView;

        internal static RegistryKey Hklm64BitRegistryView
        {
            get
            {
                if (hklm64BitRegistryView == null)
                {
                    hklm64BitRegistryView = Advapi32.OpenWow64Key(Registry.LocalMachine, string.Empty, true, RegWow64Options.KeyWow6464Key);
                }

                return hklm64BitRegistryView;
            }
        }

        internal static RegistryKey Hku64BitRegistryView
        {
            get
            {
                if (hku64BitRegistryView == null)
                {
                    hku64BitRegistryView = Advapi32.OpenWow64Key(Registry.Users, string.Empty, true, RegWow64Options.KeyWow6464Key);
                }

                return hku64BitRegistryView;
            }
        }

        internal static RegistryKey Hkcu64BitRegistryView
        {
            get
            {
                if (hkcu64BitRegistryView == null)
                {
                    hkcu64BitRegistryView = Advapi32.OpenWow64Key(Registry.CurrentUser, string.Empty, true, RegWow64Options.KeyWow6464Key);
                }

                return hkcu64BitRegistryView;
            }
        }

        internal static bool RenameSubKey(RegistryKey parentKey, string subKeyName, string newSubKeyName)
        {
            CopyKey(parentKey, subKeyName, newSubKeyName);
            parentKey.DeleteSubKeyTree(subKeyName);
            return true;
        }

        internal static bool CopyKey(RegistryKey parentKey, string keyNameToCopy, string newKeyName)
        {
            RegistryKey destinationKey = parentKey.CreateSubKey(newKeyName);
            RegistryKey sourceKey = parentKey.OpenSubKey(keyNameToCopy);
            RecursiveKeyCopy(sourceKey, destinationKey);

            return true;
        }

        internal static void UnloadRegFile(string file)
        {
            Advapi32.UnloadRegFile(file);
        }

        internal static void LoadRegFileKey(string file, string keyname)
        {
            Advapi32.LoadRegFileKey(file, keyname);
        }

        private static void RecursiveKeyCopy(RegistryKey sourceKey, RegistryKey destinationKey)
        {
            foreach (string valueName in sourceKey.GetValueNames())
            {
                object objValue = sourceKey.GetValue(valueName);
                RegistryValueKind valueKind = sourceKey.GetValueKind(valueName);
                destinationKey.SetValue(valueName, objValue, valueKind);
            }

            foreach (string sourceSubKeyName in sourceKey.GetSubKeyNames())
            {
                RegistryKey sourceSubKey = sourceKey.OpenSubKey(sourceSubKeyName);
                RegistryKey destinationSubKey = destinationKey.CreateSubKey(sourceSubKeyName);
                RecursiveKeyCopy(sourceSubKey, destinationSubKey);
            }
        }
    }
}