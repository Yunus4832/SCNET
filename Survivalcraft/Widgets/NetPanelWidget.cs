using Engine.Graphics;

namespace Game.Widgets;

/// <summary>
/// 组队面板组件
/// </summary>
public class NetPanelWidget : CanvasWidget
{
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
    /// 位图按钮组件
    /// </summary>
    private readonly BitmapButtonWidget _openGroupPlayerPanel;

    private readonly BitmapButtonWidget _openOnlinePlayersPanel;

    private readonly BitmapButtonWidget _openBlackPlayerPanel;

    private readonly BitmapButtonWidget _hideOrOpenPanel;

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
    /// 图标尺寸
    /// </summary>
    public Vector2 IconSize = new(28, 20);

    /// <summary>
    /// 最大尺寸
    /// </summary>
    public Vector2 MaxSize = new(240, 120);

    /// <summary>
    /// 最小尺寸
    /// </summary>
    public Vector2 MinSize = new(28, 28);

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
        var st1 = new Subtexture(ContentManager.Get<Texture2D>("Textures/Gui/Server_Players_Btn"), Vector2.Zero,
            Vector2.One);
        var st2 = new Subtexture(ContentManager.Get<Texture2D>("Textures/Gui/Server_Group_Btn"), Vector2.Zero,
            Vector2.One);
        var st3 = new Subtexture(ContentManager.Get<Texture2D>("Textures/Gui/Server_Black_Btn"), Vector2.Zero,
            Vector2.One);
        var st4 = new Subtexture(ContentManager.Get<Texture2D>("Textures/Gui/Server_Hide_Btn"), Vector2.Zero,
            Vector2.One);

