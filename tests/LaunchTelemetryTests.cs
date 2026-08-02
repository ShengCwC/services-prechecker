using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UndefinedSS.ServicesPrechecker;

internal static class LaunchTelemetryTests
{
    private static int failures;

    private static void Main()
    {
        TestFallbackReusesOneAnonymousEvent().GetAwaiter().GetResult();
        TestInvalidPayloadIsRejectedLocally().GetAwaiter().GetResult();

        if (failures != 0)
        {
            Console.Error.WriteLine(failures + " launch telemetry test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All launch telemetry tests passed.");
    }

    private static async Task TestFallbackReusesOneAnonymousEvent()
    {
        FakeHttpClient client = new FakeHttpClient(false, true);
        LaunchTelemetry telemetry = new LaunchTelemetry(
            client,
            new[]
            {
                new Uri("https://primary.example.test/usage"),
                new Uri("https://fallback.example.test/usage")
            },
            TimeSpan.FromSeconds(1));

        string eventId = "5c4f24f5-1029-4fef-87c4-8d1cba0df2f2";
        bool recorded = await telemetry.RecordAsync(eventId, "1.4.3");

        AssertTrue(recorded, "fallback records the launch");
        AssertEqual(2, client.Calls.Count, "primary failure uses one fallback");
        AssertEqual(eventId, client.Calls[0].EventId, "primary event ID");
        AssertEqual(eventId, client.Calls[1].EventId, "fallback reuses event ID");
        AssertEqual("1.4.3", client.Calls[1].Version, "version is included");
    }

    private static async Task TestInvalidPayloadIsRejectedLocally()
    {
        FakeHttpClient client = new FakeHttpClient(true);
        LaunchTelemetry telemetry = new LaunchTelemetry(client);

        AssertFalse(
            await telemetry.RecordAsync("device-id", "1.4.3"),
            "non-random event ID is rejected");
        AssertFalse(
            await telemetry.RecordAsync(
                "5c4f24f5-1029-4fef-87c4-8d1cba0df2f2",
                "latest"),
            "non-version payload is rejected");
        AssertEqual(0, client.Calls.Count, "invalid payload performs no request");
    }

    private sealed class FakeHttpClient : ILaunchTelemetryHttpClient
    {
        private readonly Queue<bool> results;

        internal FakeHttpClient(params bool[] results)
        {
            this.results = new Queue<bool>(results);
            Calls = new List<Call>();
        }

        internal List<Call> Calls { get; private set; }

        public Task<bool> PostAsync(
            Uri endpoint,
            string eventId,
            string version,
            TimeSpan timeout)
        {
            Calls.Add(new Call(endpoint, eventId, version));
            bool result = results.Count > 0 && results.Dequeue();
            return Task.FromResult(result);
        }
    }

    private sealed class Call
    {
        internal Call(Uri endpoint, string eventId, string version)
        {
            Endpoint = endpoint;
            EventId = eventId;
            Version = version;
        }

        internal Uri Endpoint { get; private set; }
        internal string EventId { get; private set; }
        internal string Version { get; private set; }
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
