namespace Game.Widgets;

/// <summary>
///     Selectable player row used by the full player management panel.
/// </summary>
public sealed class PlayerManagementItemWidget : CanvasWidget
{
    private readonly LabelWidget _name = new()
    {
        FontScale = 0.9f,
        VerticalAlignment = WidgetAlignment.Center
    };

    private readonly RectangleWidget _onlineState = new()
    {
        Size = new Vector2(8f),
        HorizontalAlignment = WidgetAlignment.Near,
        VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(14f, 0f),
        OutlineColor = new Color(0, 0, 0, 110),
        OutlineThickness = 1f
    };

    private readonly LabelWidget _summary = new()
    {
        FontScale = 0.72f,
        Color = new Color(225, 225, 225, 190),
        HorizontalAlignment = WidgetAlignment.Far,
        VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(24f, 0f)
    };

    private readonly PlayerData _main;

    private readonly object _item;

    private readonly PlayerListWidget.ListKind _kind;

    private readonly string _groupKey;

    public Guid PlayerGuid => _item switch
    {
        PlayerData player => player.PlayerGUID,
        PlayerListEntry player => player.PlayerGuid,
        BlacklistPlayerData player => player.PlayerGUID,
        _ => Guid.Empty
    };

    public string PlayerName => _item switch
    {
        PlayerData player => player.Name,
        PlayerListEntry player => player.Name,
        BlacklistPlayerData player => player.Name,
        _ => string.Empty
    };

    public PlayerManagementItemWidget(
        object item,
        PlayerData main,
        PlayerListWidget.ListKind kind,
        float width)
    {
        if (item is not PlayerData and not PlayerListEntry and not BlacklistPlayerData)
        {
            throw new InvalidOperationException("Unsupported player management item.");
        }

        _item = item;
        _main = main;
        _kind = kind;
        _groupKey = main.SubsystemPlayers.GetPlayerGroupKey(PlayerGuid);
        Size = new Vector2(width, 52f);
        _name.Margin = new Vector2(
            kind == PlayerListWidget.ListKind.BlackList ? 14f : 32f,
            0f);
        Children.Add(_onlineState);
        Children.Add(_name);
        Children.Add(_summary);
    }

    public override void Update()
    {
        _name.Text = PlayerName;
        _name.Color = PlayerGuid == _main.PlayerGUID
            ? new Color(255, 220, 150)
            : Color.White;
        var listedPlayer = GetListedPlayer();
        _onlineState.IsVisible =
            _kind != PlayerListWidget.ListKind.BlackList &&
            listedPlayer is not null;
        _onlineState.FillColor = listedPlayer?.IsOnline == true
            ? new Color(95, 190, 115, 230)
            : new Color(125, 130, 138, 190);
        _summary.Text = GetSummary(listedPlayer);
    }

    private string GetSummary(PlayerListEntry? listedPlayer)
    {
        if (_kind == PlayerListWidget.ListKind.BlackList)
        {
            return MultiplayerUiStyle.Text("Blocked");
        }

        if (listedPlayer is null)
        {
            return string.Empty;
        }

        if (_kind == PlayerListWidget.ListKind.SameTeamPlayers)
        {
            return _groupKey == listedPlayer.PlayerGuid.ToString()
                ? MultiplayerUiStyle.Text("Leader")
                : MultiplayerUiStyle.Text("Member");
        }

        if (listedPlayer.PlayerGuid == _main.PlayerGUID)
        {
            return string.Empty;
        }

        if (_groupKey.Length > 0 &&
            _main.SubsystemPlayers.ServerGroups.TryGetValue(_groupKey, out var group))
        {
            return group.Name;
        }

        return string.Empty;
    }

    private PlayerListEntry? GetListedPlayer()
    {
        return _item switch
        {
            PlayerData player when _main.SubsystemPlayers.PlayerList.TryGetValue(
                player.PlayerGUID,
                out var playerListEntry) => playerListEntry,
            PlayerData player => new PlayerListEntry(
                player.PlayerGUID,
                player.Name,
                true),
            PlayerListEntry player => player,
            _ => null
        };
    }
}
