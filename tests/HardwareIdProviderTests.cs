using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using UndefinedSS.ServicesPrechecker;

internal static class HardwareIdProviderTests
{
    private static int failures;

    private static void Main()
    {
        HardwareIdResult systemUuid = HardwareIdProvider.ComputeHardwareId(
            "00112233-4455-6677-8899-aabbccddeeff",
            "  board   42  ",
            "bios-007",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        AssertEqual(HardwareIdSource.SystemUuid, systemUuid.Source, "System UUID source");
        AssertEqual(
            "USS1-7DVF-T4XB-JCG0-JD40-H7T4-62SV-FT",
            systemUuid.Value,
            "System UUID vector");

        HardwareIdResult normalizedSystemUuid = HardwareIdProvider.ComputeHardwareId(
            "{00112233-4455-6677-8899-AABBCCDDEEFF}",
            "BOARD 42",
            "BIOS-007",
            null);
        AssertEqual(
            systemUuid.Value,
            normalizedSystemUuid.Value,
            "UUID normalization preserves identifier");

        HardwareIdResult baseboard = HardwareIdProvider.ComputeHardwareId(
            "00000000-0000-0000-0000-000000000000",
            "board-only",
            "To Be Filled By O.E.M.",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        AssertEqual(
            HardwareIdSource.BaseboardSerial,
            baseboard.Source,
            "baseboard fallback source");
        AssertEqual(
            "USS1-0RWW-AP1M-3VY3-QEM8-2VNG-ETM3-77",
            baseboard.Value,
            "baseboard vector");

        HardwareIdResult bios = HardwareIdProvider.ComputeHardwareId(
            "not-a-guid",
            "Default String",
            "bios-007",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        AssertEqual(HardwareIdSource.BiosSerial, bios.Source, "BIOS fallback source");
        AssertEqual(
            "USS1-586M-HGH8-ZCVR-532S-9JQZ-2HA5-4Y",
            bios.Value,
            "BIOS vector");

        HardwareIdResult machineGuid = HardwareIdProvider.ComputeHardwareId(
            "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            "Default String",
            "System Serial Number",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        AssertEqual(
            HardwareIdSource.MachineGuid,
            machineGuid.Source,
            "MachineGuid fallback source");
        AssertEqual(
            "USS1-3G0A-EKEH-3SEB-YX9H-270Z-D262-ZJ",
            machineGuid.Value,
            "MachineGuid vector");

        HardwareIdResult hardwareIgnoresMachineGuid =
            HardwareIdProvider.ComputeHardwareId(
                null,
                "board-only",
                null,
                "11111111-2222-3333-4444-555555555555");
        AssertEqual(
            baseboard.Value,
            hardwareIgnoresMachineGuid.Value,
            "MachineGuid never contributes when hardware is usable");

        HardwareIdResult priorityIgnoresLowerSources =
            HardwareIdProvider.ComputeHardwareId(
                "00112233-4455-6677-8899-aabbccddeeff",
                "a-different-board",
                "a-different-bios",
                null);
        AssertEqual(
            systemUuid.Value,
            priorityIgnoresLowerSources.Value,
            "lower-priority sources do not change a System UUID identifier");

        HardwareIdResult leadingZeroVector = HardwareIdProvider.ComputeHardwareId(
            null,
            "leading-1",
            null,
            null);
        AssertEqual(
            "USS1-00SZ-NFBS-RRM8-QEQ8-J6JR-GFJS-VF",
            leadingZeroVector.Value,
            "128-bit big-endian integer encoding left-pads zero symbols");

        HardwareIdResult nulNormalized = HardwareIdProvider.ComputeHardwareId(
            null,
            "\0  board\0   42 \0",
            null,
            null);
        HardwareIdResult nulReference = HardwareIdProvider.ComputeHardwareId(
            null,
            "BOARD 42",
            null,
            null);
        AssertEqual(
            nulReference.Value,
            nulNormalized.Value,
            "NUL removal and whitespace normalization");

        foreach (string placeholder in new[]
        {
            "OEM",
            "N/A",
            "INVALID",
            "Serial Number",
            "0",
            "F",
            "000000",
            "FFFFFF"
        })
        {
            HardwareIdResult placeholderResult =
                HardwareIdProvider.ComputeHardwareId(
                    null,
                    placeholder,
                    null,
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            AssertEqual(
                HardwareIdSource.MachineGuid,
                placeholderResult.Source,
                "placeholder rejected: " + placeholder);
        }

        HardwareIdResult mixedZeroAndF = HardwareIdProvider.ComputeHardwareId(
            null,
            "F0F0",
            null,
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        AssertEqual(
            HardwareIdSource.BaseboardSerial,
            mixedZeroAndF.Source,
            "mixed zero/F serial is not mistaken for an all-zero or all-F placeholder");

        HardwareIdResult unavailable = HardwareIdProvider.ComputeHardwareId(
            "not-a-guid",
            "unknown",
            "00000000",
            "not-a-guid");
        AssertFalse(unavailable.IsAvailable, "invalid sources are unavailable");
        AssertEqual(HardwareIdSource.Unavailable, unavailable.Source, "unavailable source");
        AssertEqual(
            "HWID \u65E0\u6CD5\u8BFB\u53D6",
            unavailable.ErrorMessage,
            "safe failure message");

        TestInjectedDataSources(systemUuid.Value, machineGuid.Value);

        AssertTrue(
            Regex.IsMatch(
                systemUuid.Value,
                @"^USS1-[0-7][0-9A-HJKMNP-TV-Z]{3}(?:-[0-9A-HJKMNP-TV-Z]{4}){5}-[0-9A-HJKMNP-TV-Z]{2}$"),
            "public format and Crockford alphabet");

        if (failures != 0)
        {
            Console.Error.WriteLine(failures + " HWID test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All HWID tests passed.");
    }

    private static void TestInjectedDataSources(
        string expectedHardwareId,
        string expectedMachineGuidId)
    {
        TestDataSource immediateHardware = new TestDataSource();
        immediateHardware.MachineGuid =
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        immediateHardware.Snapshot = new HardwareIdentifierSnapshot
        {
            SystemUuid = "00112233-4455-6677-8899-aabbccddeeff"
        };

        HardwareIdResult hardwareResult =
            HardwareIdProvider.GetHardwareIdAsync(
                immediateHardware,
                TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        AssertTrue(
            immediateHardware.WmiObservedMachineGuidRead,
            "MachineGuid is read before WMI starts");
        AssertEqual(expectedHardwareId, hardwareResult.Value, "injected WMI result");

        TestDataSource immediateFailure = new TestDataSource();
        immediateFailure.MachineGuid =
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        immediateFailure.ThrowFromWmi = true;

        HardwareIdResult exceptionFallback =
            HardwareIdProvider.GetHardwareIdAsync(
                immediateFailure,
                TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        AssertEqual(
            expectedMachineGuidId,
            exceptionFallback.Value,
            "WMI exception uses pre-read MachineGuid");

        TestDataSource blockedWmi = new TestDataSource();
        blockedWmi.MachineGuid =
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        blockedWmi.WmiDelayMilliseconds = 400;
        blockedWmi.ThrowFromWmi = true;

        Stopwatch clock = Stopwatch.StartNew();
        HardwareIdResult timeoutFallback =
            HardwareIdProvider.GetHardwareIdAsync(
                blockedWmi,
                TimeSpan.FromMilliseconds(40)).GetAwaiter().GetResult();
        clock.Stop();

        AssertEqual(
            expectedMachineGuidId,
            timeoutFallback.Value,
            "blocked WMI uses pre-read MachineGuid");
        AssertTrue(
            blockedWmi.WmiObservedMachineGuidRead,
            "blocked WMI starts after MachineGuid read");
        AssertTrue(
            clock.Elapsed < TimeSpan.FromMilliseconds(300),
            "provider enforces its total WMI budget");

        // Let the abandoned task fault. The provider's continuation observes it.
        Thread.Sleep(450);
    }

    private sealed class TestDataSource : IHardwareIdDataSource
    {
        public string MachineGuid { get; set; }
        public HardwareIdentifierSnapshot Snapshot { get; set; }
        public int WmiDelayMilliseconds { get; set; }
        public bool ThrowFromWmi { get; set; }
        public bool MachineGuidWasRead { get; private set; }
        public bool WmiObservedMachineGuidRead { get; private set; }

        public string ReadMachineGuid()
        {
            MachineGuidWasRead = true;
            return MachineGuid;
        }

        public HardwareIdentifierSnapshot ReadHardwareIdentifiers(TimeSpan timeBudget)
        {
            WmiObservedMachineGuidRead = MachineGuidWasRead;
            if (WmiDelayMilliseconds > 0)
            {
                Thread.Sleep(WmiDelayMilliseconds);
            }

            if (ThrowFromWmi)
            {
                throw new InvalidOperationException("Injected WMI failure.");
            }

            return Snapshot;
        }
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
        {
            failures++;
            Console.Error.WriteLine("FAIL: " + name);
        }
    }

    private static void AssertFalse(bool condition, string name)
    {
        AssertTrue(!condition, name);
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!object.Equals(expected, actual))
        {
            failures++;
            Console.Error.WriteLine(
                "FAIL: " + name + Environment.NewLine +
                "  expected: " + expected + Environment.NewLine +
                "  actual:   " + actual);
        }
    }
}
