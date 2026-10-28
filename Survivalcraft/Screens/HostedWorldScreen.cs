using System.Xml.Linq;

using Game.Authentication;
using Game.Network;

namespace Game.Screens;

/// <summary>A running GUI server remains available while its local player is absent or joining.</summary>
public sealed class HostedWorldScreen : Screen
{
    private readonly LabelWidget _status;

    private readonly ActionPanelWidget _actions;

    private ClientAdmissionUi? _admission;

    private bool _joining;

    private string _failure = string.Empty;

    public HostedWorldScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/HostedWorldScreen"));
        _status = Children.Find<LabelWidget>("Status")!;
        _actions = Children.Find<ActionPanelWidget>("Actions")!;
        _actions.ItemTextProvider = item => Text((string)item);
        _actions.ItemEnabledProvider = item =>
            (string)item != "Join" || !_joining && CommonLib.Net.Self?.PlayerAddress is null;
        _actions.ItemColorProvider = item => (string)item == "Join" ? new Color(45, 110, 60) : new Color(150, 50, 35);
        _actions.SetPrimaryItems(["Join", "Stop"]);
        _actions.ItemClicked += item =>
        {
            if ((string)item == "Join") { Join(); }
            else { ConfirmStop(); }
        };
    }

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(HostedWorldScreen), key);

    public override void Enter(object[] parameters)
    {
        _joining = false;
        _failure = parameters.OfType<string>().FirstOrDefault() ?? string.Empty;
        CommonLib.Net.LocalPlayerRemoved += OnLocalPlayerRemoved;
    }

    public override void Leave()
    {
        CommonLib.Net.LocalPlayerRemoved -= OnLocalPlayerRemoved;
        _admission?.Dispose();
        _admission = null;
    }

    private void OnLocalPlayerRemoved(string reason)
    {
        if (ScreensManager.CurrentScreen != this) { return; }

        GameManager.LeaveLocalPlayer();
        _joining = false;
        _failure = reason;
        _admission?.Dispose();
        _admission = null;
    }

    public override void Update()
    {
        var node = CommonLib.Net;
        if (!node.IsServer || GameManager.Project is null)
        {
            if (GameManager.Project is not null)
            {
                GameManager.SaveProject(true, true);
                GameManager.DisposeProject();
            }

            GameManager.LocalPlayerReturnTarget = null;
            ScreensManager.SwitchScreen("Play");
            return;
        }

        if (_joining && (Input.Back || Input.Cancel))
        {
            node.EndAdmission();
            _admission?.Dispose();
            _admission = null;
            _joining = false;
            GameManager.LocalPlayerReturnTarget = null;
            _failure = string.Empty;
            GameManager.UpdateProject();
            return;
        }

        node.PumpLocalAdmission();
        _admission?.Update();
        if (_joining && node.AdmissionAccepted)
        {
            ScreensManager.SwitchScreen("Game");
            return;
        }

        if (_joining && node.LocalAdmissionError is { } error)
        {
            _joining = false;
            GameManager.LocalPlayerReturnTarget = null;
            _failure = error;
            _admission?.Dispose();
            _admission = null;
        }

        GameManager.UpdateProject();
        _status.Text = string.Format(
                           Text("Running"),
                           GameManager.WorldInfo?.WorldSettings.Name ?? string.Empty,
                           node.ClientCount
                       ) +
                       "\n" +
                       (_joining ? _admission?.Status : _failure.Length > 0 ? _failure : Text("NotJoined"));
        _actions.Refresh();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked) { ConfirmStop(); }
    }

    private void Join()
    {
        var node = CommonLib.Net;
        if (!node.IsServer || node.AdmissionController is not { } controller || GameManager.WorldInfo is not { } world)
        {
            return;
        }

        GameManager.LocalPlayerReturnTarget = LocalGameReturnTarget.HostedWorld;
        _admission?.Dispose();
        _admission = new ClientAdmissionUi(node, this, AuthenticationJoinScope.ForWorld(world.DirectoryName));
        _failure = string.Empty;
        _joining = true;
        node.BeginLocalAdmission();
    }

    private void ConfirmStop()
    {
        var project = GameManager.Project;
        DialogsManager.Confirm(
            Text("ConfirmStop"),
            button =>
            {
                if (button != MessageDialogButton.Button1 ||
                    ScreensManager.CurrentScreen != this ||
                    !ReferenceEquals(project, GameManager.Project)) { return; }

                GameManager.CloseWorld();
                ScreensManager.SwitchScreen("Play");
            },
            this
        );
    }
}
