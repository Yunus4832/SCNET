using System.Collections.Concurrent;

using EntitySystem.Core;

using Game.Application;
using Game.Content;
using Game.FileStorage;
using Game.Hosting;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Transport;

namespace Game.Managers;

public static class GameManager
{
    private static bool _leavingGameplay;

    private static double _lastAutosaveTime;

    private static readonly ConcurrentDictionary<Guid, (long Generation, bool ShowDialog)> _saveNotifications = new();

    public static Project? ClientProject => Runtime?.Client?.Project;

    public static Project? AuthorityProject => Runtime?.Authority?.Project;

    private static long _runtimeGeneration;

    internal static WorldProjectActivity WorldActivity { get; } = new();

    private static readonly GameWorldController _worlds = new(WorldActivity);

    public static GameRuntime? Runtime => _worlds.Runtime;

    public static ComponentCharacter? LocalCharacter =>
        Runtime?.Client?.Project?.FindSubsystem<SubsystemCharacters>()?.LocalCharacter;

    public static WorldInfo? WorldInfo => _worlds.WorldInfo;

    /// <summary>The startup session selecting this world, independent of runtime world ownership.</summary>
    public static SessionInfo? WorldSession => _worlds.WorldSession;

    public static LocalGameReturnTarget? LocalPlayerReturnTarget
    {
        get => _worlds.LocalPlayerReturnTarget;
        internal set => _worlds.LocalPlayerReturnTarget = value;
    }

    static GameManager()
    {
        _worlds.AuthorityLoaded += BindWorldLifecycle;
    }

    private static void OnNetworkStopped(
        NetNode node,
        WorldRuntimeContext stoppedRuntime,
        NodeRole previousRole,
        NodeStopReason reason
    )
    {
        if (Runtime is { IsDisposed: true } or { IsChangingWorlds: true }) { return; }

        if (Runtime is { } gameRuntime)
        {
            if (!ReferenceEquals(gameRuntime.Authority, stoppedRuntime) &&
                !ReferenceEquals(gameRuntime.Client, stoppedRuntime)) { return; }
        }
        else if (!ReferenceEquals(node, CommonLib.Net)) { return; }

        if (Window.IsCreated)
        {
            LeaveGameplay(previousRole, Network.Presentation.DisconnectReasonText.Format(reason));
        }
    }

    public static void LoadProject(WorldInfo worldInfo, ContainerWidget gamesWidget, SessionInfo? startupSession = null)
    {
        var session = startupSession ?? SessionInfoManager.CreateWorldSession(worldInfo);
        var settings = Configuration.HostingConfiguration.Preferences;
        var mods = CurrentModRuntime.Value!;
        Network.Handling.ApplicationMessageHandlers.InstallWorldHandlers(CommonLib.Net, mods);
        _runtimeGeneration++;
        _worlds.LoadAuthority(
            new WorldLoadRequest(
                worldInfo,
                session,
                CommonLib.Net,
                mods,
                DatabaseManager.GameDatabase,
                WorldProjectPolicies.CaptureAuthority(settings),
                new BlockTextureStore(GamePaths.BlockTextures),
                session.Target == SessionTarget.World
            )
        );
        _lastAutosaveTime = Time.RealTime;
        GC.Collect();
    }

