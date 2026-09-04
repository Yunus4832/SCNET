using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib;

using ThreadState = System.Threading.ThreadState;

namespace Game.Screens;

public class NetPlayScreen : Screen
{
    public enum ConnectState
    {
        Unavailable,
        Checking,
        Available
    }

    private enum FilterType
    {
        Collect,
        Local
    }

    private const string _typeName = nameof(NetPlayScreen);

    public static Dictionary<string, string> IpToDNS = new();

    public static Dictionary<string, string> DNSToName = new();

    public bool LookingForServer;

    private readonly ButtonWidget _addButton;

    private readonly ButtonWidget _collectButton;

    private readonly ButtonWidget _filter0Button;

    private readonly ButtonWidget _filter1Button;

    private FilterType _filterType;

    private bool _isLoadingList;

    private readonly ButtonWidget _refreshButton;

    private float _refreshTime;

    private readonly ButtonWidget _removeButton;

    private readonly LabelWidget _topBarLabel;

    private readonly ListPanelWidget _worldsListWidget;

    public readonly List<Thread> RunningTasks = [];

    public NetPlayScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/NetPlayScreen");
        LoadContents(this, node);
        _worldsListWidget = Children.Find<ListPanelWidget>("WorldsList")!;

        _filter0Button = Children.Find<ButtonWidget>("TabPage")!;
        _filter0Button.Text = LanguageManager.Get("NetPlayScreen", 3);
        _filter0Button.Size = new Vector2(180, 60);
        _filter1Button = new BevelledButtonWidget
        { Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60") };
        _filter1Button.Text = LanguageManager.Get("NetPlayScreen", 4);
        _filter1Button.Size = new Vector2(180, 60);
        _filter0Button.ParentWidget?.AddChildren(_filter1Button);
        _addButton = Children.Find<ButtonWidget>("Play")!;
        _addButton.Text = LanguageManager.Get("NetPlayScreen", 7);
        _addButton.Size = new Vector2(220, 60);
        _removeButton = Children.Find<ButtonWidget>("NewWorld")!;
        _removeButton.Text = LanguageManager.Get("NetPlayScreen", 8);
        _removeButton.Size = new Vector2(220, 60);

        _collectButton = new BevelledButtonWidget
        { Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60") };
        _collectButton.Text = LanguageManager.Get("NetPlayScreen", 9);
        _collectButton.Size = new Vector2(160, 60);
        _addButton.ParentWidget?.AddChildren(_collectButton);

        _refreshButton = new BevelledButtonWidget
        {
            Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60")
        };

        _refreshButton.Text = LanguageManager.Get("NetPlayScreen", 10);
        _refreshButton.Size = new Vector2(160, 60);

        _addButton.ParentWidget?.RemoveChildren(Children.Find<ButtonWidget>("Properties")!);
        _addButton.ParentWidget?.AddChildren(_refreshButton);
        _topBarLabel = Children.Find<LabelWidget>("TopBar.Label")!;

        _worldsListWidget.ItemWidgetFactory += obj =>
        {
            var connect = (Connect)obj;
            var stackPanelWidget = new StackPanelWidget { Direction = LayoutDirection.Vertical };
            var labelWidget = new LabelWidget { Name = "line1" };
            var labelWidget2 = new LabelWidget { Name = "line2" };


            var version = connect.Version;
            var players = $" | {LanguageManager.Get("NetPlayScreen", 13)}: {connect.PlayerCount}/{connect.MaxCount}";
            var gameMode =
                $" | {LanguageManager.Get("NetPlayScreen", 14)}: " +
                $"{LanguageManager.Get("GameMode", connect.GameMode.ToString())}";
            var timeOfDay =
                $" | {LanguageManager.Get("NetPlayScreen", 20)}: " +
                $"{SubsystemTimeOfDay.GetTimeOfDayText(connect.TimeOfDay)}";
            var season =
                $" | {LanguageManager.Get("NetPlayScreen", 19)}: " +
                $"{GetSeasonText(connect.Season, connect.TimeOfSeason)}";
            var validTime = !string.IsNullOrEmpty(connect.ValidTime)
                ? $" | {LanguageManager.Get("NetPlayScreen", 18)}: {connect.ValidTime}"
                : string.Empty;


            switch (connect.State)
            {
                case ConnectState.Available:
                    {
                        labelWidget.Text = $"{connect} ({connect.UsedTime / 2:0} ms)";
                        labelWidget.Color = Color.LightGreen;
                        labelWidget2.Text = $"{version}{players}{gameMode}{timeOfDay}{season}{validTime}";

                        break;
                    }
                case ConnectState.Checking:
                    {
                        labelWidget.Text = $"{connect} {LanguageManager.Get("NetPlayScreen", 17)}";
                        labelWidget.Color = Color.White;
                        break;
                    }
                case ConnectState.Unavailable:
                    {
                        labelWidget.Text = $"{connect} {LanguageManager.Get("NetPlayScreen", 15)}";
                        labelWidget.Color = Color.LightRed;
                        break;
                    }
            }

            stackPanelWidget.Children.Add(labelWidget);
            stackPanelWidget.Children.Add(labelWidget2);
            return stackPanelWidget;
        };

        _worldsListWidget.ScrollPosition = 0f;
        _worldsListWidget.ScrollSpeed = 0f;
        _worldsListWidget.ItemClicked += OnItemClick;

        GameEntry.HandleUri += uri =>
        {
            if (uri.Uri.Host != "online")
            {
                return;
            }

            var ip = uri.Uri.AbsolutePath[1..];
            var connect = new Connect();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                connect.IP = ip;
                connect.Name = "scheme_" + DateTime.Now.Ticks;

                Time.QueueTimeDelayedExecution(Time.RealTime + 1, () =>
                {
                    Log.Information($"连接到服务器:{connect.IP}");
                    ConnectTo(connect);
                });
            }
            else
            {
                DialogsManager.Alert("提示", "不能识别的连接");
            }

            uri.IsHandle = true;
        };
    }