        _gameWidget = gameWidget;
        PlayerData = gameWidget.PlayerData;
        PlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.OnlinePlayers);
        GroupPlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.Team);
        BlackPlayerListWidget = new NetPlayerListWidget(PlayerData, ShowType.BlackList);
        GroupPanelWidget = new NetGroupPanelWidget(PlayerData);

        _openOnlinePlayersPanel = new BitmapButtonWidget
            { Size = IconSize, NormalSubtexture = st1, ClickedSubtexture = st1, Margin = new Vector2(2, 4) };
        _openGroupPlayerPanel = new BitmapButtonWidget
            { Size = IconSize, NormalSubtexture = st2, ClickedSubtexture = st2, Margin = new Vector2(2, 4) };
        _openBlackPlayerPanel = new BitmapButtonWidget
            { Size = IconSize, NormalSubtexture = st3, ClickedSubtexture = st3, Margin = new Vector2(2, 4) };
        _hideOrOpenPanel = new BitmapButtonWidget
            { Size = IconSize, NormalSubtexture = st4, ClickedSubtexture = st4, Margin = new Vector2(2, 4) };

        InitWidgets();
    }

    public void InitWidgets()
    {
        Size = MaxSize;
        var verticalBtnPanel = new StackPanelWidget { Direction = LayoutDirection.Vertical };
        var horizontalPagePanel = new StackPanelWidget();

        #region 垂直功能按钮列表

        verticalBtnPanel.AddChildren(_openOnlinePlayersPanel);
        verticalBtnPanel.AddChildren(new RectangleWidget
            { FillColor = Color.White, Size = new Vector2(float.PositiveInfinity, 1) });
        verticalBtnPanel.AddChildren(_openGroupPlayerPanel);
        if (PlayerData.ServerManager)
        {
            verticalBtnPanel.AddChildren(new RectangleWidget
                { FillColor = Color.White, Size = new Vector2(float.PositiveInfinity, 1) });
            verticalBtnPanel.AddChildren(_openBlackPlayerPanel);
        }

        verticalBtnPanel.AddChildren(new RectangleWidget
            { FillColor = Color.White, Size = new Vector2(float.PositiveInfinity, 1) });
        verticalBtnPanel.AddChildren(_hideOrOpenPanel);

        #endregion

        AddChildren(new RectangleWidget { FillColor = new Color(0, 0, 0, 75) });
        AddChildren(horizontalPagePanel);
        horizontalPagePanel.AddChildren(verticalBtnPanel);
        horizontalPagePanel.AddChildren(new RectangleWidget
            { FillColor = Color.White, Size = new Vector2(1, float.PositiveInfinity) });
        horizontalPagePanel.AddChildren(RightControl);
        RightControl.AddChildren(GroupPanelWidget);
        RightControl.AddChildren(PlayerListWidget);
        RightControl.AddChildren(GroupPlayerListWidget);
        RightControl.AddChildren(BlackPlayerListWidget);
        _currentShowList = 0;
        SetupOnlinePlayerList();
    }

    /// <summary>
    /// 在显示列表中循环切换
    /// </summary>
    public void CycleSwitch()
    {
        _newShowList = (_currentShowList + 1) % 3;
    }

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
        _openOnlinePlayersPanel.SetImageColor(Color.White);
        _openGroupPlayerPanel.SetImageColor(Color.Gray);
        _openBlackPlayerPanel.SetImageColor(Color.Gray);
        _hideOrOpenPanel.SetImageColor(Color.Gray);
        PlayerListWidget.RefreshList();
    }

    public void SetupGroupPanelList()
    {
        GroupPanelWidget.IsVisible = true;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = false;
        BlackPlayerListWidget.IsVisible = false;
        _openOnlinePlayersPanel.SetImageColor(Color.Gray);
        _openGroupPlayerPanel.SetImageColor(Color.White);
        _openBlackPlayerPanel.SetImageColor(Color.Gray);
        _hideOrOpenPanel.SetImageColor(Color.Gray);
    }

    public void SetupGroupPlayerList()
    {
        GroupPanelWidget.IsVisible = false;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = true;
        BlackPlayerListWidget.IsVisible = false;
        _openOnlinePlayersPanel.SetImageColor(Color.Gray);
        _openGroupPlayerPanel.SetImageColor(Color.White);
        _openBlackPlayerPanel.SetImageColor(Color.Gray);
        _hideOrOpenPanel.SetImageColor(Color.Gray);
        GroupPlayerListWidget.RefreshList();
    }

    public void SetupBlackPlayerList()
    {
        GroupPanelWidget.IsVisible = false;
        PlayerListWidget.IsVisible = false;
        GroupPlayerListWidget.IsVisible = false;
        BlackPlayerListWidget.IsVisible = true;
        _openOnlinePlayersPanel.SetImageColor(Color.Gray);
        _openGroupPlayerPanel.SetImageColor(Color.Gray);
        _openBlackPlayerPanel.SetImageColor(Color.White);
        _hideOrOpenPanel.SetImageColor(Color.Gray);
        BlackPlayerListWidget.RefreshList();
    }

    public override void Update()
    {
        if (_openOnlinePlayersPanel.IsClicked)
        {
            _newShowList = 0;
        }

        if (_openGroupPlayerPanel.IsClicked)
        {
            _newShowList = 1;
        }

        if (_openBlackPlayerPanel.IsClicked)
        {
            _newShowList = 2;
        }

        if (_hideOrOpenPanel.IsClicked)
        {
            if (RightControl.IsVisible)
            {
                _openBlackPlayerPanel.IsVisible = false;
                _openGroupPlayerPanel.IsVisible = false;
                _openOnlinePlayersPanel.IsVisible = false;
                RightControl.IsVisible = false;
                _hideOrOpenPanel.SetImageColor(Color.White);
                Size = MinSize;
            }
            else
            {
                _openBlackPlayerPanel.IsVisible = true;
                _openGroupPlayerPanel.IsVisible = true;
                _openOnlinePlayersPanel.IsVisible = true;
                RightControl.IsVisible = true;
                _hideOrOpenPanel.SetImageColor(Color.Gray);
                Size = MaxSize;
            }
        }

        if (_currentShowList != _newShowList)
        {
            RefreshView();
        }
    }
}
