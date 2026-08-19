using System.Diagnostics;
using System.Xml.Linq;

namespace Game.Managers;

public sealed record StarterInstanceContext(
    string Id,
    string InstancePath,
    string[] GameArguments);

public static class StarterInstanceManager
{
    public const string DefaultInstanceId = "default";
    public const string InstanceArgument = "--instance";

    private const string _settingsPath = "starter:Starter.xml";
    private const string _instancesPath = "starter:Instances";
    private const string _runtimeDirectoryName = ".runtime";

    private static string? _currentRuntimeMarkerPath;

    private static bool _processExitRegistered;

    public static StarterInstanceContext Current { get; private set; } = new(
        DefaultInstanceId,
        string.Empty,
        []);

    public static StarterInstanceContext Initialize(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parsedArguments = ParseArguments(args);
        var settings = Load();
        var selectedFromArgument = parsedArguments.InstanceId is not null;
        var instanceId = parsedArguments.InstanceId
                         ?? NormalizeOptionalInstanceId(settings.NextInstance)
                         ?? NormalizeOptionalInstanceId(settings.CurrentInstance)
                         ?? DefaultInstanceId;

        ValidateInstanceId(instanceId);
        var instancePath = GetInstancePath(instanceId);
        if (!Storage.DirectoryExists(instancePath))
        {
            Storage.CreateDirectory(instancePath);
        }

        var consumesPendingSwitch = !string.IsNullOrWhiteSpace(settings.NextInstance) &&
                                    (!selectedFromArgument || string.Equals(
                                        parsedArguments.InstanceId,
                                        settings.NextInstance,
                                        StringComparison.Ordinal));
        if (consumesPendingSwitch)
        {
            settings.CurrentInstance = instanceId;
            settings.NextInstance = string.Empty;
            Save(settings);
        }
        else if (!Storage.FileExists(_settingsPath))
        {
            Save(settings);
        }

        Current = new StarterInstanceContext(
            instanceId,
            instancePath,
            parsedArguments.GameArguments);
        try
        {
            RegisterCurrentProcess();
        }
        catch (Exception ex)
        {
            _currentRuntimeMarkerPath = null;
            Log.Warning($"Failed to register starter instance process: {ex.Message}");
        }
        return Current;
    }

    public static void RequestSwitch(string targetInstanceId)
    {
        ValidateInstanceId(targetInstanceId);
        if (!Storage.DirectoryExists(GetInstancePath(targetInstanceId)))
        {
            throw new InvalidOperationException($"Starter instance '{targetInstanceId}' does not exist.");
        }

        var settings = Load();
        settings.NextInstance = targetInstanceId;
        Save(settings);
    }

    public static void CreateInstance(string instanceId)
    {
        ValidateInstanceId(instanceId);
        var instancePath = GetInstancePath(instanceId);
        if (Storage.DirectoryExists(instancePath))
        {
            throw new InvalidOperationException($"Starter instance '{instanceId}' already exists.");
        }

        Storage.CreateDirectory(instancePath);
    }

    public static void DeleteInstance(string instanceId)
    {
        ValidateInstanceId(instanceId);
        if (string.Equals(instanceId, Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The current starter instance cannot be deleted.");
        }

        if (IsInstanceRunning(instanceId))
        {
            throw new InvalidOperationException("A running starter instance cannot be deleted.");
        }

        var instancePath = GetInstancePath(instanceId);
        if (!Storage.DirectoryExists(instancePath))
        {
            throw new InvalidOperationException($"Starter instance '{instanceId}' does not exist.");
        }

        Storage.DeleteDirectoryRecursive(instancePath);
    }

    public static bool IsInstanceRunning(string instanceId)
    {
        ValidateInstanceId(instanceId);
        var runtimeDirectory = Storage.CombinePaths(GetInstancePath(instanceId), _runtimeDirectoryName);
        if (!Storage.DirectoryExists(runtimeDirectory))
        {
            return false;
        }

        var isRunning = false;
        foreach (var fileName in Storage.ListFileNames(runtimeDirectory).ToArray())
        {
            var markerPath = Storage.CombinePaths(runtimeDirectory, fileName);
            if (IsRuntimeMarkerAlive(markerPath))
            {
                isRunning = true;
            }
            else
            {
                TryDeleteFile(markerPath);
            }
        }

        return isRunning;
    }

    public static IReadOnlyList<string> ListInstances()
    {
        if (!Storage.DirectoryExists(_instancesPath))
        {
            return [];
        }

        return Storage.ListDirectoryNames(_instancesPath)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    public static RunModeType GetRunMode(string instanceId)
    {
        ValidateInstanceId(instanceId);
        var runningSettingPath = Storage.CombinePaths(
            GetInstancePath(instanceId),
            "Config",
            "RunningSetting.xml");
        if (!Storage.FileExists(runningSettingPath))
        {
            return RunModeType.Gui;
        }

        try
        {
            using var stream = Storage.OpenFile(runningSettingPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            return Enum.TryParse<RunModeType>(
                root.Attribute(nameof(RunningSetting.RunMode))?.Value,
                true,
                out var runMode)
                ? runMode
                : RunModeType.Gui;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to read run mode for starter instance '{instanceId}': {ex.Message}");
            return RunModeType.Gui;
        }
    }

    public static void ValidateInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (instanceId is "." or ".." ||
            instanceId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Instance id can contain only ASCII letters, digits, '-' and '_'.",
                nameof(instanceId));
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        string? instanceId = null;
        var gameArguments = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], InstanceArgument, StringComparison.OrdinalIgnoreCase))
            {
                gameArguments.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            {
                throw new ArgumentException($"{InstanceArgument} requires an instance id.", nameof(args));
            }

            if (instanceId is not null)
            {
                throw new ArgumentException($"{InstanceArgument} can be specified only once.", nameof(args));
            }

            instanceId = args[++i].Trim();
            ValidateInstanceId(instanceId);
        }

