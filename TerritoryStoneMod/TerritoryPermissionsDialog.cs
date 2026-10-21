using System.Xml.Linq;

using Engine.Core;

using Game.Dialogs;
using Game.Managers;
using Game.Widgets;

namespace TerritoryStoneMod;

public sealed class TerritoryPermissionsDialog : Dialog
{
    private readonly Dictionary<TerritoryPermission, CheckboxWidget> _options = new();

    private readonly BevelledButtonWidget _confirm;

    private readonly BevelledButtonWidget _reject;

    private readonly Action<TerritoryPermission> _complete;

    public Guid RequestId { get; }

    public TerritoryPermissionsDialog(
        string playerName,
        TerritoryPermission selected,
        TerritoryPermission available,
        Action<TerritoryPermission> complete,
        Guid requestId = default
    )
    {
        _complete = complete;
        RequestId = requestId;
        Size = new Vector2(614, 300);
        HorizontalAlignment = WidgetAlignment.Center;
        VerticalAlignment = WidgetAlignment.Center;
        ClampToBounds = true;
        Children.Add(new BevelledRectangleWidget { Style = ContentManager.Get<XElement>("Styles/DialogArea") });
        Children.Add(
            new LabelWidget
            {
                Text = Text(requestId == Guid.Empty ? "EditGrant" : "RequestTitle"),
                HorizontalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(0, 18)
            }
        );
        var name = new LabelWidget { Text = playerName, Size = new Vector2(550, 32), Ellipsis = true };
        Children.Add(name);
        SetWidgetPosition(name, new Vector2(32, 58));
        var keys = new[] { "AllowEntry", "AllowExit", "AllowDigging", "AllowPlacing" };
        var flags = new[]
            { TerritoryPermission.Enter, TerritoryPermission.Exit, TerritoryPermission.Dig, TerritoryPermission.Place };
        for (var index = 0; index < flags.Length; index++)
        {
            var checkbox = new CheckboxWidget
            {
                Text = LanguageManager.Get("TerritorySettings", keys[index]),
                Size = new Vector2(270, 40),
                Margin = Vector2.Zero,
                CheckboxSize = new Vector2(32),
                IsChecked = (selected & flags[index]) != 0,
                IsEnabled = (available & flags[index]) != 0
            };
            checkbox.Children.Find<LabelWidget>("Checkbox.Label")!.FontScale = 0.8f;
            checkbox.Children.Find<LabelWidget>("Checkbox.Label")!.Ellipsis = true;
            _options.Add(flags[index], checkbox);
            Children.Add(checkbox);
            SetWidgetPosition(checkbox, new Vector2(32 + index % 2 * 280, 108 + index / 2 * 44));
        }

        _confirm = new BevelledButtonWidget
        {
            Text = Text("Confirm"),
            Size = new Vector2(180, 54)
        };
        _reject = new BevelledButtonWidget
        {
            Text = Text(requestId == Guid.Empty ? "Revoke" : "Reject"),
            Size = new Vector2(180, 54)
        };
        Children.Add(_confirm);
        Children.Add(_reject);
        SetWidgetPosition(_confirm, new Vector2(119, 224));
        SetWidgetPosition(_reject, new Vector2(315, 224));
    }

    public override void Update()
    {
        _confirm.IsEnabled = RequestId == Guid.Empty || _options.Values.Any(option => option.IsChecked);
        if (_confirm.IsClicked || _reject.IsClicked)
        {
            var permissions = TerritoryPermission.None;
            if (_confirm.IsClicked)
            {
                foreach (var (flag, checkbox) in _options)
                {
                    if (checkbox.IsEnabled && checkbox.IsChecked)
                    {
                        permissions |= flag;
                    }
                }
            }

            _complete(permissions);
            DialogsManager.HideDialog(this);
        }
        else if (Input.Cancel || Input.Back)
        {
            if (RequestId != Guid.Empty)
            {
                _complete(TerritoryPermission.None);
            }

            DialogsManager.HideDialog(this);
        }
    }

    private static string Text(string key) => LanguageManager.Get("TerritoryAccess", key);
}
