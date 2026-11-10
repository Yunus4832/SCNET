using System.Text;
using System.Xml.Linq;

using Engine.Network;

using Game.Application;
using Game.Authentication;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Transport;
using Game.Servers;

using static Game.Network.NetNode;

namespace Game.Screens;

public class GameLoadingScreen : Screen
{
    private const string _typeName = nameof(GameLoadingScreen);

    private const double _characterSpawnTimeoutSeconds =
        2 * CharacterData.SpawnPreparationTimeoutSeconds +
        NetworkTransportLimits.DisconnectTimeoutMilliseconds / 1000.0;

    private readonly StringBuilder _builder = new();

    private ClientAdmissionUi? _admissionUi;

    private TemporaryJoinAuthorization? _temporaryAuthorization;

    private SessionInfo? _worldSession;

    private LocalGameReturnTarget? _localReturnTarget;

    private bool _isAdventureRestart;

    private bool _isServerReply;

    private bool _waitingForTransport;

    private bool _isWorldSnapshotApplied;

    private bool _resumingCharacterPreparation;

    private byte[] _projectXmlData = [];

    private WorldRuntimeContext? _replyRuntime;

    private NetNode? _clientNode;

    private BlockIndexMapping _blockMapping = BlockIndexMapping.Empty;

    private ServerConnectionPreparation? _serverPreparation;

    private readonly StateMachine _stateMachine = new();

    private WorldInfo _worldInfo = null!;

    private string _worldSnapshotName = string.Empty;

    public readonly UnSeasonSpawnDialog SpawnDialog = new();

    private Stopwatch _timer = null!;

    public WorldInfo? CurrentWorldInfo => _serverPreparation == null ? _worldInfo : null;

    public SessionInfo? CurrentWorldSession => _serverPreparation is null ? _worldSession : null;

    public NetworkTarget? CurrentRemoteTarget => _serverPreparation?.Target;

