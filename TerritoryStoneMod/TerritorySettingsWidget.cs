using Engine.Core;

using Game;
using Game.Components;
using Game.Managers;
using Game.Widgets;

namespace TerritoryStoneMod;

public sealed class TerritorySettingsWidget : CanvasWidget, IGameInputCapturingWidget
{
    private readonly TerritorySubsystem _subsystem;
    private readonly ComponentPlayer _player;
    private readonly Guid _owner;
    private readonly Action _close;
    private readonly BevelledButtonWidget _closeButton;
    private readonly CheckboxWidget _show;
    private readonly CheckboxWidget _restrict;
    private readonly CheckboxWidget _team;
    private int _responseVersion;
    private bool _applyingResponse;

    public TerritorySettingsWidget(TerritorySubsystem subsystem, Territory territory, ComponentPlayer player,
        Action close)
    {
        _subsystem = subsystem;
        _player = player;
        _owner = territory.Owner;
        _responseVersion = subsystem.SettingsResponseVersion;
        _close = close;
        Size = new Vector2(614, 382);
        Children.Add(new BevelledRectangleWidget { Size = Size, BevelSize = 3 });
        Children.Add(new LabelWidget
        {
            Text = Text("Title"),
            Color = new Color(255, 255, 255, 192),
            Margin = new Vector2(28, 14)
        });
        var rows = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        Children.Add(rows);
        _show = CreateCheckbox("ShowBoundary", territory.ShowBoundary);
        _restrict = CreateCheckbox("RestrictEntry", territory.RestrictEntry);
        _team = CreateCheckbox("ApplyToTeam", territory.ApplyToTeam);
        rows.Children.Add(_show);
        rows.Children.Add(_restrict);
        rows.Children.Add(_team);
        void Apply(bool _)
        {
            if (!_applyingResponse)
            {
                subsystem.ChangeSettings(territory, _team.IsChecked, _show.IsChecked, _restrict.IsChecked);
            }
        }

        _show.CheckStatusChanged += Apply;
        _restrict.CheckStatusChanged += Apply;
        _team.CheckStatusChanged += Apply;
        _closeButton = new BevelledButtonWidget
        {
            Text = Text("Close"),
            Size = new Vector2(160, 54),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(0, 18)
        };
        Children.Add(_closeButton);
    }

    public override void Update()
    {
        if (_responseVersion != _subsystem.SettingsResponseVersion)
        {
            _responseVersion = _subsystem.SettingsResponseVersion;
            if (_subsystem.LastSettingsResponse is { } response && response.Owner == _owner)
            {
                _applyingResponse = true;
                _team.IsChecked = response.ApplyToTeam;
                _show.IsChecked = response.ShowBoundary;
                _restrict.IsChecked = response.RestrictEntry;
                _applyingResponse = false;
                if (!response.Accepted)
                {
                    _player.ComponentGui.DisplaySmallMessage(Text("OwnerOnly"), Color.Yellow, false, true);
                }
            }
        }

        if (_closeButton.IsClicked || Input.Cancel || Input.Back)
        {
            _close();
        }
    }

    private static CheckboxWidget CreateCheckbox(string key, bool value) => new()
    {
        Text = Text(key),
        Size = new Vector2(500, 48),
        CheckboxSize = new Vector2(36),
        Margin = new Vector2(0, 6),
        IsChecked = value
    };

    private static string Text(string key) => LanguageManager.Get("TerritorySettings", key);
}
