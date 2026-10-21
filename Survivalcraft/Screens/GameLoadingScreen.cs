using System.Text;
using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;
using Engine.Network;

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
                        _stateMachine.TransitionTo("Loading");
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
                SetSpawnDialogMessage("等待服务器响应", 0.25f);
                //Fix me::客户端中断连接口再次连接会卡住
                if (CommonLib.Net.CurrentStage == Stage.Bootstrapping)
                {
                    if (_timer.ElapsedMilliseconds >= CommonLib.DisconnectTimeout)
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
                    NetworkSender.SendToServer(new ClientPackage(CommonLib.Net.Self!.ID, ClientState.Connected));
                }
            }
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
                    GameManager.LoadProject(_worldInfo, gamesWidget);
                    if (_isAdventureRestart && CommonLib.WorkType == WorkType.Client)
                    {
                        NetworkSender.SendToServer(
                            PlayerPackageBuilder.Capture(
                                CommonLib.MainPlayer!,
                                ComponentPlayerPackage.PlayerAction.IntoPlaying
                            )
                        );
                    }

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
                    GameManager.Project?.FindSubsystem<SubsystemPlayers>()?.PlayersData
                        .Any(player => player.IsMainPlayer) ==
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
                GameManager.DisposeProject();
                WorldsManager.RestoreWorldFromSnapshot(_worldInfo.DirectoryName, _worldSnapshotName);
                _stateMachine.TransitionTo("Loading");
            },
            Actions.Empty
        );
    }

    private static void Handle(NetNode node, IEnumerable<ReceivedPackage> packages)
    {
        foreach (var received in packages)
        {
            var package = received.Package;
            if (package is ClientPackage clientPackage)
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
            AbortLoading();
            return;
        }

        try
        {
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

    private static void AbortLoading()
    {
        var previous = ScreensManager.PreviousScreen;
        CommonLib.Net.StopImmediate();
        GameManager.DisposeProject();
        ScreensManager.SwitchScreen(previous);
    }

    public override void Leave()
    {
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
        _isServerReply = false;
        _isWorldSnapshotApplied = false;
        if (CommonLib.WorkType == WorkType.Local)
        {
            CommonLib.Net.StartLocal();
        }

        if (parameters.Length < 3)
        {
            _worldInfo = (WorldInfo)parameters[0];
            _worldSnapshotName = (string)parameters[1];
            _serverPreparation = null;
            _isAdventureRestart = _worldSnapshotName == "AdventureRestart";
        }
        else
        {
            _worldSnapshotName = string.Empty;
            _serverPreparation = (ServerConnectionPreparation)parameters[2];
        }

        _stateMachine.TransitionTo("WaitingForFadeIn");
        ProgressManager.UpdateProgress("Loading World", 0f);
    }
}
