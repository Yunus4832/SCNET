using System.Xml.Linq;

using Engine.Network;

using Game.Admission;

using static Game.Startup.SessionInfoSerializer;

namespace Game.Startup;

/// <summary>Instance-scoped startup profiles and session selection with explicit storage paths.</summary>
public sealed class StartupProfileStore
{
    public StartupProfileStore(string runningSettingsPath, string sessionsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runningSettingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionsPath);
        RunningSettingsPath = runningSettingsPath;
        SessionsPath = sessionsPath;
    }

    public string RunningSettingsPath { get; }

    public string SessionsPath { get; }

    public StartupContext LoadStartup(
        string[] args,
        Func<ServerAdmissionOverrides>? readAdmissionEnvironment = null,
        SessionTarget defaultTarget = SessionTarget.MainMenu
    )
    {
        var settings = LoadRunningSettings();
        var request = StartupArgumentsParser.Parse(
            settings,
            args,
            readAdmissionEnvironment ?? StartupArgumentsParser.ReadAdmissionEnvironment
        );
        var session = ResolveStartupSession(
            settings,
            request,
            ResolveActiveSessionId(settings, request),
            defaultTarget
        );
        var context = new StartupContext(settings, request, session);
        if (request.Save)
        {
            SaveRunningSettings(settings);
            SaveSession(session);
        }

        return context;
    }

    public RunningSetting CurrentRunningSettings { get; private set; } = new();

    public RunningSetting LoadRunningSettings()
    {
        var runningSetting = ReadRunningSettingsFile();
        NormalizeRunningSettings(runningSetting);
        EnsureFileExists(runningSetting);
        CurrentRunningSettings = CloneRunningSettings(runningSetting);
        return runningSetting;
    }

    public void SaveRunningSettings(RunningSetting runningSetting)
    {
        try
        {
            EnsureParentDirectory(RunningSettingsPath);

            using var stream = Storage.OpenFile(RunningSettingsPath, OpenFileMode.Create);
            RunningSettingSerializer.Save(stream, runningSetting);
            CurrentRunningSettings = CloneRunningSettings(runningSetting);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {RunningSettingsPath}: {ex.Message}");
        }
    }


    public void SetPendingSession(string? sessionId)
    {
        UpdateRunningSettings(setting => setting.PendingSessionId = NormalizePendingSessionId(sessionId));
    }

    public void ClearPendingSession()
    {
        SetPendingSession(null);
    }

    public void UpdateRunningSettings(Action<RunningSetting> update)
    {
        var runningSetting = ReadRunningSettingsFile();
        NormalizeRunningSettings(runningSetting);
        update(runningSetting);
        SaveRunningSettings(runningSetting);
    }

    private RunningSetting ReadRunningSettingsFile()
    {
        var runningSetting = new RunningSetting();
        try
        {
            if (!Storage.FileExists(RunningSettingsPath))
            {
                return runningSetting;
            }

            using var stream = Storage.OpenFile(RunningSettingsPath, OpenFileMode.Read);
            runningSetting = RunningSettingSerializer.Load(stream);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {RunningSettingsPath}: {ex.Message}");
        }

        return runningSetting;
    }

    private void EnsureFileExists(RunningSetting runningSetting)
    {
        if (!Storage.FileExists(RunningSettingsPath))
        {
            SaveRunningSettings(runningSetting);
        }
    }

    private void NormalizeRunningSettings(RunningSetting runningSetting)
    {
        runningSetting.WindowWidth = Math.Max(runningSetting.WindowWidth, 0);
        runningSetting.WindowHeight = Math.Max(runningSetting.WindowHeight, 0);
        runningSetting.DefaultSessionId = NormalizePersistedSessionReference(runningSetting.DefaultSessionId);
        runningSetting.PendingSessionId = NormalizePersistedSessionReference(runningSetting.PendingSessionId);
    }

    private RunningSetting CloneRunningSettings(RunningSetting runningSetting)
    {
        return new RunningSetting
        {
            LogLevel = runningSetting.LogLevel,
            WindowMode = runningSetting.WindowMode,
            WindowWidth = runningSetting.WindowWidth,
            WindowHeight = runningSetting.WindowHeight,
            DefaultSessionId = runningSetting.DefaultSessionId,
            PendingSessionId = runningSetting.PendingSessionId,
            RemainingArgs = runningSetting.RemainingArgs.ToArray()
        };
    }

    private string NormalizePendingSessionId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private string NormalizePersistedSessionReference(string? value)
    {
        var sessionId = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return SessionExists(sessionId) ? sessionId : string.Empty;
    }

    public SessionInfo LoadSession(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return CreateDefault(string.Empty);
        }

        var sessionInfo = CreateDefault(normalizedSessionId);
        try
        {
            if (!Storage.FileExists(SessionsPath))
            {
                return sessionInfo;
            }

            using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElement(root, normalizedSessionId);
            if (sessionElement == null)
            {
                return sessionInfo;
            }

            Populate(sessionInfo, sessionElement);
        }
        catch (AdmissionConfigurationException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Startup session configuration is unreadable or malformed; refusing fallback.",
                error
            );
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionsPath}: {ex.Message}");
        }

        return sessionInfo;
    }

    public SessionInfo? LoadSessionByName(string? sessionName)
    {
        var normalizedSessionName = NormalizeSessionName(sessionName);
        if (string.IsNullOrWhiteSpace(normalizedSessionName))
        {
            return null;
        }

        try
        {
            if (!Storage.FileExists(SessionsPath))
            {
                return null;
            }

            using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElementByName(root, normalizedSessionName);
            if (sessionElement == null)
            {
                return null;
            }

            var sessionInfo =
                CreateDefault(NormalizeSessionId(sessionElement.Attribute(nameof(SessionInfo.SessionId))?.Value));
            Populate(sessionInfo, sessionElement);
            return sessionInfo;
        }
        catch (AdmissionConfigurationException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Startup session configuration is unreadable or malformed; refusing fallback.",
                error
            );
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionsPath}: {ex.Message}");
            return null;
        }
    }

    public string ResolveSessionIdForName(string? sessionName)
    {
        var normalizedSessionName = NormalizeSessionName(sessionName);
        if (string.IsNullOrWhiteSpace(normalizedSessionName))
        {
            return Guid.NewGuid().ToString("N");
        }

        return LoadSessionByName(normalizedSessionName)?.SessionId ?? Guid.NewGuid().ToString("N");
    }

    public bool SessionExists(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        return IsValidSessionId(normalizedSessionId) && TryLoadExisting(normalizedSessionId, out _);
    }

    public void SaveSession(SessionInfo sessionInfo)
    {
        try
        {
            EnsureParentDirectory(SessionsPath);

            Normalize(sessionInfo);

            var root = LoadSessionRoot();
            var existing = FindSessionElement(root, sessionInfo.SessionId);
            existing?.Remove();
            root.Add(Serialize(sessionInfo));

            using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Create);
            root.Save(stream);
        }
        catch (AdmissionConfigurationException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Startup session configuration is unreadable or malformed; refusing fallback.",
                error
            );
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {SessionsPath}: {ex.Message}");
        }
    }

    public SessionInfo ResolveStartupSession(
        RunningSetting settings,
        StartupRequest request,
        string activeSessionId,
        SessionTarget defaultTarget = SessionTarget.MainMenu
    )
    {
        var sessionId = NormalizeSessionId(activeSessionId);
        if (!TryLoadExisting(sessionId, out var sessionInfo))
        {
            sessionInfo = CreateSessionForStartup(settings, request, sessionId, defaultTarget);
        }

        ApplySessionOverrides(sessionInfo, request);
        Normalize(sessionInfo);
        return sessionInfo;
    }

    private bool TryLoadExisting(string? sessionId, out SessionInfo sessionInfo)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        sessionInfo = CreateDefault(normalizedSessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return false;
        }

        try
        {
            if (!Storage.FileExists(SessionsPath))
            {
                return false;
            }

            using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElement(root, normalizedSessionId);
            if (sessionElement == null)
            {
                return false;
            }

            Populate(sessionInfo, sessionElement);
            return true;
        }
        catch (AdmissionConfigurationException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Startup session configuration is unreadable or malformed; refusing fallback.",
                error
            );
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionsPath}: {ex.Message}");
            return false;
        }
    }

    private XElement LoadSessionRoot()
    {
        if (!Storage.FileExists(SessionsPath))
        {
            return new XElement("Sessions");
        }

        using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Read);
        var root = XElement.Load(stream);
        if (!string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            return root;
        }

        var sessions = new XElement("Sessions");
        sessions.Add(new XElement(root));
        return sessions;
    }

    private SessionInfo CreateSessionForStartup(
        RunningSetting settings,
        StartupRequest request,
        string sessionId,
        SessionTarget defaultTarget
    )
    {
        var explicitNamedSession = request.HasExplicitSession &&
                                   !string.IsNullOrWhiteSpace(request.SessionName);
        var createWorldSession = explicitNamedSession || defaultTarget == SessionTarget.World;
        var startupWorldName = !string.IsNullOrWhiteSpace(request.World)
            ? NormalizeWorld(request.World)
            : explicitNamedSession
                ? NormalizeWorld(request.SessionName)
                : "World";

        return new SessionInfo
        {
            SessionId = sessionId,
            Name = request.HasExplicitSession
                ? NormalizeSessionName(request.SessionName)
                : string.Empty,
            Target = createWorldSession ? SessionTarget.World : SessionTarget.MainMenu,
            World = startupWorldName,
            Seed = string.Empty
        };
    }

    private void ApplySessionOverrides(SessionInfo sessionInfo, StartupRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConnectHost) &&
            request.ConnectPort is > 0)
        {
            sessionInfo.Target = SessionTarget.RemoteServer;
            sessionInfo.RemoteTarget = new AddressNetworkTarget(request.ConnectHost, request.ConnectPort.Value);
        }
        else if (request.HasExplicitSession)
        {
            if (sessionInfo.Target != SessionTarget.HostedWorld) { sessionInfo.Target = SessionTarget.World; }

            sessionInfo.RemoteTarget = null;
        }

        if (!string.IsNullOrWhiteSpace(request.World))
        {
            sessionInfo.World = NormalizeWorld(request.World);
        }

        if (request.Seed != null)
        {
            sessionInfo.Seed = request.Seed;
        }

        if (request.GameMode is { } gameMode)
        {
            sessionInfo.GameMode = gameMode;
        }

        if (request.TerrainGenerationMode is { } terrain)
        {
            sessionInfo.TerrainGenerationMode = terrain;
        }

        if (request.TerrainLevel is { } level)
        {
            sessionInfo.TerrainLevel = level;
        }

        if (request.ServerPort is { } serverPort)
        {
            sessionInfo.ServerPort = serverPort;
        }

        if (request.BroadcastPort is { } broadcastPort)
        {
            sessionInfo.BroadcastPort = broadcastPort;
        }

        if (request.AutoJoinCharacterName is not null)
        {
            sessionInfo.AutoJoinCharacterName = request.AutoJoinCharacterName;
        }

        sessionInfo.P2POverrides = sessionInfo.P2POverrides.Merge(request.P2POverrides);
        if (request.ServerIdentityAddress is not null)
        {
            sessionInfo.ServerIdentityAddress =
                IdentityAddress.Normalize(request.ServerIdentityAddress);
        }

        sessionInfo.AdmissionOverrides = AdmissionOverridesPolicy.Merge(
            sessionInfo.AdmissionOverrides,
            request.AdmissionOverrides
        );

        if (request.HttpCommandEnabled is { } httpCommandEnabled)
        {
            sessionInfo.HttpCommandEnabled = httpCommandEnabled;
        }

        if (request.HttpCommandPort is { } httpCommandPort)
        {
            sessionInfo.HttpCommandPort = httpCommandPort;
        }

        if (request.HttpCommandAccessToken is not null)
        {
            sessionInfo.HttpCommandAccessToken = request.HttpCommandAccessToken;
        }
    }

    public void DeleteSession(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return;
        }

        try
        {
            if (!Storage.FileExists(SessionsPath))
            {
                return;
            }

            var root = LoadSessionRoot();
            var existing = FindSessionElement(root, normalizedSessionId);
            if (existing == null)
            {
                return;
            }

            existing.Remove();
            using var stream = Storage.OpenFile(SessionsPath, OpenFileMode.Create);
            root.Save(stream);
        }
        catch (AdmissionConfigurationException) { throw; }
        catch (Exception error) when (error is System.Xml.XmlException or IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Startup session configuration is unreadable or malformed; refusing fallback.",
                error
            );
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to delete session \"{normalizedSessionId}\": {ex.Message}");
        }
    }

    private string ResolveActiveSessionId(
        RunningSetting settings,
        StartupRequest request
    )
    {
        if (request.HasExplicitSession)
        {
            return ResolveSessionIdForName(request.SessionName);
        }

        if (!string.IsNullOrWhiteSpace(settings.PendingSessionId))
        {
            return settings.PendingSessionId;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultSessionId))
        {
            return settings.DefaultSessionId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Storage.GetDirectoryName(path);
        var separator = path.IndexOf(':');
        if (directory.Length == 0 && separator >= 0)
        {
            directory = path[..(separator + 1)];
        }

        if (!Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }
    }
}
