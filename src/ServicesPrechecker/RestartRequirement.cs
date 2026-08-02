using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class RestartRequirement
    {
        private const string RegistryPath = @"Software\UndefinedSS\ServicesPrechecker";
        private const string BootIdentityValue = "PendingRestartBootIdentity";
        private const string BootIdentitySetPrefix = "boot-set:";
        private const string BootIdentityPrefix = "boot-id:";
        private const string WmiBootIdentityPrefix = "wmi-boot:";
        private const string FallbackBootIdentityPrefix = "fallback-boot:";
        private const string UnknownBootIdentity = "boot-id:unavailable";
        private const string BootIdRegistryPath =
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters";

        public static void MarkPendingForCurrentBoot()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key != null)
                {
                    string identity = SerializeBootIdentities(
                        GetCurrentBootIdentities());
                    key.SetValue(BootIdentityValue, identity, RegistryValueKind.String);
                }
            }
        }

        public static bool IsPendingForCurrentBoot()
        {
            IList<string> currentIdentities = GetCurrentBootIdentities();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null)
                {
                    return false;
                }

                string storedIdentity = key.GetValue(BootIdentityValue) as string;
                if (string.IsNullOrWhiteSpace(storedIdentity))
                {
                    return false;
                }

                if (string.Equals(storedIdentity, UnknownBootIdentity, StringComparison.Ordinal))
                {
                    key.SetValue(
                        BootIdentityValue,
                        SerializeBootIdentities(currentIdentities),
                        RegistryValueKind.String);
                    return true;
                }

                IList<string> storedIdentities = ParseBootIdentities(
                    storedIdentity);
                if (HaveMatchingIdentity(storedIdentities, currentIdentities))
                {
                    key.SetValue(
                        BootIdentityValue,
                        SerializeBootIdentities(currentIdentities),
                        RegistryValueKind.String);
                    return true;
                }

                // Migrate the minute-based identity written by versions before 1.4.0.
                if (string.Equals(
                    storedIdentity,
                    GetLegacyBootIdentity(),
                    StringComparison.Ordinal))
                {
                    key.SetValue(
                        BootIdentityValue,
                        SerializeBootIdentities(currentIdentities),
                        RegistryValueKind.String);
                    return true;
                }

                if (ContainsOnlyFallbackIdentity(storedIdentities) &&
                    ContainsStrongIdentity(currentIdentities))
                {
                    // A stronger source became readable during the same boot. Retain the
                    // gate conservatively rather than treating source recovery as a reboot.
                    key.SetValue(
                        BootIdentityValue,
                        SerializeBootIdentities(currentIdentities),
                        RegistryValueKind.String);
                    return true;
                }

                if (ContainsStrongIdentity(storedIdentities) &&
                    ContainsStrongIdentity(currentIdentities) &&
                    !HaveMatchingStrongIdentityKind(
                        storedIdentities,
                        currentIdentities))
                {
                    // The preferred identity provider changed availability. A provider
                    // transition is not proof of reboot, so retain and migrate safely.
                    key.SetValue(
                        BootIdentityValue,
                        SerializeBootIdentities(currentIdentities),
                        RegistryValueKind.String);
                    return true;
                }

                key.DeleteValue(BootIdentityValue, false);
                return false;
            }
        }

        private static IList<string> ParseBootIdentities(string value)
        {
            string payload = value.StartsWith(
                BootIdentitySetPrefix,
                StringComparison.Ordinal)
                ? value.Substring(BootIdentitySetPrefix.Length)
                : value;
            return new List<string>(payload.Split(
                new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        private static string SerializeBootIdentities(
            IEnumerable<string> identities)
        {
            return BootIdentitySetPrefix + string.Join(",", identities);
        }

        private static bool HaveMatchingIdentity(
            IEnumerable<string> storedIdentities,
            IEnumerable<string> currentIdentities)
        {
            HashSet<string> current = new HashSet<string>(
                currentIdentities,
                StringComparer.Ordinal);
            foreach (string identity in storedIdentities)
            {
                if (current.Contains(identity))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsOnlyFallbackIdentity(
            IEnumerable<string> identities)
        {
            bool found = false;
            foreach (string identity in identities)
            {
                found = true;
                if (!identity.StartsWith(
                    FallbackBootIdentityPrefix,
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return found;
        }

        private static bool ContainsStrongIdentity(
            IEnumerable<string> identities)
        {
            foreach (string identity in identities)
            {
                if (identity.StartsWith(BootIdentityPrefix, StringComparison.Ordinal) ||
                    identity.StartsWith(WmiBootIdentityPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HaveMatchingStrongIdentityKind(
            IEnumerable<string> storedIdentities,
            IEnumerable<string> currentIdentities)
        {
            bool storedHasBootId = HasIdentityWithPrefix(
                storedIdentities,
                BootIdentityPrefix);
            bool currentHasBootId = HasIdentityWithPrefix(
                currentIdentities,
                BootIdentityPrefix);
            bool storedHasWmi = HasIdentityWithPrefix(
                storedIdentities,
                WmiBootIdentityPrefix);
            bool currentHasWmi = HasIdentityWithPrefix(
                currentIdentities,
                WmiBootIdentityPrefix);
            return (storedHasBootId && currentHasBootId) ||
                (storedHasWmi && currentHasWmi);
        }

        private static bool HasIdentityWithPrefix(
            IEnumerable<string> identities,
            string prefix)
        {
            foreach (string identity in identities)
            {
                if (identity.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<string> GetCurrentBootIdentities()
        {
            List<string> identities = new List<string>();
            string registryIdentity = TryGetRegistryBootIdentity();
            if (!string.IsNullOrWhiteSpace(registryIdentity))
            {
                identities.Add(registryIdentity);
            }
            else
            {
                string wmiIdentity = TryGetWmiBootIdentity();
                if (!string.IsNullOrWhiteSpace(wmiIdentity))
                {
                    identities.Add(wmiIdentity);
                }
            }

            identities.Add(FallbackBootIdentityPrefix + GetLegacyBootIdentity());
            return identities;
        }

        private static string TryGetRegistryBootIdentity()
        {
            try
            {
                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                using (RegistryKey key = localMachine.OpenSubKey(
                    BootIdRegistryPath,
                    false))
                {
                    object value = key == null ? null : key.GetValue("BootId");
                    if (value == null)
                    {
                        return null;
                    }

                    ulong bootId = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                    return BootIdentityPrefix +
                        bootId.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetWmiBootIdentity()
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(2);
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            using (item)
                            {
                                string dmtf = Convert.ToString(
                                    item["LastBootUpTime"],
                                    CultureInfo.InvariantCulture);
                                if (string.IsNullOrWhiteSpace(dmtf))
                                {
                                    continue;
                                }

                                DateTime bootTime =
                                    ManagementDateTimeConverter.ToDateTime(dmtf)
                                        .ToUniversalTime();
                                return WmiBootIdentityPrefix +
                                    bootTime.Ticks.ToString(
                                        CultureInfo.InvariantCulture);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string GetLegacyBootIdentity()
        {
            ulong uptimeMilliseconds = GetTickCount64();
            DateTime bootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(uptimeMilliseconds);
            return bootTimeUtc.ToString(
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture);
        }

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
    }
}
