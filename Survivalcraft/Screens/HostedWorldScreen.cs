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

    private Task<(Game.Protocol.Identity.PlayerAddress Subject, Guid Token)>? _verified;

    private Task<NetNode>? _connection;

    private Task? _leaving;

    private CancellationTokenSource _joinCancellation = new();

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
        _joinCancellation.Dispose();
        _joinCancellation = new CancellationTokenSource();
        _joining = false;
        _verified = null;
        _connection = null;
        _leaving = null;
        _failure = parameters.OfType<string>().FirstOrDefault() ?? string.Empty;
    }

    public override void Leave()
    {
        _joinCancellation.Cancel();
        _admission?.Dispose();
        _admission = null;
    }

    public override void Update()
    {
        if (!ReferenceEquals(ScreensManager.CurrentScreen, this)) { return; }

        if (_leaving is { IsCompleted: false }) { return; }

        _leaving = null;

        var execution = GameManager.Runtime?.AuthorityExecution;
        var state = execution?.Status;
        if (state?.State != AuthorityExecutionState.Running || !state.Listening)
        {
            _leaving = StopAndReturnAsync();
            GameManager.ObserveApplicationTask(_leaving);
            return;
        }

        if (_joining && (Input.Back || Input.Cancel))
        {
            _joinCancellation.Cancel();
            _admission?.CancelJoin();
            _admission?.Dispose();
            _admission = null;
            _joining = false;
            GameManager.LocalPlayerReturnTarget = null;
            _failure = string.Empty;
            return;
        }

        _admission?.Update();
        if (_joining && _verified == null && _admission?.AdmissionAccepted == true)
        {
            _verified = _admission.ConsumeLocalAdmissionAsync();
        }

        if (_joining && _verified is { IsCompleted: true } verified && _connection == null)
        {
            try
            {
                var value = verified.GetAwaiter().GetResult();
                var generation = _joinCancellation;
                _connection = GameManager.Runtime!.ConnectLocalClientAsync(
                    value.Subject,
                    value.Token,
                    Network.Handling.ApplicationMessageHandlers.CreatePreWorld(NodeRole.Client, true),
                    client =>
                    {
                        if (!ReferenceEquals(_joinCancellation, generation)) { throw new OperationCanceledException(); }

                        generation.Token.ThrowIfCancellationRequested();
                        GameManager.ObserveEndpoint(client);
                        ScreensManager.SwitchScreen("GameLoading", client.Runtime);
                    }
                );
            }
            catch (Exception error) { JoinFailed(error); }
        }

        if (_connection is { IsFaulted: true } failed) { JoinFailed(failed.Exception!.GetBaseException()); }

        if (_joining && _admission?.LocalAdmissionError is { } rejection)
        {
            JoinFailed(new InvalidOperationException(Localization.ClientLanguage.Resolver.Resolve(rejection)));
        }

        _status.Text = string.Format(
                           Text("Running"),
                           GameManager.WorldInfo?.WorldSettings.Name ?? string.Empty,
                           state.PlayerCount
                       ) +
                       "\n" +
                       (_failure.Length > 0 ? _failure : Text("NotJoined"));
        _joinButton.IsEnabled = !_joining && GameManager.Runtime?.Client == null;
        if (_joinButton.IsClicked) { Join(); }

        if (_stopButton.IsClicked || Input.Back || Input.Cancel) { ConfirmStop(); }
    }

    private void JoinFailed(Exception error)
    {
        _joining = false;
        _connection = null;
        _verified = null;
        _failure = error.Message;
        GameManager.LocalPlayerReturnTarget = null;
        _admission?.Dispose();
        _admission = null;
    }

    private void Join()
    {
        if (GameManager.Runtime?.AuthorityExecution is not { } execution || GameManager.WorldInfo is not { } world)
        {
            return;
        }

        _joinCancellation.Dispose();
        _joinCancellation = new CancellationTokenSource();
        GameManager.LocalPlayerReturnTarget = LocalGameReturnTarget.HostedWorld;
        _admission?.Dispose();
        _admission = new ClientAdmissionUi(
            CommonLib.Net,
            this,
            AuthenticationJoinScope.ForWorld(world.DirectoryName),
            authority: execution
        );
        _verified = null;
        _connection = null;
        _joining = true;
        _failure = string.Empty;
        _admission.BeginLocalAdmission();
    }

    private void ConfirmStop()
    {
        var execution = GameManager.Runtime?.AuthorityExecution;
        DialogsManager.Confirm(
            Text("ConfirmStop"),
            button =>
            {
                if (button != MessageDialogButton.Button1 ||
                    ScreensManager.CurrentScreen != this ||
                    !ReferenceEquals(execution, GameManager.Runtime?.AuthorityExecution)) { return; }

                _leaving = StopAndReturnAsync();
                GameManager.ObserveApplicationTask(_leaving);
            },
            this
        );
    }

    private async Task StopAndReturnAsync()
    {
        try
        {
            await GameManager.CloseWorldAsync().ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => ScreensManager.SwitchScreen("Play")).ConfigureAwait(false);
        }
        finally { await Dispatcher.InvokeAsync(() => _leaving = null).ConfigureAwait(false); }
    }
}