    public static void LoadClientProject(
        WorldRuntimeContext runtime,
        byte[] messagePackData,
        ContainerWidget gamesWidget,
        BlockIndexMapping mapping
    )
    {
        if (runtime.IsAuthority || runtime.IsClosed || runtime.HasProjectResources)
        {
            throw new InvalidOperationException("A bootstrap requires an unused client runtime.");
        }

        _worlds.AttachClient(runtime);
        var gameRuntime = _worlds.RequireRuntime();
        if (!ReferenceEquals(gameRuntime.Client, runtime)) { gameRuntime.ReleaseClient(); }

        try
        {
            gameRuntime.Attach(runtime);
            var project = WorldProjectFactory.CreateClient(
                runtime,
                CurrentModRuntime.Value!,
                DatabaseManager.GameDatabase,
                messagePackData,
                gamesWidget,
                mapping,
                runtime.HasPresentation ? ClientApplication.Content : null,
                WorldInfo,
                Runtime?.Authority,
                (WorldSession ?? StartupManager.Current.Session).AutoJoinCharacterName,
                gameRuntime.IsMultiplayer
            );
            if (ClientApplication.World is { } presentation)
            {
                presentation.SetMultiplayer(gameRuntime.IsMultiplayer);
                presentation.SaveRequested += request => SaveProject(
                    request.WaitForCompletion,
                    request.ShowErrorDialog
                );
                presentation.LeaveRequested += () => LeaveGameplay();
            }

            _lastAutosaveTime = Time.RealTime;
            SetupNetworkHandlers(project);

            Log.Information("加载NetProject");
            GC.Collect();
        }
        catch
        {
            gameRuntime.ReleaseClient();
            throw;
        }
    }

    public static void SaveProject(bool waitForCompletion, bool showErrorDialog)
    {
        var project = Runtime?.Authority?.BoundProject;
        if (project is null || project.IsDisposed) { return; }

        var id = Guid.NewGuid();
        _saveNotifications[id] = (_runtimeGeneration, showErrorDialog);
        try { WorldProjectPersistence.For(project).Save(waitForCompletion, id); }
        catch (Exception error)
        {
            if (_saveNotifications.TryRemove(id, out var notification))
            {
                Log.Error($"Could not start world save: {error}");
                ReportSaveFailure(
                    project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.Name,
                    notification.Generation,
                    notification.ShowDialog,
                    error
                );
            }

            if (waitForCompletion) { throw; }
        }
    }

    private static void BindWorldLifecycle(Project project)
    {
        var generation = _runtimeGeneration;
        var lifecycle = project.FindSubsystem<SubsystemWorldLifecycle>(true)!;
        lifecycle.Persistence.SaveCompleted += outcome =>
        {
            var notification = _saveNotifications.TryRemove(outcome.OperationId, out var requested)
                ? requested
                : (Generation: generation, ShowDialog: true);
            if (outcome.Failure is not null)
            {
                ReportSaveFailure(outcome.WorldName, notification.Generation, notification.ShowDialog, outcome.Failure);
            }
        };
        lifecycle.Stopped += stopped =>
        {
            if (!ReferenceEquals(Runtime?.Authority, stopped.World)) { return; }

            LeaveGameplay(NodeRole.Authority, null);
        };
    }

