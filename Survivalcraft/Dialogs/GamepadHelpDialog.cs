using System.Xml.Linq;

using Engine.Input;

namespace Game.Dialogs;

public class GamepadHelpDialog : Dialog
{
    private readonly ButtonWidget _helpButton;
    private readonly ButtonWidget _okButton;

    public GamepadHelpDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/GamepadHelpDialog");
        LoadContents(this, node);
        _okButton = Children.Find<ButtonWidget>("OkButton")!;
        _helpButton = Children.Find<ButtonWidget>("HelpButton")!;
    }

    public override void Update()
    {
        _helpButton.IsVisible = ScreensManager.CurrentScreen is not HelpScreen;
        if (_okButton.IsClicked || Input.Cancel || Input.IsPadButtonDownOnce(GamePadButton.Start))
        {
            DialogsManager.HideDialog(this);
        }

        if (!_helpButton.IsClicked)
        {
            return;
        }

        DialogsManager.HideDialog(this);
        ScreensManager.SwitchScreen("Help");
    }
}
