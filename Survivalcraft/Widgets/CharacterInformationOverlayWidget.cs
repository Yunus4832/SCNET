namespace Game.Widgets;

/// <summary>
///     Read-only filtered player list shown while playing.
///     Player and team operations belong to <see cref="MultiplayerPanelWidget" />.
/// </summary>
public sealed class CharacterInformationOverlayWidget : CanvasWidget
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

    private readonly CharacterData _characterData;

    private Guid CurrentTeamId =>
        CharacterTeamQuery.GetTeamId(_characterData.Project, _characterData.CharacterId);

    private Guid _lastTeamId;

    public readonly CharacterListWidget CharacterListWidget;

    public bool DisplayEnabled { get; private set; }

    public PlayerListFilter Filter { get; private set; }

    public CharacterInformationOverlayWidget(GameWidget gameWidget)
    {
        _characterData = gameWidget.CharacterData;
        var overlaySize = new Vector2(196f, 105f);
        DisplayEnabled = SettingsManager.Current.ShowCharacterInformationOverlay;
        Filter = SettingsManager.Current.CharacterInformationFilter;
        CharacterListWidget = new CharacterListWidget(
            _characterData,
            GetListKind(Filter),
            true,
            overlaySize
        );

        Size = overlaySize;
        ClampToBounds = true;
        IsHitTestVisible = false;
        Children.Add(_background);
        Children.Add(CharacterListWidget);
        _lastTeamId = CurrentTeamId;
        RefreshView();
    }

    public void SetDisplayEnabled(bool enabled)
    {
        DisplayEnabled = enabled;
        SettingsManager.Current.ShowCharacterInformationOverlay = enabled;
        RefreshView();
    }

    public void ToggleDisplay()
    {
        SetDisplayEnabled(!DisplayEnabled);
    }

    public void SetFilter(PlayerListFilter filter)
    {
        Filter = filter;
        SettingsManager.Current.CharacterInformationFilter = filter;
        RefreshView();
    }

    public void RefreshView()
    {
        IsVisible = DisplayEnabled;
        CharacterListWidget.Kind = GetListKind(Filter);
        if (IsVisible)
        {
            CharacterListWidget.RefreshList();
        }
    }

    public override void Update()
    {
        if (Filter != PlayerListFilter.SameTeam || _lastTeamId == CurrentTeamId)
        {
            return;
        }

        _lastTeamId = CurrentTeamId;
        RefreshView();
    }

    private static CharacterListWidget.ListKind GetListKind(PlayerListFilter filter)
    {
        return filter == PlayerListFilter.SameTeam
            ? CharacterListWidget.ListKind.SameTeamCharacters
            : CharacterListWidget.ListKind.Characters;
    }
}
