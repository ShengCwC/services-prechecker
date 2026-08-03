using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
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

        private const string FirstPartyReleaseManifestUrl =
            "https://undefined-ss-downloads.smfsheng.workers.dev/release.json";
        private const string GitHubLatestReleaseApiUrl =
            "https://api.github.com/repos/ShengCwC/services-prechecker/releases/latest";
        private const string JsonAcceptHeader = "application/json";
        private const string GitHubAcceptHeader = "application/vnd.github+json";
        private const string GitHubApiVersionHeader = "2022-11-28";
        private static readonly TimeSpan DefaultRequestTimeout =
            TimeSpan.FromSeconds(4);
        private static readonly Regex ReleaseVersionPattern = new Regex(
            @"^v(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex ManifestVersionPattern = new Regex(
            @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex CurrentVersionPattern = new Regex(
            @"^v?(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:\.(?<revision>[0-9]+))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly object ProductionCheckLock = new object();
        private static readonly UpdateSource[] UpdateSources =
        {
            new UpdateSource(
                FirstPartyReleaseManifestUrl,
                "version",
                false,
                JsonAcceptHeader,
                null),
            new UpdateSource(
                GitHubLatestReleaseApiUrl,
                "tag_name",
                true,
                GitHubAcceptHeader,
                GitHubApiVersionHeader)
        };
        // This task is scoped to the current process only. Every normal application
        // launch starts a new process and therefore performs a fresh version check.
        private static Task<UpdateCheckResult> productionCheckTask;

        private readonly IUpdateHttpClient httpClient;
        private readonly TimeSpan requestTimeout;

        internal UpdateChecker(IUpdateHttpClient httpClient)
            : this(httpClient, DefaultRequestTimeout)
        {
        }

        internal UpdateChecker(
            IUpdateHttpClient httpClient,
            TimeSpan requestTimeout)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (requestTimeout <= TimeSpan.Zero ||
                requestTimeout > TimeSpan.FromSeconds(30))
            {
                throw new ArgumentOutOfRangeException(
                    "requestTimeout",
                    "The update request timeout must be between zero and 30 seconds.");
            }

            this.httpClient = httpClient;
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
                        new WebUpdateHttpClient());
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

            foreach (UpdateSource source in UpdateSources)
            {
                UpdateCheckResult result = await TryCheckSourceAsync(
                    parsedCurrentVersion,
                    source).ConfigureAwait(false);
                if (result != null)
                {
                    return result;
                }
            }

            return UpdateCheckResult.Unavailable();
        }

        private async Task<UpdateCheckResult> TryCheckSourceAsync(
            VersionNumber parsedCurrentVersion,
            UpdateSource source)
        {
            UpdateHttpHeaders headers = new UpdateHttpHeaders
            {
                UserAgent = "UndefinedSS-ServicesPrechecker/" +
                    parsedCurrentVersion.ToString(),
                Accept = source.Accept,
                GitHubApiVersion = source.GitHubApiVersion
            };

            UpdateHttpResponse response;
            Task<UpdateHttpResponse> requestTask;
            try
            {
                requestTask = httpClient.GetAsync(
                    source.Uri,
                    headers,
                    requestTimeout);
                if (requestTask == null)
                {
                    return null;
                }

                Task completedTask = await Task.WhenAny(
                    requestTask,
                    Task.Delay(requestTimeout)).ConfigureAwait(false);
                if (completedTask != requestTask)
                {
                    ObserveFault(requestTask);
                    return null;
                }

                response = await requestTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                return null;
            }

            string latestVersion;
            VersionNumber parsedLatestVersion;
            if (response == null ||
                response.StatusCode != (int)HttpStatusCode.OK ||
                !TryReadLatestVersion(
                    response.Content,
                    source.VersionPropertyName,
                    source.RequiresVPrefix,
                    out latestVersion,
                    out parsedLatestVersion))
            {
                return null;
            }

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
            string versionPropertyName,
            bool requiresVPrefix,
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
                object versionValue;
                if (root == null ||
                    !root.TryGetValue(versionPropertyName, out versionValue) ||
                    (value = versionValue as string) == null)
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

            if (!TryParseReleaseVersion(
                    value,
                    requiresVPrefix,
                    out parsedVersion))
            {
                return false;
            }

            latestVersion = "v" + parsedVersion.ToString();
            return true;
        }

        private static bool TryParseReleaseVersion(
            string value,
            bool requiresVPrefix,
            out VersionNumber parsedVersion)
        {
            parsedVersion = default(VersionNumber);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            Match match = requiresVPrefix
                ? ReleaseVersionPattern.Match(value)
                : ManifestVersionPattern.Match(value);
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

        private sealed class UpdateSource
        {
            internal UpdateSource(
                string url,
                string versionPropertyName,
                bool requiresVPrefix,
                string accept,
                string gitHubApiVersion)
            {
                Uri = new Uri(url);
                VersionPropertyName = versionPropertyName;
                RequiresVPrefix = requiresVPrefix;
                Accept = accept;
                GitHubApiVersion = gitHubApiVersion;
            }

            internal Uri Uri { get; private set; }
            internal string VersionPropertyName { get; private set; }
            internal bool RequiresVPrefix { get; private set; }
            internal string Accept { get; private set; }
            internal string GitHubApiVersion { get; private set; }
        }

        private sealed class WebUpdateHttpClient : IUpdateHttpClient
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
                if (!string.IsNullOrWhiteSpace(headers.GitHubApiVersion))
                {
                    request.Headers["X-GitHub-Api-Version"] =
                        headers.GitHubApiVersion;
                }
                request.AllowAutoRedirect = true;
                request.CachePolicy = new RequestCachePolicy(
                    RequestCacheLevel.NoCacheNoStore);
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
