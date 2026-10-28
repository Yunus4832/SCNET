using System.Text;
using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media.Images;
using Engine.Network;

using Game.Authentication;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Servers;

using static Game.Network.NetNode;

namespace Game.Screens;

public class GameLoadingScreen : Screen
{
    private const string _typeName = nameof(GameLoadingScreen);

    private readonly StringBuilder _builder = new();

    private ClientAdmissionUi? _admissionUi;

    private TemporaryJoinAuthorization? _temporaryAuthorization;

    private SessionInfo? _worldSession;

    private LocalGameReturnTarget? _localReturnTarget;

    private bool _isAdventureRestart;

    private bool _isServerReply;

    private bool _isWorldSnapshotApplied;

    private byte[] _projectXmlData = [];

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
                    SetSpawnDialogMessage("连接服务器", 0.2f);
                    if (_serverPreparation != null)
                    {
                        CommonLib.Net.ConnectServer(_serverPreparation);
                        DialogsManager.ShowDialog(this, SpawnDialog);
                        _stateMachine.TransitionTo("WaitServerReply");
                        // 客户端开启包处理
                        CommonLib.Net.OnReceive += Handle;
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
            delegate { _timer = Stopwatch.StartNew(); },
            delegate
            {
                SetSpawnDialogMessage(_admissionUi?.Status is { Length: > 0 } status ? status : "等待服务器响应", 0.25f);
                if (_admissionUi?.IsWaitingForDetachedAuthentication == true) { return; }

                if (CommonLib.Net.CurrentStage == Stage.Bootstrapping)
                {
                    if (CommonLib.Net.AdmissionDeadline is { } deadline
                            ? DateTimeOffset.UtcNow >= deadline
                            : _timer.ElapsedMilliseconds >= CommonLib.DisconnectTimeout)
                    {
                        CommonLib.Net.Stop("连接超时");
                    }
                }
                else if (CommonLib.Net.CurrentStage == Stage.Connected)
                {
                    _stateMachine.TransitionTo("Loading");
                }
                else
                {
                    CommonLib.Net.Stop("连接断开");
                }
            },
            delegate
            {
                if (CommonLib.Net.CurrentStage == Stage.Connected)
                {
                    NetworkSender.SendToServer(
                        new ClientStateChangedPackage
                            { ClientId = CommonLib.Net.Self!.ID, State = ClientState.Connected }
                    );
                }
            }
        );

        _stateMachine.AddState(
            "WaitLocalAdmission",
            Actions.Empty,
            () =>
            {
                SetSpawnDialogMessage(_admissionUi?.Status is { Length: > 0 } status ? status : "认证", 0.25f);
                if (CommonLib.Net.IsClosing)
                {
                    AbortLoading();
                    return;
                }

                if (CommonLib.Net.LocalAdmissionError is { } error)
                {
                    ReturnFromLocalWorld(error);
                    return;
                }

                if (CommonLib.Net.AdmissionAccepted)
                {
                    ScreensManager.SwitchScreen("Game");
                }
            },
            Actions.Empty
        );

        _stateMachine.AddState(
            "Loading",
            Actions.Empty,
            delegate
            {
                if (_serverPreparation != null)
                {
                    SetSpawnDialogMessage("等待Project和材质", 0.4f);
                    if (_isServerReply)
                    {
                        _stateMachine.TransitionTo("LoadServerReply");
                    }

                    if (!CommonLib.Net.IsConnected)
                    {
                        CommonLib.Net.Stop("与服务器连接断开");
                    }
                }
                else
                {
                    var gamesWidget = ScreensManager.FindScreen<GameScreen>("Game", true)!.Children
                        .Find<ContainerWidget>("GamesWidget")!;
                    GameManager.LoadProject(_worldInfo, gamesWidget, _worldSession);
                    GameManager.LocalPlayerReturnTarget = _localReturnTarget;
                    if (_worldInfo.WorldSettings.RunServer)
                    {
                        var saved = WorldsManager.ReadAdmissionPolicy(_worldInfo.DirectoryName);
                        var hostPolicy = _worldSession is { } session
                            ? ServerAdmissionManager.Resolve(session.AdmissionOverrides, saved)
                            : saved;
                        var started = _worldSession is { } hostSession
                            ? CommonLib.StartServer(hostSession, _worldInfo.WorldSettings, hostPolicy)
                            : CommonLib.StartServer(_worldInfo.WorldSettings, hostPolicy);
                        if (!started) { throw new InvalidOperationException(CommonLib.Net.Error); }

                        if (_worldSession?.Target == SessionTarget.HostedWorld && !_isAdventureRestart)
                        {
                            ScreensManager.SwitchScreen("HostedWorld");
                        }
                        else { BeginLocalPlayerJoin(); }

                        return;
                    }

                    CommonLib.Net.JoinSinglePlayerWorld();

                    ScreensManager.SwitchScreen("Game");
                }
            },
            delegate { _isServerReply = false; }
        );

