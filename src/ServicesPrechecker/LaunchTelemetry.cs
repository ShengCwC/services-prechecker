using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace UndefinedSS.ServicesPrechecker
{
    internal interface ILaunchTelemetryHttpClient
    {
        Task<bool> PostAsync(
            Uri endpoint,
            string eventId,
            string version,
            TimeSpan timeout);
    }

    internal sealed class LaunchTelemetry
    {
        internal const string PrimaryEndpoint =
            "https://dl.screenshare.cn/api/services-prechecker/usage";
        internal const string FallbackEndpoint =
            "https://undefined-ss-downloads.smfsheng.workers.dev/api/services-prechecker/usage";

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
        private static readonly Regex VersionPattern = new Regex(
            @"^\d+(?:\.\d+){1,3}(?:[-+][0-9A-Za-z.-]+)?$",
            RegexOptions.CultureInvariant);
        private static readonly object ProductionLock = new object();
        private static Task<bool> productionTask;

        private readonly ILaunchTelemetryHttpClient httpClient;
        private readonly Uri[] endpoints;
        private readonly TimeSpan timeout;

        internal LaunchTelemetry(ILaunchTelemetryHttpClient httpClient)
            : this(
                httpClient,
                new[] { new Uri(PrimaryEndpoint), new Uri(FallbackEndpoint) },
                DefaultTimeout)
        {
        }

        internal LaunchTelemetry(
            ILaunchTelemetryHttpClient httpClient,
            Uri[] endpoints,
            TimeSpan timeout)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (endpoints == null || endpoints.Length == 0)
            {
                throw new ArgumentException("At least one telemetry endpoint is required.", "endpoints");
            }

            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(10))
            {
                throw new ArgumentOutOfRangeException("timeout");
            }

            this.httpClient = httpClient;
            this.endpoints = (Uri[])endpoints.Clone();
            this.timeout = timeout;
        }

        internal static void RecordInBackground(string version)
        {
            Task<bool> task;
            lock (ProductionLock)
            {
                if (productionTask == null)
                {
                    string eventId = Guid.NewGuid().ToString("D");
                    LaunchTelemetry telemetry = new LaunchTelemetry(
                        new WebRequestLaunchTelemetryHttpClient());
                    productionTask = telemetry.RecordAsync(eventId, version);
                }

                task = productionTask;
            }

            ObserveFault(task);
        }

        internal async Task<bool> RecordAsync(string eventId, string version)
        {
            Guid parsedEventId;
            if (!Guid.TryParseExact(eventId, "D", out parsedEventId) ||
                string.IsNullOrWhiteSpace(version) ||
                version.Length > 40 ||
                !VersionPattern.IsMatch(version))
            {
                return false;
            }

            foreach (Uri endpoint in endpoints)
            {
                if (endpoint == null || endpoint.Scheme != Uri.UriSchemeHttps)
                {
                    continue;
                }

                try
                {
                    if (await httpClient.PostAsync(
                            endpoint,
                            eventId,
                            version,
                            timeout).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception))
                    {
                        throw;
                    }
                }
            }

            return false;
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

        private sealed class WebRequestLaunchTelemetryHttpClient :
            ILaunchTelemetryHttpClient
        {
            public async Task<bool> PostAsync(
                Uri endpoint,
                string eventId,
                string version,
                TimeSpan timeout)
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(
                    new Dictionary<string, object>
                    {
                        { "eventId", eventId },
                        { "version", version }
                    });
                byte[] body = Encoding.UTF8.GetBytes(json);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = body.Length;
                request.UserAgent = "UndefinedSS-ServicesPrechecker/" + version;
                request.Headers["X-Services-Prechecker-Client"] = "desktop";
                request.AllowAutoRedirect = false;
                request.KeepAlive = false;
                int timeoutMilliseconds = Math.Max(
                    1,
                    (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds));
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;

                Task<bool> requestTask = Task.Factory.StartNew(
                    delegate
                    {
                        return Send(request, body);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);

                Task completedTask = await Task.WhenAny(
                    requestTask,
                    Task.Delay(timeout)).ConfigureAwait(false);
                if (completedTask != requestTask)
                {
                    request.Abort();
                    ObserveFault(requestTask);
                    return false;
                }

                return await requestTask.ConfigureAwait(false);
            }

            private static bool Send(HttpWebRequest request, byte[] body)
            {
                try
                {
                    using (Stream requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(body, 0, body.Length);
                    }

                    using (HttpWebResponse response =
                        (HttpWebResponse)request.GetResponse())
                    {
                        int statusCode = (int)response.StatusCode;
                        return statusCode >= 200 && statusCode < 300;
                    }
                }
                catch (WebException exception)
                {
                    using (HttpWebResponse response =
                        exception.Response as HttpWebResponse)
                    {
                        return false;
                    }
                }
            }
        }
    }
}
