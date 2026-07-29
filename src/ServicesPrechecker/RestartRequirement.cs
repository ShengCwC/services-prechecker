using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class RestartRequirement
    {
        private const string RegistryPath = @"Software\UndefinedSS\ServicesPrechecker";
        private const string BootIdentityValue = "PendingRestartBootIdentity";

        public static void MarkPendingForCurrentBoot()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key != null)
                {
                    key.SetValue(BootIdentityValue, GetCurrentBootIdentity(), RegistryValueKind.String);
                }
            }
        }

        public static bool IsPendingForCurrentBoot()
        {
            string currentIdentity = GetCurrentBootIdentity();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null)
                {
                    return false;
                }

                string storedIdentity = key.GetValue(BootIdentityValue) as string;
                if (string.Equals(storedIdentity, currentIdentity, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(storedIdentity))
                {
                    key.DeleteValue(BootIdentityValue, false);
                }

                return false;
            }
        }

        private static string GetCurrentBootIdentity()
        {
            ulong uptimeMilliseconds = GetTickCount64();
            DateTime bootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(uptimeMilliseconds);
            return bootTimeUtc.ToString("yyyyMMddHHmm");
        }

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
    }
}

