using Engine.Core;

using Game;
using Game.Commands;
using Game.Components;
using Game.Dialogs;
using Game.Managers;
using Game.Network;
using Game.Widgets;

namespace TerritoryStoneMod;

public sealed class TerritorySettingsWidget : CanvasWidget, IGameInputCapturingWidget
{
    private readonly TerritorySubsystem _subsystem;
    private readonly ComponentPlayer _player;
    private readonly Guid _owner;
    private readonly Action _close;
    private readonly BevelledButtonWidget _closeButton;
    private readonly BevelledButtonWidget _applyButton;
    private readonly LabelWidget _sizeLabel;
    private readonly SliderWidget _size;
    private readonly CheckboxWidget _entry;
    private readonly CheckboxWidget _exit;
    private readonly CheckboxWidget _digging;
    private readonly CheckboxWidget _placing;
    private readonly BevelledButtonWidget _settingsTab;
    private readonly BevelledButtonWidget _grantsTab;
    private readonly BevelledButtonWidget _editGrant;
    private readonly BevelledButtonWidget _revokeGrant;
    private readonly CanvasWidget _grantPage;
    private readonly ListPanelWidget _grantList;
    private readonly LabelWidget _emptyGrants;
    private KeyValuePair<Guid, TerritoryGrant>[] _grantSnapshot = [];
    private readonly CheckboxWidget _show;
    private readonly CanvasWidget _rows;
    private int _responseVersion;
    private int _pendingRequestId;
    private double? _pendingUntil;

