using System.Net.Sockets;
using System.Xml.Linq;

using Game.ContentProviders;
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
        Local,
        Community,
        CommunityOther
    }

    private const string _typeName = "NetPlayScreen";

    public static Dictionary<string, string> IpToDNS = new();

    public static Dictionary<string, string> DNSToName = new();

    public bool LookingForServer;

    private readonly ButtonWidget _addButton;

    private readonly ButtonWidget _collectButton;

    private readonly ButtonWidget _filter0Button;

    private readonly ButtonWidget _filter1Button;

    private readonly ButtonWidget _filter2Button;

    private readonly ButtonWidget _filter3Button;

    private FilterType _filterType; // 0收藏，1本地，2社区服，3其他服

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
        _filter0Button.Text = LanguageManager.Get("NetPlayScreen", 1);
        _filter0Button.Size = new Vector2(180, 60);
        _filter1Button = new BevelledButtonWidget
            { Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60") };
        _filter1Button.Text = LanguageManager.Get("NetPlayScreen", 2);
        _filter1Button.Size = new Vector2(180, 60);
        _filter0Button.ParentWidget?.AddChildren(_filter1Button);
        _filter2Button = new BevelledButtonWidget
            { Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60") };
        _filter2Button.Text = LanguageManager.Get("NetPlayScreen", 3);
        _filter2Button.Size = new Vector2(180, 60);
        _filter0Button.ParentWidget?.AddChildren(_filter2Button);
        _filter3Button = new BevelledButtonWidget
            { Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60") };
        _filter3Button.Text = LanguageManager.Get("NetPlayScreen", 4);
        _filter3Button.Size = new Vector2(180, 60);
        _filter0Button.ParentWidget?.AddChildren(_filter3Button);

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
            var needLogin = connect.IsNeedLoginCommunity
                ? $" | {LanguageManager.Get("NetPlayScreen", "16")}"
                : string.Empty;
            var validTime = !string.IsNullOrEmpty(connect.ValidTime)
                ? $" | {LanguageManager.Get("NetPlayScreen", 18)}: {connect.ValidTime}"
                : string.Empty;


            switch (connect.State)
            {
                case ConnectState.Available:
                {
                    labelWidget.Text = $"{connect} ({connect.UsedTime / 2:0} ms)";
                    labelWidget.Color = Color.LightGreen;
                    labelWidget2.Text = $"{version}{players}{gameMode}{timeOfDay}{season}{needLogin}{validTime}";

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
            var arr = ip.Split(['/'], StringSplitOptions.None);
            if (arr.Length > 0)
            {
                switch (arr.Length)
                {
                    case 1:
                        connect.IP = arr[0];
                        connect.SavedPassword = string.Empty;
                        connect.Name = "scheme_" + DateTime.Now.Ticks;
                        break;
                    case 2:
                        connect.IP = arr[0];
                        connect.SavedPassword = arr[1];
                        connect.Name = "scheme_" + DateTime.Now.Ticks;
                        break;
                    case 3:
                        connect.IP = arr[0];
                        connect.SavedPassword = arr[1];
                        connect.Name = arr[2];
                        break;
                }

                Time.QueueTimeDelayedExecution(Time.RealTime + 1, () =>
                {
                    Log.Information($"连接到服务器:{connect.IP}/{connect.SavedPassword}");
                    ConnectTo(connect, connect.SavedPassword);
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
            FilterType.Community or FilterType.CommunityOther =>
                ConnectionDirectory.Discovered.Find(x => x.Equals(connect)),
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
        if (connect.IsNeedLoginCommunity && string.IsNullOrEmpty(SettingsManager.Current.CommunityAccessUser))
        {
            DialogsManager.Confirm("请登录社区后再进行操作", btn =>
            {
                if (btn == MessageDialogButton.Button1)
                {
                    DialogsManager.ShowDialog(null, new LoginDialog());
                }
            });
        }
        else
        {
            if (connect.HasPassword)
            {
                var pwdx = string.Empty;
                if (CheckConnectExists(connect, out var found))
                {
                    found?.SavedPassword = connect.SavedPassword;
                    pwdx = found?.SavedPassword ?? string.Empty;
                }

                DialogsManager.ShowDialog(
                    this,
                    new TextBoxDialog(
                        "请输入房间密码",
                        pwdx,
                        16,
                        pwd =>
                        {
                            if (!string.IsNullOrEmpty(pwd))
                            {
                                ConnectTo(connect, pwd);
                            }
                            else
                            {
                                DialogsManager.HideAllDialogs();
                            }
                        }
                    )
                );
            }
            else
            {
                ConnectTo(connect);
            }
        }
    }

    public void ConnectTo(Connect connect, string passwd = "")
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
            found!.SavedPassword = passwd;
            found.Name = connect.Name;
            found.IP = connect.IP;
        }

        if (CommonLib.Resolve(found.IP, out var ep))
        {
            if (!string.IsNullOrWhiteSpace(connect.ModServerAddress))
            {
                Log.Information($"远程模组仓库已声明为: {connect.ModServerAddress}");
            }

            if (ModRestartHelper.PrepareRemoteSessionIfNeeded(
                    SessionInfoManager.CreateRemoteClientSession(ep!, passwd),
                    connect.RequiredModProfile,
                    Log.Information)
               )
            {
                return;
            }

            DialogsManager.HideAllDialogs();
            ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, ep!, passwd);
        }
        else
        {
            DialogsManager.Alert("连接服务器失败");
        }
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
            else if (_filterType is FilterType.Community or FilterType.CommunityOther)
            {
                UpdateCommunityList();
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

    private void UpdateCommunityList()
    {
        if (!CheckingConnects(ConnectionDirectory.Discovered))
        {
            return;
        }

        ConnectionDirectory.Discovered.Sort((c1, c2) => (int)c2.State - (int)c1.State);
        foreach (var connection in ConnectionDirectory.Discovered)
        {
            if (_filterType == FilterType.Community && connection.FromCommunity)
            {
                AddConnectToListWidget(connection);
            }

            if (_filterType == FilterType.CommunityOther && connection.FromCommunityOther)
            {
                AddConnectToListWidget(connection);
            }
        }

        _isLoadingList = false;
    }

    /// <summary>
    /// 发现局域网服务器
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

        if (_filterType == FilterType.Community || _filterType == FilterType.CommunityOther) //社区服&其他服
        {
            ConnectionDirectory.Discovered.Clear();
            LoadExternalServerList(SchubExternalContentProvider.RedirectUri + "/com/serverlist?version=" +
                                   VersionsManager.ProtocolVersion);
            LoadExternalServerList("http://schelper.trk34.top:34340" + "/com/serverlist?version=" +
                                   VersionsManager.ProtocolVersion);
        }
    }

    public void LoadExternalServerList(string url)
    {
        WebManager.Get(
            url,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new CancellableProgress(),
            data =>
            {
                var streamReader = new StreamReader(new MemoryStream(data) { Position = 0L });
                var connects = JsonUtils.Deserialize<ServerList>(streamReader.ReadToEnd()) ?? new ServerList();
                ProcessConnectList(connects);
            },
            _ => { }
        );
    }

    public void ProcessConnectList(ServerList connects)
    {
        foreach (var c in connects.ConnectionList)
        {
            DNSToName[c.IP] = c.Name;
            if (!CheckOnlineConnectExists(c, out var found))
            {
                found = c;
                found.FromCommunity = c.Level == 1;
                found.FromCommunityOther = c.Level == 0;
                found.State = ConnectState.Checking;
                ConnectionDirectory.Discovered.Add(found);
                AddIntoCheckList(found);
            }
            else
            {
                found!.Name = c.Name;
                found.FromCommunity = c.Level == 1;
                found.FromCommunityOther = c.Level == 0;
                found.State = ConnectState.Checking;
                found.ValidTime = c.ValidTime;
                AddIntoCheckList(found);
            }
        }

        UpdateList();
    }

    public override void Enter(object[] parameters)
    {
        _filterType = FilterType.Community;
        //_filterButton.Text = "自定义";
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
            _filter0Button.Color = _filterType == FilterType.Community ? Color.Green : Color.White;
            _filter1Button.Color = _filterType == FilterType.CommunityOther ? Color.Green : Color.White;
            _filter2Button.Color = _filterType == FilterType.Collect ? Color.Green : Color.White;
            _filter3Button.Color = _filterType == FilterType.Local ? Color.Green : Color.White;

            var loadingText = LanguageManager.Get("NetPlayScreen", 17);
            _filter0Button.Text = _filterType == FilterType.Community
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 1);
            _filter1Button.Text = _filterType == FilterType.CommunityOther
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 2);
            _filter2Button.Text = _filterType == FilterType.Collect
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 3);
            _filter3Button.Text = _filterType == FilterType.Local
                ? loadingText
                : LanguageManager.Get("NetPlayScreen", 4);

            if (!_isLoadingList)
            {
                _filter0Button.Text = _filterType == FilterType.Community
                    ? LanguageManager.Get("NetPlayScreen", 1)
                    : _filter0Button.Text;
                _filter1Button.Text = _filterType == FilterType.CommunityOther
                    ? LanguageManager.Get("NetPlayScreen", 2)
                    : _filter1Button.Text;
                _filter2Button.Text = _filterType == FilterType.Collect
                    ? LanguageManager.Get("NetPlayScreen", 3)
                    : _filter2Button.Text;
                _filter3Button.Text = _filterType == FilterType.Local
                    ? LanguageManager.Get("NetPlayScreen", 4)
                    : _filter3Button.Text;
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

            if (_filter0Button.IsClicked && _filterType != FilterType.Community) //社区服
            {
                _isLoadingList = true;
                _filterType = FilterType.Community;
                RefreshConnects();
            }

            if (_filter1Button.IsClicked && _filterType != FilterType.CommunityOther) //个人服
            {
                _isLoadingList = true;
                _filterType = FilterType.CommunityOther;
                RefreshConnects();
            }

            if (_filter2Button.IsClicked && _filterType != FilterType.Collect) //收藏
            {
                _isLoadingList = true;
                _filterType = FilterType.Collect;
                RefreshConnects();
            }

            if (_filter3Button.IsClicked && _filterType != FilterType.Local) //本地
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
    /// 获取存档的季节
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

    public class ServerList
    {
        public readonly List<Connect> ConnectionList = [];
    }

    public class Connect
    {
        public bool FromBroadcast;

        public bool FromCollect;

        public bool FromCommunity;

        public bool FromCommunityOther;

        public bool FromLocal;

        public GameMode GameMode;

        public bool HasPassword;

        public string IP = string.Empty;

        public bool IsNeedLoginCommunity;

        public long Level;

        public ushort MaxCount;

        public string ModServerAddress = string.Empty;

        public string Name = string.Empty;

        public ushort PlayerCount;

        public ModProfile? RequiredModProfile;

        public string SavedPassword = string.Empty;

        public ConnectState State;

        public float TimeOfDay;

        public long UsedTime;

        public string ValidTime = string.Empty;

        public string Version = string.Empty;

        /// <summary>
        /// 季节
        /// </summary>
        public Season Season { get; set; }

        /// <summary>
        /// 季节进度
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
