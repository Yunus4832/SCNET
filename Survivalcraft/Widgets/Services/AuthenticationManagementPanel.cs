using System.Xml.Linq;

using Game.Authentication;
using Game.Identity;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;

namespace Game.Widgets.Services;

public sealed class AuthenticationManagementPanel : ServicePanelWidget
{
    private enum ServiceAction
    {
        Add = 0,

        Login = 1,

        Info = 2,

        Toggle = 3,

        Address = 4,

        Logout = 5,

        Remove = 6
    }

    private sealed record BrowserRequest(Uri Url, CancellationToken Token, TaskCompletionSource Completion);

    private readonly AuthenticationManager _manager;

    private readonly Func<string> _currentSubject;

    private readonly ActionPanelWidget _actions;

    private readonly ListPanelWidget _list;

    private readonly Lock _browserGate = new();

    private BrowserRequest? _browser;

    private CancellationTokenSource? _cancel;

    private Task? _operation;

    private Action? _finish;

    private CancellableBusyDialog? _busy;

    private TextBoxDialog? _editor;

    private MessageDialog? _message;

    private int _generation;

    private int _operationGeneration;

    private int _languageRevision = -1;

    public AuthenticationManagementPanel() : this(AuthenticationManager.Current)
    {
    }

    public AuthenticationManagementPanel(AuthenticationManager manager, Func<string>? currentSubject = null)
    {
        _manager = manager;
        _currentSubject = currentSubject ??
                          (() => LocalIdentityManager.Current.GetSelectedAddress(IdentityPurpose.Player)!);
        LoadContents(this, ContentManager.Get<XElement>("Widgets/Services/AuthenticationManagementPanel"));
        _actions = Children.Find<ActionPanelWidget>("Actions")!;
        _list = Children.Find<ListPanelWidget>("Services")!;
        _list.ItemWidgetFactory = CreateRow;
        _actions.ItemTextProvider = action => action is ServiceAction.Toggle
            ? Text(Selected?.Enabled == true ? "Disable" : "Enable")
            : Text(action.ToString()!);
        _actions.ItemEnabledProvider = IsActionEnabled;
        _actions.ItemColorProvider = action => action is ServiceAction.Remove ? new Color(150, 50, 35) :
            action is ServiceAction.Login ? new Color(50, 150, 35) : null;
        _actions.ItemClicked += Execute;
        _actions.SetPrimaryItems([ServiceAction.Add, ServiceAction.Login, ServiceAction.Info], [1f, 1f, 1f, 0f]);
        _actions.SetSecondaryItems(
            [ServiceAction.Toggle, ServiceAction.Address, ServiceAction.Logout, ServiceAction.Remove]
        );
        Refresh();
    }

    private InstalledAuthenticationService? Selected => _list.SelectedItem as InstalledAuthenticationService;

    private string Subject => _currentSubject();

    public override void Activate()
    {
        _generation++;
        Refresh();
    }

    public override void Deactivate()
    {
        _generation++;
        _cancel?.Cancel();
        lock (_browserGate)
        {
            _browser?.Completion.TrySetCanceled();
            _browser = null;
        }

        if (_busy is not null)
        {
            DialogsManager.HideDialog(_busy);
            _busy = null;
        }

        if (_editor is not null)
        {
            DialogsManager.HideDialog(_editor);
            _editor = null;
        }

        if (_message is not null)
        {
            DialogsManager.HideDialog(_message);
            _message = null;
        }

        _operation = null;
        _finish = null;
        _cancel?.Dispose();
        _cancel = null;
    }

    public override void Update()
    {
        if (_languageRevision != LanguageManager.Revision) { Refresh(Selected?.Endpoint.Identity.Address); }

        lock (_browserGate)
        {
            if (_browser is { } request)
            {
                _browser = null;
                if (request.Token.IsCancellationRequested) { request.Completion.TrySetCanceled(request.Token); }
                else if (WebBrowserManager.LaunchBrowser(request.Url.AbsoluteUri))
                {
                    request.Completion.TrySetResult();
                }
                else
                {
                    request.Completion.TrySetException(
                        new AuthenticationProtocolException("Browser launch unavailable.")
                    );
                }
            }
        }

        if (_operation is { IsCompleted: true } task)
        {
            var finish = _finish;
            _operation = null;
            _finish = null;
            var cancelled = _cancel!.IsCancellationRequested || _operationGeneration != _generation;
            _cancel.Dispose();
            _cancel = null;
            if (_busy is not null)
            {
                DialogsManager.HideDialog(_busy);
                _busy = null;
            }

            if (!cancelled)
            {
                if (task.IsCompletedSuccessfully)
                {
                    try { finish?.Invoke(); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                                      or InvalidOperationException)
                    {
                        Notify(Text("SaveFailed"));
                    }
                }
                else { Notify(Error(task.Exception?.GetBaseException())); }
            }

            Refresh(Selected?.Endpoint.Identity.Address);
        }

        _actions.Refresh();
        _actions.IsEnabled = _operation is null;
    }

