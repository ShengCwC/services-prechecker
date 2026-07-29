using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UndefinedSS.ServicesPrechecker
{
    internal interface IHardwareIdDataSource
    {
        string ReadMachineGuid();
        HardwareIdentifierSnapshot ReadHardwareIdentifiers(TimeSpan timeBudget);
    }

    internal sealed class HardwareIdentifierSnapshot
    {
        public string SystemUuid { get; set; }
        public string BaseboardSerial { get; set; }
        public string BiosSerial { get; set; }
    }

    internal enum HardwareIdSource
    {
        Unavailable,
        SystemUuid,
        BaseboardSerial,
        BiosSerial,
        MachineGuid
    }

    internal sealed class HardwareIdResult
    {
        private HardwareIdResult(
            bool isAvailable,
            string value,
            HardwareIdSource source,
            string errorMessage)
        {
            IsAvailable = isAvailable;
            Value = value;
            Source = source;
            ErrorMessage = errorMessage;
        }

        public bool IsAvailable { get; private set; }
        public string Value { get; private set; }
        public HardwareIdSource Source { get; private set; }
        public string ErrorMessage { get; private set; }

        internal static HardwareIdResult Available(string value, HardwareIdSource source)
        {
            return new HardwareIdResult(true, value, source, null);
        }

        internal static HardwareIdResult Unavailable()
        {
            return new HardwareIdResult(
                false,
                null,
                HardwareIdSource.Unavailable,
                "HWID \u65E0\u6CD5\u8BFB\u53D6");
        }
    }

    internal static class HardwareIdProvider
    {
        internal const string AlgorithmNamespace =
            "UndefinedSS.ServicesPrechecker/HWID/v1";

        private const string PublicPrefix = "USS1";
        private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private static readonly TimeSpan DefaultTotalBudget =
            TimeSpan.FromSeconds(4);
        private static readonly TimeSpan DefaultWmiOperationTimeout =
            TimeSpan.FromSeconds(1);

        private static readonly HashSet<string> PlaceholderTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DEFAULT",
                "DEFAULTSTRING",
                "INVALID",
                "NA",
                "NONE",
                "NOTAPPLICABLE",
                "NOTAVAILABLE",
                "NOTPRESENT",
                "NOTSPECIFIED",
                "NULL",
                "OEM",
                "SERIALNUMBER",
                "SYSTEMSERIALNUMBER",
                "TOBEFILLEDBYOEM",
                "UNKNOWN"
            };

        public static Task<HardwareIdResult> GetHardwareIdAsync()
        {
            return GetHardwareIdAsync(
                new WindowsHardwareIdDataSource(),
                DefaultTotalBudget);
        }

        public static HardwareIdResult GetHardwareId()
        {
            return GetHardwareIdAsync().GetAwaiter().GetResult();
        }

        internal static async Task<HardwareIdResult> GetHardwareIdAsync(
            IHardwareIdDataSource dataSource,
            TimeSpan totalBudget)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException("dataSource");
            }

            if (totalBudget <= TimeSpan.Zero ||
                totalBudget > TimeSpan.FromSeconds(30))
            {
                throw new ArgumentOutOfRangeException(
                    "totalBudget",
                    "The HWID read budget must be between zero and 30 seconds.");
            }

            Stopwatch budgetClock = Stopwatch.StartNew();
            string machineGuid = null;
            try
            {
                // Read this inexpensive fallback first so it is ready if WMI blocks.
                machineGuid = dataSource.ReadMachineGuid();
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }
            }

            HardwareIdResult fallbackResult = ComputeHardwareId(
                null,
                null,
                null,
                machineGuid);

            TimeSpan remainingBudget = totalBudget - budgetClock.Elapsed;
            if (remainingBudget <= TimeSpan.Zero)
            {
                return fallbackResult;
            }

            Task<HardwareIdentifierSnapshot> wmiTask = Task.Factory.StartNew(
                delegate
                {
                    return dataSource.ReadHardwareIdentifiers(remainingBudget);
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            Task completedTask = await Task.WhenAny(
                wmiTask,
                Task.Delay(remainingBudget)).ConfigureAwait(false);

            if (completedTask != wmiTask)
            {
                ObserveFault(wmiTask);
                return fallbackResult;
            }

            HardwareIdentifierSnapshot identifiers;
            try
            {
                identifiers = await wmiTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                return fallbackResult;
            }

            if (identifiers == null)
            {
                return fallbackResult;
            }

            return ComputeHardwareId(
                identifiers.SystemUuid,
                identifiers.BaseboardSerial,
                identifiers.BiosSerial,
                machineGuid);
        }

        // This method is intentionally free of WMI, registry, file, and network access.
        // Tests can pass fixed source values and verify the public identifier exactly.
        internal static HardwareIdResult ComputeHardwareId(
            string systemUuid,
            string baseboardSerial,
            string biosSerial,
            string machineGuid)
        {
            string normalizedUuid = NormalizeGuid(systemUuid);
            string normalizedBaseboard = NormalizeSerial(baseboardSerial);
            string normalizedBios = NormalizeSerial(biosSerial);

            if (normalizedUuid != null)
            {
                return CreateResult("system_uuid", normalizedUuid, HardwareIdSource.SystemUuid);
            }

            if (normalizedBaseboard != null)
            {
                return CreateResult(
                    "baseboard_serial",
                    normalizedBaseboard,
                    HardwareIdSource.BaseboardSerial);
            }

            if (normalizedBios != null)
            {
                return CreateResult("bios_serial", normalizedBios, HardwareIdSource.BiosSerial);
            }

            string normalizedMachineGuid = NormalizeGuid(machineGuid);
            if (normalizedMachineGuid == null)
            {
                return HardwareIdResult.Unavailable();
            }

            return CreateResult(
                "machine_guid",
                normalizedMachineGuid,
                HardwareIdSource.MachineGuid);
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(
                delegate(Task faultedTask)
                {
                    AggregateException observed = faultedTask.Exception;
                    GC.KeepAlive(observed);
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                    TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException ||
                exception is StackOverflowException ||
                exception is ThreadAbortException ||
                exception is AccessViolationException;
        }

        private sealed class WindowsHardwareIdDataSource : IHardwareIdDataSource
        {
            public HardwareIdentifierSnapshot ReadHardwareIdentifiers(
                TimeSpan timeBudget)
            {
                HardwareIdentifierSnapshot identifiers =
                    new HardwareIdentifierSnapshot();
                Stopwatch operationClock = Stopwatch.StartNew();

                try
                {
                    TimeSpan operationTimeout = GetOperationTimeout(
                        timeBudget,
                        operationClock.Elapsed);
                    if (operationTimeout <= TimeSpan.Zero)
                    {
                        return identifiers;
                    }

                    ConnectionOptions connectionOptions = new ConnectionOptions();
                    connectionOptions.Timeout = operationTimeout;

                    ManagementScope scope = new ManagementScope(
                        @"\\.\root\cimv2",
                        connectionOptions);
                    scope.Connect();

                    operationTimeout = GetOperationTimeout(
                        timeBudget,
                        operationClock.Elapsed);
                    if (operationTimeout <= TimeSpan.Zero)
                    {
                        return identifiers;
                    }

                    identifiers.SystemUuid = ReadFirstUsableWmiValue(
                        scope,
                        "SELECT UUID FROM Win32_ComputerSystemProduct",
                        "UUID",
                        true,
                        operationTimeout);
                    if (identifiers.SystemUuid != null)
                    {
                        return identifiers;
                    }

                    operationTimeout = GetOperationTimeout(
                        timeBudget,
                        operationClock.Elapsed);
                    if (operationTimeout <= TimeSpan.Zero)
                    {
                        return identifiers;
                    }

                    identifiers.BaseboardSerial = ReadFirstUsableWmiValue(
                        scope,
                        "SELECT SerialNumber FROM Win32_BaseBoard",
                        "SerialNumber",
                        false,
                        operationTimeout);
                    if (identifiers.BaseboardSerial != null)
                    {
                        return identifiers;
                    }

                    operationTimeout = GetOperationTimeout(
                        timeBudget,
                        operationClock.Elapsed);
                    if (operationTimeout <= TimeSpan.Zero)
                    {
                        return identifiers;
                    }

                    identifiers.BiosSerial = ReadFirstUsableWmiValue(
                        scope,
                        "SELECT SerialNumber FROM Win32_BIOS",
                        "SerialNumber",
                        false,
                        operationTimeout);
                }
                catch (ManagementException)
                {
                    // A blocked or unavailable WMI provider uses MachineGuid fallback.
                }
                catch (UnauthorizedAccessException)
                {
                    // No elevation is requested; restricted systems use the fallback.
                }
                catch (COMException)
                {
                    // WMI service/provider failures are non-fatal for the application.
                }

                return identifiers;
            }

            public string ReadMachineGuid()
            {
                try
                {
                    using (RegistryKey localMachine = RegistryKey.OpenBaseKey(
                        RegistryHive.LocalMachine,
                        RegistryView.Registry64))
                    using (RegistryKey cryptography = localMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Cryptography",
                        false))
                    {
                        if (cryptography == null)
                        {
                            return null;
                        }

                        return Convert.ToString(cryptography.GetValue("MachineGuid"));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (System.Security.SecurityException)
                {
                    return null;
                }
                catch (System.IO.IOException)
                {
                    return null;
                }
            }

            private static TimeSpan GetOperationTimeout(
                TimeSpan timeBudget,
                TimeSpan elapsed)
            {
                TimeSpan remaining = timeBudget - elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return TimeSpan.Zero;
                }

                return remaining < DefaultWmiOperationTimeout
                    ? remaining
                    : DefaultWmiOperationTimeout;
            }

            private static string ReadFirstUsableWmiValue(
                ManagementScope scope,
                string queryText,
                string propertyName,
                bool isGuid,
                TimeSpan operationTimeout)
            {
                List<string> usableValues = new List<string>();

                try
                {
                    EnumerationOptions options = new EnumerationOptions();
                    options.ReturnImmediately = true;
                    options.Rewindable = false;
                    options.Timeout = operationTimeout;

                    using (ManagementObjectSearcher searcher =
                        new ManagementObjectSearcher(
                            scope,
                            new ObjectQuery(queryText),
                            options))
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementBaseObject result in results)
                        {
                            try
                            {
                                object rawValue = result[propertyName];
                                string normalized = isGuid
                                    ? NormalizeGuid(Convert.ToString(rawValue))
                                    : NormalizeSerial(Convert.ToString(rawValue));

                                if (normalized != null &&
                                    !usableValues.Contains(normalized))
                                {
                                    usableValues.Add(normalized);
                                }
                            }
                            finally
                            {
                                if (result != null)
                                {
                                    result.Dispose();
                                }
                            }
                        }
                    }
                }
                catch (ManagementException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (COMException)
                {
                    return null;
                }

                if (usableValues.Count == 0)
                {
                    return null;
                }

                usableValues.Sort(StringComparer.Ordinal);
                return usableValues[0];
            }
        }

        private static string NormalizeGuid(string value)
        {
            string normalized = NormalizeWhitespace(value);
            if (normalized == null || IsPlaceholder(normalized))
            {
                return null;
            }

            Guid parsed;
            if (!Guid.TryParse(normalized, out parsed) ||
                parsed == Guid.Empty ||
                parsed == new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"))
            {
                return null;
            }

            return parsed.ToString("N").ToUpperInvariant();
        }

        private static string NormalizeSerial(string value)
        {
            string normalized = NormalizeWhitespace(value);
            if (normalized == null || IsPlaceholder(normalized))
            {
                return null;
            }

            string compact = KeepLettersAndDigits(normalized);
            if (compact.Length > 0 && IsAllZeroOrAllF(compact))
            {
                return null;
            }

            return normalized.ToUpperInvariant();
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasWhitespace = false;

            foreach (char character in value)
            {
                if (character == '\0')
                {
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (builder.Length > 0 && !previousWasWhitespace)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }
                }
                else
                {
                    builder.Append(character);
                    previousWasWhitespace = false;
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            {
                builder.Length--;
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static bool IsPlaceholder(string value)
        {
            string token = KeepLettersAndDigits(value);
            return token.Length == 0 || PlaceholderTokens.Contains(token);
        }

        private static string KeepLettersAndDigits(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static bool IsAllZeroOrAllF(string value)
        {
            char expected = value[0];
            if (expected != '0' && expected != 'F')
            {
                return false;
            }

            foreach (char character in value)
            {
                if (character != expected)
                {
                    return false;
                }
            }

            return true;
        }

        private static HardwareIdResult CreateResult(
            string sourceTag,
            string normalizedValue,
            HardwareIdSource source)
        {
            string canonicalPayload = string.Join(
                "\n",
                new[]
                {
                    AlgorithmNamespace,
                    "source=" + sourceTag,
                    "value=" + normalizedValue
                });

            return HardwareIdResult.Available(
                CreatePublicIdentifier(canonicalPayload),
                source);
        }

        private static string CreatePublicIdentifier(string canonicalPayload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(payloadBytes);
            }

            byte[] first128Bits = new byte[16];
            Buffer.BlockCopy(digest, 0, first128Bits, 0, first128Bits.Length);
            string encoded = EncodeCrockfordBase32(first128Bits);

            StringBuilder formatted = new StringBuilder(PublicPrefix);
            for (int index = 0; index < encoded.Length; index += 4)
            {
                int remaining = encoded.Length - index;
                formatted.Append('-');
                formatted.Append(encoded, index, Math.Min(4, remaining));
            }

            return formatted.ToString();
        }

        private static string EncodeCrockfordBase32(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 16)
            {
                throw new ArgumentException(
                    "Crockford encoding requires exactly 128 bits.",
                    "bytes");
            }

            // A 128-bit big-endian integer needs 26 Base32 digits. The first
            // digit contains two leading zero bits followed by the top 3 data bits.
            StringBuilder encoded = new StringBuilder(26);
            for (int digitIndex = 0; digitIndex < 26; digitIndex++)
            {
                int alphabetIndex = 0;
                for (int digitBit = 0; digitBit < 5; digitBit++)
                {
                    alphabetIndex <<= 1;
                    int sourceBitIndex = (digitIndex * 5) + digitBit - 2;
                    if (sourceBitIndex >= 0)
                    {
                        int byteIndex = sourceBitIndex / 8;
                        int bitInByte = 7 - (sourceBitIndex % 8);
                        alphabetIndex |= (bytes[byteIndex] >> bitInByte) & 1;
                    }
                }

                encoded.Append(CrockfordAlphabet[alphabetIndex]);
            }

            return encoded.ToString();
        }
    }
}