    public GameLoadingScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/GameLoadingScreen");
        LoadContents(this, node);
        _stateMachine.AddState(
            "WaitingForFadeIn",
            Actions.Empty,
            delegate
            {
                if (ScreensManager.IsAnimating)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_worldSnapshotName))
                {
                    SetSpawnDialogMessage(
                        Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "Connecting"),
                        0.2f
                    );
                    if (_clientNode != null)
                    {
                        _clientNode.OnReceive += Handle;
                        DialogsManager.ShowDialog(this, SpawnDialog);
                        _stateMachine.TransitionTo("WaitServerReply");
                    }
                    else if (_serverPreparation != null)
                    {
                        _clientNode = CommonLib.Net;
                        _clientNode.ConnectServer(
                            _serverPreparation,
                            new ClientConnectionOptions(
                                CurrentModRuntime.Value?.ModDataHash ?? GameProtocol.EmptyModDataHash
                            )
                        );
                        DialogsManager.ShowDialog(this, SpawnDialog);
                        _stateMachine.TransitionTo("WaitServerReply");
                        // 客户端开启包处理
                        _clientNode.OnReceive += Handle;
                    }
                    else
                    {
                        BeginLocalWorldLoad();
                    }
                }
                else
                {
                    _stateMachine.TransitionTo("RestoringSnapshot");
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "WaitServerReply",
            delegate
            {
                _timer = Stopwatch.StartNew();
                _waitingForTransport = false;
            },
            delegate
            {
                SetSpawnDialogMessage(
                    _admissionUi?.Status is { Length: > 0 } status
                        ? status
                        : Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "WaitingForServer"),
                    0.25f
                );
                if (_admissionUi?.IsWaitingForDetachedAuthentication == true) { return; }

                if (_clientNode!.CurrentStage == Stage.Bootstrapping)
                {
                    if (HasServerReplyTimedOut(_timer.ElapsedMilliseconds))
                    {
                        _clientNode!.Stop(
                            Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "ConnectionTimedOut")
                        );
                    }
                }
                else if (_clientNode!.CurrentStage == Stage.Connected)
                {
                    _stateMachine.TransitionTo("Loading");
                }
                else
                {
                    _clientNode!.Stop(Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "Disconnected"));
                }
            },
            delegate
            {
                if (_clientNode!.CurrentStage == Stage.Connected)
                {
                    NetworkSender.SendToServer(
                        _clientNode!.Runtime,
                        ClientPackageBuilder.State(_clientNode!.Self!) with { State = ClientState.Connected }
                    );
                }
            }
        );

        _stateMachine.AddState(
            "WaitLocalAdmission",
            Actions.Empty,
            () =>
            {
                SetSpawnDialogMessage(
                    _admissionUi?.Status is { Length: > 0 } status
                        ? status
                        : Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "Authenticating"),
                    0.25f
                );
                if (CommonLib.Net.IsClosing)
                {
                    AbortLoading();
                    return;
                }

                if (CommonLib.Net.LocalAdmissionError is { } error)
                {
                    GameManager.LeaveGameplay(Localization.ClientLanguage.Resolver.Resolve(error));
                    return;
                }

                if (CommonLib.Net.AdmissionAccepted)
                {
                    var verified = CommonLib.Net.ConsumeLocalAdmission();
                    BeginInProcessClient(verified.Subject, verified.Token);
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "Loading",
            Actions.Empty,
            delegate
            {
                if (_clientNode != null)
                {
                    SetSpawnDialogMessage(
                        Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "WaitingForWorldData"),
                        0.4f
                    );
                    if (_isServerReply)
                    {
                        _stateMachine.TransitionTo("LoadServerReply");
                    }

                    if (!_clientNode.IsConnected)
                    {
                        _clientNode.Stop(
                            Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "ServerDisconnected")
                        );
                    }
                }
                else
                {
                    var gamesWidget = ScreensManager.FindScreen<GameScreen>("Game", true)!.Children
                        .Find<ContainerWidget>("GamesWidget")!;
                    GameManager.LoadProject(_worldInfo, gamesWidget, _worldSession);
                    GameManager.LocalPlayerReturnTarget = _localReturnTarget;
                    if (_worldInfo.Hosting.RunServer)
                    {
                        var saved = ClientApplication.Worlds.ReadAdmissionPolicy(_worldInfo.DirectoryName);
                        var hostPolicy = _worldSession is { } session
                            ? ServerAdmissionManager.Resolve(session.AdmissionOverrides, saved)
                            : saved;
                        var started = _worldSession is { } hostSession
                            ? CommonLib.StartServer(
                                hostSession,
                                _worldInfo.WorldSettings,
                                _worldInfo.Hosting,
                                hostPolicy
                            )
                            : CommonLib.StartServer(_worldInfo.WorldSettings, _worldInfo.Hosting, hostPolicy);
                        if (!started)
                        {
                            throw new InvalidOperationException(
                                Network.Presentation.DisconnectReasonText.Format(CommonLib.Net.StopReason)
                            );
                        }

                        if (_worldSession?.Target == SessionTarget.HostedWorld && !_isAdventureRestart)
                        {
                            ScreensManager.SwitchScreen("HostedWorld");
                        }
                        else { BeginLocalPlayerJoin(); }

                        return;
                    }

                    using var identity = LocalIdentityManager.Current.OpenPlayerIdentity();
                    BeginInProcessClient(
                        new Game.Protocol.Identity.PlayerAddress(identity.PublicIdentity.Address),
                        Guid.NewGuid()
                    );
                }
            },
            delegate { _isServerReply = false; }
        );

        _stateMachine.AddState(
            "LoadServerReply",
            Actions.Empty,
            delegate
            {
                SetSpawnDialogMessage(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "LoadingServerWorld"),
                    0.6f
                );
                var runtime = _replyRuntime ?? throw new InvalidOperationException("Missing bootstrap runtime.");
                if (runtime.IsClosed ||
                    WorldNetworkBinding.For(runtime).RequireNode().CurrentStage != Stage.Connected)
                {
                    return;
                }

                GameManager.LoadClientProject(
                    runtime,
                    _projectXmlData,
                    ScreensManager.FindScreen<GameScreen>("Game", true)!.Children
                        .Find<ContainerWidget>("GamesWidget")!,
                    _blockMapping
                );
                _projectXmlData = [];
                _isServerReply = false;
                _stateMachine.TransitionTo("WaitWorldSnapshot");
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "WaitWorldSnapshot",
            delegate { _timer.Restart(); },
            delegate
            {
                SetSpawnDialogMessage(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "ApplyingSnapshot"),
                    0.8f
                );
                if (_isWorldSnapshotApplied) { _stateMachine.TransitionTo("WaitForCharacter"); }
                else if (_timer.ElapsedMilliseconds >=
                         NetworkTransportLimits.DisconnectTimeoutMilliseconds)
                {
                    _clientNode!.Stop(
                        Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "SnapshotTimedOut")
                    );
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "WaitForCharacter",
            delegate { _timer.Restart(); },
            delegate
            {
                SetSpawnDialogMessage(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "WaitingForCharacter"),
                    0.85f
                );
                if (ClientApplication.World?.Project?.FindSubsystem<SubsystemCharacters>()?.Characters
                        .Any(player => player.IsLocalCharacter) ==
                    true)
                {
                    ClientApplication.World!.Project.FindSubsystem<SubsystemCharacters>(true)!
                        .ClearCharacterCreationRequest();
                    _stateMachine.TransitionTo("PrepareCharacterView");
                }
                else if (_timer.Elapsed.TotalSeconds >= _characterSpawnTimeoutSeconds)
                {
                    _clientNode!.Stop(
                        Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "CharacterTimedOut")
                    );
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "PrepareCharacterView",
            Actions.Empty,
            delegate
            {
                var world = ClientApplication.World;
                var character = world?.Project.FindSubsystem<SubsystemCharacters>()?.Characters
                    .FirstOrDefault(candidate => candidate.IsLocalCharacter);
                if (world is null || character is null) { return; }

                var view = world.Project.FindSubsystem<SubsystemCharacterPresentation>(true)!.Get(character);
                SetSpawnDialogMessage(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(
                        _typeName,
                        character.ComponentCharacter is null ? "WaitingForCharacter" : "PreparingView"
                    ),
                    0.85f + .15f * view.PreparationProgress
                );
                if (view.IsReadyForPlaying)
                {
                    world.CompleteEntry();
                    ScreensManager.SwitchScreen("Game");
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "RestoringSnapshot",
            Actions.Empty,
            delegate
            {
                CommonLib.Net.StopImmediate();
                GameManager.DisposeProject();
                var runServer = _worldInfo.Hosting.RunServer;
                ClientApplication.Worlds.RestoreWorldFromSnapshot(_worldInfo.DirectoryName, _worldSnapshotName);
                _worldInfo = ClientApplication.Worlds.GetWorldInfo(_worldInfo.DirectoryName) ??
                             throw new InvalidDataException("The restored world cannot be read.");
                _worldInfo.Hosting.RunServer = runServer;
                BeginLocalWorldLoad();
            },
            Actions.Empty
        );
    }

    private void BeginLocalWorldLoad()
    {
        CommonLib.Net.PrepareAuthorityWorld();

        _stateMachine.TransitionTo("Loading");
    }

    private void BeginLocalPlayerJoin()
    {
        _admissionUi = new ClientAdmissionUi(
            CommonLib.Net,
            this,
            AuthenticationJoinScope.ForWorld(_worldInfo.DirectoryName),
            _temporaryAuthorization
        );
        _temporaryAuthorization = null;
        DialogsManager.ShowDialog(this, SpawnDialog);
        CommonLib.Net.BeginLocalAdmission();
        _stateMachine.TransitionTo("WaitLocalAdmission");
    }

    private void BeginInProcessClient(Game.Protocol.Identity.PlayerAddress subject, Guid token)
    {
        _clientNode = GameManager.Runtime!.ConnectLocalClient(
            subject,
            token,
            Network.Handling.ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
        );
        GameManager.ObserveEndpoint(_clientNode);
        _replyRuntime = _clientNode.Runtime;
        _clientNode.OnReceive += Handle;
        DialogsManager.ShowDialog(this, SpawnDialog);
        _stateMachine.TransitionTo("WaitServerReply");
    }

    private static void Handle(NetNode node, IEnumerable<ReceivedPackage> packages)
    {
        foreach (var received in packages)
        {
            var package = received.Package;
            if (package is AdmissionChallengePackage or AdmissionResultPackage or ConnectionRejectPackage)
            {
                WorldNetworkBinding.For(received.Context.Runtime).Dispatch(received);
            }
            else if (package is ClientPackage clientPackage)
            {
                try
                {
                    WorldNetworkBinding.For(received.Context.Runtime).Dispatch(clientPackage, received.Context);
                }
                catch (Exception e)
                {
                    Log.Error($"[{package.GetType().Name}]{e.Message}");
                }
            }
            else if (package is BootstrapPackage bootstrapPackage)
            {
                try
                {
                    WorldNetworkBinding.For(received.Context.Runtime).Dispatch(bootstrapPackage, received.Context);
                }
                catch (Exception e)
                {
                    Log.Error($"[{package.GetType().Name}]{e.Message}");
                }
            }
            else // 无法处理的包在Project加载后进行处理
            {
                node.AddPendingHandlePackage(received);
            }
        }
    }

    public void ReplyCall(
        WorldRuntimeContext runtime,
        bool hasTexture,
        byte[] textureData,
        byte[] projectData,
        BlockIndexMapping blockMapping
    )
    {
        WorldNetworkBinding.For(runtime).RequireNode().OnReceive -= Handle;
        _replyRuntime = runtime;
        _isServerReply = true;
        ClientBootstrapContent.For(runtime).BlocksTextureBytes = hasTexture ? textureData.ToArray() : null;

        _projectXmlData = projectData;
        _blockMapping = blockMapping;
    }

    public void WorldSnapshotApplied(WorldRuntimeContext runtime)
    {
        if (ReferenceEquals(runtime, _replyRuntime) && !runtime.IsClosed)
        {
            _isWorldSnapshotApplied = true;
        }
    }

    private void SetSpawnDialogMessage(string msg, float progress)
    {
        _builder.Clear();
        _builder.Append(msg);
        var d = (int)Time.RealTime;
        d %= 4;
        switch (d)
        {
            case 1:
                _builder.Append('.');
                break;
            case 2:
                _builder.Append("..");
                break;
            case 3:
                _builder.Append("...");
                break;
        }

        SpawnDialog.LargeMessage = _builder.ToString();
        SpawnDialog.Progress = progress;
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel)
        {
            Log.Information("Cancelled game loading from back or cancel input.");
            AbortLoading();
            return;
        }

        try
        {
            CommonLib.Net.PumpLocalAdmission();
            _admissionUi?.Update();
            if (_admissionUi?.DetachedFailure is { } failure)
            {
                AbortLoading();
                DialogsManager.Alert(failure);
                return;
            }

            if (_serverPreparation is { } prepared &&
                _admissionUi?.TakeReconnectAuthorization() is { } authorization)
            {
                var target = prepared.Target;
                CommonLib.Net.StopForAdmissionRecovery();
                ScreensManager.SwitchScreen("NetPlay", target, authorization);
                return;
            }

            _stateMachine.Update();
        }
        catch (Exception e)
        {
            Log.Error(e);
            AbortLoading();
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    Localization.ClientLanguage.Catalog.Get(_typeName, 1),
                    ExceptionManager.MakeFullErrorMessage(e),
                    Localization.ClientLanguage.Catalog.Get("Usual", "ok")
                )
            );
        }
    }


    private void AbortLoading()
    {
        var previous = ScreensManager.PreviousScreen;
        if (_serverPreparation is null &&
            CommonLib.Net.IsServer &&
            !CommonLib.Net.IsClosing &&
            CommonLib.Net.IsTransportRunning &&
            GameManager.AuthorityProject is not null)
        {
            ClientApplication.World?.RequestLeave();
            return;
        }

        if (_serverPreparation is null && GameManager.AuthorityProject is not null)
        {
            ClientApplication.World?.RequestSave(true, true);
        }

        CommonLib.Net.StopImmediate();
        GameManager.DisposeProject();
        if (previous is null or LoadingScreen or GameLoadingScreen or GameScreen or CharacterScreen)
        {
            ScreensManager.SwitchScreen(_serverPreparation is null ? "Play" : "NetPlay");
        }
        else { ScreensManager.SwitchScreen(previous); }
    }

    public override void Leave()
    {
        _temporaryAuthorization = null;
        _admissionUi?.Dispose();
        _admissionUi = null;
        DialogsManager.HideDialog(SpawnDialog);
        if (_serverPreparation is not null)
        {
            var prepared = _serverPreparation;
            Task.Run(async () => await prepared.DisposeAsync()).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Log.Warning(
                            $"Could not close loading preparation: {task.Exception!.GetBaseException().Message}"
                        );
                    }
                }
            );
        }
    }

    private bool HasServerReplyTimedOut(long elapsedMilliseconds)
    {
        if (_clientNode!.AdmissionDeadline is { } admissionDeadline && DateTimeOffset.UtcNow >= admissionDeadline)
        {
            return true;
        }

        if (_clientNode.IsTransportConnecting)
        {
            _waitingForTransport = true;
            _timer.Restart();
            return false;
        }

        if (_waitingForTransport)
        {
            _waitingForTransport = false;
            _timer.Restart();
            elapsedMilliseconds = 0;
        }

        return _clientNode.AdmissionDeadline is { } deadline
            ? DateTimeOffset.UtcNow >= deadline
            : elapsedMilliseconds >= NetworkTransportLimits.DisconnectTimeoutMilliseconds;
    }

    public void ShowCharacterCreationFailure(WorldRuntimeContext runtime, CharacterCreationError error)
    {
        if (!ReferenceEquals(runtime, ClientApplication.World?.Runtime) || runtime.IsClosed) { return; }

        var key = error switch
        {
            CharacterCreationError.ExistingCharacter => "CreationExistingCharacter",
            CharacterCreationError.InvalidProfile => "CreationInvalidProfile",
            CharacterCreationError.NameUnavailable => "CreationNameUnavailable",
            _ => "CreationUnavailable"
        };
        ScreensManager.SwitchScreen("Character", CharacterScreen.Mode.Initial, runtime.Project!);
        DialogsManager.Alert(Localization.ClientLanguage.Catalog.GetContentWidgets(nameof(CharacterScreen), key));
    }

    public void ContinueCharacterPreparation(WorldRuntimeContext runtime)
    {
        if (!ReferenceEquals(runtime, ClientApplication.World?.Runtime) || runtime.IsClosed)
        {
            throw new InvalidOperationException("Character preparation requires the active client world.");
        }

        var hasCharacter = ClientApplication.World!.Project.FindSubsystem<SubsystemCharacters>(true)!.Characters
            .Any(character => character.IsLocalCharacter);
        _stateMachine.TransitionTo(hasCharacter ? "PrepareCharacterView" : "WaitForCharacter");
        _timer.Restart();
        if (ReferenceEquals(ScreensManager.CurrentScreen, this)) { return; }

        _resumingCharacterPreparation = true;
        ScreensManager.SwitchScreen(this);
    }

    public override void Enter(object[] parameters)
    {
        if (_resumingCharacterPreparation)
        {
            _resumingCharacterPreparation = false;
            if (ClientApplication.World?.Runtime is not { IsClosed: false })
            {
                AbortLoading();
                return;
            }

            DialogsManager.ShowDialog(this, SpawnDialog);
            return;
        }

        _temporaryAuthorization = null;
        _admissionUi?.Dispose();
        _admissionUi = null;
        _isServerReply = false;
        _replyRuntime = null;
        _clientNode = null;
        _projectXmlData = [];
        _blockMapping = BlockIndexMapping.Empty;
        _isWorldSnapshotApplied = false;
        _worldSession = null;
        if (parameters.FirstOrDefault() is WorldRuntimeContext local)
        {
            if (!ReferenceEquals(GameManager.Runtime?.Client, local) || local.IsClosed)
            {
                throw new InvalidOperationException("The loading view does not own this local client runtime.");
            }

            _clientNode = WorldNetworkBinding.For(local).RequireNode();
            _replyRuntime = local;
            _worldInfo = GameManager.WorldInfo!;
            _worldSession = GameManager.WorldSession;
            _localReturnTarget = GameManager.LocalPlayerReturnTarget;
            _worldSnapshotName = string.Empty;
            _serverPreparation = null;
        }
        else if (parameters.Length >= 3 && parameters[2] is ServerConnectionPreparation prepared)
        {
            GameManager.LocalPlayerReturnTarget = null;
            _worldSnapshotName = string.Empty;
            _serverPreparation = prepared;
            _admissionUi = new ClientAdmissionUi(
                CommonLib.Net,
                this,
                AuthenticationJoinScope.ForRemote(prepared.Target),
                parameters.OfType<TemporaryJoinAuthorization>().FirstOrDefault()
            );
        }
        else
        {
            _worldInfo = (WorldInfo)parameters[0];
            _worldSnapshotName = (string)parameters[1];
            _serverPreparation = null;
            _isAdventureRestart = _worldSnapshotName == "AdventureRestart";
            _worldSession = parameters.OfType<SessionInfo>().FirstOrDefault() ??
                            SessionInfoManager.CreateWorldSession(_worldInfo);
            _localReturnTarget = _worldSession.Target == SessionTarget.World
                ? LocalGameReturnTarget.WorldList
                : null;

            if (_isAdventureRestart)
            {
                _localReturnTarget = GameManager.LocalPlayerReturnTarget;
                _temporaryAuthorization = CommonLib.Net.TemporaryAuthentication;
                _worldSession = GameManager.WorldSession ?? _worldSession;
                if (CommonLib.Net.IsTransportRunning) { _worldInfo.Hosting.RunServer = true; }
            }

            if (_worldSession.Target == SessionTarget.HostedWorld) { _worldInfo.Hosting.RunServer = true; }
        }

        _stateMachine.TransitionTo("WaitingForFadeIn");
        ProgressManager.UpdateProgress("Loading World", 0f);
    }
}
