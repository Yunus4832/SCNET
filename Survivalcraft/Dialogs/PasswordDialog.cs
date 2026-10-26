using System.Xml.Linq;

namespace Game.Dialogs;

public sealed class PasswordDialog : Dialog
{
    private readonly TextBoxWidget _password;

    private readonly TextBoxWidget _repeat;

    private readonly ButtonWidget _confirm;

    private readonly ButtonWidget _cancel;

    private readonly bool _confirmPassword;

    private readonly int _minimumLength;

    private readonly Action<char[]?> _complete;

    private readonly CheckboxWidget _remember;

    public bool RememberPassword => _remember.IsChecked;

    private bool _closed;

    public PasswordDialog(
        string title,
        bool confirmPassword,
        Action<char[]?> complete,
        int minimumLength = 8,
        int maximumLength = 128,
        bool allowRemember = false
    )
    {
        if (minimumLength < 1 || maximumLength < minimumLength || maximumLength > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        _minimumLength = minimumLength;
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/PasswordDialog"));
        Children.Find<CanvasWidget>("Surface")!.Size = new Vector2(
            600,
            confirmPassword ? 360 : allowRemember ? 310 : 260
        );
        Children.Find<LabelWidget>("PasswordLabel")!.Text = string.Format(
            LanguageManager.GetContentWidgets(nameof(PasswordDialog), "PasswordMinimum"),
            minimumLength
        );
        Children.Find<LabelWidget>("Title")!.Text = title;
        _remember = Children.Find<CheckboxWidget>("Remember")!;
        _remember.IsVisible = allowRemember;
        _remember.IsChecked = false;
        _password = Children.Find<TextBoxWidget>("Password")!;
        _repeat = Children.Find<TextBoxWidget>("RepeatPassword")!;
        _password.MaximumLength = maximumLength;
        _repeat.MaximumLength = maximumLength;
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
        _confirm.IsEnabled = _password.Text.Length >= _minimumLength &&
                             (!_confirmPassword || _password.Text == _repeat.Text);
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
