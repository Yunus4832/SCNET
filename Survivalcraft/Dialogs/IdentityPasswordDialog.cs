using System.Xml.Linq;

namespace Game.Dialogs;

public sealed class IdentityPasswordDialog : Dialog
{
    private readonly TextBoxWidget _password;

    private readonly TextBoxWidget _repeat;

    private readonly ButtonWidget _confirm;

    private readonly ButtonWidget _cancel;

    private readonly bool _confirmPassword;

    private readonly Action<char[]?> _complete;

    private bool _closed;

    public IdentityPasswordDialog(string title, bool confirmPassword, Action<char[]?> complete)
    {
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/IdentityPasswordDialog"));
        Children.Find<LabelWidget>("Title")!.Text = title;
        _password = Children.Find<TextBoxWidget>("Password")!;
        _repeat = Children.Find<TextBoxWidget>("RepeatPassword")!;
        _confirm = Children.Find<ButtonWidget>("Confirm")!;
        _cancel = Children.Find<ButtonWidget>("Cancel")!;
        _confirm.Text = LanguageManager.Get("Usual", "ok");
        _cancel.Text = LanguageManager.Get("Usual", "cancel");
        _confirmPassword = confirmPassword;
        _complete = complete;
        Children.Find<StackPanelWidget>("Confirmation")!.IsVisible = confirmPassword;
        _confirm.IsEnabled = false;
        _password.HasFocus = true;
    }

    public override void Update()
    {
        _confirm.IsEnabled = _password.Text.Length >= 8 && (!_confirmPassword || _password.Text == _repeat.Text);
        if (Input.Back || Input.Cancel || _cancel.IsClicked) { Complete(false); }
        else if (_confirm.IsEnabled && (_confirm.IsClicked || Input.Ok)) { Complete(true); }
    }

    public void Cancel()
    {
        Complete(false);
    }

    private void Complete(bool accepted)
    {
        if (_closed) { return; }

        _closed = true;
        var password = accepted ? _password.Text.ToCharArray() : null;
        Clear();
        DialogsManager.HideDialog(this);
        _complete(password);
    }

    private void Clear()
    {
        _password.HasFocus = false;
        _repeat.HasFocus = false;
        _password.Text = string.Empty;
        _repeat.Text = string.Empty;
    }

    public override void Dispose()
    {
        Clear();
        base.Dispose();
    }
}
