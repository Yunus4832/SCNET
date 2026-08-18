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