        _stateMachine.AddState(
            "LoadServerReply",
            Actions.Empty,
            delegate
            {
                SetSpawnDialogMessage("加载服务器Project", 0.6f);
                if (CommonLib.Net.CurrentStage != Stage.Connected)
                {
                    return;
                }

                GameManager.LoadProject(
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
                SetSpawnDialogMessage("应用初始世界快照", 0.8f);
                if (_isWorldSnapshotApplied &&
                    GameManager.Project?.FindSubsystem<SubsystemCharacters>()?.Characters
                        .Any(player => player.IsLocalCharacter) ==
                    true)
                {
                    ScreensManager.SwitchScreen("Game");
                }
                else if (_timer.ElapsedMilliseconds >= CommonLib.DisconnectTimeout)
                {
                    CommonLib.Net.Stop("初始世界快照超时");
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
                var runServer = _worldInfo.WorldSettings.RunServer;
                WorldsManager.RestoreWorldFromSnapshot(_worldInfo.DirectoryName, _worldSnapshotName);
                _worldInfo = WorldsManager.GetWorldInfo(_worldInfo.DirectoryName) ??
                             throw new InvalidDataException("The restored world cannot be read.");
                _worldInfo.WorldSettings.RunServer = runServer;
                BeginLocalWorldLoad();
            },
            Actions.Empty
        );
    }

    private void BeginLocalWorldLoad()
    {
        if (_worldInfo.WorldSettings.RunServer) { CommonLib.Net.PrepareServerWorld(); }
        else { CommonLib.Net.PrepareSinglePlayerWorld(); }

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

    private static void Handle(NetNode node, IEnumerable<ReceivedPackage> packages)
    {
        foreach (var received in packages)
        {
            var package = received.Package;
            if (package is AdmissionChallengePackage or AdmissionResultPackage or ConnectionRejectPackage)
            {
                PackageDispatcher.Handle(received);
            }
            else if (package is ClientPackage clientPackage)
            {
                try
                {
                    PackageDispatcher.Handle(clientPackage, received.Context);
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
                    PackageDispatcher.Handle(bootstrapPackage, received.Context);
                }
                catch (Exception e)
                {
                    Log.Error($"[{package.GetType().Name}]{e.Message}");
                }
            }
            else // 无法处理的包在Project加载后进行处理
            {
                CommonLib.Net.AddPendingHandlePackage(received);
            }
        }
    }

    public void ReplyCall(bool hasTexture, byte[] textureData, byte[] projectData, BlockIndexMapping blockMapping)
    {
        CommonLib.Net.OnReceive -= Handle;
        _isServerReply = true;
        if (hasTexture)
        {
            if (CommonLib.BlockTexture != null)
            {
                CommonLib.BlockTexture.Dispose();
            }

            CommonLib.BlockTexture = Texture2D.Load(Image.Load(new MemoryStream(textureData)));
        }

        _projectXmlData = projectData;
        _blockMapping = blockMapping;
    }

    public void WorldSnapshotApplied()
    {
        _isWorldSnapshotApplied = true;
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

            GameManager.UpdateProject();
            _stateMachine.Update();
        }
        catch (Exception e)
        {
            Log.Error(e);
            AbortLoading();
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 1),
                    ExceptionManager.MakeFullErrorMessage(e),
                    LanguageManager.Get("Usual", "ok")
                )
            );
        }
    }

    private void OnLocalPlayerRemoved(string reason)
    {
        if (ScreensManager.CurrentScreen == this) { ReturnFromLocalWorld(reason); }
    }

    private static void ReturnFromLocalWorld(string? error = null)
    {
        if (GameManager.LocalPlayerReturnTarget == LocalGameReturnTarget.HostedWorld && CommonLib.Net.IsServer)
        {
            GameManager.LeaveLocalPlayer();
            ScreensManager.SwitchScreen("HostedWorld", error ?? string.Empty);
        }
        else
        {
            GameManager.CloseWorld();
            ScreensManager.SwitchScreen("Play");
            if (!string.IsNullOrEmpty(error)) { DialogsManager.Alert(error); }
        }
    }

    private void AbortLoading()
    {
        var previous = ScreensManager.PreviousScreen;
        if (_serverPreparation is null &&
            CommonLib.Net.IsServer &&
            !CommonLib.Net.IsClosing &&
            CommonLib.Net.IsTransportRunning &&
            GameManager.Project is not null)
        {
            ReturnFromLocalWorld();
            return;
        }

        if (_serverPreparation is null && GameManager.Project is not null) { GameManager.SaveProject(true, true); }

        CommonLib.Net.StopImmediate();
        GameManager.DisposeProject();
        if (previous is null or LoadingScreen or GameLoadingScreen or GameScreen)
        {
            ScreensManager.SwitchScreen(_serverPreparation is null ? "Play" : "NetPlay");
        }
        else { ScreensManager.SwitchScreen(previous); }
    }

    public override void Leave()
    {
        CommonLib.Net.LocalPlayerRemoved -= OnLocalPlayerRemoved;
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

    public override void Enter(object[] parameters)
    {
        _temporaryAuthorization = null;
        _admissionUi?.Dispose();
        _admissionUi = null;
        _isServerReply = false;
        _isWorldSnapshotApplied = false;
        _worldSession = null;
        if (parameters.Length >= 3 && parameters[2] is ServerConnectionPreparation prepared)
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
                if (CommonLib.Net.IsServer) { _worldInfo.WorldSettings.RunServer = true; }
            }

            if (_worldSession.Target == SessionTarget.HostedWorld) { _worldInfo.WorldSettings.RunServer = true; }
        }

        _stateMachine.TransitionTo("WaitingForFadeIn");
        ProgressManager.UpdateProgress("Loading World", 0f);
        CommonLib.Net.LocalPlayerRemoved += OnLocalPlayerRemoved;
    }
}