    private Widget CreateRow(object item)
    {
        var service = (InstalledAuthenticationService)item;
        var row = (ContainerWidget)LoadWidget(null, ContentManager.Get<XElement>("Widgets/ServerSourceItem"), null);
        row.Children.Find<LabelWidget>("ServerSourceItem.Name")!.Text = service.Endpoint.Description.DisplayName;
        row.Children.Find<LabelWidget>("ServerSourceItem.Address")!.Text = service.Endpoint.BaseUri.AbsoluteUri;
        row.Children.Find<LabelWidget>("ServerSourceItem.State")!.Text = Text(service.Enabled ? "Enabled" : "Disabled");
        return row;
    }

    private void Refresh(string? selected = null)
    {
        _languageRevision = LanguageManager.Revision;
        _list.ClearItems();
        foreach (var service in _manager.Services.Services) { _list.AddItem(service); }

        _list.SelectedItem = selected is null ? null : _manager.Services.Find(selected);
        Children.Find<LabelWidget>("Empty")!.IsVisible = _manager.Services.Services.Count == 0;
    }

    private bool IsActionEnabled(object action)
    {
        if (_operation is not null) { return false; }

        if (action is ServiceAction.Add) { return true; }

        if (Selected is not { } selected) { return false; }

        return action switch
        {
            ServiceAction.Login => selected.Enabled,
            ServiceAction.Logout => _manager.Grants.Find(Subject, selected.Endpoint) is not null,
            _ => true
        };
    }

    private void Execute(object value)
    {
        var action = (ServiceAction)value;
        if (!IsActionEnabled(action)) { return; }

        var selected = Selected;
        switch (action)
        {
            case ServiceAction.Add: EditAddress(null); break;
            case ServiceAction.Address: EditAddress(selected); break;
            case ServiceAction.Login:
                Run(
                    async token =>
                    {
                        using var key = AuthenticationUserKey.Open();
                        var client = new AuthenticationLoginClient(_manager.Grants);
                        return await client.AuthenticateAsync(key, selected!.Endpoint, null, true, OpenBrowser, token);
                    },
                    result =>
                    {
                        if (result.Grant is not { } grant || grant.Subject != Subject)
                        {
                            Notify(Text("IdentityChanged"));
                            return;
                        }

                        _manager.Grants.Set(grant.Subject, selected!.Endpoint, grant.Secret, grant.ExpiresAt);
                        Notify(Text("LoggedIn"));
                    }
                );
                break;
            case ServiceAction.Logout:
                Run(
                    async token =>
                    {
                        using var key = AuthenticationUserKey.Open();
                        return await _manager.LogoutAsync(key, selected!, token);
                    },
                    revoked => Notify(Text(revoked ? "LoggedOut" : "LocalLogoutOnly"))
                );
                break;
            case ServiceAction.Toggle:
                try
                {
                    _manager.Services.SetEnabled(selected!.Endpoint.Identity.Address, !selected.Enabled);
                    Refresh(selected.Endpoint.Identity.Address);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                                  or InvalidOperationException)
                {
                    Notify(Text("SaveFailed"));
                }

                break;
            case ServiceAction.Remove:
                Confirm(
                    Text("RemoveConfirm"),
                    () =>
                    {
                        _manager.RemoveService(selected!.Endpoint.Identity.Address);
                        Refresh();
                    }
                );
                break;
            case ServiceAction.Info:
                var cached = _manager.Grants.Find(Subject, selected!.Endpoint);
                Notify(
                    selected.Endpoint.Description.DisplayName +
                    "\n" +
                    selected.Endpoint.BaseUri.AbsoluteUri +
                    "\n" +
                    Text("ServiceKey") +
                    ": " +
                    selected.Endpoint.Identity.Address +
                    "\n" +
                    Text("PlayerIdentity") +
                    ": " +
                    Subject +
                    "\n" +
                    Text("Source") +
                    ": " +
                    (selected.Source.Length == 0 ? Text("ManualSource") : selected.Source) +
                    "\n" +
                    (cached is null
                        ? Text("NotLoggedIn")
                        : DateTimeOffset.FromUnixTimeSeconds(cached.ExpiresAt).ToString("O"))
                );
                break;
        }
    }

