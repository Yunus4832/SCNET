namespace Game.Widgets;

/// <summary>
/// 玩家列表组件
/// </summary>
public class PlayerListWidget : CanvasWidget
{
    public enum ListKind
    {
        Players,
        SameTeamPlayers,
        BlackList
    }

    /// <summary>
    /// 主玩家
    /// </summary>
    private readonly PlayerData _mainPlayer;

    /// <summary>
    /// 展示类型
    /// </summary>
    private ListKind _kind;

    public ListKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            RefreshList();
        }
    }

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
    /// <param name="kind">列表数据范围</param>
    /// <param name="isReadOnly"></param>
    /// <param name="size"></param>
    public PlayerListWidget(
        PlayerData main,
        ListKind kind,
        bool isReadOnly = false,
        Vector2? size = null)
    {
        Size = size ?? new Vector2(220f, 120f);

        _kind = kind;
        var isReadOnly1 = isReadOnly;

        var itemWidth = MathUtils.Max(Size.X - (isReadOnly ? 8f : 32f), 0f);
        Players.HorizontalAlignment = WidgetAlignment.Center;
        Children.Add(Players);

        _mainPlayer = main;
        Players.ItemSize = isReadOnly1 ? 21f : 52f;
        Players.SelectionColor = MultiplayerUiStyle.ListSelectionColor;
        Players.IsHitTestVisible = !isReadOnly1;
        Players.IsSelectionEnabled = !isReadOnly1;
        if (isReadOnly1)
        {
            Players.ScrollPosition = 0f;
            Players.ScrollSpeed = 0f;
        }

        Players.ItemWidgetFactory = obj =>
        {
            if (!isReadOnly1)
            {
                return new PlayerManagementItemWidget(obj, _mainPlayer, _kind, itemWidth);
            }

            return new PlayerObservationItemWidget(obj, _mainPlayer, itemWidth);
        };

        _mainPlayer.SubsystemPlayers.PlayerAdded += PlayerDataChange;
        _mainPlayer.SubsystemPlayers.PlayerRemoved += PlayerDataChange;
        _mainPlayer.SubsystemPlayers.PlayerListChanged += RefreshList;
        _mainPlayer.EntityRemoved += Unsubscribe;
        RefreshList();
    }

    public override void Dispose()
    {
        Unsubscribe();
        base.Dispose();
    }

    private void Unsubscribe()
    {
        _mainPlayer.SubsystemPlayers.PlayerAdded -= PlayerDataChange;
        _mainPlayer.SubsystemPlayers.PlayerRemoved -= PlayerDataChange;
        _mainPlayer.SubsystemPlayers.PlayerListChanged -= RefreshList;
        _mainPlayer.EntityRemoved -= Unsubscribe;
    }

    private void PlayerDataChange(PlayerData player)
    {
        RefreshList();
    }

    public void RefreshList()
    {
        switch (_kind)
        {
            case ListKind.Players:
                PopulateAllPlayers();
                break;
            case ListKind.SameTeamPlayers:
                PopulateTeamPlayers();
                break;
            case ListKind.BlackList:
                PopulateBlacklist();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PopulateTeamPlayers()
    {
        Players.ClearItems();
        if (_mainPlayer.GroupKey == string.Empty)
        {
            return;
        }

        foreach (var item in GetPlayerListItems())
        {
            if (_mainPlayer.SubsystemPlayers.GetPlayerGroupKey(item.Entry.PlayerGuid) ==
                _mainPlayer.GroupKey)
            {
                Players.AddItem(item.Item);
            }
        }
    }

    private void PopulateBlacklist()
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

    private void PopulateAllPlayers()
    {
        Players.ClearItems();
        foreach (var item in GetPlayerListItems())
        {
            Players.AddItem(item.Item);
        }
    }

    private IEnumerable<(PlayerListEntry Entry, object Item)> GetPlayerListItems()
    {
        return from entry in _mainPlayer.SubsystemPlayers.PlayerList.Values
                .OrderByDescending(player => player.IsOnline)
                .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            let activePlayer = _mainPlayer.SubsystemPlayers.PlayersData
                .Find(player => player.PlayerGUID == entry.PlayerGuid)
            select ((PlayerListEntry Entry, object Item))(entry, activePlayer is not null ? activePlayer : entry);
    }
}
