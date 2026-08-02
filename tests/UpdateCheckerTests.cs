using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UndefinedSS.ServicesPrechecker;

internal static class UpdateCheckerTests
{
    private static int failures;
    private static readonly DateTime TestNow = new DateTime(
        2026,
        8,
        2,
        3,
        0,
        0,
        DateTimeKind.Utc);

    private static void Main()
    {
        TestRequestContractAndNumericalComparison();
        TestEquivalentAndOlderVersions();
        TestSuccessfulCache();
        TestFailureBackoff();
        TestSilentFailures();
        TestTimeout();
        TestCacheExceptionsAreNonFatal();

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
        MemoryCache cache = new MemoryCache();
        UpdateChecker checker = CreateChecker(httpClient, cache);

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
        AssertEqual(TestNow, cache.SuccessAtUtc, "success time is cached");
        AssertEqual("v1.10.0", cache.SuccessVersion, "latest version is cached");
        AssertEqual(0, cache.FailureWriteCount, "success does not write failure backoff");
    }

    private static void TestEquivalentAndOlderVersions()
    {
        UpdateCheckResult equivalent = CreateChecker(
            Responding(200, "{\"tag_name\": \"v1.3.0\"}"),
            new MemoryCache()).CheckAsync("1.3.0.0").GetAwaiter().GetResult();
        AssertTrue(equivalent.IsCheckAvailable, "three-part remote equals four-part local");
        AssertFalse(equivalent.IsUpdateAvailable, "1.3.0 is not newer than 1.3.0.0");

        UpdateCheckResult older = CreateChecker(
            Responding(200, "{\"tag_name\":\"v1.2.9\"}"),
            new MemoryCache()).CheckAsync("v1.3.0").GetAwaiter().GetResult();
        AssertFalse(older.IsUpdateAvailable, "older release does not prompt");

        FakeHttpClient invalidCurrentClient = Responding(
            200,
            "{\"tag_name\":\"v2.0.0\"}");
        UpdateCheckResult invalidCurrent = CreateChecker(
            invalidCurrentClient,
            new MemoryCache()).CheckAsync("version one").GetAwaiter().GetResult();
        AssertFalse(invalidCurrent.IsCheckAvailable, "invalid local version is silent");
        AssertEqual(0, invalidCurrentClient.CallCount, "invalid local version never uses network");
    }

