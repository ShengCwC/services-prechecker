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
