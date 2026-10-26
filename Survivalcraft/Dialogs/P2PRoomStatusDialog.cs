using Game.Commands;
using Game.Network;
using Game.Servers;

namespace Game.Dialogs;

public sealed class P2PRoomStatusDialog : MessageDialog
{
    private readonly NetNode _node;

    private readonly LabelWidget _status;

    private readonly ButtonWidget _copy;

    public P2PRoomStatusDialog(NetNode node) : base(
        Text("StatusTitle"),
        Text("Closed"),
        Text("Copy"),
        LanguageManager.Get("Usual", "cancel"),
        button =>
        {
            if (button == MessageDialogButton.Button1 && node.GetP2PStatus().JoinCode is { } code)
            {
                ClipboardManager.ClipboardString = code;
            }
        }
    )
    {
        _node = node;
        _status = Children.Find<LabelWidget>("MessageDialog.SmallLabel")!;
        _copy = Children.Find<ButtonWidget>("MessageDialog.Button1")!;
    }

    public static string Text(string key) => LanguageManager.GetContentWidgets(nameof(P2PRoomStatusDialog), key);

    public override void Update()
    {
        var room = _node.GetP2PStatus();
        _copy.IsEnabled = room.JoinCode is not null;
        _status.Text = room.JoinCode is { } code
            ? string.Format(Text("Code"), code) + "\n" + room.ControlUrl
            : room.State is P2PRoomState.RetryWaiting
                ? Text("Retrying") + "\n" + room.LastError
                : room.State is P2PRoomState.Registering
                    ? Text("Opening")
                    : room.State is P2PRoomState.Stopping
                        ? CommandText.Get("P2PStopping_Message", "P2P 房间正在停止，请稍后再试。")
                        : room.LastError.Length > 0
                            ? room.LastError
                            : Text("Closed");
        base.Update();
    }
}
