using System;
using System.Collections.Generic;
using System.ServiceProcess;
using UndefinedSS.ServicesPrechecker;

internal static class ServiceStatePolicyTests
{
    private static int failures;

    private static void Main()
    {
        TestDisabledRunningIsNotHealthy();
        TestWrongStartTypeRunningIsNotHealthy();
        TestCorrectRunningIsHealthy();
        TestRestartPolicyTracksActualChangesOnly();
        TestDisabledServiceIsReconfiguredBeforeStart();
        TestConfigurationMustBeConfirmedBeforeStart();
        TestDriverConfigurationWaitsForRestart();
        TestWindowsServiceConfigurationReadPath();

        if (failures != 0)
        {
            Console.Error.WriteLine(failures + " service policy test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All service state policy tests passed.");
    }

    private static void TestDisabledRunningIsNotHealthy()
    {
        ServiceSnapshot snapshot = CreateSnapshot(2);
        ServiceManager.ApplyObservedState(
            snapshot,
            4,
            ServiceControllerStatus.Running);
        AssertEqual(
            ServiceVisualState.Disabled,
            snapshot.VisualState,
            "disabled service stays unhealthy even while temporarily running");
        AssertFalse(snapshot.IsHealthy, "disabled running service is not healthy");
    }

    private static void TestWrongStartTypeRunningIsNotHealthy()
    {
        ServiceSnapshot snapshot = CreateSnapshot(2);
        ServiceManager.ApplyObservedState(
            snapshot,
            3,
            ServiceControllerStatus.Running);
        AssertEqual(
            ServiceVisualState.Stopped,
            snapshot.VisualState,
            "wrong start type is attention state");
        AssertEqual("启动方式异常", snapshot.StatusText, "misconfiguration is explicit");
    }

    private static void TestCorrectRunningIsHealthy()
    {
        ServiceSnapshot snapshot = CreateSnapshot(2);
        ServiceManager.ApplyObservedState(
            snapshot,
            2,
            ServiceControllerStatus.Running);
        AssertEqual(
            ServiceVisualState.Running,
            snapshot.VisualState,
            "correct running service is healthy");
        AssertTrue(snapshot.IsHealthy, "correct running service reports healthy");
    }

    private static void TestRestartPolicyTracksActualChangesOnly()
    {
        AssertFalse(
            ServiceManager.RequiresRestartAfterEnable(
                new[] { new EnableResult { Success = true } }),
            "already-healthy success does not invalidate current boot");
        AssertTrue(
            ServiceManager.RequiresRestartAfterEnable(
                new[] { new EnableResult { ConfigurationChanged = true } }),
            "configuration change requires restart");
        AssertTrue(
            ServiceManager.RequiresRestartAfterEnable(
                new[] { new EnableResult { RuntimeStateChanged = true } }),
            "runtime state change requires restart");
        AssertTrue(
            ServiceManager.RequiresRestartAfterEnable(
                new[] { new EnableResult { RequiresRestart = true } }),
            "driver restart requirement is preserved");
    }

    private static void TestDisabledServiceIsReconfiguredBeforeStart()
    {
        FakeServiceOperations operations = new FakeServiceOperations
        {
            StartType = 4,
            Status = ServiceControllerStatus.Stopped
        };
        ServiceDefinition definition = CreateDefinition(2, false);

        EnableResult result = ServiceManager.Enable(definition, operations);

        AssertTrue(result.Success, "disabled service is repaired and started");
        AssertTrue(result.ConfigurationChanged, "disabled start type is changed");
        AssertTrue(result.RuntimeStateChanged, "stopped runtime is started");
        AssertEqual(2, operations.StartType, "desired automatic start type is applied");
        AssertEqual(
            "read,configure:2,read,status,start,status",
            string.Join(",", operations.Calls.ToArray()),
            "SCM configuration is confirmed before the start request");
    }

    private static void TestConfigurationMustBeConfirmedBeforeStart()
    {
        FakeServiceOperations operations = new FakeServiceOperations
        {
            StartType = 4,
            IgnoreConfigurationChange = true,
            Status = ServiceControllerStatus.Stopped
        };

        EnableResult result = ServiceManager.Enable(
            CreateDefinition(2, false),
            operations);

        AssertFalse(result.Success, "unconfirmed configuration is not reported successful");
        AssertFalse(operations.StartWasCalled, "service is not started while SCM still reports disabled");
        AssertEqual(
            "read,configure:2,read",
            string.Join(",", operations.Calls.ToArray()),
            "configuration verification stops the unsafe flow");
    }

    private static void TestDriverConfigurationWaitsForRestart()
    {
        FakeServiceOperations operations = new FakeServiceOperations
        {
            StartType = 4,
            Status = ServiceControllerStatus.Stopped
        };

        EnableResult result = ServiceManager.Enable(
            CreateDefinition(1, true),
            operations);

        AssertTrue(result.Success, "driver start type repair succeeds");
        AssertTrue(result.RequiresRestart, "boot driver waits for restart");
        AssertFalse(operations.StartWasCalled, "boot driver is not force-started in the current boot");
        AssertEqual(1, operations.StartType, "driver receives its required system start type");
    }

    private static void TestWindowsServiceConfigurationReadPath()
    {
        WindowsServiceOperations operations = new WindowsServiceOperations();
        int? eventLogStartType = operations.ReadStartType("EventLog");

        AssertTrue(eventLogStartType.HasValue, "Windows Event Log configuration is readable from SCM");
        AssertTrue(
            eventLogStartType.HasValue &&
            eventLogStartType.Value >= 0 &&
            eventLogStartType.Value <= 4,
            "SCM returns a valid Windows start type");
        AssertEqual(
            null,
            operations.ReadStartType("UndefinedSS_Missing_Service_For_Test"),
            "missing SCM service returns no configuration");
    }

    private static ServiceSnapshot CreateSnapshot(int desiredStartType)
    {
        return new ServiceSnapshot
        {
            Definition = new ServiceDefinition(
                "Test",
                "TestService",
                "Test service",
                desiredStartType,
                false),
            Detail = string.Empty
        };
    }

    private static ServiceDefinition CreateDefinition(int desiredStartType, bool isDriver)
    {
        return new ServiceDefinition(
            "Test",
            "TestService",
            "Test service",
            desiredStartType,
            isDriver);
    }

    private sealed class FakeServiceOperations : IServiceOperations
    {
        internal FakeServiceOperations()
        {
            Calls = new List<string>();
        }

        internal int? StartType { get; set; }
        internal ServiceControllerStatus Status { get; set; }
        internal bool IgnoreConfigurationChange { get; set; }
        internal bool StartWasCalled { get; private set; }
        internal List<string> Calls { get; private set; }

        public int? ReadStartType(string serviceName)
        {
            Calls.Add("read");
            return StartType;
        }

        public void ChangeStartType(string serviceName, int startType)
        {
            Calls.Add("configure:" + startType);
            if (!IgnoreConfigurationChange)
            {
                StartType = startType;
            }
        }

        public ServiceControllerStatus GetStatus(string serviceName)
        {
            Calls.Add("status");
            return Status;
        }

        public void StartAndWaitForRunning(string serviceName, TimeSpan timeout)
        {
            Calls.Add("start");
            StartWasCalled = true;
            Status = ServiceControllerStatus.Running;
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
