using System;
using System.Collections.Generic;
using UndefinedSS.ServicesPrechecker;

internal static class ForensicArtifactManagerTests
{
    private const string UserSid = "S-1-5-21-111111111-222222222-333333333-1001";
    private static int failures;

    private static void Main()
    {
        TestShimCachePoliciesAndVersionSpecificService();
        TestAmcachePolicyTasksAndMissingComponents();
        TestUserAssistTargetsOnlyTheLaunchingUser();
        TestUserAssistRequiresExplorerShell();
        TestRestartAggregation();

        if (failures != 0)
        {
            Console.Error.WriteLine(failures + " forensic artifact test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All forensic artifact tests passed.");
    }

    private static void TestShimCachePoliciesAndVersionSpecificService()
    {
        FakeDataSource disabled = CreateReadyDataSource();
        disabled.MachineValues[MachineKey("DisableEngine")] = 1;
        disabled.Services["AeLookupSvc"] = new ServiceComponentState
        {
            Exists = true,
            StartType = 4,
            IsRunning = false
        };
        ForensicArtifactManager manager = new ForensicArtifactManager(disabled);

        ForensicArtifactSnapshot before = manager.GetSnapshots(UserSid)[0];
        AssertEqual(
            ServiceVisualState.Disabled,
            before.VisualState,
            "ShimCache detects disabled engine and service");

        ForensicArtifactEnableResult enabled = manager.EnableAll(UserSid)[0];
        AssertTrue(enabled.Success, "ShimCache enable succeeds");
        AssertTrue(enabled.ConfigurationChanged, "ShimCache reports configuration change");
        AssertTrue(enabled.RequiresRestart, "ShimCache change requires restart");
        AssertEqual(0, disabled.MachineValues[MachineKey("DisableEngine")], "engine policy enabled");
        AssertEqual(3, disabled.Services["AeLookupSvc"].StartType, "AeLookupSvc restored to manual");

        FakeDataSource headerOnly = CreateReadyDataSource();
        headerOnly.MachineValues[ShimCacheKey()] = new byte[52];
        ForensicArtifactManager headerOnlyManager =
            new ForensicArtifactManager(headerOnly);
        AssertEqual(
            ServiceVisualState.Stopped,
            headerOnlyManager.GetSnapshots(UserSid)[0].VisualState,
            "ShimCache header-only value is not reported as collectible data");
        ForensicArtifactEnableResult headerOnlyEnable =
            headerOnlyManager.EnableAll(UserSid)[0];
        AssertTrue(headerOnlyEnable.Success, "header-only cache keeps valid engine configuration");
        AssertFalse(headerOnlyEnable.ConfigurationChanged, "header-only cache does not invent a configuration write");
        AssertTrue(headerOnlyEnable.RequiresRestart, "header-only cache requires a new boot baseline");

        FakeDataSource missing = CreateReadyDataSource();
        missing.MachineValues.Remove(ShimCacheKey());
        ForensicArtifactManager missingManager =
            new ForensicArtifactManager(missing);
        AssertEqual(
            ServiceVisualState.Stopped,
            missingManager.GetSnapshots(UserSid)[0].VisualState,
            "missing ShimCache value is not reported as healthy");
        AssertTrue(
            missingManager.EnableAll(UserSid)[0].RequiresRestart,
            "missing ShimCache value requires a new boot baseline");

        FakeDataSource persisted = CreateReadyDataSource();
        byte[] persistedValue = new byte[256];
        persistedValue[80] = 1;
        persisted.MachineValues[ShimCacheKey()] = persistedValue;
        ForensicArtifactSnapshot persistedSnapshot =
            new ForensicArtifactManager(persisted).GetSnapshots(UserSid)[0];
        AssertEqual(
            ServiceVisualState.Running,
            persistedSnapshot.VisualState,
            "ShimCache requires persisted payload before reporting healthy");

        FakeDataSource malformed = CreateReadyDataSource();
        malformed.MachineValues[ShimCacheKey()] = "not binary";
        ForensicArtifactManager malformedManager =
            new ForensicArtifactManager(malformed);
        AssertEqual(
            ServiceVisualState.Error,
            malformedManager.GetSnapshots(UserSid)[0].VisualState,
            "non-binary ShimCache value is reported as invalid");
        AssertFalse(
            malformedManager.EnableAll(UserSid)[0].Success,
            "invalid ShimCache value is not claimed as repaired");

        FakeDataSource modern = CreateReadyDataSource();
        modern.Services.Remove("AeLookupSvc");
        ForensicArtifactSnapshot modernSnapshot =
            new ForensicArtifactManager(modern).GetSnapshots(UserSid)[0];
        AssertEqual(
            ServiceVisualState.Running,
            modernSnapshot.VisualState,
            "missing standalone AeLookupSvc is allowed on versions using the built-in engine");
    }

    private static void TestAmcachePolicyTasksAndMissingComponents()
    {
        FakeDataSource source = CreateReadyDataSource();
        source.MachineValues[MachineKey("DisableInventory")] = 1;
        source.Tasks[ForensicArtifactManager.CompatibilityAppraiserTask] =
            ScheduledTaskState.Disabled;
        source.Tasks[ForensicArtifactManager.ProgramDataUpdaterTask] =
            ScheduledTaskState.Missing;
        ForensicArtifactManager manager = new ForensicArtifactManager(source);

        AssertEqual(
            ServiceVisualState.Disabled,
            manager.GetSnapshots(UserSid)[1].VisualState,
            "Amcache detects disabled inventory policy");
        ForensicArtifactEnableResult result = manager.EnableAll(UserSid)[1];
        AssertTrue(result.Success, "Amcache can use one available inventory task");
        AssertTrue(result.ConfigurationChanged, "Amcache reports policy and task changes");
        AssertEqual(0, source.MachineValues[MachineKey("DisableInventory")], "inventory policy enabled");
        AssertEqual(
            ScheduledTaskState.Enabled,
            source.Tasks[ForensicArtifactManager.CompatibilityAppraiserTask],
            "existing compatibility task enabled");

        FakeDataSource stripped = CreateReadyDataSource();
        stripped.Tasks[ForensicArtifactManager.CompatibilityAppraiserTask] =
            ScheduledTaskState.Missing;
        stripped.Tasks[ForensicArtifactManager.ProgramDataUpdaterTask] =
            ScheduledTaskState.Missing;
        ForensicArtifactManager strippedManager =
            new ForensicArtifactManager(stripped);
        AssertEqual(
            ServiceVisualState.Missing,
            strippedManager.GetSnapshots(UserSid)[1].VisualState,
            "missing Windows inventory tasks are reported, not recreated");
        AssertFalse(
            strippedManager.EnableAll(UserSid)[1].Success,
            "missing system tasks cannot be claimed as repaired");
    }

    private static void TestUserAssistTargetsOnlyTheLaunchingUser()
    {
        FakeDataSource source = CreateReadyDataSource();
        source.UserValues[UserKey(UserSid, "NoInstrumentation")] = 1;
        source.UserValues[UserKey(UserSid, "Start_TrackProgs")] = 0;
        ForensicArtifactManager manager = new ForensicArtifactManager(source);

        AssertEqual(
            ServiceVisualState.Disabled,
            manager.GetSnapshots(UserSid)[2].VisualState,
            "UserAssist detects both current-user tracking switches");
        ForensicArtifactEnableResult result = manager.EnableAll(UserSid)[2];
        AssertTrue(result.Success, "UserAssist enable succeeds for loaded target hive");
        AssertTrue(result.RequiresRestart, "UserAssist policy changes require restart");
        AssertEqual(0, source.UserValues[UserKey(UserSid, "NoInstrumentation")], "NoInstrumentation disabled");
        AssertEqual(1, source.UserValues[UserKey(UserSid, "Start_TrackProgs")], "launch tracking enabled");
        AssertEqual(2, source.UserWrites.Count, "only the two documented tracking switches are written");

        string unavailableSid = "S-1-5-21-111111111-222222222-333333333-1002";
        AssertEqual(
            ServiceVisualState.Missing,
            manager.GetSnapshots(unavailableSid)[2].VisualState,
            "an unloaded different profile is not silently modified");
        AssertFalse(
            manager.EnableAll(unavailableSid)[2].Success,
            "an unloaded different profile is not enabled");
        AssertEqual(2, source.UserWrites.Count, "no writes target the unavailable profile");
    }

    private static void TestUserAssistRequiresExplorerShell()
    {
        FakeDataSource source = CreateReadyDataSource();
        source.ExplorerRunning = false;
        ForensicArtifactManager manager = new ForensicArtifactManager(source);
        AssertEqual(
            ServiceVisualState.Stopped,
            manager.GetSnapshots(UserSid)[2].VisualState,
            "custom or absent shell is reported separately from policy state");
        AssertFalse(
            manager.EnableAll(UserSid)[2].Success,
            "the application does not start or replace Explorer on behalf of the user");
    }

    private static void TestRestartAggregation()
    {
        AssertFalse(
            ForensicArtifactManager.RequiresRestartAfterEnable(null),
            "null artifact results do not require restart");
        AssertTrue(
            ForensicArtifactManager.RequiresRestartAfterEnable(
                new[]
                {
                    new ForensicArtifactEnableResult
                    {
                        Success = true,
                        ConfigurationChanged = true
                    }
                }),
            "artifact configuration changes require restart");
    }

    private static FakeDataSource CreateReadyDataSource()
    {
        FakeDataSource source = new FakeDataSource();
        source.LoadedUsers.Add(UserSid);
        source.ExplorerRunning = true;
        source.Services["PcaSvc"] = ReadyService(3);
        source.Services["DiagTrack"] = ReadyService(2);
        source.Services["AeLookupSvc"] = ReadyService(3);
        byte[] shimCacheValue = new byte[256];
        shimCacheValue[80] = 1;
        source.MachineValues[ShimCacheKey()] = shimCacheValue;
        source.Tasks[ForensicArtifactManager.CompatibilityAppraiserTask] =
            ScheduledTaskState.Enabled;
        source.Tasks[ForensicArtifactManager.ProgramDataUpdaterTask] =
            ScheduledTaskState.Enabled;
        return source;
    }

    private static ServiceComponentState ReadyService(int startType)
    {
        return new ServiceComponentState
        {
            Exists = true,
            StartType = startType,
            IsRunning = true
        };
    }

    private static string MachineKey(string valueName)
    {
        return ForensicArtifactManager.AppCompatPolicyPath + "|" + valueName;
    }

    private static string ShimCacheKey()
    {
        return ForensicArtifactManager.AppCompatCachePath + "|" +
            ForensicArtifactManager.AppCompatCacheValueName;
    }

    private static string UserKey(string sid, string valueName)
    {
        string path = valueName == "NoInstrumentation"
            ? ForensicArtifactManager.UserTrackingPolicyPath
            : ForensicArtifactManager.UserTrackingSettingsPath;
        return sid + "|" + path + "|" + valueName;
    }

    private sealed class FakeDataSource : IForensicReadinessDataSource
    {
        internal FakeDataSource()
        {
            MachineValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            UserValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            LoadedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Services = new Dictionary<string, ServiceComponentState>(StringComparer.OrdinalIgnoreCase);
            Tasks = new Dictionary<string, ScheduledTaskState>(StringComparer.OrdinalIgnoreCase);
            ExistingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UserWrites = new List<string>();
        }

        internal Dictionary<string, object> MachineValues { get; private set; }
        internal Dictionary<string, object> UserValues { get; private set; }
        internal HashSet<string> LoadedUsers { get; private set; }
        internal Dictionary<string, ServiceComponentState> Services { get; private set; }
        internal Dictionary<string, ScheduledTaskState> Tasks { get; private set; }
        internal HashSet<string> ExistingFiles { get; private set; }
        internal List<string> UserWrites { get; private set; }
        internal bool ExplorerRunning { get; set; }

        public object ReadMachineValue(string path, string name)
        {
            object value;
            return MachineValues.TryGetValue(path + "|" + name, out value)
                ? value
                : null;
        }

        public object ReadUserValue(string userSid, string path, string name)
        {
            object value;
            return UserValues.TryGetValue(
                userSid + "|" + path + "|" + name,
                out value)
                ? value
                : null;
        }

        public bool IsUserHiveLoaded(string userSid)
        {
            return LoadedUsers.Contains(userSid);
        }

        public ServiceComponentState ReadService(string serviceName)
        {
            ServiceComponentState value;
            return Services.TryGetValue(serviceName, out value)
                ? value
                : new ServiceComponentState();
        }

        public ScheduledTaskComponentState ReadScheduledTask(string taskPath)
        {
            ScheduledTaskState state;
            return new ScheduledTaskComponentState
            {
                State = Tasks.TryGetValue(taskPath, out state)
                    ? state
                    : ScheduledTaskState.Missing
            };
        }

        public bool IsExplorerRunningInCurrentSession()
        {
            return ExplorerRunning;
        }

        public bool FileExists(string path)
        {
            return ExistingFiles.Contains(path);
        }

        public void WriteMachineDword(string path, string name, int value)
        {
            MachineValues[path + "|" + name] = value;
        }

        public void WriteUserDword(string userSid, string path, string name, int value)
        {
            string key = userSid + "|" + path + "|" + name;
            UserValues[key] = value;
            UserWrites.Add(key);
        }

        public void WriteServiceStartType(string serviceName, int startType)
        {
            Services[serviceName].StartType = startType;
        }

        public bool EnableScheduledTask(string taskPath)
        {
            if (!Tasks.ContainsKey(taskPath) ||
                Tasks[taskPath] == ScheduledTaskState.Missing)
            {
                return false;
            }
            Tasks[taskPath] = ScheduledTaskState.Enabled;
            return true;
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
