using System.Security.Cryptography;

using Game.Identity;
using Game.Network;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

/// <summary>Loading-screen admission interaction. Network tasks never mutate widgets or publish trust.</summary>
internal sealed class ClientAdmissionUi : IDisposable
{
    private sealed class BrowserLaunchFailure : Exception;

    private sealed record BrowserRequest(Uri Url, TaskCompletionSource Completion);

    private readonly NetNode _node;

    private readonly Screen _owner;

    private readonly string _scope;

    private TemporaryJoinAuthorization? _temporary;

    private readonly CancellationTokenSource _cancel = new();

    private AdmissionChallengePackage? _challenge;

    private AuthenticationServiceEndpoint? _endpoint;

    private Task<AuthenticationLoginResult>? _authorization;

    private AdmissionChallenge? _authorizationChallenge;

    private bool _browserOpened;

    private bool _detached;

    private TemporaryJoinAuthorization? _reconnectAuthorization;

    private TemporaryJoinAuthorization? _completedAuthorization;

    internal bool IsWaitingForDetachedAuthentication => _detached;

    internal string? DetachedFailure { get; private set; }

    internal TemporaryJoinAuthorization? TakeReconnectAuthorization()
    {
        var result = _reconnectAuthorization;
        _reconnectAuthorization = null;
        return result;
    }

    private bool PreserveExternalAuthentication()
    {
        Log.Debug(
            $"External admission interruption: browser={_browserOpened}, pending={_authorization is not null}, completed={_completedAuthorization is not null}, cancelled={_cancel.IsCancellationRequested}, disposed={_disposed}."
        );
        if (_disposed ||
            !_scope.StartsWith("remote:", StringComparison.Ordinal) ||
            !_browserOpened ||
            _authorization is null && _completedAuthorization?.ValidGrant(DateTimeOffset.UtcNow) is null ||
            _authorizationChallenge is null ||
            _cancel.IsCancellationRequested) { return false; }

        _detached = true;
        Log.Information("Preserved external authentication after transport interruption.");
        if (_authorization is null) { _reconnectAuthorization = _completedAuthorization; }

        return true;
    }

    private void Fail(string message)
    {
        if (_detached) { DetachedFailure = message; }
        else { _node.EndAdmission(message); }
    }

    private BrowserRequest? _browserRequest;

    private PasswordDialog? _passwordDialog;

    private AuthenticationConsentDialog? _consentDialog;

    private bool _save;

    private bool _disposed;

    private bool _passwordPending;

    private bool _incorrectPassword;

    private string? _passwordServer;

    private string? _submittedPassword;

    private bool _rememberPassword;

    private DateTimeOffset _retryAt;

    private string _statusKey = string.Empty;

    internal ClientAdmissionUi(NetNode node, Screen owner, string scope, TemporaryJoinAuthorization? temporary = null)
    {
        _node = node;
        _owner = owner;
        _scope = scope;
        _temporary = temporary;
        node.AdmissionTransportInterrupted += PreserveExternalAuthentication;
        node.AdmissionRequired += Begin;
        node.AdmissionResultReceived += Receive;
        if (node.PendingAdmissionChallenge is { } challenge && challenge.Mode != ServerAdmissionMode.Open)
        {
            Begin(challenge);
        }
    }

    public string Status => _statusKey.Length == 0 ? string.Empty : Text(_statusKey);