    public TerritorySettingsWidget(TerritorySubsystem subsystem, Territory territory, ComponentPlayer player,
        Action close)
    {
        _subsystem = subsystem;
        _player = player;
        _owner = territory.Owner;
        _responseVersion = subsystem.SettingsResponseVersion;
        _close = close;
        Size = new Vector2(614, 382);
        ClampToBounds = true;
        Children.Add(new BevelledRectangleWidget { Size = Size, BevelSize = 3 });
        var tabs = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(0, 8)
        };
        _settingsTab = CreateButton(Text("SettingsTab"));
        _grantsTab = CreateButton(AccessText("ManageGrants"));
        tabs.Children.Add(_settingsTab);
        tabs.Children.Add(_grantsTab);
        Children.Add(tabs);
        var body = new CanvasWidget { Size = new Vector2(566, 234), ClampToBounds = true };
        Children.Add(body);
        SetWidgetPosition(body, new Vector2(24, 70));
        body.Children.Add(new BevelledRectangleWidget { BevelSize = -2, DirectionalLight = 0.15f });
        _rows = new CanvasWidget { Size = body.Size };
        body.Children.Add(_rows);
        _sizeLabel = new LabelWidget { Size = new Vector2(300, 28), FontScale = 0.8f, Ellipsis = true };
        _size = new SliderWidget
        {
            Size = new Vector2(542, 54),
            MinValue = TerritorySettings.MinimumSize,
            MaxValue = TerritorySettings.MaximumSize,
            Granularity = 1,
            LabelWidth = 0,
            IsLabelVisible = false
        };
        _rows.Children.Add(_sizeLabel);
        _rows.SetWidgetPosition(_sizeLabel, new Vector2(12, 10));
        _rows.Children.Add(_size);
        _rows.SetWidgetPosition(_size, new Vector2(12, 48));
        var permissions = new LabelWidget
        {
            Text = Text("VisitorPermissions"),
            Size = new Vector2(542, 22),
            FontScale = 0.75f,
            Color = new Color(255, 255, 255, 192)
        };
        _rows.Children.Add(permissions);
        _rows.SetWidgetPosition(permissions, new Vector2(12, 120));
        _entry = CreateCheckbox("AllowEntry");
        _exit = CreateCheckbox("AllowExit");
        _digging = CreateCheckbox("AllowDigging");
        _placing = CreateCheckbox("AllowPlacing");
        _show = CreateCheckbox("ShowBoundary");
        _show.Size = new Vector2(230, 40);
        _rows.Children.Add(_show);
        _rows.SetWidgetPosition(_show, new Vector2(324, 4));
        var separator = new RectangleWidget
        {
            Size = new Vector2(542, 1),
            FillColor = new Color(40, 40, 40, 40),
            OutlineColor = Color.Transparent
        };
        _rows.Children.Add(separator);
        _rows.SetWidgetPosition(separator, new Vector2(12, 110));
        var options = new[] { _entry, _exit, _digging, _placing };
        for (var index = 0; index < options.Length; index++)
        {
            _rows.Children.Add(options[index]);
            _rows.SetWidgetPosition(options[index], new Vector2(12 + index % 2 * 272, 146 + index / 2 * 44));
        }
        _grantPage = new CanvasWidget { Size = body.Size };
        body.Children.Add(_grantPage);
        _grantList = new ListPanelWidget
        {
            Name = "Territory.Grants",
            Direction = LayoutDirection.Vertical,
            ItemSize = 60,
            SelectionColor = new Color(10, 70, 0, 90),
            ItemWidgetFactory = CreateGrantRow
        };
        var grantListHost = new CanvasWidget { Size = new Vector2(554, 222), ClampToBounds = true };
        grantListHost.Children.Add(_grantList);
        _grantPage.Children.Add(grantListHost);
        _grantPage.SetWidgetPosition(grantListHost, new Vector2(6, 6));
        _emptyGrants = new LabelWidget
        {
            Text = AccessText("NoGrants"),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            FontScale = 0.85f
        };
        _grantPage.Children.Add(_emptyGrants);
        var actions = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(0, 16)
        };
        _applyButton = CreateButton(Text("Apply"));
        _editGrant = CreateButton(AccessText("EditAction"));
        _revokeGrant = CreateButton(AccessText("Revoke"));
        _revokeGrant.CenterColor = new Color(150, 50, 35);
        _revokeGrant.BevelColor = new Color(150, 50, 35);
        _closeButton = CreateButton(Text("Close"));
        actions.Children.Add(_applyButton);
        actions.Children.Add(_editGrant);
        actions.Children.Add(_revokeGrant);
        actions.Children.Add(_closeButton);
        Children.Add(actions);
        RefreshGrants(territory);
        ShowSettings(territory.Settings);
        ShowPage(false);
    }

    public override void Update()
    {
        if (!_subsystem.Store.TryGet(_owner, out var territory))
        {
            _close();
            return;
        }
        if (_settingsTab.IsClicked || _grantsTab.IsClicked)
        {
            ShowPage(_grantsTab.IsClicked);
        }
        RefreshGrants(territory!);
        var selected = _grantList.SelectedItem as KeyValuePair<Guid, TerritoryGrant>?;
        _editGrant.IsEnabled = selected.HasValue;
        _revokeGrant.IsEnabled = selected.HasValue;
        if (_editGrant.IsClicked && selected is { } edit)
        {
            EditGrant(edit);
        }
        if (_revokeGrant.IsClicked && selected is { } revoke)
        {
            DialogsManager.ShowDialog(_player.GameWidget.GuiWidget, new MessageDialog(
                AccessText("Revoke"), string.Format(AccessText("ConfirmRevoke"), revoke.Value.Name),
                AccessText("Revoke"), Text("Close"), response =>
                {
                    if (response == MessageDialogButton.Button1)
                    {
                        SetGrant(revoke.Key, TerritoryPermission.None);
                    }
                }));
        }
        if (_responseVersion != _subsystem.SettingsResponseVersion)
        {
            _responseVersion = _subsystem.SettingsResponseVersion;
            if (_subsystem.LastSettingsResponse is { } response && response.Owner == _owner &&
                _pendingUntil != null && response.RequestId == _pendingRequestId)
            {
                _pendingUntil = null;
                ShowSettings(response.Settings);
                if (!response.Accepted)
                {
                    Notify(response.Error);
                }
            }
        }
        if (_pendingUntil is { } deadline && Time.RealTime >= deadline)
        {
            _pendingUntil = null;
            ShowSettings(territory!.Settings);
            Notify("Unavailable");
        }
        _applyButton.IsEnabled = _pendingUntil == null;
        _rows.IsEnabled = _pendingUntil == null;
        if (_applyButton.IsClicked && _pendingUntil == null)
        {
            var settings = new TerritorySettings((int)_size.Value, _entry.IsChecked, _exit.IsChecked,
                _digging.IsChecked, _placing.IsChecked, _show.IsChecked);
            var error = _subsystem.ChangeSettings(territory!, settings);
            if (error != null)
            {
                ShowSettings(territory!.Settings);
                Notify(error);
            }
            else if (CommonLib.WorkType == WorkType.Client)
            {
                _pendingRequestId = _subsystem.SettingsRequestId;
                _pendingUntil = Time.RealTime + 10;
            }
        }
        _sizeLabel.Text = string.Format(Text("Size"), (int)_size.Value, (int)_size.Value * 2 + 1);
        if (_closeButton.IsClicked || Input.Cancel || Input.Back)
        {
            _close();
        }
    }

    private void ShowSettings(TerritorySettings settings)
    {
        _size.Value = settings.Size;
        _entry.IsChecked = settings.AllowEntry;
        _exit.IsChecked = settings.AllowExit;
        _digging.IsChecked = settings.AllowDigging;
        _placing.IsChecked = settings.AllowPlacing;
        _show.IsChecked = settings.ShowBoundary;
        _sizeLabel.Text = string.Format(Text("Size"), settings.Size, settings.Size * 2 + 1);
    }

    private void ShowPage(bool grants)
    {
        _settingsTab.IsChecked = !grants;
        _grantsTab.IsChecked = grants;
        _rows.IsVisible = !grants;
        _grantPage.IsVisible = grants;
        _applyButton.IsVisible = !grants;
        _editGrant.IsVisible = grants;
        _revokeGrant.IsVisible = grants;
        _editGrant.IsEnabled = _grantList.SelectedItem != null;
        _revokeGrant.IsEnabled = _grantList.SelectedItem != null;
    }

    private void RefreshGrants(Territory territory)
    {
        var grants = territory.Grants.OrderBy(grant => grant.Value.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(grant => grant.Key).ToArray();
        if (!grants.SequenceEqual(_grantSnapshot))
        {
            var selected = (_grantList.SelectedItem as KeyValuePair<Guid, TerritoryGrant>?)?.Key;
            var scroll = _grantList.ScrollPosition;
            _grantSnapshot = grants;
            _grantList.ClearItems();
            foreach (var grant in grants)
            {
                _grantList.AddItem(grant);
                if (grant.Key == selected)
                {
                    _grantList.SelectedItem = grant;
                }
            }
            _grantList.ScrollPosition = scroll;
        }
        _emptyGrants.IsVisible = grants.Length == 0;
        _grantList.IsVisible = grants.Length > 0;
    }

    private static Widget CreateGrantRow(object item)
    {
        var grant = ((KeyValuePair<Guid, TerritoryGrant>)item).Value;
        var row = new CanvasWidget { Size = new Vector2(554, 60) };
        var name = new LabelWidget
        {
            Text = grant.Name,
            Size = new Vector2(530, 26),
            FontScale = 0.85f,
            Ellipsis = true
        };
        var permissions = new LabelWidget
        {
            Text = string.Join(" · ", new[]
            {
                (TerritoryPermission.Enter, "AllowEntry"), (TerritoryPermission.Exit, "AllowExit"),
                (TerritoryPermission.Dig, "AllowDigging"), (TerritoryPermission.Place, "AllowPlacing")
            }.Where(option => (grant.Permissions & option.Item1) != 0).Select(option => Text(option.Item2))),
            Size = new Vector2(530, 24),
            FontScale = 0.65f,
            Ellipsis = true,
            Color = new Color(220, 220, 220)
        };
        row.Children.Add(name);
        row.SetWidgetPosition(name, new Vector2(12, 4));
        row.Children.Add(permissions);
        row.SetWidgetPosition(permissions, new Vector2(12, 30));
        return row;
    }

    private void EditGrant(KeyValuePair<Guid, TerritoryGrant> grant) =>
        DialogsManager.ShowDialog(_player.GameWidget.GuiWidget, new TerritoryPermissionsDialog(
            grant.Value.Name, grant.Value.Permissions, TerritoryPermission.All,
            selected => SetGrant(grant.Key, selected)));

    private void SetGrant(Guid player, TerritoryPermission permissions) =>
        CommandGateway.Submit(_player.PlayerData,
            new SetTerritoryGrantCommand(player, permissions.ToString().ToLowerInvariant().Replace(" ", "")));

    private static BevelledButtonWidget CreateButton(string text) => new()
    {
        Text = text,
        Size = new Vector2(180, 54),
        Margin = new Vector2(8, 0),
        FontScale = 0.85f
    };

    private static string AccessText(string key) => LanguageManager.Get("TerritoryAccess", key);

    private void Notify(string key) =>
        _player.ComponentGui.DisplaySmallMessage(Text(key), Color.Yellow, false, true);

    private static CheckboxWidget CreateCheckbox(string key)
    {
        var checkbox = new CheckboxWidget
        {
            Name = key,
            Text = Text(key),
            Size = new Vector2(262, 40),
            Margin = Vector2.Zero,
            CheckboxSize = new Vector2(32)
        };
        var label = checkbox.Children.Find<LabelWidget>("Checkbox.Label")!;
        label.FontScale = 0.8f;
        label.Ellipsis = true;
        return checkbox;
    }

    private static string Text(string key) => LanguageManager.Get("TerritorySettings", key);
}
