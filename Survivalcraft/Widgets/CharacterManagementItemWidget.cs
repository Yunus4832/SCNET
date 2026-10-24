using Game.Network;

namespace Game.Widgets;

/// <summary>
///     Selectable player row used by the full player management panel.
/// </summary>
public sealed class CharacterManagementItemWidget : CanvasWidget
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

    private readonly CharacterData _main;

    private readonly object _item;

    private readonly CharacterListWidget.ListKind _kind;

    private Guid TeamId => CharacterTeamQuery.GetTeamId(_main.Project, CharacterId);

    public Guid CharacterId => _item switch
    {
        CharacterData player => player.CharacterId,
        CharacterListEntry player => player.CharacterId,
        _ => Guid.Empty
    };

    public string CharacterName => _item switch
    {
        CharacterData player => player.Name,
        CharacterListEntry player => player.Name,
        _ => string.Empty
    };

    public CharacterManagementItemWidget(
        object item,
        CharacterData main,
        CharacterListWidget.ListKind kind,
        float width
    )
    {
        if (item is not CharacterData and not CharacterListEntry)
        {
            throw new InvalidOperationException("Unsupported player management item.");
        }

        _item = item;
        _main = main;
        _kind = kind;
        Size = new Vector2(width, 52f);
        _name.Margin = new Vector2(
            32f,
            0f
        );
        Children.Add(_onlineState);
        Children.Add(_name);
        Children.Add(_summary);
    }

    public override void Update()
    {
        _name.Text = CharacterName;
        _name.Color = CharacterId == _main.CharacterId
            ? new Color(255, 220, 150)
            : Color.White;
        var listedPlayer = GetListedPlayer();
        _onlineState.IsVisible =
            listedPlayer is not null;
        _onlineState.FillColor = listedPlayer?.IsOnline == true
            ? new Color(95, 190, 115, 230)
            : new Color(125, 130, 138, 190);
        _summary.Text = GetSummary(listedPlayer);
    }

    private string GetSummary(CharacterListEntry? listedPlayer)
    {
        if (listedPlayer is null)
        {
            return string.Empty;
        }

        if (_kind == CharacterListWidget.ListKind.SameTeamCharacters)
        {
            return _main.Project.FindSubsystem<SubsystemTeams>(true)!.Store.Teams.TryGetValue(TeamId, out var team) &&
                   team.Leader == PlayerSessionQuery.GetController(CharacterId)
                ? MultiplayerUiStyle.Text("Leader")
                : MultiplayerUiStyle.Text("Member");
        }

        if (listedPlayer.CharacterId == _main.CharacterId)
        {
            return string.Empty;
        }

        if (TeamId != Guid.Empty &&
            _main.Project.FindSubsystem<SubsystemTeams>(true)!.Store.Teams.TryGetValue(TeamId, out var group))
        {
            return group.Name;
        }

        return string.Empty;
    }

    private CharacterListEntry? GetListedPlayer()
    {
        return _item switch
        {
            CharacterData player when _main.SubsystemCharacters.CharacterList.TryGetValue(
                player.CharacterId,
                out var playerListEntry
            ) => playerListEntry,
            CharacterData player => new CharacterListEntry(
                player.CharacterId,
                player.OwnerAddress,
                player.Name,
                true
            ),
            CharacterListEntry player => player,
            _ => null
        };
    }
}
