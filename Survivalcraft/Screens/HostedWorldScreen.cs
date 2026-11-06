using System.Xml.Linq;

using Game.Authentication;
using Game.Network;

namespace Game.Screens;

/// <summary>A running GUI server remains available while its local player is absent or joining.</summary>
public sealed class HostedWorldScreen : Screen
{
    private readonly LabelWidget _status;

    private readonly ButtonWidget _joinButton;

    private readonly ButtonWidget _stopButton;

    private ClientAdmissionUi? _admission;

    private bool _joining;

    private string _failure = string.Empty;

    public HostedWorldScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/HostedWorldScreen"));
        _status = Children.Find<LabelWidget>("Status")!;
        _joinButton = Children.Find<ButtonWidget>("Join")!;
        _stopButton = Children.Find<ButtonWidget>("Stop")!;
    }

    private static string Text(string key) =>
        Localization.ClientLanguage.Catalog.GetContentWidgets(nameof(HostedWorldScreen), key);

    public override void Enter(object[] parameters)
    {
        _joining = false;
        _failure = parameters.OfType<string>().FirstOrDefault() ?? string.Empty;
    }

    public override void Leave()
    {
        _admission?.Dispose();
        _admission = null;
    }


    public override void Update()
    {
        if (!ReferenceEquals(ScreensManager.CurrentScreen, this)) { return; }

        var node = CommonLib.Net;
        if (!node.IsServer || GameManager.AuthorityProject is null)
        {
            if (GameManager.AuthorityProject is not null)
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
            return;
        }

        node.PumpLocalAdmission();
        _admission?.Update();
        if (_joining && node.AdmissionAccepted)
        {
            var verified = node.ConsumeLocalAdmission();
            _joining = false;
            var client = GameManager.Runtime!.ConnectLocalClient(
                verified.Subject,
                verified.Token,
                Network.Handling.ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true)
            );
            GameManager.ObserveEndpoint(client);
            ScreensManager.SwitchScreen("GameLoading", client.Runtime);
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

        _status.Text = string.Format(
                           Text("Running"),
                           GameManager.WorldInfo?.WorldSettings.Name ?? string.Empty,
                           node.Connections.PlayerCount
                       ) +
                       "\n" +
                       (_joining ? _admission?.Status : _failure.Length > 0 ? _failure : Text("NotJoined"));
        _joinButton.IsEnabled = !_joining && node.Self?.PlayerAddress is null;
        if (_joinButton.IsClicked) { Join(); }

        if (_stopButton.IsClicked || Input.Back || Input.Cancel) { ConfirmStop(); }
    }

    private void Join()
    {
        var node = CommonLib.Net;
        if (!node.IsServer || node.AdmissionPolicy is null || GameManager.WorldInfo is not { } world)
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
        var project = GameManager.AuthorityProject;
        DialogsManager.Confirm(
            Text("ConfirmStop"),
            button =>
            {
                if (button != MessageDialogButton.Button1 ||
                    ScreensManager.CurrentScreen != this ||
                    !ReferenceEquals(project, GameManager.AuthorityProject)) { return; }

                GameManager.CloseWorld();
                ScreensManager.SwitchScreen("Play");
            },
            this
        );
    }
}
