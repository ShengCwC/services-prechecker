using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UndefinedSS.ServicesPrechecker;

internal static class UpdateCheckerTests
{
    private static int failures;

    private static void Main()
    {
        TestRequestContractAndNumericalComparison();
        TestEquivalentAndOlderVersions();
        TestEveryCheckUsesNetwork();
        TestFailureDoesNotSuppressNextCheck();
        TestSilentFailures();
        TestTimeout();

        if (failures != 0)
        {
            Console.Error.WriteLine(failures + " update checker test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All update checker tests passed.");
    }

    private static void TestRequestContractAndNumericalComparison()
    {
        FakeHttpClient httpClient = Responding(
            200,
            "{\"tag_name\":\"v1.10.0\",\"html_url\":\"https://attacker.invalid/file.exe\"}");
        UpdateChecker checker = CreateChecker(httpClient);

        UpdateCheckResult result = checker.CheckAsync("1.9.0")
            .GetAwaiter().GetResult();

        AssertTrue(result.IsCheckAvailable, "successful API result is available");
        AssertTrue(result.IsUpdateAvailable, "1.10.0 is newer than 1.9.0 numerically");
        AssertEqual("v1.10.0", result.LatestVersion, "latest tag is preserved for UI");
        AssertEqual(
            "https://dl.screenshare.cn/services-prechecker",
            UpdateChecker.DownloadUrl,
            "download link is the fixed first-party URL");
        AssertEqual(1, httpClient.CallCount, "one request is issued");
        AssertEqual(
            "https://api.github.com/repos/ShengCwC/services-prechecker/releases/latest",
            httpClient.LastUri.AbsoluteUri,
            "only the GitHub latest-release endpoint is queried");
        AssertEqual(
            "application/vnd.github+json",
            httpClient.LastHeaders.Accept,
            "GitHub JSON Accept header");
        AssertEqual(
            "2022-11-28",
            httpClient.LastHeaders.GitHubApiVersion,
            "GitHub API version header");
        AssertEqual(
            "UndefinedSS-ServicesPrechecker/1.9.0",
            httpClient.LastHeaders.UserAgent,
            "descriptive User-Agent without credentials");
        AssertEqual(TimeSpan.FromSeconds(4), httpClient.LastTimeout, "four-second timeout");
    }

    private static void TestEquivalentAndOlderVersions()
    {
        UpdateCheckResult equivalent = CreateChecker(
            Responding(200, "{\"tag_name\": \"v1.3.0\"}"))
            .CheckAsync("1.3.0.0").GetAwaiter().GetResult();
        AssertTrue(equivalent.IsCheckAvailable, "three-part remote equals four-part local");
        AssertFalse(equivalent.IsUpdateAvailable, "1.3.0 is not newer than 1.3.0.0");

        UpdateCheckResult older = CreateChecker(
            Responding(200, "{\"tag_name\":\"v1.2.9\"}"))
            .CheckAsync("v1.3.0").GetAwaiter().GetResult();
        AssertFalse(older.IsUpdateAvailable, "older release does not prompt");

        FakeHttpClient invalidCurrentClient = Responding(
            200,
            "{\"tag_name\":\"v2.0.0\"}");
        UpdateCheckResult invalidCurrent = CreateChecker(
            invalidCurrentClient).CheckAsync("version one").GetAwaiter().GetResult();
        AssertFalse(invalidCurrent.IsCheckAvailable, "invalid local version is silent");
        AssertEqual(0, invalidCurrentClient.CallCount, "invalid local version never uses network");
    }

    private static void TestEveryCheckUsesNetwork()
    {
        FakeHttpClient httpClient = Responding(
            200,
            "{\"tag_name\":\"v1.4.1\"}");
        UpdateChecker checker = CreateChecker(httpClient);

        UpdateCheckResult first = checker.CheckAsync("1.4.0")
            .GetAwaiter().GetResult();
        AssertTrue(first.IsUpdateAvailable, "first launch discovers the update");

        httpClient.Response = new UpdateHttpResponse(
            200,
            "{\"tag_name\":\"v1.4.2\"}");
        UpdateCheckResult second = checker.CheckAsync("1.4.0")
            .GetAwaiter().GetResult();
        AssertTrue(second.IsUpdateAvailable, "later launch checks again");
        AssertEqual("v1.4.2", second.LatestVersion, "later launch sees the new release");
        AssertEqual(2, httpClient.CallCount, "each independent check uses the network");
    }

    private static void TestFailureDoesNotSuppressNextCheck()
    {
        FakeHttpClient httpClient = new FakeHttpClient();
        httpClient.Exception = new InvalidOperationException("Injected network failure.");
        UpdateChecker checker = CreateChecker(httpClient);

        UpdateCheckResult failed = checker.CheckAsync("1.4.0")
            .GetAwaiter().GetResult();
        AssertFalse(failed.IsCheckAvailable, "failure is silent");

        httpClient.Exception = null;
        httpClient.Response = new UpdateHttpResponse(
            200,
            "{\"tag_name\":\"v1.4.1\"}");
        UpdateCheckResult recovered = checker.CheckAsync("1.4.0")
            .GetAwaiter().GetResult();
        AssertTrue(recovered.IsUpdateAvailable, "next launch retries after a failure");
        AssertEqual(2, httpClient.CallCount, "failure creates no cross-launch backoff");
    }

    private static void TestSilentFailures()
    {
        AssertSilentFailure(
            Responding(403, null),
            "GitHub rate limit is silent");
        AssertSilentFailure(
            Responding(500, "server error"),
            "server error is silent");
        AssertSilentFailure(
            Responding(200, "{}"),
            "missing tag is silent");
        AssertSilentFailure(
            Responding(200, "not-json {\"tag_name\":\"v9.0.0\"}"),
            "malformed JSON containing a tag-like fragment is rejected");
        AssertSilentFailure(
            Responding(200, "{\"tag_name\":\"v1.4.0-beta\"}"),
            "prerelease-shaped tag is rejected");
        AssertSilentFailure(
            Responding(200, "{\"tag_name\":\"v01.4.0\"}"),
            "non-canonical numeric tag is rejected");
        AssertSilentFailure(
            Responding(200, "{\"tag_name\":\"1.4.0\"}"),
            "tag without v prefix is rejected");

        FakeHttpClient exceptionClient = new FakeHttpClient();
        exceptionClient.Exception = new InvalidOperationException("Injected network failure.");
        AssertSilentFailure(exceptionClient, "network exception is silent");

        FakeHttpClient nullTaskClient = new FakeHttpClient();
        nullTaskClient.ReturnNullTask = true;
        AssertSilentFailure(nullTaskClient, "invalid client response is silent");
    }

    private static void TestTimeout()
    {
        FakeHttpClient blockedClient = new FakeHttpClient();
        blockedClient.PendingResponse = new TaskCompletionSource<UpdateHttpResponse>();
        UpdateChecker checker = new UpdateChecker(
            blockedClient,
            TimeSpan.FromMilliseconds(30));

        Stopwatch stopwatch = Stopwatch.StartNew();
        UpdateCheckResult result = checker.CheckAsync("1.3.0")
            .GetAwaiter().GetResult();
        stopwatch.Stop();

        AssertFalse(result.IsCheckAvailable, "timeout is silent");
        AssertTrue(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(500),
            "checker enforces total request timeout");
        AssertEqual(TimeSpan.FromMilliseconds(30), blockedClient.LastTimeout, "timeout is passed to client");
    }

    private static void AssertSilentFailure(FakeHttpClient httpClient, string name)
    {
        UpdateCheckResult result = CreateChecker(httpClient)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertFalse(result.IsCheckAvailable, name);
        AssertFalse(result.IsUpdateAvailable, name + " does not prompt");
        AssertEqual(null, result.LatestVersion, name + " has no displayed version");
    }

    private static UpdateChecker CreateChecker(FakeHttpClient httpClient)
    {
        return new UpdateChecker(httpClient);
    }

    private static FakeHttpClient Responding(int statusCode, string content)
    {
        FakeHttpClient client = new FakeHttpClient();
        client.Response = new UpdateHttpResponse(statusCode, content);
        return client;
    }

    private sealed class FakeHttpClient : IUpdateHttpClient
    {
        public UpdateHttpResponse Response { get; set; }
        public Exception Exception { get; set; }
        public bool ReturnNullTask { get; set; }
        public TaskCompletionSource<UpdateHttpResponse> PendingResponse { get; set; }
        public int CallCount { get; private set; }
        public Uri LastUri { get; private set; }
        public UpdateHttpHeaders LastHeaders { get; private set; }
        public TimeSpan LastTimeout { get; private set; }

        public Task<UpdateHttpResponse> GetAsync(
            Uri uri,
            UpdateHttpHeaders headers,
            TimeSpan timeout)
        {
            CallCount++;
            LastUri = uri;
            LastHeaders = headers;
            LastTimeout = timeout;

            if (ReturnNullTask)
            {
                return null;
            }

            if (Exception != null)
            {
                TaskCompletionSource<UpdateHttpResponse> failed =
                    new TaskCompletionSource<UpdateHttpResponse>();
                failed.SetException(Exception);
                return failed.Task;
            }

            if (PendingResponse != null)
            {
                return PendingResponse.Task;
            }

            return Task.FromResult(Response);
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
