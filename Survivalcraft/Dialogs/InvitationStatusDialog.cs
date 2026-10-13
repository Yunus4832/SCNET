using Game.Network;

namespace Game.Dialogs;

public sealed class InvitationStatusDialog : MessageDialog
{
    private readonly NetNode _node;
    private readonly LabelWidget _status;
    private readonly ButtonWidget _copy;

    public InvitationStatusDialog(NetNode node) : base(InvitationStatusDialog.Text("StatusTitle"),
        InvitationStatusDialog.Text("Closed"), InvitationStatusDialog.Text("Copy"), LanguageManager.Get("Usual", "cancel"),
        button =>
        {
            if (button == MessageDialogButton.Button1 && node.InvitationCode is { } code)
            {
                ClipboardManager.ClipboardString = code;
            }
        })
    {
        _node = node;
        _status = Children.Find<LabelWidget>("MessageDialog.SmallLabel")!;
        _copy = Children.Find<ButtonWidget>("MessageDialog.Button1")!;
    }

    public static string Text(string key) => LanguageManager.GetContentWidgets(nameof(InvitationStatusDialog), key);

    public override void Update()
    {
        var code = _node.InvitationCode;
        _copy.IsEnabled = code is not null;
        _status.Text = code is not null
            ? string.Format(InvitationStatusDialog.Text("Code"), code) + "\n" + _node.InvitationControlAddress
            : _node.IsInvitationStarting && _node.InvitationError.Length > 0 ? InvitationStatusDialog.Text("Retrying")
            : _node.IsInvitationStarting ? InvitationStatusDialog.Text("Opening")
            : _node.InvitationError.Length > 0 ? _node.InvitationError
            : InvitationStatusDialog.Text("Closed");
        base.Update();
    }
}