    public void AddConnectToListWidget(Connect? connect)
    {
        if (connect != null && !_worldsListWidget.Items.Contains(connect))
        {
            _worldsListWidget.AddItem(connect);
        }
    }

    public void RemoveConnectToListWidget(Connect? connect)
    {
        if (connect != null && _worldsListWidget.Items.Contains(connect))
        {
            _worldsListWidget.RemoveItem(connect);
        }
    }

    //连接是否存在
    public bool CheckConnectExists(Connect connect, out Connect? found)
    {
        found = _filterType switch
        {
            FilterType.Collect => ConnectionDirectory.Collected.Find(x => x.Equals(connect)),
            FilterType.Local => ConnectionDirectory.Saved.Find(x => x.Equals(connect)),
            _ => null
        };

        return found != null;
    }

    //本地连接是否存在
    public bool CheckSaveConnectExists(Connect connect, out Connect? found)
    {
        found = ConnectionDirectory.Saved.Find(x => x.Equals(connect));
        return found != null;
    }

    //收藏连接是否存在
    public bool CheckCollectConnectExists(Connect connect, out Connect? found)
    {
        found = ConnectionDirectory.Collected.Find(x => x.Equals(connect));
        return found != null;
    }

    //更多服连接是否存在
    public bool CheckOnlineConnectExists(Connect connect, out Connect? found)
    {
        found = ConnectionDirectory.Discovered.Find(x => x.Equals(connect));
        return found != null;
    }

    public void OnItemClick(object? item)
    {
        if (item == null || _worldsListWidget.SelectedItem != item)
        {
            return;
        }

        var connect = (Connect)item;
        ConnectTo(connect);
    }

    public void ConnectTo(Connect connect)
    {
        //如果本地没有，则保存到本地
        if (!CheckSaveConnectExists(connect, out _))
        {
            ConnectionDirectory.Saved.Add(connect);
        }

        if (!CheckConnectExists(connect, out var found))
        {
            found = connect;
        }
        else
        {
            found!.Name = connect.Name;
            found.IP = connect.IP;
        }

        if (CommonLib.Resolve(found.IP, out var ep))
        {
            if (!string.IsNullOrWhiteSpace(connect.ContentServerUrl))
            {
                Log.Information($"远程内容服务已声明为: {connect.ContentServerUrl}");
            }

            PrepareRemoteSessionAndConnect(ep!, connect.RequiredModProfile);
        }
        else
        {
            DialogsManager.Alert("连接服务器失败");
        }
    }

