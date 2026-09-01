namespace Game.Widgets;

/// <summary>
///     Read-only filtered player list shown while playing.
///     Player, team and blacklist operations belong to <see cref="PlayerPanelWidget" />.
/// </summary>
public sealed class PlayerInformationOverlayWidget : CanvasWidget
{
    private readonly BevelledRectangleWidget _background = new()
    {
        CenterColor = new Color(0, 0, 0, 88),
        BevelColor = Color.Transparent,
        BevelSize = 0f,
        RoundingRadius = 8f,
        ShadowColor = Color.Transparent,
        ShadowSize = 0f
    };

    private readonly PlayerData _playerData;

    private string _lastGroupKey;

    public readonly PlayerListWidget PlayerListWidget;

    public bool DisplayEnabled { get; private set; }

    public PlayerListFilter Filter { get; private set; }

    public PlayerInformationOverlayWidget(GameWidget gameWidget)
    {
        _playerData = gameWidget.PlayerData;
        var overlaySize = new Vector2(196f, 105f);
        DisplayEnabled = SettingsManager.Current.ShowPlayerInformationOverlay;
        Filter = SettingsManager.Current.PlayerInformationFilter;
        PlayerListWidget = new PlayerListWidget(
            _playerData,
            GetListKind(Filter),
            true,
            overlaySize);

        Size = overlaySize;
        ClampToBounds = true;
        IsHitTestVisible = false;
        Children.Add(_background);
        Children.Add(PlayerListWidget);
        _lastGroupKey = _playerData.GroupKey;
        RefreshView();
    }

    public void SetDisplayEnabled(bool enabled)
    {
        DisplayEnabled = enabled;
        SettingsManager.Current.ShowPlayerInformationOverlay = enabled;
        RefreshView();
    }

    public void ToggleDisplay()
    {
        SetDisplayEnabled(!DisplayEnabled);
    }

    public void SetFilter(PlayerListFilter filter)
    {
        Filter = filter;
        SettingsManager.Current.PlayerInformationFilter = filter;
        RefreshView();
    }

    public void RefreshView()
    {
        IsVisible = DisplayEnabled;
        PlayerListWidget.Kind = GetListKind(Filter);
        if (IsVisible)
        {
            PlayerListWidget.RefreshList();
        }
    }

    public override void Update()
    {
        if (Filter != PlayerListFilter.SameTeam || _lastGroupKey == _playerData.GroupKey)
        {
            return;
        }

        _lastGroupKey = _playerData.GroupKey;
        RefreshView();
    }

    private static PlayerListWidget.ListKind GetListKind(PlayerListFilter filter)
    {
        return filter == PlayerListFilter.SameTeam
            ? PlayerListWidget.ListKind.SameTeamPlayers
            : PlayerListWidget.ListKind.Players;
    }
}