    private void EditAddress(InstalledAuthenticationService? current)
    {
        var generation = _generation;
        _editor = new TextBoxDialog(
            Text(current is null ? "AddAddress" : "UpdateAddress"),
            current?.Endpoint.BaseUri.AbsoluteUri ?? "https://",
            2048,
            address =>
            {
                _editor = null;
                if (generation != _generation || string.IsNullOrWhiteSpace(address)) { return; }

                Run(
                    token => AuthenticationHttpClient.DiscoverAsync(address.Trim(), cancellationToken: token),
                    endpoint =>
                    {
                        if (current is not null && endpoint.Identity.Address != current.Endpoint.Identity.Address)
                        {
                            Notify(Text("KeyConflict"));
                            return;
                        }

                        Confirm(
                            endpoint.Description.DisplayName +
                            "\n" +
                            endpoint.BaseUri.AbsoluteUri +
                            "\n" +
                            IdentityAddressDisplay.Abbreviate(endpoint.Identity.Address) +
                            "\n" +
                            Text(current is null ? "InstallOnly" : "AddressWarning"),
                            () =>
                            {
                                if (current is null) { _manager.Services.AddConfirmed(endpoint); }
                                else
                                {
                                    _manager.Grants.RemoveAuthority(current.Endpoint.Identity.Address);
                                    _manager.Services.UpdateConfirmedEndpoint(
                                        current.Endpoint.Identity.Address,
                                        endpoint
                                    );
                                }

                                Refresh(endpoint.Identity.Address);
                            }
                        );
                    }
                );
            }
        );
        DialogsManager.ShowDialog(null, _editor);
    }

    private void Run<T>(Func<CancellationToken, Task<T>> operation, Action<T> finish)
    {
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _operationGeneration = _generation;
        var task = Task.Run(() => operation(token), token);
        _operation = task;
        _finish = () => finish(task.GetAwaiter().GetResult());
        _ = task.ContinueWith(failed => { _ = failed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _busy = new CancellableBusyDialog(Text("Working"), false);
        _busy.CancelRequested += () => _cancel?.Cancel();
        DialogsManager.ShowDialog(null, _busy);
    }

    private async Task OpenBrowser(Uri uri, CancellationToken token)
    {
        var request = new BrowserRequest(
            uri,
            token,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        );
        lock (_browserGate)
        {
            token.ThrowIfCancellationRequested();
            _browser?.Completion.TrySetCanceled();
            _browser = request;
        }

        await request.Completion.Task.WaitAsync(token);
    }

    private void Confirm(string description, Action accepted)
    {
        var generation = _generation;
        _message = new MessageDialog(
            Text("Title"),
            description,
            LanguageManager.Get("Usual", "ok"),
            LanguageManager.Get("Usual", "cancel"),
            button =>
            {
                _message = null;
                if (generation != _generation || button != MessageDialogButton.Button1) { return; }

                try { accepted(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                                  or InvalidOperationException)
                {
                    Notify(Text("SaveFailed"));
                }
            }
        );
        DialogsManager.ShowDialog(null, _message);
    }

    private void Notify(string text)
    {
        _message = new MessageDialog(
            Text("Title"),
            text,
            LanguageManager.Get("Usual", "ok"),
            string.Empty,
            _ => _message = null
        );
        DialogsManager.ShowDialog(null, _message);
    }

    private static string Error(Exception? error)
    {
        return Text(
            error is AuthenticationServiceIdentityException ? "KeyConflict" :
            error is AuthenticationRemoteException remote &&
            remote.Error == SCNET.Authentication.Protocol.Contracts.AuthenticationError.SubjectBanned ? "Banned" :
            "Unavailable"
        );
    }

    private static string Text(string key) =>
        LanguageManager.GetContentWidgets(nameof(AuthenticationManagementPanel), key);
}