    public void Update()
    {
        if (_disposed || !_detached && !_node.HasPendingAdmission) { return; }

        if (!_detached &&
            _challenge is not null &&
            !_node.CanSubmitAdmission(_challenge.Nonce) &&
            !PreserveExternalAuthentication())
        {
            _cancel.Cancel();
            return;
        }

        if (Interlocked.Exchange(ref _browserRequest, null) is { } browser)
        {
            if (_cancel.IsCancellationRequested) { browser.Completion.TrySetCanceled(); }
            else if (WebBrowserManager.LaunchBrowser(browser.Url.AbsoluteUri))
            {
                _browserOpened = true;
                Log.Information("Opened external authentication browser.");
                _statusKey = "WaitBrowser";
                browser.Completion.TrySetResult();
            }
            else
            {
                browser.Completion.TrySetException(new BrowserLaunchFailure());
            }
        }

        if (_passwordPending && DateTimeOffset.UtcNow >= _retryAt && _passwordDialog is null) { AskPassword(); }

        if (_authorization is not { IsCompleted: true } task) { return; }

        _authorization = null;
        if (_cancel.IsCancellationRequested ||
            !_detached && _node.PendingAdmissionChallenge?.Nonce != _challenge?.Nonce) { return; }

        if (!task.IsCompletedSuccessfully)
        {
            var error = task.IsCanceled ? new TaskCanceledException() : task.Exception?.GetBaseException();
            Log.Warning($"External authentication failed ({error?.GetType().Name ?? "unknown"}).");
            Fail(
                error is BrowserLaunchFailure ? Text("BrowserFailed") :
                error is AuthenticationServiceIdentityException ? Text("KeyConflict") :
                error is AuthenticationRemoteException remote ? ErrorText(remote.Error) :
                error is HttpRequestException or TaskCanceledException ? Text("Unavailable") : Text("Denied")
            );
            return;
        }

        var result = task.Result;
        Log.Debug($"External authorization completed: detached={_detached}.");
        var authorizationChallenge = _authorizationChallenge ?? _node.CaptureAdmissionChallenge();
        if (_save && _endpoint is { } endpoint)
        {
            var manager = AuthenticationManager.Current;
            var installed = manager.Services.Find(endpoint.Identity.Address);
            var subject = result.Grant?.Subject ?? authorizationChallenge.Subject;
            var previous = installed is not null ? manager.Grants.Find(subject, installed.Endpoint) : null;
            try
            {
                if (result.Grant is { } grant)
                {
                    manager.Grants.Set(grant.Subject, endpoint, grant.Secret, grant.ExpiresAt);
                }

                if (installed is null)
                {
                    manager.Services.AddConfirmed(endpoint, "game:" + authorizationChallenge.Server);
                }
                else if (installed.Endpoint.BaseUri != endpoint.BaseUri)
                {
                    manager.Services.UpdateConfirmedEndpoint(endpoint.Identity.Address, endpoint);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                              or InvalidOperationException)
            {
                try
                {
                    if (previous is not null && installed is not null)
                    {
                        manager.Grants.Set(previous.Subject, installed.Endpoint, previous.Secret, previous.ExpiresAt);
                    }
                    else if (result.Grant is not null) { manager.Grants.Remove(subject, endpoint.Identity.Address); }
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException
                                                    or InvalidOperationException) { }

                Fail(Text("SaveFailed"));
                return;
            }
        }

        if (string.IsNullOrEmpty(result.Approval))
        {
            Fail(Text("Denied"));
            return;
        }

        _completedAuthorization = new TemporaryJoinAuthorization(
            authorizationChallenge.Subject,
            authorizationChallenge.Server,
            _scope,
            _endpoint!,
            result.Grant
        );
        _completedAuthorization.Validate();
        if (_detached)
        {
            if (result.Grant is not { } grant || !grant.IsValid(DateTimeOffset.UtcNow))
            {
                Fail(Text("NetworkInterrupted"));
                return;
            }

            _reconnectAuthorization = _completedAuthorization;
            return;
        }

        if (!_save && _endpoint is { } temporaryEndpoint)
        {
            var challenge = _node.CaptureAdmissionChallenge();
            var authorization = new TemporaryJoinAuthorization(
                challenge.Subject,
                challenge.Server,
                _scope,
                temporaryEndpoint,
                result.Grant
            );
            authorization.Validate();
            _node.TemporaryAuthentication = authorization;
        }
        else { _node.TemporaryAuthentication = null; }

        _statusKey = "WaitServer";
        _node.TrySubmitAdmission(new AdmissionProofPackage(_challenge!.Nonce, Approval: result.Approval));
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _temporary = null;
        _reconnectAuthorization = null;
        _completedAuthorization = null;
        _submittedPassword = null;
        _node.AdmissionTransportInterrupted -= PreserveExternalAuthentication;
        _node.AdmissionRequired -= Begin;
        _node.AdmissionResultReceived -= Receive;
        _cancel.Cancel();
        Interlocked.Exchange(ref _browserRequest, null)?.Completion.TrySetCanceled();
        _passwordDialog?.Cancel();
        _passwordDialog?.Dispose();
        _consentDialog?.Cancel();
        _consentDialog?.Dispose();
        _ = _authorization?.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _cancel.Dispose();
    }

    private void Begin(AdmissionChallengePackage challenge)
    {
        if (_disposed || _challenge?.Nonce == challenge.Nonce) { return; }

        _challenge = challenge;
        if (challenge.Mode == ServerAdmissionMode.Password)
        {
            _passwordServer = _node.CaptureAdmissionChallenge().Server;
            var saved = AuthenticationManager.Current.ServerCredentials.Find(_passwordServer);
            if (saved is not null)
            {
                _submittedPassword = saved;
                _rememberPassword = true;
                _statusKey = "WaitServer";
                _node.TrySubmitAdmission(new AdmissionProofPackage(challenge.Nonce, Password: saved));
            }
            else
            {
                _statusKey = "WaitPassword";
                _passwordPending = true;
            }

            return;
        }

        try
        {
            _endpoint = new AuthenticationServiceEndpoint(
                new AuthenticationServiceDescription(
                    AuthenticationProtocol.Version,
                    challenge.AuthorityName,
                    challenge.AuthorityUrl,
                    challenge.AuthorityPublicKey
                )
            );
            var registry = AuthenticationManager.Current.Services;
            var installed = registry.Find(_endpoint.Identity.Address);
            if (installed is { Enabled: false })
            {
                _node.EndAdmission(Text("Disabled"));
                return;
            }

            if (_temporary is { } temporary)
            {
                if (temporary.Matches(_node.CaptureAdmissionChallenge(), _endpoint, _scope))
                {
                    StartExternal(false, false, temporary.ValidGrant(DateTimeOffset.UtcNow));
                    return;
                }

                _temporary = null;
            }

            if (installed?.Endpoint.BaseUri == _endpoint.BaseUri)
            {
                _endpoint = installed.Endpoint;
                StartExternal(true, true);
                return;
            }

            var description = installed is not null ? "EndpointChanged" :
                registry.Services.Any(service => service.Endpoint.BaseUri == _endpoint.BaseUri) ? "KeyChanged" :
                "Unknown";
            _consentDialog = new AuthenticationConsentDialog(
                _endpoint,
                _node.AdmissionServerAddress ?? string.Empty,
                description,
                choice =>
                {
                    _consentDialog = null;
                    if (_disposed) { return; }

                    if (choice == AuthenticationTrustChoice.Reject) { Cancel(); }
                    else { StartExternal(choice == AuthenticationTrustChoice.LoginAndSave, false); }
                }
            );
            DialogsManager.ShowDialog(_owner, _consentDialog);
        }
        catch (Exception error) when (error is AuthenticationProtocolException or CryptographicException
                                          or FormatException or ArgumentException)
        {
            _node.EndAdmission(Text("KeyConflict"));
        }
    }

    private void AskPassword()
    {
        _passwordPending = false;
        var title = Text(_incorrectPassword ? "IncorrectPassword" : "PasswordTitle");
        _passwordDialog = new PasswordDialog(
            title,
            false,
            password =>
            {
                var remember = _passwordDialog?.RememberPassword == true;
                _passwordDialog = null;
                try
                {
                    if (_disposed) { return; }

                    if (password is null)
                    {
                        Cancel();
                        return;
                    }

                    _rememberPassword = remember;
                    _submittedPassword = new string(password);
                    _statusKey = "WaitServer";
                    _node.TrySubmitAdmission(
                        new AdmissionProofPackage(_challenge!.Nonce, Password: _submittedPassword)
                    );
                }
                finally
                {
                    if (password is not null) { Array.Clear(password); }
                }
            },
            1,
            1024,
            allowRemember: true,
            description: string.IsNullOrEmpty(_node.AdmissionServerAddress)
                ? null
                : string.Format(Text("ServerIdentity"), IdentityAddressDisplay.Abbreviate(_node.AdmissionServerAddress))
        );
        DialogsManager.ShowDialog(_owner, _passwordDialog);
    }

    private void StartExternal(bool save, bool useCached, StoredAuthenticationGrant? temporaryGrant = null)
    {
        if (_disposed || _challenge is null || !_node.CanSubmitAdmission(_challenge.Nonce)) { return; }

        _save = save;
        _statusKey = "WaitApproval";
        var endpoint = _endpoint!;
        var challenge = _node.CaptureAdmissionChallenge();
        _authorizationChallenge = challenge;
        var token = _cancel.Token;
        _authorization = Task.Run(
            async () =>
            {
                token.ThrowIfCancellationRequested();
                using var key = AuthenticationUserKey.Open();
                var client = new AuthenticationLoginClient(AuthenticationManager.Current.Grants);
                return await client.AuthenticateAsync(
                    key,
                    endpoint,
                    challenge,
                    useCached,
                    async (uri, cancellation) =>
                    {
                        var request = new BrowserRequest(
                            uri,
                            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
                        );
                        Interlocked.Exchange(ref _browserRequest, request)?.Completion.TrySetCanceled();
                        await request.Completion.Task.WaitAsync(cancellation);
                    },
                    token,
                    temporaryGrant
                );
            },
            token
        );
    }

    private void Receive(AdmissionResultPackage result)
    {
        if (_disposed || result.Nonce != _challenge?.Nonce) { return; }

        if (result.Code == AdmissionResultCode.Accepted &&
            _passwordServer is not null &&
            _submittedPassword is not null)
        {
            try
            {
                var store = AuthenticationManager.Current.ServerCredentials;
                if (_rememberPassword) { store.Set(_passwordServer, _submittedPassword); }
                else { store.Remove(_passwordServer); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                              or InvalidOperationException)
            {
                DialogsManager.Alert(Text("PasswordSaveFailed"));
            }

            _submittedPassword = null;
        }

        if (result.Code is AdmissionResultCode.Accepted or AdmissionResultCode.Cancelled or AdmissionResultCode.Expired
            or AdmissionResultCode.Denied)
        {
            Dispose();
            return;
        }

        if (_challenge.Mode == ServerAdmissionMode.Password &&
            result.Code is AdmissionResultCode.IncorrectPassword or AdmissionResultCode.RateLimited)
        {
            if (result.Code == AdmissionResultCode.IncorrectPassword && _passwordServer is not null)
            {
                try { AuthenticationManager.Current.ServerCredentials.Remove(_passwordServer); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                                  or InvalidOperationException)
                {
                    DialogsManager.Alert(Text("PasswordSaveFailed"));
                }
            }

            _submittedPassword = null;
            _rememberPassword = false;
            _incorrectPassword = result.Code == AdmissionResultCode.IncorrectPassword;
            _retryAt = DateTimeOffset.UtcNow.AddSeconds(result.RetryAfterSeconds);
            _passwordPending = true;
            _statusKey = _incorrectPassword ? "IncorrectPassword" : "WaitPassword";
        }
        else if (result.Code == AdmissionResultCode.ApprovalRejected) { _node.EndAdmission(Text("Denied")); }
    }

    private void Cancel()
    {
        _submittedPassword = null;
        _cancel.Cancel();
        if (_node.PendingAdmissionChallenge?.Nonce == _challenge?.Nonce)
        {
            _node.TrySubmitAdmission(new AdmissionProofPackage(_challenge!.Nonce, Cancel: true));
        }

        _node.EndAdmission();
    }

    private static string ErrorText(AuthenticationError error)
    {
        return Text(
            error switch
            {
                AuthenticationError.SubjectBanned => "Banned",
                AuthenticationError.Expired or AuthenticationError.GrantExpired => "Expired",
                AuthenticationError.Unavailable or AuthenticationError.RateLimited => "Unavailable",
                _ => "Denied"
            }
        );
    }

    private static string Text(string key) => LanguageManager.GetContentWidgets(nameof(ClientAdmissionUi), key);
}
