using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class ServiceManager
    {
        private static readonly IServiceOperations Operations =
            new WindowsServiceOperations();

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static IList<ServiceSnapshot> GetSnapshots()
        {
            List<ServiceSnapshot> snapshots = new List<ServiceSnapshot>();
            foreach (ServiceDefinition definition in ServiceCatalog.All)
            {
                snapshots.Add(GetSnapshot(definition));
            }

            return snapshots;
        }

        public static ServiceSnapshot GetSnapshot(ServiceDefinition definition)
        {
            ServiceSnapshot snapshot = new ServiceSnapshot();
            snapshot.Definition = definition;
            snapshot.Detail = string.Empty;

            int? startType;
            try
            {
                startType = Operations.ReadStartType(definition.ServiceName);
            }
            catch (Exception exception)
            {
                snapshot.VisualState = ServiceVisualState.Error;
                snapshot.StatusText = "读取失败";
                snapshot.StartTypeText = "未知";
                snapshot.Detail = GetUsefulError(exception);
                return snapshot;
            }

            if (!startType.HasValue)
            {
                snapshot.VisualState = ServiceVisualState.Missing;
                snapshot.StatusText = "未找到";
                snapshot.StartTypeText = "不可用";
                snapshot.Detail = "当前 Windows 版本未提供此服务";
                return snapshot;
            }

            snapshot.StartTypeText = GetStartTypeText(startType.Value);

            try
            {
                using (ServiceController controller = new ServiceController(definition.ServiceName))
                {
                    ServiceControllerStatus status = controller.Status;
                    ApplyObservedState(snapshot, startType.Value, status);
                }
            }
            catch (InvalidOperationException exception)
            {
                snapshot.VisualState = ServiceVisualState.Error;
                snapshot.StatusText = "读取失败";
                snapshot.Detail = GetUsefulError(exception);
            }
            catch (Win32Exception exception)
            {
                snapshot.VisualState = ServiceVisualState.Error;
                snapshot.StatusText = "读取失败";
                snapshot.Detail = GetUsefulError(exception);
            }

            return snapshot;
        }

        internal static void ApplyObservedState(
            ServiceSnapshot snapshot,
            int startType,
            ServiceControllerStatus status)
        {
            if (snapshot == null || snapshot.Definition == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            if (startType != snapshot.Definition.DesiredStartType)
            {
                snapshot.VisualState = startType == 4
                    ? ServiceVisualState.Disabled
                    : ServiceVisualState.Stopped;
                snapshot.StatusText = startType == 4
                    ? "已禁用"
                    : "启动方式异常";
                snapshot.Detail =
                    "当前启动方式为" + GetStartTypeText(startType) +
                    "，应为" + GetStartTypeText(snapshot.Definition.DesiredStartType);
                if (status == ServiceControllerStatus.Running)
                {
                    snapshot.Detail += "；服务虽暂时运行，重启后仍可能不可用";
                }

                return;
            }

            if (status == ServiceControllerStatus.Running)
            {
                snapshot.VisualState = ServiceVisualState.Running;
                snapshot.StatusText = "正在运行";
            }
            else if (status == ServiceControllerStatus.StartPending)
            {
                snapshot.VisualState = ServiceVisualState.Stopped;
                snapshot.StatusText = "正在启动";
            }
            else
            {
                snapshot.VisualState = ServiceVisualState.Stopped;
                snapshot.StatusText = "未运行";
            }
        }

        internal static bool RequiresRestartAfterEnable(
            IEnumerable<EnableResult> results)
        {
            if (results == null)
            {
                return false;
            }

            foreach (EnableResult result in results)
            {
                if (result != null &&
                    (result.ConfigurationChanged ||
                     result.RuntimeStateChanged ||
                     result.RequiresRestart))
                {
                    return true;
                }
            }

            return false;
        }

        public static IList<EnableResult> EnableAll()
        {
            if (!IsAdministrator())
            {
                throw new InvalidOperationException("启用系统服务需要管理员权限。");
            }

            List<EnableResult> results = new List<EnableResult>();
            foreach (ServiceDefinition definition in ServiceCatalog.All)
            {
                results.Add(Enable(definition, Operations));
            }

            return results;
        }

        public static bool RelaunchElevated(string targetUserSid)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Process.GetCurrentProcess().MainModule.FileName;
            info.Arguments = "--enable-all";
            if (!string.IsNullOrWhiteSpace(targetUserSid))
            {
                SecurityIdentifier sid = new SecurityIdentifier(targetUserSid);
                info.Arguments += " --target-user-sid=" + sid.Value;
            }
            info.UseShellExecute = true;
            info.Verb = "runas";
            Process.Start(info);
            return true;
        }

        internal static EnableResult Enable(
            ServiceDefinition definition,
            IServiceOperations operations)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            if (operations == null)
            {
                throw new ArgumentNullException("operations");
            }

            EnableResult result = new EnableResult();
            result.Definition = definition;

            int? currentStart;
            try
            {
                currentStart = operations.ReadStartType(definition.ServiceName);
                if (!currentStart.HasValue)
                {
                    result.Success = false;
                    result.Message = "当前系统未找到此服务";
                    return result;
                }

                if (currentStart.Value != definition.DesiredStartType)
                {
                    operations.ChangeStartType(
                        definition.ServiceName,
                        definition.DesiredStartType);
                    result.ConfigurationChanged = true;

                    int? confirmedStart = operations.ReadStartType(definition.ServiceName);
                    if (!confirmedStart.HasValue ||
                        confirmedStart.Value != definition.DesiredStartType)
                    {
                        result.Success = false;
                        result.Message = "启动方式修改后未通过系统确认";
                        return result;
                    }
                }
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Message = "无法修改启动方式：" + GetUsefulError(exception);
                return result;
            }

            try
            {
                ServiceControllerStatus initialStatus =
                    operations.GetStatus(definition.ServiceName);

                if (definition.IsDriver)
                {
                    result.Success = true;
                    result.RequiresRestart =
                        initialStatus != ServiceControllerStatus.Running;
                    result.Message = result.RequiresRestart
                        ? "已启用，重启 Windows 后生效"
                        : "已启用并正在运行";
                    return result;
                }

                if (initialStatus != ServiceControllerStatus.Running)
                {
                    result.RuntimeStateChanged = true;
                    operations.StartAndWaitForRunning(
                        definition.ServiceName,
                        TimeSpan.FromSeconds(20));
                }

                ServiceControllerStatus finalStatus =
                    operations.GetStatus(definition.ServiceName);
                result.Success = finalStatus == ServiceControllerStatus.Running;
                if (result.Success)
                {
                    result.Message = result.ConfigurationChanged
                        ? "启动方式已修复，服务正在运行"
                        : "已启用并正在运行";
                }
                else
                {
                    result.Message = "启动请求已完成，但服务未进入运行状态";
                }

                return result;
            }
            catch (Exception exception)
            {
                if (definition.IsDriver)
                {
                    result.Success = true;
                    result.RequiresRestart = true;
                    result.Message = "已启用，重启 Windows 后生效";
                }
                else
                {
                    result.Success = false;
                    if (result.ConfigurationChanged)
                    {
                        result.Message =
                            "启动方式已修复，但服务启动失败：" +
                            GetUsefulError(exception);
                    }
                    else
                    {
                        result.Message = "启动失败：" + GetUsefulError(exception);
                    }
                }

                return result;
            }
        }

        private static string GetStartTypeText(int startType)
        {
            switch (startType)
            {
                case 0:
                    return "引导启动";
                case 1:
                    return "系统启动";
                case 2:
                    return "自动";
                case 3:
                    return "手动";
                case 4:
                    return "已禁用";
                default:
                    return "未知";
            }
        }

        private static string GetUsefulError(Exception exception)
        {
            if (exception.InnerException != null && !string.IsNullOrWhiteSpace(exception.InnerException.Message))
            {
                return exception.InnerException.Message;
            }

            return exception.Message;
        }
    }
}