    private static void ReportSaveFailure(string worldName, long generation, bool showDialog, Exception exception)
    {
        if (!showDialog || !Window.IsCreated) { return; }

        Dispatcher.Dispatch(() =>
            {
                if (Window.IsCreated && _runtimeGeneration == generation)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            string.Format(
                                Localization.ClientLanguage.Catalog.Get("GameManager", "SaveFailedTitle"),
                                worldName
                            ),
                            string.Format(
                                Localization.ClientLanguage.Catalog.Get("GameManager", "SaveFailedMessage"),
                                exception.Message
                            ),
                            Localization.ClientLanguage.Catalog.Ok
                        )
                    );
                }
            }
        );
    }

    public static void UpdateRuntime()
    {
        if (Runtime is { } messageRuntime) { messageRuntime.UpdateMessages(); }
        else { CommonLib.Net.Update(); }

        if (Runtime is { } gameRuntime)
        {
            var local = gameRuntime.Client?.Project?.FindSubsystem<SubsystemCharacters>()?.LocalCharacter;
            var gui = local?.Entity.FindComponent<ComponentCharacterPresentation>(true)?.GuiWidget;
            var menu = gui != null &&
                       DialogsManager.ReadOnlyDialogs.Any(dialog =>
                           dialog is GameMenuDialog && ReferenceEquals(dialog.ParentWidget, gui)
                       );
            gameRuntime.SetLocalPause(menu);
            gameRuntime.UpdateWorlds();
        }

        if (Runtime?.Authority?.Project is { IsDisposed: false } && Time.RealTime - _lastAutosaveTime > 120)
        {
            _lastAutosaveTime = Time.RealTime;
            SaveProject(false, true);
        }
    }

    /// <summary>Ends GUI gameplay and returns according to the current player's runtime entry flow.</summary>
    public static void LeaveGameplay(string? reason = null) => LeaveGameplay(CommonLib.Net.Role, reason);

    private static void LeaveGameplay(NodeRole previousRole, string? reason)
    {
        if (_leavingGameplay) { return; }

        _leavingGameplay = true;
        try
        {
            if (LocalPlayerReturnTarget == LocalGameReturnTarget.HostedWorld &&
                CommonLib.Net.IsServer &&
                Runtime?.Authority is { IsClosed: false, Project: { IsDisposed: false } })
            {
                LeaveLocalPlayer();
                ScreensManager.SwitchScreen("HostedWorld", reason ?? string.Empty);
            }
            else
            {
                var returnToWorldList = LocalPlayerReturnTarget == LocalGameReturnTarget.WorldList;
                CloseWorld();
                ScreensManager.SwitchScreen(!returnToWorldList && previousRole == NodeRole.Client ? "NetPlay" : "Play");
                if (!string.IsNullOrEmpty(reason)) { DialogsManager.Alert(reason); }
            }
        }
        finally { _leavingGameplay = false; }
    }

    public static void LeaveLocalPlayer()
    {
        SaveProject(true, true);
        CommonLib.Net.EndAdmission();
        try { Runtime?.ReleaseClient(); }
        finally { ClientApplication.ReleaseWorld(); }

        LocalPlayerReturnTarget = null;
    }

    public static void CloseWorld()
    {
        if (Runtime?.Authority?.BoundProject is not null) { SaveProject(true, true); }

        if (Runtime is null) { CommonLib.Net.StopImmediate(); }

        DisposeProject();
    }

    internal static void WaitForPendingSave() => WorldProjectPersistence.WaitForPendingSaves();

    public static void DisposeProject()
    {
        WaitForPendingSave();
        try { _worlds.Close(); }
        finally { ClientApplication.ReleaseWorld(); }

        GC.Collect();
    }

    private static void SetupNetworkHandlers(Project project)
    {
        var runtime = WorldRuntimeContext.For(project);
        var netNode = WorldNetworkBinding.For(runtime).RequireNode();

        netNode.TurnOnPackageHandle(project);

        if (runtime.IsAuthority && netNode.IsServer)
        {
            AuthorityWorldNetworkSetup.Attach(project, netNode, new BlockTextureStore(GamePaths.BlockTextures));
            runtime.OwnWorldResource(new WorldTimeReplication(project));
        }

        if (!runtime.IsAuthority)
        {
            SetupClientNetworkHandlers(project, netNode);
        }
    }

    internal static void ObserveEndpoint(NetNode node)
    {
        node.Stopped -= OnNetworkStopped;
        node.Stopped += OnNetworkStopped;
    }

    private static void SetupClientNetworkHandlers(Project project, NetNode net)
    {
        net.Connections.Authority!.ConnectionPhase = ConnectionPhase.BootstrapApplied;
        net.QueuePackage(
            new ConnectionPhaseAckPackage(
                net.Connections.Authority!.ConnectionEpoch,
                ConnectionPhase.BootstrapApplied
            ),
            PackageAudience.To(net.Server!)
        );
        net.OnClientStateChanged += c =>
        {
            if (c.State != ClientState.NotConnected)
            {
                return;
            }

            var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
            var characterData = subsystemCharacters.Characters.Find(pd => pd.CharacterId == c.CharacterId);
            if (characterData is { IsLocalCharacter: false })
            {
                subsystemCharacters.MakeCharacterOffline(characterData.CharacterId);
            }
        };
    }
}