    private static void TestSuccessfulCache()
    {
        MemoryCache freshCache = new MemoryCache();
        freshCache.State = new UpdateCheckCacheState
        {
            LastSuccessfulCheckUtc = TestNow - TimeSpan.FromHours(23),
            LatestVersion = "v2.0.0"
        };
        FakeHttpClient unusedClient = new FakeHttpClient();
        unusedClient.Exception = new InvalidOperationException("Network must not be used.");

        UpdateCheckResult cached = CreateChecker(unusedClient, freshCache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertTrue(cached.IsUpdateAvailable, "fresh successful cache can report update");
        AssertEqual("v2.0.0", cached.LatestVersion, "cached release tag is returned");
        AssertEqual(0, unusedClient.CallCount, "24-hour success cache avoids network");

        MemoryCache boundaryCache = new MemoryCache();
        boundaryCache.State = new UpdateCheckCacheState
        {
            LastSuccessfulCheckUtc = TestNow - TimeSpan.FromHours(24),
            LatestVersion = "v2.0.0"
        };
        FakeHttpClient refreshClient = Responding(
            200,
            "{\"tag_name\":\"v2.1.0\"}");
        UpdateCheckResult refreshed = CreateChecker(refreshClient, boundaryCache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertEqual(1, refreshClient.CallCount, "cache refreshes at 24-hour boundary");
        AssertEqual("v2.1.0", refreshed.LatestVersion, "refreshed version is returned");

        MemoryCache malformedCache = new MemoryCache();
        malformedCache.State = new UpdateCheckCacheState
        {
            LastSuccessfulCheckUtc = TestNow - TimeSpan.FromHours(1),
            LatestVersion = "not-a-version"
        };
        FakeHttpClient malformedRefresh = Responding(
            200,
            "{\"tag_name\":\"v1.4.0\"}");
        CreateChecker(malformedRefresh, malformedCache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertEqual(1, malformedRefresh.CallCount, "malformed cache is ignored");
    }

    private static void TestFailureBackoff()
    {
        MemoryCache recentFailureCache = new MemoryCache();
        recentFailureCache.State = new UpdateCheckCacheState
        {
            LastFailedCheckUtc = TestNow -
                TimeSpan.FromHours(23) - TimeSpan.FromMinutes(59)
        };
        FakeHttpClient unusedClient = Responding(
            200,
            "{\"tag_name\":\"v9.0.0\"}");
        UpdateCheckResult backedOff = CreateChecker(unusedClient, recentFailureCache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertFalse(backedOff.IsCheckAvailable, "recent failure is silently unavailable");
        AssertEqual(0, unusedClient.CallCount, "24-hour failure backoff avoids network");

        MemoryCache expiredFailureCache = new MemoryCache();
        expiredFailureCache.State = new UpdateCheckCacheState
        {
            LastFailedCheckUtc = TestNow - TimeSpan.FromHours(24)
        };
        FakeHttpClient retryClient = Responding(
            200,
            "{\"tag_name\":\"v1.4.0\"}");
        UpdateCheckResult retried = CreateChecker(retryClient, expiredFailureCache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertTrue(retried.IsUpdateAvailable, "check retries at 24-hour boundary");
        AssertEqual(1, retryClient.CallCount, "expired failure performs request");
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
        MemoryCache cache = new MemoryCache();
        UpdateChecker checker = new UpdateChecker(
            blockedClient,
            cache,
            new FixedClock(TestNow),
            TimeSpan.FromMilliseconds(30));

        Stopwatch stopwatch = Stopwatch.StartNew();
        UpdateCheckResult result = checker.CheckAsync("1.3.0")
            .GetAwaiter().GetResult();
        stopwatch.Stop();

        AssertFalse(result.IsCheckAvailable, "timeout is silent");
        AssertTrue(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(500),
            "checker enforces total request timeout");
        AssertEqual(1, cache.FailureWriteCount, "timeout starts failure backoff");
        AssertEqual(TimeSpan.FromMilliseconds(30), blockedClient.LastTimeout, "timeout is passed to client");
    }

    private static void TestCacheExceptionsAreNonFatal()
    {
        MemoryCache brokenReadCache = new MemoryCache();
        brokenReadCache.ThrowOnRead = true;
        UpdateCheckResult readFailureResult = CreateChecker(
            Responding(200, "{\"tag_name\":\"v1.4.0\"}"),
            brokenReadCache).CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertTrue(readFailureResult.IsUpdateAvailable, "cache read failure does not block check");

        MemoryCache brokenWriteCache = new MemoryCache();
        brokenWriteCache.ThrowOnWrite = true;
        UpdateCheckResult writeFailureResult = CreateChecker(
            Responding(200, "{\"tag_name\":\"v1.4.0\"}"),
            brokenWriteCache).CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertTrue(writeFailureResult.IsUpdateAvailable, "cache write failure does not hide update");
    }

    private static void AssertSilentFailure(FakeHttpClient httpClient, string name)
    {
        MemoryCache cache = new MemoryCache();
        UpdateCheckResult result = CreateChecker(httpClient, cache)
            .CheckAsync("1.3.0").GetAwaiter().GetResult();
        AssertFalse(result.IsCheckAvailable, name);
        AssertFalse(result.IsUpdateAvailable, name + " does not prompt");
        AssertEqual(null, result.LatestVersion, name + " has no displayed version");
        AssertEqual(1, cache.FailureWriteCount, name + " starts 24-hour backoff");
    }

    private static UpdateChecker CreateChecker(
        FakeHttpClient httpClient,
        MemoryCache cache)
    {
        return new UpdateChecker(httpClient, cache, new FixedClock(TestNow));
    }

    private static FakeHttpClient Responding(int statusCode, string content)
    {
        FakeHttpClient client = new FakeHttpClient();
        client.Response = new UpdateHttpResponse(statusCode, content);
        return client;
    }

    private sealed class FixedClock : IUpdateClock
    {
        internal FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; private set; }
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

    private sealed class MemoryCache : IUpdateCheckCache
    {
        public UpdateCheckCacheState State { get; set; }
        public bool ThrowOnRead { get; set; }
        public bool ThrowOnWrite { get; set; }
        public DateTime SuccessAtUtc { get; private set; }
        public string SuccessVersion { get; private set; }
        public int FailureWriteCount { get; private set; }

        public UpdateCheckCacheState Read()
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Injected cache read failure.");
            }

            return State;
        }

        public void WriteSuccess(DateTime checkedAtUtc, string latestVersion)
        {
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Injected cache write failure.");
            }

            SuccessAtUtc = checkedAtUtc;
            SuccessVersion = latestVersion;
            State = new UpdateCheckCacheState
            {
                LastSuccessfulCheckUtc = checkedAtUtc,
                LatestVersion = latestVersion
            };
        }

        public void WriteFailure(DateTime failedAtUtc)
        {
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Injected cache write failure.");
            }

            FailureWriteCount++;
            State = new UpdateCheckCacheState
            {
                LastFailedCheckUtc = failedAtUtc
            };
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