    private void PrepareRemoteSessionAndConnect(
        IPEndPoint endPoint,
        ModProfile? requiredProfile)
    {
        if (requiredProfile is not { Packages.Count: > 0 })
        {
            ConnectPreparedRemoteSession(endPoint);
            return;
        }

        var busyDialog = new BusyDialog("准备服务器模组", "正在检查所需模组...");
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                var result = ModRestartHelper.PrepareRemoteSession(
                    SessionInfoManager.CreateRemoteClientSession(endPoint),
                    requiredProfile,
                    message => Dispatcher.Dispatch(() => busyDialog.SmallMessage = message));
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    if (!result.RequiresRestart)
                    {
                        ConnectPreparedRemoteSession(endPoint);
                        return;
                    }

                    ConfirmRemoteModRestart(result);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(
                        "模组下载失败",
                        $"无法准备服务器需要的模组。\n{ex.Message}");
                });
            }
        });
    }

    private static void ConnectPreparedRemoteSession(IPEndPoint endPoint)
    {
        DialogsManager.HideAllDialogs();
        ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, endPoint);
    }

    private static void ConfirmRemoteModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                "需要重启游戏",
                $"{result.RestartReason}\n\n是否现在重启？",
                "重启",
                "取消",
                button =>
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    GameExitManager.RequestRestart(result.RemoteSession!, result.SessionProfile!);
                }));
    }

    public void UpdateList()
    {
        try
        {
            _worldsListWidget.ClearItems();

            if (_filterType == FilterType.Collect)
            {
                UpdateCollectList();
            }
            else if (_filterType == FilterType.Local)
            {
                UpdateLocalList();
            }
        }
        catch (Exception e)
        {
            Log.Error("UpdateNetList:" + e.Message);
        }
    }

    //检查所有的Connect是否可以正常连接
    private bool CheckingConnects(List<Connect> connects)
    {
        foreach (var c in connects)
        {
            if (c is { State: ConnectState.Checking })
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateCollectList()
    {
        if (!CheckingConnects(ConnectionDirectory.Collected))
        {
            return;
        }

        ConnectionDirectory.Collected.Sort((c1, c2) => (int)c2.State - (int)c1.State);
        foreach (var connection in ConnectionDirectory.Collected)
        {
            AddConnectToListWidget(connection);
        }

        _isLoadingList = false;
    }

    private void UpdateLocalList()
    {
        if (!CheckingConnects(ConnectionDirectory.Saved))
        {
            return;
        }

        ConnectionDirectory.Saved.Sort((c1, c2) => (int)c2.State - (int)c1.State);
        foreach (var connection in ConnectionDirectory.Saved)
        {
            AddConnectToListWidget(connection);
        }

        _isLoadingList = false;
    }

    /// <summary>
    ///     发现局域网服务器
    /// </summary>
    /// <param name="end"></param>
    private static void DiscoverLocalServers(Action end)
    {
        var listener = new EventBasedNetListener();
        var net = new NetManager(listener) { ReuseAddress = true };
        var received = false;
        try
        {
            net.UnconnectedMessagesEnabled = true;
            net.Start();
            var s = Stopwatch.StartNew();
            listener.NetworkReceiveUnconnectedEvent += (ep, r, _) =>
            {
                if (ep.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    return;
                }

                var serverInfoPackage =
                    PackageManager.DecodePackage<ServerInfoPackage>(null, r, null, null, ep);
                serverInfoPackage.Ping = (int)s.ElapsedMilliseconds;
                serverInfoPackage.From?.IsLocalRemote = true;
                PackageDispatcher.Handle(serverInfoPackage, null, false);
                received = true;
            };
            NetNode.SendWriterFromPackage(net, [new ServerInfoPackage(true)], null);
            while (s.ElapsedMilliseconds < 500 && !received)
            {
                net.PollEvents();
                Thread.Sleep(1);
            }

            Log.Debug("Exit Discover");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        finally
        {
            net.Stop();
            end.Invoke();
        }
    }

    private static void CheckConnect(Connect c)
    {
        var listener = new EventBasedNetListener();
        var net = new NetManager(listener) { ReuseAddress = true };
        try
        {
            net.UnconnectedMessagesEnabled = true;
            net.Start();
            var received = false;
            c.State = ConnectState.Checking;
            if (CommonLib.Resolve(c.IP, out var cep))
            {
                IpToDNS[cep!.Address + ":" + cep.Port] = c.IP;
                var s = Stopwatch.StartNew();
                listener.NetworkReceiveUnconnectedEvent += (ep, r, _) =>
                {
                    if (!ep.Address.Equals(cep.Address))
                    {
                        return;
                    }

                    var serverInfoPackage =
                        PackageManager.DecodePackage<ServerInfoPackage>(null, r, null, null, ep);
                    serverInfoPackage.Ping = (int)s.ElapsedMilliseconds;
                    PackageDispatcher.Handle(serverInfoPackage, null, false);
                    received = true;
                };
                NetNode.SendWriterFromPackage(net, [new ServerInfoPackage(true)], cep);
                while (s.ElapsedMilliseconds < 500 && !received)
                {
                    net.PollEvents();
                    Thread.Sleep(1);
                }

                Log.Debug("Exit Check Connect");
            }

            if (c.State == ConnectState.Checking)
            {
                c.State = ConnectState.Unavailable;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            c.State = ConnectState.Unavailable;
        }
        finally
        {
            net.Stop();
        }
    }

    private void AddIntoCheckList(Connect connect)
    {
        var thread = new Thread(() => CheckConnect(connect)) { IsBackground = true };
        thread.Start();
        RunningTasks.Add(thread);
    }

    private void RefreshConnects()
    {
        RunningTasks.Clear();
        _worldsListWidget.ClearItems();
        if (_filterType == FilterType.Collect) //收藏
        {
            var count = ConnectionDirectory.Collected.Count;
            for (var i = 0; i < count; i++)
            {
                if (i < ConnectionDirectory.Collected.Count)
                {
                    var c = ConnectionDirectory.Collected[i];
                    c.State = ConnectState.Checking;
                    c.FromCollect = true;
                    AddIntoCheckList(c);
                }
            }

            UpdateList();
        }

        if (_filterType == FilterType.Local) //本地
        {
            LookingForServer = true;
            var thread = new Thread(() => DiscoverLocalServers(delegate { LookingForServer = false; }))
            { IsBackground = true };
            thread.Start();
            RunningTasks.Add(thread);

            var count = ConnectionDirectory.Saved.Count;
            for (var i = 0; i < count; i++)
            {
                if (i < ConnectionDirectory.Saved.Count)
                {
                    var c = ConnectionDirectory.Saved[i];
                    c.State = ConnectState.Checking;
                    c.FromLocal = true;
                    AddIntoCheckList(c);
                }
            }

            UpdateList();
        }
    }

    public override void Enter(object[] parameters)
    {
        _filterType = FilterType.Local;
        RefreshConnects();
    }

    public override void Leave()
    {
        RunningTasks.Clear();
    }

    public override void Update()
    {
        try
        {
            for (var i = RunningTasks.Count - 1; i >= 0; i--)
            {
                if (RunningTasks[i].ThreadState == ThreadState.Stopped)
                {
                    RunningTasks.RemoveAt(i);
                    UpdateList();
                    break;
                }
            }

            _addButton.IsEnabled = _filterType is FilterType.Collect or FilterType.Local;
            _removeButton.IsEnabled = _worldsListWidget.SelectedItem != null &&
                                      _filterType is FilterType.Collect or FilterType.Local;
            _topBarLabel.Text = LanguageManager.Get("NetPlayScreen", 5) + "(" + _worldsListWidget.Items.Count + ")" +
                                (LookingForServer ? LanguageManager.Get("NetPlayScreen", 6) : "");
            if (Time.PeriodicEvent(0.1f, 0))
            {
                _refreshTime += 0.1f;
            }

            _refreshButton.IsEnabled = _refreshTime > 1f;
            _refreshButton.Color = _refreshButton.IsEnabled ? Color.Green : Color.LightGray;
            _filter0Button.Color = _filterType == FilterType.Collect ? Color.Green : Color.White;
            _filter1Button.Color = _filterType == FilterType.Local ? Color.Green : Color.White;

            var loadingText = LanguageManager.Get("NetPlayScreen", 17);
            _filter0Button.Text = _filterType == FilterType.Collect
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 3);
            _filter1Button.Text = _filterType == FilterType.Local
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 4);

            if (!_isLoadingList)
            {
                _filter0Button.Text = _filterType == FilterType.Collect
                    ? LanguageManager.Get("NetPlayScreen", 3)
                    : _filter0Button.Text;
                _filter1Button.Text = _filterType == FilterType.Local
                    ? LanguageManager.Get("NetPlayScreen", 4)
                    : _filter1Button.Text;
            }

            if (_addButton.IsClicked) //添加服务器
            {
                DialogsManager.ShowDialog(null, new AddServerDialog((name, ip) =>
                {
                    var connect = new Connect
                    {
                        Name = name,
                        IP = ip
                    };
                    if (_filterType == FilterType.Collect)
                    {
                        if (CheckCollectConnectExists(connect, out var found))
                        {
                            ConnectionDirectory.Collected.Remove(found!);
                        }

                        ConnectionDirectory.Collected.Add(connect);
                    }
                    else
                    {
                        if (CheckSaveConnectExists(connect, out var found))
                        {
                            ConnectionDirectory.Saved.Remove(found!);
                        }

                        ConnectionDirectory.Saved.Add(connect);
                    }

                    AddIntoCheckList(connect);
                    UpdateList();
                }));
            }

            if (_removeButton.IsClicked) //删除服务器
            {
                if (_worldsListWidget.SelectedItem != null)
                {
                    var c = (Connect)_worldsListWidget.SelectedItem;
                    if (_filterType == FilterType.Collect && CheckCollectConnectExists(c, out var found))
                    {
                        ConnectionDirectory.Collected.Remove(found!);
                    }

                    if (_filterType == FilterType.Local && CheckSaveConnectExists(c, out var found2))
                    {
                        ConnectionDirectory.Saved.Remove(found2!);
                    }

                    UpdateList();
                }
            }

            if (_collectButton.IsClicked)
            {
                if (_worldsListWidget.SelectedItem != null && _filterType != FilterType.Collect)
                {
                    var c = (Connect)_worldsListWidget.SelectedItem;
                    if (CheckCollectConnectExists(c, out var found))
                    {
                        ConnectionDirectory.Collected.Remove(found!);
                    }

                    ConnectionDirectory.Collected.Add(c);
                    DialogsManager.ShowDialog(
                        this,
                        new MessageDialog(
                            "收藏成功",
                            $"服[{c.Name}]已添加到自定义列表",
                            "确定"
                        )
                    );
                }
            }

            if (_refreshButton.IsClicked && _refreshTime > 1f) //刷新
            {
                _isLoadingList = true;
                _refreshTime = 0;
                RefreshConnects();
            }

            if (_filter0Button.IsClicked && _filterType != FilterType.Collect)
            {
                _isLoadingList = true;
                _filterType = FilterType.Collect;
                RefreshConnects();
            }

            if (_filter1Button.IsClicked && _filterType != FilterType.Local)
            {
                _isLoadingList = true;
                _filterType = FilterType.Local;
                RefreshConnects();
            }

            if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
            {
                ScreensManager.SwitchScreen("MainMenu");
                _worldsListWidget.SelectedItem = null;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e.Message);
        }
    }

    /// <summary>
    ///     获取存档的季节
    /// </summary>
    /// <param name="season">季节枚举</param>
    /// <param name="timeOfSeason">季节进度</param>
    /// <returns>季节字符串</returns>
    private string GetSeasonText(Season season, float timeOfSeason)
    {
        var seasonIndex = season switch
        {
            Season.Summer => timeOfSeason < 0.33f ? 0 : timeOfSeason < 0.67f ? 1 : 2,
            Season.Autumn => timeOfSeason < 0.33f ? 3 : timeOfSeason < 0.67f ? 4 : 5,
            Season.Winter => timeOfSeason < 0.33f ? 6 : timeOfSeason < 0.67f ? 7 : 8,
            Season.Spring => timeOfSeason < 0.33f ? 9 : timeOfSeason < 0.67f ? 10 : 11,
            _ => 1 // 默认盛夏
        };

        return LanguageManager.Get("SubsystemSeasons", seasonIndex);
    }

    public class Connect
    {
        public bool FromBroadcast;

        public bool FromCollect;

        public bool FromLocal;

        public GameMode GameMode;

        public string IP = string.Empty;

        public long Level;

        public ushort MaxCount;

        public string ContentServerUrl = string.Empty;

        public string Name = string.Empty;

        public ushort PlayerCount;

        public ModProfile? RequiredModProfile;

        public ConnectState State;

        public float TimeOfDay;

        public long UsedTime;

        public string ValidTime = string.Empty;

        public string Version = string.Empty;

        /// <summary>
        ///     季节
        /// </summary>
        public Season Season { get; set; }

        /// <summary>
        ///     季节进度
        /// </summary>
        public float TimeOfSeason { get; set; }

        public override int GetHashCode()
        {
            return IP.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return obj is Connect connect && connect.IP == IP;
        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
