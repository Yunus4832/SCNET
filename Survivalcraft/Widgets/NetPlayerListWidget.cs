namespace Game.Widgets;

/// <summary>
/// 联机玩家列表组件
/// </summary>
public class NetPlayerListWidget : CanvasWidget
{
    /// <summary>
    /// 主玩家
    /// </summary>
    private readonly PlayerData _mainPlayer;

    /// <summary>
    /// 展示类型
    /// </summary>
    private readonly NetPanelWidget.ShowType _showType;

    /// <summary>
    /// 玩家列表
    /// </summary>
    public readonly ListPanelWidget Players = new() { Direction = LayoutDirection.Vertical };

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="main">主玩家</param>
    /// <param name="showType">展示类型</param>
    public NetPlayerListWidget(PlayerData main, NetPanelWidget.ShowType showType)
    {
        Size = new Vector2(220, 120);

        _showType = showType;

        Children.Add(Players);

        _mainPlayer = main;
        Players.ItemSize = 30f;
        Players.ItemWidgetFactory = obj => new PlayerItemWidget(
            obj as PlayerData ?? throw new InvalidOperationException("Input object is not a PlayerData"),
            _mainPlayer,
            this,
            _showType
        );

        _mainPlayer.SubsystemPlayers.PlayerAdded += PlayerDataChange;
        _mainPlayer.SubsystemPlayers.PlayerRemoved += PlayerDataChange;
        _mainPlayer.EntityRemoved += () =>
        {
            _mainPlayer.SubsystemPlayers.PlayerAdded -= PlayerDataChange;
            _mainPlayer.SubsystemPlayers.PlayerRemoved -= PlayerDataChange;
        };
        RefreshList();
    }

    private void PlayerDataChange(PlayerData player)
    {
        RefreshList();
    }

    public void RefreshList()
    {
        switch (_showType)
        {
            case NetPanelWidget.ShowType.OnlinePlayers:
                SortOnlinePlayerList();
                break;
            case NetPanelWidget.ShowType.Team:
                SortGroupPlayerList();
                break;
            case NetPanelWidget.ShowType.BlackList:
                SortBlackPlayerList();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SortGroupPlayerList()
    {
        if (_mainPlayer.GroupKey == string.Empty)
        {
            return;
        }

        _mainPlayer.SubsystemPlayers.ServerGroups.TryGetValue(_mainPlayer.GroupKey, out var group);
        if (group == null)
        {
            _mainPlayer.GroupKey = string.Empty;
            return;
        }

        Players.ClearItems();
        Players.AddItem(_mainPlayer);
        foreach (var playerData in _mainPlayer.SubsystemPlayers.PlayersData)
        {
            if (!playerData.IsMainPlayer && group.Members.Contains(playerData.PlayerGUID))
            {
                Players.AddItem(playerData);
            }
        }
    }

    private void SortBlackPlayerList()
    {
        Players.ClearItems();
        var l = new List<string>();
        foreach (var xc in _mainPlayer.SubsystemPlayers.BlackPlayerGuidList)
        {
            try
            {
                var playerData = new BlacklistPlayerData(new Guid(xc.Key), xc.Value);
                Players.AddItem(playerData);
            }
            catch
            {
                l.Add(xc.Key);
            }
        }

        foreach (var c in l)
        {
            _mainPlayer.SubsystemPlayers.BlackPlayerGuidList.Remove(c);
        }
    }

    private void SortOnlinePlayerList()
    {
        Players.ClearItems();
        foreach (var playerData in _mainPlayer.SubsystemPlayers.PlayersData)
        {
            if (!playerData.IsMainPlayer)
            {
                Players.AddItem(playerData);
            }
        }
    }
}