        return new ParsedArguments(instanceId, gameArguments.ToArray());
    }

    private static StarterSettings Load()
    {
        if (!Storage.FileExists(_settingsPath))
        {
            return new StarterSettings();
        }

        using var stream = Storage.OpenFile(_settingsPath, OpenFileMode.Read);
        var root = XElement.Load(stream);
        return new StarterSettings
        {
            CurrentInstance = root.Attribute(nameof(StarterSettings.CurrentInstance))?.Value ?? DefaultInstanceId,
            NextInstance = root.Attribute(nameof(StarterSettings.NextInstance))?.Value ?? string.Empty
        };
    }

    private static void Save(StarterSettings settings)
    {
        var root = new XElement("Starter",
            new XAttribute(nameof(StarterSettings.CurrentInstance), settings.CurrentInstance),
            new XAttribute(nameof(StarterSettings.NextInstance), settings.NextInstance));
        using var stream = Storage.OpenFile(_settingsPath, OpenFileMode.Create);
        root.Save(stream);
    }

    private static string GetInstancePath(string instanceId)
    {
        return Storage.CombinePaths(_instancesPath, instanceId);
    }

    private static void RegisterCurrentProcess()
    {
        if (_currentRuntimeMarkerPath != null)
        {
            TryDeleteFile(_currentRuntimeMarkerPath);
        }

        var runtimeDirectory = Storage.CombinePaths(Current.InstancePath, _runtimeDirectoryName);
        Storage.CreateDirectory(runtimeDirectory);
        var process = Process.GetCurrentProcess();
        _currentRuntimeMarkerPath = Storage.CombinePaths(runtimeDirectory, $"{process.Id}.xml");
        var marker = new XElement("InstanceProcess",
            new XAttribute("Pid", process.Id),
            new XAttribute("StartTimeUtcTicks", process.StartTime.ToUniversalTime().Ticks));
        using (var stream = Storage.OpenFile(_currentRuntimeMarkerPath, OpenFileMode.Create))
        {
            marker.Save(stream);
        }

        if (!_processExitRegistered)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (_currentRuntimeMarkerPath != null)
                {
                    TryDeleteFile(_currentRuntimeMarkerPath);
                }
            };
            _processExitRegistered = true;
        }
    }

    private static bool IsRuntimeMarkerAlive(string markerPath)
    {
        try
        {
            using var stream = Storage.OpenFile(markerPath, OpenFileMode.Read);
            var marker = XElement.Load(stream);
            var pid = (int?)marker.Attribute("Pid");
            var startTimeUtcTicks = (long?)marker.Attribute("StartTimeUtcTicks");
            if (!pid.HasValue || !startTimeUtcTicks.HasValue)
            {
                return false;
            }

            using var process = Process.GetProcessById(pid.Value);
            return !process.HasExited &&
                   process.StartTime.ToUniversalTime().Ticks == startTimeUtcTicks.Value;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (Storage.FileExists(path))
            {
                Storage.DeleteFile(path);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to delete starter runtime marker '{path}': {ex.Message}");
        }
    }

    private static string? NormalizeOptionalInstanceId(string? instanceId)
    {
        return string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
    }

    private sealed class StarterSettings
    {
        public string CurrentInstance { get; set; } = DefaultInstanceId;

        public string NextInstance { get; set; } = string.Empty;
    }

    private sealed record ParsedArguments(string? InstanceId, string[] GameArguments);
}
