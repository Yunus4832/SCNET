using System.Net;
using System.Text;
using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

using static Game.Network.NetNode;

namespace Game.Screens;

public class GameLoadingScreen : Screen
{
    private const string _typeName = "GameLoadingScreen";

    private readonly StringBuilder _builder = new();

    public bool ExistBlockTexture;

    private bool _isAdventureRestart;

    private bool _isServerReply;

    private byte[] _blockTextureData = [];

    private string _password = string.Empty;

    private byte[] _projectXmlData = [];

    private IPEndPoint? _serverEndPoint;

    private readonly StateMachine _stateMachine = new();

    private WorldInfo _worldInfo = null!;

    private string _worldSnapshotName = string.Empty;

    public readonly UnSeasonSpawnDialog SpawnDialog = new();

    private Stopwatch _timer = null!;

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
                    if (_serverEndPoint != null)
                    {
                        CommonLib.Net.ConnectServer(_serverEndPoint, _password);
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
                if (CommonLib.Net.CurrentStage == Stage.WaitForClientList)
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
                    CommonLib.Net.QueuePackage(new ClientPackage(CommonLib.Net.Self!.ID, ClientState.Connected));
                }
            });

        _stateMachine.AddState(
            "Loading",
            Actions.Empty,
            delegate
            {
                if (_serverEndPoint != null)
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
                        CommonLib.Net.QueuePackage(new ComponentPlayerPackage(CommonLib.MainPlayer!,
                            ComponentPlayerPackage.PlayerAction.IntoPlaying));
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

                GameManager.LoadProject(_projectXmlData,
                    ScreensManager.FindScreen<GameScreen>("Game", true)!.Children
                        .Find<ContainerWidget>("GamesWidget")!);
                ScreensManager.SwitchScreen("Game");
                _projectXmlData = [];
                _blockTextureData = [];
                _isServerReply = false;
                ExistBlockTexture = false;
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

    private static void Handle(NetNode node, IEnumerable<IPackage> packages)
    {
        foreach (var package in packages)
        {
            if (package is ClientPackage clientPackage)
            {
                try
                {
                    PackageDispatcher.Handle(clientPackage, node, false);
                }
                catch (Exception e)
                {
                    Log.Error($"[{package.GetType().Name}]{e.Message}");
                }
            }
            else if (package is ProjectPackage projectPackage)
            {
                try
                {
                    PackageDispatcher.Handle(projectPackage, node, false);
                }
                catch (Exception e)
                {
                    Log.Error($"[{package.GetType().Name}]{e.Message}");
                }
            }
            else // 无法处理的包在Project加载后进行处理
            {
                CommonLib.Net.AddPendingHandlePackage(package);
            }
        }
    }

    public void ReplyCall(bool hasTexture, byte[] textureData, byte[] projectData)
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
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }

        try
        {
            GameManager.UpdateProject();
            _stateMachine.Update();
        }
        catch (Exception e)
        {
            Log.Error(e);
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageControl.Get(_typeName, 1),
                    ExceptionManager.MakeFullErrorMessage(e),
                    LanguageControl.Get("Usual", "ok")
                )
            );
        }
    }

    public override void Leave()
    {
        DialogsManager.HideDialog(SpawnDialog);
    }

    public override void Enter(object[] parameters)
    {
        if (CommonLib.WorkType == WorkType.Local)
        {
            CommonLib.Net.StartLocal();
        }

        if (parameters.Length < 3)
        {
            _worldInfo = (WorldInfo)parameters[0];
            _worldSnapshotName = (string)parameters[1];
            _serverEndPoint = null;
            _isAdventureRestart = _worldSnapshotName == "AdventureRestart";
        }
        else
        {
            _worldSnapshotName = string.Empty;
            _serverEndPoint = (IPEndPoint)parameters[2];
            if (parameters.Length == 4)
            {
                _password = (string)parameters[3];
            }
        }

        _stateMachine.TransitionTo("WaitingForFadeIn");
        ProgressManager.UpdateProgress("Loading World", 0f);
    }
}
