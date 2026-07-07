namespace Game.Widgets;

/// <summary>
/// 组队面板组件
/// </summary>
public class NetPanelWidget : CanvasWidget
{
    private const int _hiddenList = -1;

    /// <summary>
    /// 展示模式
    /// </summary>
    public enum ShowType
    {
        /// <summary>
        /// 在线玩家
        /// </summary>
        OnlinePlayers = 0,

        /// <summary>
        /// 队伍
        /// </summary>
        Team = 1,

        /// <summary>
        /// 黑名单
        /// </summary>
        BlackList = 2
    }

    /// <summary>
    /// 游戏面板组件
    /// </summary>
    private readonly GameWidget _gameWidget;

    /// <summary>
    /// 创建和加入队伍组件
    /// </summary>
    public readonly NetGroupPanelWidget GroupPanelWidget;

    /// <summary>
    /// 联机玩家列表组件
    /// </summary>
    public readonly NetPlayerListWidget PlayerListWidget;

    public readonly NetPlayerListWidget GroupPlayerListWidget;

    public readonly NetPlayerListWidget BlackPlayerListWidget;

    /// <summary>
    /// 当前展示列表
    /// </summary>
    private int _currentShowList;

    /// <summary>
    /// 更新的展示列表
    /// </summary>
    private int _newShowList;

    /// <summary>
    /// 最大尺寸
    /// </summary>
    public Vector2 MaxSize = new(220, 120);

    /// <summary>
    /// ??
    /// </summary>
    public PlayerData PlayerData;

    /// <summary>
    /// ??
    /// </summary>
    public CanvasWidget RightControl = new();

    /// <summary>
    /// 玩家子系统
    /// </summary>
    public SubsystemPlayers? SubsystemPlayers;

    public NetPanelWidget(GameWidget gameWidget)
    {
        _gameWidget = gameWidget;
        PlayerData = gameWidget.PlayerData;
        PlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.OnlinePlayers);
        GroupPlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.Team);
        BlackPlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.BlackList);
        GroupPanelWidget = new NetGroupPanelWidget(PlayerData);

        InitWidgets();
    }

    public void InitWidgets()
    {
        Size = MaxSize;
        AddChildren(new BevelledRectangleWidget
        {
            CenterColor = new Color(0, 0, 0, 150),
            BevelColor = new Color(160, 160, 160, 180),
            ShadowColor = new Color(0, 0, 0, 96),
            RoundingRadius = 8f,
            RoundingCount = 4,
            BevelSize = 1.5f
        });
        AddChildren(RightControl);
        RightControl.AddChildren(GroupPanelWidget);
        RightControl.AddChildren(PlayerListWidget);
        RightControl.AddChildren(GroupPlayerListWidget);
        RightControl.AddChildren(BlackPlayerListWidget);
        _currentShowList = _hiddenList;
        _newShowList = _hiddenList;
        IsVisible = false;
        SetupOnlinePlayerList();
    }

    /// <summary>
    /// 在显示列表中循环切换
    /// </summary>
    public void CycleSwitch()
    {
        if (!IsVisible || _currentShowList == _hiddenList)
        {
            IsVisible = true;
            _newShowList = (int)ShowType.OnlinePlayers;
            RefreshView();
            return;
        }

        var availableLists = PlayerData.ServerManager ? 3 : 2;
        if (_currentShowList + 1 >= availableLists)
        {
            _currentShowList = _hiddenList;
            _newShowList = _hiddenList;
            IsVisible = false;
            return;
        }

        _newShowList = _currentShowList + 1;
        RefreshView();
    }

    public ShowType? CurrentShowType => _currentShowList == _hiddenList
        ? null
        : (ShowType)_currentShowList;

    /// <summary>
    /// 创建队伍
    /// </summary>
    public void CreateTeam()
    {
        _newShowList = 1;
        GroupPanelWidget.CreateTeam();
    }

    /// <summary>
    /// 加入队伍
    /// </summary>
    public void JoinTeam()
    {
        _newShowList = 1;
        GroupPanelWidget.JoinTeam();
    }

    /// <summary>
    /// 离开队伍
    /// </summary>
    public void LeaveTeam()
    {
        if (_currentShowList != 1)
        {
            _newShowList = 1;
            return;
        }

        if (GroupPlayerListWidget.Players.WidgetsByIndex.Count == 0)
        {
            GroupPlayerListWidget.Players.CreateListWidgets(GroupPlayerListWidget.Players.Items.Count);
        }

        var keyPair = GroupPlayerListWidget.Players.WidgetsByIndex.Where(keyPair =>
            {
                if (keyPair.Value is PlayerItemWidget playerItem)
                {
                    return PlayerData.PlayerGUID == playerItem.Player.PlayerGUID;
                }

                return false;
            })
            .FirstOrDefault();

        if (keyPair.Value is PlayerItemWidget widget)
        {
            widget.LeaveTeam();
        }
    }

    /// <summary>
    /// 刷新显示列表
    /// </summary>
    public void RefreshView()
    {
        _currentShowList = _newShowList;

        switch (_currentShowList)
        {
            case 0:
                SetupOnlinePlayerList();
                break;
            case 1:
                if (_gameWidget.PlayerData.GroupKey != string.Empty)
                {
                    SetupGroupPlayerList();
                }
                else
                {
                    SetupGroupPanelList();
                }

                break;
            case 2:
                SetupBlackPlayerList();
                break;
        }
    }

    public void SetupOnlinePlayerList()
    {
        GroupPanelWidget.IsVisible = false;
        PlayerListWidget.IsVisible = true;
        GroupPlayerListWidget.IsVisible = false;
        BlackPlayerListWidget.IsVisible = false;
        PlayerListWidget.RefreshList();
    }

    public void SetupGroupPanelList()
    {
        GroupPanelWidget.IsVisible = true;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = false;
        BlackPlayerListWidget.IsVisible = false;
    }

    public void SetupGroupPlayerList()
    {
        GroupPanelWidget.IsVisible = false;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = true;
        BlackPlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.RefreshList();
    }

    public void SetupBlackPlayerList()
    {
        GroupPanelWidget.IsVisible = false;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = false;
        BlackPlayerListWidget.IsVisible = true;
        BlackPlayerListWidget.RefreshList();
    }

    public override void Update()
    {
        if (_currentShowList != _newShowList)
        {
            RefreshView();
        }
    }
}
