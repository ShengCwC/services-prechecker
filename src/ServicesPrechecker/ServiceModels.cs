using System.Collections.Generic;

namespace UndefinedSS.ServicesPrechecker
{
    internal enum ServiceVisualState
    {
        Running,
        Stopped,
        Disabled,
        Missing,
        Error,
        RebootRequired
    }

    internal sealed class ServiceDefinition
    {
        public ServiceDefinition(
            string displayName,
            string serviceName,
            string description,
            int desiredStartType,
            bool isDriver)
        {
            DisplayName = displayName;
            ServiceName = serviceName;
            Description = description;
            DesiredStartType = desiredStartType;
            IsDriver = isDriver;
        }

        public string DisplayName { get; private set; }
        public string ServiceName { get; private set; }
        public string Description { get; private set; }
        public int DesiredStartType { get; private set; }
        public bool IsDriver { get; private set; }
    }

    internal sealed class ServiceSnapshot
    {
        public ServiceDefinition Definition { get; set; }
        public ServiceVisualState VisualState { get; set; }
        public string StatusText { get; set; }
        public string StartTypeText { get; set; }
        public string Detail { get; set; }

        public bool IsHealthy
        {
            get { return VisualState == ServiceVisualState.Running; }
        }
    }

    internal sealed class EnableResult
    {
        public ServiceDefinition Definition { get; set; }
        public bool Success { get; set; }
        public bool RequiresRestart { get; set; }
        public string Message { get; set; }
    }

    internal static class ServiceCatalog
    {
        public static readonly IList<ServiceDefinition> All = new List<ServiceDefinition>
        {
            new ServiceDefinition("DNS Client", "Dnscache", "域名解析缓存与访问记录", 2, false),
            new ServiceDefinition("DPS", "DPS", "Windows 故障诊断数据", 2, false),
            new ServiceDefinition("DiagTrack", "DiagTrack", "系统遥测与诊断事件", 2, false),
            new ServiceDefinition("PcaSvc", "PcaSvc", "程序运行与兼容性线索", 3, false),
            new ServiceDefinition("SysMain", "SysMain", "应用使用与预取活动", 2, false),
            new ServiceDefinition("Windows Event Log", "EventLog", "系统与应用事件日志", 2, false),
            new ServiceDefinition("BAM", "bam", "后台程序活动记录", 1, true)
        };
    }
}
