using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace UndefinedSS.ServicesPrechecker
{
    internal interface IForensicReadinessDataSource
    {
        object ReadMachineValue(string path, string name);
        object ReadUserValue(string userSid, string path, string name);
        bool IsUserHiveLoaded(string userSid);
        ServiceComponentState ReadService(string serviceName);
        ScheduledTaskComponentState ReadScheduledTask(string taskPath);
        bool IsExplorerRunningInCurrentSession();
        bool FileExists(string path);
        void WriteMachineDword(string path, string name, int value);
        void WriteUserDword(string userSid, string path, string name, int value);
        void WriteServiceStartType(string serviceName, int startType);
        bool EnableScheduledTask(string taskPath);
    }

    internal sealed class ForensicArtifactManager
    {
        internal const string AppCompatPolicyPath =
            @"SOFTWARE\Policies\Microsoft\Windows\AppCompat";
        internal const string AppCompatCachePath =
            @"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache";
        internal const string AppCompatCacheValueName = "AppCompatCache";
        private const int MaximumModernShimCacheHeaderLength = 64;
        internal const string UserTrackingPolicyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        internal const string UserTrackingSettingsPath =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        internal const string CompatibilityAppraiserTask =
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser";
        internal const string ProgramDataUpdaterTask =
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater";

        private readonly IForensicReadinessDataSource dataSource;

        internal ForensicArtifactManager(IForensicReadinessDataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException("dataSource");
            }

            this.dataSource = dataSource;
        }

        internal static ForensicArtifactManager CreateProduction()
        {
            return new ForensicArtifactManager(
                new WindowsForensicReadinessDataSource());
        }

        internal IList<ForensicArtifactSnapshot> GetSnapshots(string userSid)
        {
            return new List<ForensicArtifactSnapshot>
            {
                GetSnapshotSafely(ForensicArtifactKind.ShimCache, userSid),
                GetSnapshotSafely(ForensicArtifactKind.Amcache, userSid),
                GetSnapshotSafely(ForensicArtifactKind.UserAssist, userSid)
            };
        }

        internal IList<ForensicArtifactEnableResult> EnableAll(string userSid)
        {
            return new List<ForensicArtifactEnableResult>
            {
                EnableSafely(ForensicArtifactKind.ShimCache, userSid),
                EnableSafely(ForensicArtifactKind.Amcache, userSid),
                EnableSafely(ForensicArtifactKind.UserAssist, userSid)
            };
        }

        internal static bool RequiresRestartAfterEnable(
            IEnumerable<ForensicArtifactEnableResult> results)
        {
            if (results == null)
            {
                return false;
            }

            foreach (ForensicArtifactEnableResult result in results)
            {
                if (result != null &&
                    (result.ConfigurationChanged || result.RequiresRestart))
                {
                    return true;
                }
            }

            return false;
        }

        private ForensicArtifactSnapshot GetSnapshotSafely(
            ForensicArtifactKind kind,
            string userSid)
        {
            try
            {
                switch (kind)
                {
                    case ForensicArtifactKind.ShimCache:
                        return GetShimCacheSnapshot();
                    case ForensicArtifactKind.Amcache:
                        return GetAmcacheSnapshot();
                    default:
                        return GetUserAssistSnapshot(userSid);
                }
            }
            catch (Exception exception)
            {
                return CreateSnapshot(
                    kind,
                    ServiceVisualState.Error,
                    "读取失败",
                    GetUsefulError(exception));
            }
        }

        private ForensicArtifactEnableResult EnableSafely(
            ForensicArtifactKind kind,
            string userSid)
        {
            try
            {
                switch (kind)
                {
                    case ForensicArtifactKind.ShimCache:
                        return EnableShimCache();
                    case ForensicArtifactKind.Amcache:
                        return EnableAmcache();
                    default:
                        return EnableUserAssist(userSid);
                }
            }
            catch (Exception exception)
            {
                return new ForensicArtifactEnableResult
                {
                    Kind = kind,
                    DisplayName = GetDisplayName(kind),
                    Success = false,
                    Message = GetUsefulError(exception)
                };
            }
        }

        private ForensicArtifactSnapshot GetShimCacheSnapshot()
        {
            int? disableEngine;
            bool policyValid = TryReadDword(
                dataSource.ReadMachineValue(
                    AppCompatPolicyPath,
                    "DisableEngine"),
                out disableEngine);
            ServiceComponentState service = dataSource.ReadService("AeLookupSvc");

            if (!policyValid)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.ShimCache,
                    ServiceVisualState.Error,
                    "策略异常",
                    "DisableEngine 不是有效的 DWORD 值");
            }

            if (!string.IsNullOrWhiteSpace(service.Error))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.ShimCache,
                    ServiceVisualState.Error,
                    "组件读取失败",
                    service.Error);
            }

            if (disableEngine == 1 ||
                (service.Exists && service.StartType == 4))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.ShimCache,
                    ServiceVisualState.Disabled,
                    "记录机制已关闭",
                    disableEngine == 1
                        ? "应用程序兼容性引擎被策略关闭"
                        : "AeLookupSvc 已被禁用，应设为手动触发");
            }

            int cacheSize;
            ShimCacheDataState cacheState = InspectShimCacheData(
                dataSource.ReadMachineValue(
                    AppCompatCachePath,
                    AppCompatCacheValueName),
                out cacheSize);
            if (cacheState == ShimCacheDataState.Invalid)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.ShimCache,
                    ServiceVisualState.Error,
                    "落盘缓存格式异常",
                    "AppCompatCache 不是有效的二进制值，无法确认采集器可读取");
            }

            if (cacheState != ShimCacheDataState.HasPayload)
            {
                string cacheDetail = cacheState == ShimCacheDataState.Missing
                    ? "AppCompatCache 尚未生成"
                    : "AppCompatCache 当前仅有 " +
                        cacheSize.ToString(CultureInfo.InvariantCulture) +
                        " 字节头部";
                return CreateSnapshot(
                    ForensicArtifactKind.ShimCache,
                    ServiceVisualState.Stopped,
                    "当前落盘缓存无记录",
                    cacheDetail + " · 当前采集可能为空，必须正常重启后复检");
            }

            string detail = service.Exists
                ? "检测到 " + cacheSize.ToString(CultureInfo.InvariantCulture) +
                    " 字节落盘数据 · AeLookupSvc 未被禁用"
                : "检测到 " + cacheSize.ToString(CultureInfo.InvariantCulture) +
                    " 字节落盘数据 · 当前系统使用内置兼容性引擎";
            return CreateSnapshot(
                ForensicArtifactKind.ShimCache,
                ServiceVisualState.Running,
                "落盘数据可用",
                detail);
        }

        private ForensicArtifactSnapshot GetAmcacheSnapshot()
        {
            int? disableInventory;
            bool policyValid = TryReadDword(
                dataSource.ReadMachineValue(
                    AppCompatPolicyPath,
                    "DisableInventory"),
                out disableInventory);
            if (!policyValid)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Error,
                    "策略异常",
                    "DisableInventory 不是有效的 DWORD 值");
            }

            ServiceComponentState pca = dataSource.ReadService("PcaSvc");
            ServiceComponentState diagTrack = dataSource.ReadService("DiagTrack");
            if (!string.IsNullOrWhiteSpace(pca.Error) ||
                !string.IsNullOrWhiteSpace(diagTrack.Error))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Error,
                    "组件读取失败",
                    !string.IsNullOrWhiteSpace(pca.Error)
                        ? pca.Error
                        : diagTrack.Error);
            }

            ScheduledTaskComponentState appraiser =
                dataSource.ReadScheduledTask(CompatibilityAppraiserTask);
            ScheduledTaskComponentState updater =
                dataSource.ReadScheduledTask(ProgramDataUpdaterTask);
            if (appraiser.State == ScheduledTaskState.Error ||
                updater.State == ScheduledTaskState.Error)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Error,
                    "任务读取失败",
                    appraiser.State == ScheduledTaskState.Error
                        ? appraiser.Error
                        : updater.Error);
            }

            if (disableInventory == 1)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Disabled,
                    "清单收集已关闭",
                    "DisableInventory 策略关闭了兼容性清单收集");
            }

            if (!IsServiceReady(pca) || !IsServiceReady(diagTrack))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Stopped,
                    "依赖服务未就绪",
                    "需要 PcaSvc 与 DiagTrack 正常启用并运行");
            }

            if (appraiser.State == ScheduledTaskState.Disabled ||
                updater.State == ScheduledTaskState.Disabled)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Disabled,
                    "清单任务已禁用",
                    "将启用系统中现有的 Application Experience 清单任务");
            }

            int availableTasks = CountAvailableTasks(appraiser, updater);
            if (availableTasks == 0)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.Amcache,
                    ServiceVisualState.Missing,
                    "系统组件不完整",
                    "未找到兼容性清单计划任务；不会擅自创建系统任务");
            }

            string windowsDirectory = GetWindowsDirectory();
            string hivePath = Path.Combine(
                windowsDirectory,
                @"AppCompat\Programs\Amcache.hve");
            string hiveText = dataSource.FileExists(hivePath)
                ? "Amcache.hve 已存在"
                : "Amcache.hve 尚未生成";
            string taskText = availableTasks == 2
                ? "2 项清单任务可用"
                : "1 项清单任务可用，另一项在此版本缺失";
            return CreateSnapshot(
                ForensicArtifactKind.Amcache,
                ServiceVisualState.Running,
                "记录机制已启用",
                taskText + " · " + hiveText);
        }

        private ForensicArtifactSnapshot GetUserAssistSnapshot(string userSid)
        {
            if (!IsValidSid(userSid))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.UserAssist,
                    ServiceVisualState.Error,
                    "用户身份不可用",
                    "无法确定启动本程序的交互用户");
            }

            if (!dataSource.IsUserHiveLoaded(userSid))
            {
                return CreateSnapshot(
                    ForensicArtifactKind.UserAssist,
                    ServiceVisualState.Missing,
                    "用户配置未加载",
                    "目标用户的 NTUSER.DAT 当前未加载，未修改其他账户");
            }

            int? noInstrumentation;
            int? startTrackPrograms;
            bool policyValid = TryReadDword(
                dataSource.ReadUserValue(
                    userSid,
                    UserTrackingPolicyPath,
                    "NoInstrumentation"),
                out noInstrumentation);
            bool settingValid = TryReadDword(
                dataSource.ReadUserValue(
                    userSid,
                    UserTrackingSettingsPath,
                    "Start_TrackProgs"),
                out startTrackPrograms);
            if (!policyValid || !settingValid)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.UserAssist,
                    ServiceVisualState.Error,
                    "用户策略异常",
                    "用户跟踪设置不是有效的 DWORD 值");
            }

            if (noInstrumentation == 1 || startTrackPrograms == 0)
            {
                return CreateSnapshot(
                    ForensicArtifactKind.UserAssist,
                    ServiceVisualState.Disabled,
                    "当前用户跟踪已关闭",
                    noInstrumentation == 1
                        ? "NoInstrumentation 策略关闭了用户跟踪"
                        : "Windows 应用启动跟踪隐私设置已关闭");
            }

            if (!dataSource.IsExplorerRunningInCurrentSession())
            {
                return CreateSnapshot(
                    ForensicArtifactKind.UserAssist,
                    ServiceVisualState.Stopped,
                    "Explorer Shell 未运行",
                    "UserAssist 只记录当前交互用户通过 Windows Shell 的活动");
            }

            return CreateSnapshot(
                ForensicArtifactKind.UserAssist,
                ServiceVisualState.Running,
                "当前用户跟踪已启用",
                "仅检查启动本程序的用户 · 不修改或伪造既有记录");
        }

        private ForensicArtifactEnableResult EnableShimCache()
        {
            bool changed = false;
            int? disableEngine;
            bool policyValid = TryReadDword(
                dataSource.ReadMachineValue(
                    AppCompatPolicyPath,
                    "DisableEngine"),
                out disableEngine);
            if (!policyValid || disableEngine == 1)
            {
                dataSource.WriteMachineDword(
                    AppCompatPolicyPath,
                    "DisableEngine",
                    0);
                changed = true;
            }

            ServiceComponentState service = dataSource.ReadService("AeLookupSvc");
            if (!string.IsNullOrWhiteSpace(service.Error))
            {
                throw new InvalidOperationException(service.Error);
            }

            if (service.Exists && service.StartType == 4)
            {
                dataSource.WriteServiceStartType("AeLookupSvc", 3);
                changed = true;
            }

            int cacheSize;
            ShimCacheDataState cacheState = InspectShimCacheData(
                dataSource.ReadMachineValue(
                    AppCompatCachePath,
                    AppCompatCacheValueName),
                out cacheSize);
            bool cacheNeedsBaseline =
                cacheState == ShimCacheDataState.Missing ||
                cacheState == ShimCacheDataState.HeaderOnly;
            bool cacheValid = cacheState != ShimCacheDataState.Invalid;

            return new ForensicArtifactEnableResult
            {
                Kind = ForensicArtifactKind.ShimCache,
                DisplayName = GetDisplayName(ForensicArtifactKind.ShimCache),
                Success = cacheValid,
                ConfigurationChanged = changed,
                RequiresRestart = changed || cacheNeedsBaseline,
                Message = !cacheValid
                    ? "兼容性引擎设置已处理，但 AppCompatCache 值格式异常"
                    : (cacheNeedsBaseline
                        ? "兼容性引擎已启用；当前落盘缓存无记录，必须重启后建立新基线"
                        : (changed
                            ? "兼容性引擎已启用，重启后建立新的落盘周期"
                            : "兼容性引擎与落盘缓存均已就绪"))
            };
        }

        private ForensicArtifactEnableResult EnableAmcache()
        {
            bool changed = false;
            int? disableInventory;
            bool policyValid = TryReadDword(
                dataSource.ReadMachineValue(
                    AppCompatPolicyPath,
                    "DisableInventory"),
                out disableInventory);
            if (!policyValid || disableInventory == 1)
            {
                dataSource.WriteMachineDword(
                    AppCompatPolicyPath,
                    "DisableInventory",
                    0);
                changed = true;
            }

            ScheduledTaskComponentState appraiser =
                dataSource.ReadScheduledTask(CompatibilityAppraiserTask);
            ScheduledTaskComponentState updater =
                dataSource.ReadScheduledTask(ProgramDataUpdaterTask);
            if (appraiser.State == ScheduledTaskState.Error ||
                updater.State == ScheduledTaskState.Error)
            {
                throw new InvalidOperationException(
                    appraiser.State == ScheduledTaskState.Error
                        ? appraiser.Error
                        : updater.Error);
            }
            if (appraiser.State == ScheduledTaskState.Disabled)
            {
                if (!dataSource.EnableScheduledTask(CompatibilityAppraiserTask))
                {
                    throw new InvalidOperationException(
                        "无法启用 Microsoft Compatibility Appraiser 任务");
                }
                changed = true;
            }
            if (updater.State == ScheduledTaskState.Disabled)
            {
                if (!dataSource.EnableScheduledTask(ProgramDataUpdaterTask))
                {
                    throw new InvalidOperationException(
                        "无法启用 ProgramDataUpdater 任务");
                }
                changed = true;
            }

            int availableTasks = CountAvailableTasks(appraiser, updater);
            bool componentsAvailable = availableTasks > 0;
            return new ForensicArtifactEnableResult
            {
                Kind = ForensicArtifactKind.Amcache,
                DisplayName = GetDisplayName(ForensicArtifactKind.Amcache),
                Success = componentsAvailable,
                ConfigurationChanged = changed,
                RequiresRestart = changed,
                Message = componentsAvailable
                    ? (changed
                        ? "兼容性清单策略与现有任务已启用"
                        : "兼容性清单机制已处于启用状态")
                    : "系统缺少兼容性清单计划任务，未擅自创建或下载系统组件"
            };
        }

        private ForensicArtifactEnableResult EnableUserAssist(string userSid)
        {
            if (!IsValidSid(userSid) || !dataSource.IsUserHiveLoaded(userSid))
            {
                return new ForensicArtifactEnableResult
                {
                    Kind = ForensicArtifactKind.UserAssist,
                    DisplayName = GetDisplayName(ForensicArtifactKind.UserAssist),
                    Success = false,
                    Message = "启动本程序的用户配置单元未加载，未修改其他账户"
                };
            }

            bool changed = false;
            int? noInstrumentation;
            int? startTrackPrograms;
            bool policyValid = TryReadDword(
                dataSource.ReadUserValue(
                    userSid,
                    UserTrackingPolicyPath,
                    "NoInstrumentation"),
                out noInstrumentation);
            bool settingValid = TryReadDword(
                dataSource.ReadUserValue(
                    userSid,
                    UserTrackingSettingsPath,
                    "Start_TrackProgs"),
                out startTrackPrograms);
            if (!policyValid || noInstrumentation == 1)
            {
                dataSource.WriteUserDword(
                    userSid,
                    UserTrackingPolicyPath,
                    "NoInstrumentation",
                    0);
                changed = true;
            }
            if (!settingValid || startTrackPrograms == 0)
            {
                dataSource.WriteUserDword(
                    userSid,
                    UserTrackingSettingsPath,
                    "Start_TrackProgs",
                    1);
                changed = true;
            }

            bool shellAvailable = dataSource.IsExplorerRunningInCurrentSession();
            return new ForensicArtifactEnableResult
            {
                Kind = ForensicArtifactKind.UserAssist,
                DisplayName = GetDisplayName(ForensicArtifactKind.UserAssist),
                Success = shellAvailable,
                ConfigurationChanged = changed,
                RequiresRestart = changed,
                Message = shellAvailable
                    ? (changed
                        ? "当前用户的 Shell 启动跟踪已启用"
                        : "当前用户的 Shell 启动跟踪已处于启用状态")
                    : "策略已处理，但当前会话没有 Explorer Shell，无法保证产生 UserAssist"
            };
        }

        private static int CountAvailableTasks(
            ScheduledTaskComponentState first,
            ScheduledTaskComponentState second)
        {
            int count = 0;
            if (first.State == ScheduledTaskState.Enabled ||
                first.State == ScheduledTaskState.Disabled)
            {
                count++;
            }
            if (second.State == ScheduledTaskState.Enabled ||
                second.State == ScheduledTaskState.Disabled)
            {
                count++;
            }
            return count;
        }

        private static bool IsServiceReady(ServiceComponentState service)
        {
            return service != null &&
                service.Exists &&
                service.StartType != 4 &&
                service.IsRunning;
        }

        private static bool TryReadDword(object value, out int? result)
        {
            result = null;
            if (value == null)
            {
                return true;
            }

            try
            {
                result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ShimCacheDataState InspectShimCacheData(
            object value,
            out int byteLength)
        {
            byteLength = 0;
            if (value == null)
            {
                return ShimCacheDataState.Missing;
            }

            byte[] bytes = value as byte[];
            if (bytes == null)
            {
                return ShimCacheDataState.Invalid;
            }

            byteLength = bytes.Length;
            if (bytes.Length < 4)
            {
                return ShimCacheDataState.Invalid;
            }

            // Windows 10/11 keeps a small binary header even when there are no
            // persisted entries. Treating that header as a healthy cache caused
            // the UI to report green while acquisition tools received []. A
            // payload larger than the modern header proves that persisted data
            // exists without parsing or exposing individual forensic entries.
            if (bytes.Length <= MaximumModernShimCacheHeaderLength)
            {
                return ShimCacheDataState.HeaderOnly;
            }

            for (int index = MaximumModernShimCacheHeaderLength;
                index < bytes.Length;
                index++)
            {
                if (bytes[index] != 0)
                {
                    return ShimCacheDataState.HasPayload;
                }
            }

            return ShimCacheDataState.HeaderOnly;
        }

        private static bool IsValidSid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                SecurityIdentifier sid = new SecurityIdentifier(value);
                return string.Equals(
                    sid.Value,
                    value,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static ForensicArtifactSnapshot CreateSnapshot(
            ForensicArtifactKind kind,
            ServiceVisualState state,
            string status,
            string detail)
        {
            return new ForensicArtifactSnapshot
            {
                Kind = kind,
                DisplayName = GetDisplayName(kind),
                CodeName = GetCodeName(kind),
                Description = GetDescription(kind),
                VisualState = state,
                StatusText = status,
                Detail = detail
            };
        }

        private static string GetDisplayName(ForensicArtifactKind kind)
        {
            switch (kind)
            {
                case ForensicArtifactKind.ShimCache:
                    return "ShimCache";
                case ForensicArtifactKind.Amcache:
                    return "Amcache";
                default:
                    return "UserAssist";
            }
        }

        private static string GetCodeName(ForensicArtifactKind kind)
        {
            switch (kind)
            {
                case ForensicArtifactKind.ShimCache:
                    return "AppCompatCache";
                case ForensicArtifactKind.Amcache:
                    return "Amcache.hve";
                default:
                    return "Current User";
            }
        }

        private static string GetDescription(ForensicArtifactKind kind)
        {
            switch (kind)
            {
                case ForensicArtifactKind.ShimCache:
                    return "程序兼容性缓存与访问线索";
                case ForensicArtifactKind.Amcache:
                    return "应用兼容性清单与文件存在痕迹";
                default:
                    return "当前用户通过 Explorer Shell 的交互记录";
            }
        }

        private static string GetWindowsDirectory()
        {
            string value = Environment.GetEnvironmentVariable("WINDIR");
            return string.IsNullOrWhiteSpace(value) ? @"C:\Windows" : value;
        }

        private static string GetUsefulError(Exception exception)
        {
            return exception.InnerException != null &&
                !string.IsNullOrWhiteSpace(exception.InnerException.Message)
                ? exception.InnerException.Message
                : exception.Message;
        }

        private sealed class WindowsForensicReadinessDataSource :
            IForensicReadinessDataSource
        {
            private const string ServicesRegistryPath =
                @"SYSTEM\CurrentControlSet\Services\";

            public object ReadMachineValue(string path, string name)
            {
                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                using (RegistryKey key = localMachine.OpenSubKey(path, false))
                {
                    return key == null ? null : key.GetValue(name);
                }
            }

            public object ReadUserValue(string userSid, string path, string name)
            {
                using (RegistryKey users = RegistryKey.OpenBaseKey(
                    RegistryHive.Users,
                    RegistryView.Default))
                using (RegistryKey key = users.OpenSubKey(
                    userSid + @"\" + path,
                    false))
                {
                    return key == null ? null : key.GetValue(name);
                }
            }

            public bool IsUserHiveLoaded(string userSid)
            {
                using (RegistryKey users = RegistryKey.OpenBaseKey(
                    RegistryHive.Users,
                    RegistryView.Default))
                using (RegistryKey key = users.OpenSubKey(userSid, false))
                {
                    return key != null;
                }
            }

            public ServiceComponentState ReadService(string serviceName)
            {
                ServiceComponentState result = new ServiceComponentState();
                try
                {
                    object start = ReadMachineValue(
                        ServicesRegistryPath + serviceName,
                        "Start");
                    if (start == null)
                    {
                        return result;
                    }

                    result.Exists = true;
                    result.StartType = Convert.ToInt32(
                        start,
                        CultureInfo.InvariantCulture);
                    using (ServiceController controller =
                        new ServiceController(serviceName))
                    {
                        result.IsRunning =
                            controller.Status == ServiceControllerStatus.Running;
                    }
                }
                catch (Exception exception)
                {
                    result.Error = GetUsefulError(exception);
                }

                return result;
            }

            public ScheduledTaskComponentState ReadScheduledTask(string taskPath)
            {
                ProcessResult result = RunScheduledTasks(
                    "/Query /TN \"" + taskPath + "\" /XML",
                    TimeSpan.FromSeconds(5));
                if (result.TimedOut)
                {
                    return new ScheduledTaskComponentState
                    {
                        State = ScheduledTaskState.Error,
                        Error = "读取计划任务超时"
                    };
                }
                if (result.ExitCode != 0)
                {
                    return new ScheduledTaskComponentState
                    {
                        State = ScheduledTaskState.Missing
                    };
                }

                Match enabled = Regex.Match(
                    result.StandardOutput ?? string.Empty,
                    @"<Enabled>\s*(?<value>true|false)\s*</Enabled>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                bool isEnabled = !enabled.Success ||
                    string.Equals(
                        enabled.Groups["value"].Value,
                        "true",
                        StringComparison.OrdinalIgnoreCase);
                return new ScheduledTaskComponentState
                {
                    State = isEnabled
                        ? ScheduledTaskState.Enabled
                        : ScheduledTaskState.Disabled
                };
            }

            public bool IsExplorerRunningInCurrentSession()
            {
                int sessionId = Process.GetCurrentProcess().SessionId;
                foreach (Process process in Process.GetProcessesByName("explorer"))
                {
                    using (process)
                    {
                        try
                        {
                            if (process.SessionId == sessionId)
                            {
                                return true;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                return false;
            }

            public bool FileExists(string path)
            {
                return File.Exists(path);
            }

            public void WriteMachineDword(string path, string name, int value)
            {
                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                using (RegistryKey key = localMachine.CreateSubKey(
                    path,
                    RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            "无法打开系统策略注册表项");
                    }
                    key.SetValue(name, value, RegistryValueKind.DWord);
                    key.Flush();
                }
            }

            public void WriteUserDword(
                string userSid,
                string path,
                string name,
                int value)
            {
                using (RegistryKey users = RegistryKey.OpenBaseKey(
                    RegistryHive.Users,
                    RegistryView.Default))
                using (RegistryKey key = users.CreateSubKey(
                    userSid + @"\" + path,
                    RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            "无法打开目标用户的注册表配置单元");
                    }
                    key.SetValue(name, value, RegistryValueKind.DWord);
                    key.Flush();
                }
            }

            public void WriteServiceStartType(string serviceName, int startType)
            {
                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                using (RegistryKey key = localMachine.OpenSubKey(
                    ServicesRegistryPath + serviceName,
                    RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            "当前系统未找到服务 " + serviceName);
                    }
                    key.SetValue("Start", startType, RegistryValueKind.DWord);
                    key.Flush();
                }
            }

            public bool EnableScheduledTask(string taskPath)
            {
                ProcessResult result = RunScheduledTasks(
                    "/Change /TN \"" + taskPath + "\" /ENABLE",
                    TimeSpan.FromSeconds(8));
                return !result.TimedOut && result.ExitCode == 0;
            }

            private static ProcessResult RunScheduledTasks(
                string arguments,
                TimeSpan timeout)
            {
                string executable = Path.Combine(
                    Environment.SystemDirectory,
                    "schtasks.exe");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    int timeoutMilliseconds = Math.Max(
                        1,
                        (int)Math.Min(
                            int.MaxValue,
                            timeout.TotalMilliseconds));
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        return new ProcessResult
                        {
                            TimedOut = true,
                            StandardOutput = standardOutput,
                            StandardError = standardError
                        };
                    }

                    return new ProcessResult
                    {
                        ExitCode = process.ExitCode,
                        StandardOutput = standardOutput,
                        StandardError = standardError
                    };
                }
            }

            private sealed class ProcessResult
            {
                public int ExitCode { get; set; }
                public bool TimedOut { get; set; }
                public string StandardOutput { get; set; }
                public string StandardError { get; set; }
            }
        }

        private enum ShimCacheDataState
        {
            Missing,
            HeaderOnly,
            HasPayload,
            Invalid
        }
    }
}
