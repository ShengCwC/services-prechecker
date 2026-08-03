using System.Collections.Generic;

namespace UndefinedSS.ServicesPrechecker
{
    internal enum ForensicArtifactKind
    {
        ShimCache,
        Amcache,
        UserAssist
    }

    internal enum ScheduledTaskState
    {
        Enabled,
        Disabled,
        Missing,
        Error
    }

    internal sealed class ServiceComponentState
    {
        public bool Exists { get; set; }
        public int? StartType { get; set; }
        public bool IsRunning { get; set; }
        public string Error { get; set; }
    }

    internal sealed class ScheduledTaskComponentState
    {
        public ScheduledTaskState State { get; set; }
        public string Error { get; set; }
    }

    internal sealed class ForensicArtifactSnapshot
    {
        public ForensicArtifactKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string CodeName { get; set; }
        public string Description { get; set; }
        public ServiceVisualState VisualState { get; set; }
        public string StatusText { get; set; }
        public string Detail { get; set; }

        public bool IsHealthy
        {
            get { return VisualState == ServiceVisualState.Running; }
        }
    }

    internal sealed class ForensicArtifactEnableResult
    {
        public ForensicArtifactKind Kind { get; set; }
        public string DisplayName { get; set; }
        public bool Success { get; set; }
        public bool ConfigurationChanged { get; set; }
        public bool RequiresRestart { get; set; }
        public string Message { get; set; }
    }

    internal sealed class ReadinessSnapshotBundle
    {
        public IList<ServiceSnapshot> Services { get; set; }
        public IList<ForensicArtifactSnapshot> Artifacts { get; set; }
    }

    internal sealed class ReadinessEnableBundle
    {
        public IList<EnableResult> Services { get; set; }
        public IList<ForensicArtifactEnableResult> Artifacts { get; set; }
    }
}
