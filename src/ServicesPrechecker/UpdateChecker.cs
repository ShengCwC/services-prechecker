using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace UndefinedSS.ServicesPrechecker
{
    internal interface IUpdateHttpClient
    {
        Task<UpdateHttpResponse> GetAsync(
            Uri uri,
            UpdateHttpHeaders headers,
            TimeSpan timeout);
    }

    internal interface IUpdateClock
    {
        DateTime UtcNow { get; }
    }

    internal interface IUpdateCheckCache
    {
        UpdateCheckCacheState Read();
        void WriteSuccess(DateTime checkedAtUtc, string latestVersion);
        void WriteFailure(DateTime failedAtUtc);
    }

    internal sealed class UpdateHttpHeaders
    {
        public string UserAgent { get; set; }
        public string Accept { get; set; }
        public string GitHubApiVersion { get; set; }
    }

    internal sealed class UpdateHttpResponse
    {
        public UpdateHttpResponse(int statusCode, string content)
        {
            StatusCode = statusCode;
            Content = content;
        }

        public int StatusCode { get; private set; }
        public string Content { get; private set; }
    }

    internal sealed class UpdateCheckCacheState
    {
        public DateTime? LastSuccessfulCheckUtc { get; set; }
        public DateTime? LastFailedCheckUtc { get; set; }
        public string LatestVersion { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        private UpdateCheckResult(
            bool isCheckAvailable,
            bool isUpdateAvailable,
            string latestVersion)
        {
            IsCheckAvailable = isCheckAvailable;
            IsUpdateAvailable = isUpdateAvailable;
            LatestVersion = latestVersion;
        }

        public bool IsCheckAvailable { get; private set; }
        public bool IsUpdateAvailable { get; private set; }
        public string LatestVersion { get; private set; }

        internal static UpdateCheckResult Available(
            bool isUpdateAvailable,
            string latestVersion)
        {
            return new UpdateCheckResult(true, isUpdateAvailable, latestVersion);
        }

        internal static UpdateCheckResult Unavailable()
        {
            return new UpdateCheckResult(false, false, null);
        }
    }

    internal sealed class UpdateChecker
    {
        private const int MaximumResponseCharacters = 1024 * 1024;
        internal const string DownloadUrl =
            "https://dl.screenshare.cn/services-prechecker";

        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/ShengCwC/services-prechecker/releases/latest";
        private const string GitHubAcceptHeader = "application/vnd.github+json";
        private const string GitHubApiVersionHeader = "2022-11-28";
        private static readonly TimeSpan DefaultRequestTimeout =
            TimeSpan.FromSeconds(4);
        private static readonly TimeSpan SuccessfulCacheLifetime =
            TimeSpan.FromHours(24);
        private static readonly TimeSpan FailureRetryDelay =
            TimeSpan.FromHours(24);
        private static readonly Regex ReleaseVersionPattern = new Regex(
            @"^v(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex CurrentVersionPattern = new Regex(
            @"^v?(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:\.(?<revision>[0-9]+))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly object ProductionCheckLock = new object();
        private static Task<UpdateCheckResult> productionCheckTask;

        private readonly IUpdateHttpClient httpClient;
        private readonly IUpdateCheckCache cache;
        private readonly IUpdateClock clock;
        private readonly TimeSpan requestTimeout;

        internal UpdateChecker(
            IUpdateHttpClient httpClient,
            IUpdateCheckCache cache,
            IUpdateClock clock)
            : this(httpClient, cache, clock, DefaultRequestTimeout)
        {
        }

        internal UpdateChecker(
            IUpdateHttpClient httpClient,
            IUpdateCheckCache cache,
            IUpdateClock clock,
            TimeSpan requestTimeout)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (cache == null)
            {
                throw new ArgumentNullException("cache");
            }

            if (clock == null)
            {
                throw new ArgumentNullException("clock");
            }

            if (requestTimeout <= TimeSpan.Zero ||
                requestTimeout > TimeSpan.FromSeconds(30))
            {
                throw new ArgumentOutOfRangeException(
                    "requestTimeout",
                    "The update request timeout must be between zero and 30 seconds.");
            }

            this.httpClient = httpClient;
            this.cache = cache;
            this.clock = clock;
            this.requestTimeout = requestTimeout;
        }

        public static Task<UpdateCheckResult> CheckForUpdateAsync(
            string currentVersion)
        {
            lock (ProductionCheckLock)
            {
                if (productionCheckTask == null)
                {
                    UpdateChecker checker = new UpdateChecker(
                        new GitHubUpdateHttpClient(),
                        new RegistryUpdateCheckCache(),
                        new SystemUpdateClock());
                    productionCheckTask = checker.CheckAsync(currentVersion);
                }

                return productionCheckTask;
            }
        }

        public static Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            return CheckForUpdateAsync(ReadCurrentAssemblyVersion());
        }

        internal async Task<UpdateCheckResult> CheckAsync(string currentVersion)
        {
            VersionNumber parsedCurrentVersion;
            if (!TryParseCurrentVersion(currentVersion, out parsedCurrentVersion))
            {
                return UpdateCheckResult.Unavailable();
            }

            DateTime nowUtc = EnsureUtc(clock.UtcNow);
            UpdateCheckCacheState cacheState = SafeReadCache();
            VersionNumber cachedLatestVersion;
            if (cacheState != null &&
                cacheState.LastSuccessfulCheckUtc.HasValue &&
                IsWithinWindow(
                    nowUtc,
                    cacheState.LastSuccessfulCheckUtc.Value,
                    SuccessfulCacheLifetime) &&
                TryParseReleaseVersion(
                    cacheState.LatestVersion,
                    out cachedLatestVersion))
            {
                return CreateAvailableResult(
                    parsedCurrentVersion,
                    cachedLatestVersion,
                    cacheState.LatestVersion);
            }

            if (cacheState != null &&
                cacheState.LastFailedCheckUtc.HasValue &&
                IsWithinWindow(
                    nowUtc,
                    cacheState.LastFailedCheckUtc.Value,
                    FailureRetryDelay))
            {
                return UpdateCheckResult.Unavailable();
            }

            UpdateHttpHeaders headers = new UpdateHttpHeaders
            {
                UserAgent = "UndefinedSS-ServicesPrechecker/" +
                    parsedCurrentVersion.ToString(),
                Accept = GitHubAcceptHeader,
                GitHubApiVersion = GitHubApiVersionHeader
            };

            UpdateHttpResponse response;
            Task<UpdateHttpResponse> requestTask;
            try
            {
                requestTask = httpClient.GetAsync(
                    new Uri(LatestReleaseApiUrl),
                    headers,
                    requestTimeout);
                if (requestTask == null)
                {
                    SafeWriteFailure(nowUtc);
                    return UpdateCheckResult.Unavailable();
                }

                Task completedTask = await Task.WhenAny(
                    requestTask,
                    Task.Delay(requestTimeout)).ConfigureAwait(false);
                if (completedTask != requestTask)
                {
                    ObserveFault(requestTask);
                    SafeWriteFailure(nowUtc);
                    return UpdateCheckResult.Unavailable();
                }

                response = await requestTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                SafeWriteFailure(nowUtc);
                return UpdateCheckResult.Unavailable();
            }

            string latestVersion;
            VersionNumber parsedLatestVersion;
            if (response == null ||
                response.StatusCode != (int)HttpStatusCode.OK ||
                !TryReadLatestVersion(
                    response.Content,
                    out latestVersion,
                    out parsedLatestVersion))
            {
                SafeWriteFailure(nowUtc);
                return UpdateCheckResult.Unavailable();
            }

            SafeWriteSuccess(nowUtc, latestVersion);
            return CreateAvailableResult(
                parsedCurrentVersion,
                parsedLatestVersion,
                latestVersion);
        }

        private static UpdateCheckResult CreateAvailableResult(
            VersionNumber currentVersion,
            VersionNumber latestVersion,
            string latestVersionText)
        {
            return UpdateCheckResult.Available(
                latestVersion.CompareTo(currentVersion) > 0,
                latestVersionText);
        }

        private static bool TryReadLatestVersion(
            string json,
            out string latestVersion,
            out VersionNumber parsedVersion)
        {
            latestVersion = null;
            parsedVersion = default(VersionNumber);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            string value;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 1024 * 1024;
                Dictionary<string, object> root =
                    serializer.DeserializeObject(json) as Dictionary<string, object>;
                object tagName;
                if (root == null ||
                    !root.TryGetValue("tag_name", out tagName) ||
                    (value = tagName as string) == null)
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                return false;
            }

            if (!TryParseReleaseVersion(value, out parsedVersion))
            {
                return false;
            }

            latestVersion = value;
            return true;
        }

        private static bool TryParseReleaseVersion(
            string value,
            out VersionNumber parsedVersion)
        {
            parsedVersion = default(VersionNumber);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            Match match = ReleaseVersionPattern.Match(value);
            return match.Success && TryCreateVersion(match, out parsedVersion);
        }

        private static bool TryParseCurrentVersion(
            string value,
            out VersionNumber parsedVersion)
        {
            parsedVersion = default(VersionNumber);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            Match match = CurrentVersionPattern.Match(value);
            return match.Success && TryCreateVersion(match, out parsedVersion);
        }

        private static bool TryCreateVersion(
            Match match,
            out VersionNumber parsedVersion)
        {
            parsedVersion = default(VersionNumber);
            int major;
            int minor;
            int patch;
            if (!int.TryParse(
                    match.Groups["major"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out major) ||
                !int.TryParse(
                    match.Groups["minor"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out minor) ||
                !int.TryParse(
                    match.Groups["patch"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out patch))
            {
                return false;
            }

            parsedVersion = new VersionNumber(major, minor, patch);
            return true;
        }

        private static bool IsWithinWindow(
            DateTime nowUtc,
            DateTime recordedAtUtc,
            TimeSpan lifetime)
        {
            TimeSpan age = nowUtc - EnsureUtc(recordedAtUtc);
            return age >= TimeSpan.Zero && age < lifetime;
        }

        private UpdateCheckCacheState SafeReadCache()
        {
            try
            {
                return cache.Read();
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                return null;
            }
        }

        private void SafeWriteSuccess(DateTime checkedAtUtc, string latestVersion)
        {
            try
            {
                cache.WriteSuccess(checkedAtUtc, latestVersion);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }
            }
        }

        private void SafeWriteFailure(DateTime failedAtUtc)
        {
            try
            {
                cache.WriteFailure(failedAtUtc);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }
            }
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

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string ReadCurrentAssemblyVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyInformationalVersionAttribute informationalVersion =
                (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(AssemblyInformationalVersionAttribute));
            if (informationalVersion != null &&
                !string.IsNullOrWhiteSpace(informationalVersion.InformationalVersion))
            {
                return informationalVersion.InformationalVersion;
            }

            Version assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion == null)
            {
                return null;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}",
                assemblyVersion.Major,
                assemblyVersion.Minor,
                Math.Max(0, assemblyVersion.Build));
        }

        private struct VersionNumber : IComparable<VersionNumber>
        {
            internal VersionNumber(int major, int minor, int patch)
                : this()
            {
                Major = major;
                Minor = minor;
                Patch = patch;
            }

            internal int Major { get; private set; }
            internal int Minor { get; private set; }
            internal int Patch { get; private set; }

            public int CompareTo(VersionNumber other)
            {
                int comparison = Major.CompareTo(other.Major);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Minor.CompareTo(other.Minor);
                return comparison != 0
                    ? comparison
                    : Patch.CompareTo(other.Patch);
            }

            public override string ToString()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.{1}.{2}",
                    Major,
                    Minor,
                    Patch);
            }
        }

        private sealed class SystemUpdateClock : IUpdateClock
        {
            public DateTime UtcNow
            {
                get { return DateTime.UtcNow; }
            }
        }

        private sealed class RegistryUpdateCheckCache : IUpdateCheckCache
        {
            private const string RegistryPath =
                @"Software\UndefinedSS\ServicesPrechecker\UpdateCheck";
            private const string LastSuccessValueName = "LastSuccessUtc";
            private const string LastFailureValueName = "LastFailureUtc";
            private const string LatestVersionValueName = "LatestVersion";

            public UpdateCheckCacheState Read()
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    RegistryPath,
                    false))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    return new UpdateCheckCacheState
                    {
                        LastSuccessfulCheckUtc = ParseRegistryDate(
                            key.GetValue(LastSuccessValueName)),
                        LastFailedCheckUtc = ParseRegistryDate(
                            key.GetValue(LastFailureValueName)),
                        LatestVersion = Convert.ToString(
                            key.GetValue(LatestVersionValueName),
                            CultureInfo.InvariantCulture)
                    };
                }
            }

            public void WriteSuccess(DateTime checkedAtUtc, string latestVersion)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    RegistryPath,
                    RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.SetValue(
                        LastSuccessValueName,
                        EnsureUtc(checkedAtUtc).ToString("O", CultureInfo.InvariantCulture),
                        RegistryValueKind.String);
                    key.SetValue(
                        LatestVersionValueName,
                        latestVersion,
                        RegistryValueKind.String);
                    key.DeleteValue(LastFailureValueName, false);
                }
            }

            public void WriteFailure(DateTime failedAtUtc)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    RegistryPath,
                    RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue(
                            LastFailureValueName,
                            EnsureUtc(failedAtUtc).ToString(
                                "O",
                                CultureInfo.InvariantCulture),
                            RegistryValueKind.String);
                    }
                }
            }

            private static DateTime? ParseRegistryDate(object value)
            {
                DateTime parsed;
                return DateTime.TryParseExact(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsed)
                    ? (DateTime?)EnsureUtc(parsed)
                    : null;
            }
        }

        private sealed class GitHubUpdateHttpClient : IUpdateHttpClient
        {
            public async Task<UpdateHttpResponse> GetAsync(
                Uri uri,
                UpdateHttpHeaders headers,
                TimeSpan timeout)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Method = "GET";
                request.Accept = headers.Accept;
                request.UserAgent = headers.UserAgent;
                request.Headers["X-GitHub-Api-Version"] =
                    headers.GitHubApiVersion;
                request.AllowAutoRedirect = true;
                request.AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate;
                int timeoutMilliseconds = Math.Max(
                    1,
                    (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds));
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;

                Task<UpdateHttpResponse> requestTask = ExecuteRequestAsync(request);
                Task completedTask = await Task.WhenAny(
                    requestTask,
                    Task.Delay(timeout)).ConfigureAwait(false);
                if (completedTask != requestTask)
                {
                    request.Abort();
                    ObserveFault(requestTask);
                    throw new TimeoutException("The update request timed out.");
                }

                return await requestTask.ConfigureAwait(false);
            }

            private static async Task<UpdateHttpResponse> ExecuteRequestAsync(
                HttpWebRequest request)
            {
                try
                {
                    using (HttpWebResponse response =
                        (HttpWebResponse)await request.GetResponseAsync()
                            .ConfigureAwait(false))
                    using (Stream responseStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        string content = await ReadLimitedContentAsync(reader)
                            .ConfigureAwait(false);
                        return new UpdateHttpResponse(
                            (int)response.StatusCode,
                            content);
                    }
                }
                catch (WebException exception)
                {
                    using (HttpWebResponse response =
                        exception.Response as HttpWebResponse)
                    {
                        if (response == null)
                        {
                            throw;
                        }

                        return new UpdateHttpResponse(
                            (int)response.StatusCode,
                            null);
                    }
                }
            }

            private static async Task<string> ReadLimitedContentAsync(
                StreamReader reader)
            {
                char[] buffer = new char[4096];
                StringBuilder content = new StringBuilder();
                while (true)
                {
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        return content.ToString();
                    }

                    if (content.Length > MaximumResponseCharacters - read)
                    {
                        throw new InvalidDataException(
                            "The update response exceeded the size limit.");
                    }

                    content.Append(buffer, 0, read);
                }
            }
        }
    }
}
