namespace Game.Widgets;

public sealed class TeamListWidget : CanvasWidget
{
    public sealed record TeamItem(
        Guid TeamId,
        string Name,
        int MemberCount,
        bool IsCurrent
    );

    public readonly ListPanelWidget Teams = new()
    {
        Direction = LayoutDirection.Vertical,
        ItemSize = 52f,
        SelectionColor = MultiplayerUiStyle.ListSelectionColor
    };

    private readonly CharacterData _characterData;

    private Guid CurrentTeamId =>
        CharacterTeamQuery.GetTeamId(_characterData.Project, _characterData.CharacterId);

    public TeamItem? SelectedTeam => Teams.SelectedItem as TeamItem;

    public TeamListWidget(CharacterData characterData, Vector2 size)
    {
        _characterData = characterData;
        Size = size;
        var itemWidth = MathUtils.Max(size.X - 32f, 0f);
        Teams.HorizontalAlignment = WidgetAlignment.Center;
        Teams.ItemWidgetFactory = item => new TeamItemWidget((TeamItem)item, itemWidth);
        Children.Add(Teams);
        RefreshList();
    }

    public void RefreshList()
    {
        var selectedTeamId = SelectedTeam?.TeamId;
        if (selectedTeamId is null && CurrentTeamId != Guid.Empty)
        {
            selectedTeamId = CurrentTeamId;
        }

        Teams.ClearItems();
        foreach (var pair in _characterData.Project.FindSubsystem<SubsystemTeams>(true)!.Store.Teams
                     .OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase))
        {
            Teams.AddItem(
                new TeamItem(
                    pair.Key,
                    pair.Value.Name,
                    pair.Value.Members.Count,
                    pair.Key == CurrentTeamId
                )
            );
        }

        if (selectedTeamId is null)
        {
            return;
        }

        Teams.SelectedItem = Teams.Items
            .OfType<TeamItem>()
            .FirstOrDefault(team => team.TeamId == selectedTeamId);
    }

    private sealed class TeamItemWidget : CanvasWidget
    {
        private readonly TeamItem _team;

        private readonly LabelWidget _name = new()
        {
            FontScale = 0.9f,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(14f, 0f)
        };

        private readonly LabelWidget _summary = new()
        {
            FontScale = 0.72f,
            Color = new Color(225, 225, 225, 190),
            HorizontalAlignment = WidgetAlignment.Far,
            VerticalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(24f, 0f)
        };

        public TeamItemWidget(TeamItem team, float width)
        {
            _team = team;
            Size = new Vector2(width, 52f);
            Children.Add(_name);
            Children.Add(_summary);
        }

        public override void Update()
        {
            _name.Text = _team.Name;
            _name.Color = _team.IsCurrent
                ? new Color(255, 220, 150)
                : Color.White;
            _summary.Text = string.Format(
                MultiplayerUiStyle.Text("TeamMemberCount"),
                _team.MemberCount
            );
        }
    }
}
