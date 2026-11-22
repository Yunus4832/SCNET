using Game.Network;

using SCNET.Authentication.Protocol.Contracts;

namespace Game.Authentication;

/// <summary>由帧循环管理的准入交互，本地权威操作经其执行边界调用。</summary>
internal sealed class AdmissionInteraction : IDisposable
{
    private sealed record View(
        long Revision,
        bool Pending,
        bool Accepted,
        bool CanSubmit,
        AdmissionChallengePackage? Challenge,
        AdmissionChallenge? ProofChallenge,
        string? Server,
        LocalizedText? Error
    );

    private readonly NetNode _node;

    private readonly AuthorityWorldExecutor? _authority;

    private View _view = new(0, false, false, false, null, null, null, null);

    private long _revision;

    private Task<View>? _poll;

    private bool _disposed;

    public event Action<AdmissionChallengePackage>? AdmissionRequired;

    public event Action<AdmissionResultPackage>? AdmissionResultReceived;

    public event Func<bool>? AdmissionTransportInterrupted;

    public AdmissionInteraction(NetNode node, AuthorityWorldExecutor? authority)
    {
        _node = node;
        _authority = authority;
        node.AdmissionRequired += OnRequired;
        node.AdmissionResultReceived += OnResult;
        node.AdmissionTransportInterrupted += OnInterrupted;
        if (authority == null) { _view = Capture(); }
    }

    public bool HasPendingAdmission => _authority == null ? _node.HasPendingAdmission : _view.Pending;

    public bool AdmissionAccepted => _authority == null ? _node.AdmissionAccepted : _view.Accepted;

    public LocalizedText? LocalAdmissionError => _authority == null ? _node.LocalAdmissionError : _view.Error;

    public AdmissionChallengePackage? PendingAdmissionChallenge =>
        _authority == null ? _node.PendingAdmissionChallenge : _view.Challenge;

    public string? AdmissionServerAddress => _authority == null ? _node.AdmissionServerAddress : _view.Server;

    public TemporaryJoinAuthorization? TemporaryAuthentication
    {
        set => Submit(node => node.TemporaryAuthentication = value);
    }

    public bool CanSubmitAdmission(string nonce) => _authority == null
        ? _node.CanSubmitAdmission(nonce)
        : _view.CanSubmit && _view.Challenge?.Nonce == nonce;

    public AdmissionChallenge CaptureAdmissionChallenge() => _authority == null
        ? _node.CaptureAdmissionChallenge()
        : _view.ProofChallenge ?? throw new InvalidOperationException("No admission challenge is ready.");

    public void BeginLocalAdmission() => Submit(node => node.BeginLocalAdmission());

    public void TrySubmitAdmission(AdmissionProofPackage proof) => Submit(node => node.TrySubmitAdmission(proof));

    public void EndAdmission(string error = "") => Submit(node => node.EndAdmission(error));

    public Task<(Game.Protocol.Identity.PlayerAddress Subject, Guid Token)> ConsumeLocalAdmissionAsync()
    {
        return (_authority ?? throw new InvalidOperationException("Only a local authority has local admission."))
            .InvokeAsync(_ => _node.ConsumeLocalAdmission());
    }

    public void Update()
    {
        if (_disposed || _authority == null) { return; }

        if (_poll is { IsCompleted: true } completed)
        {
            _poll = null;
            if (completed.IsCompletedSuccessfully) { Apply(completed.Result); }
            else
            {
                _view = _view with
                {
                    Error = LocalizedText.Literal(
                        completed.Exception?.GetBaseException().Message ?? "Admission stopped."
                    )
                };
            }
        }

        if (_poll == null && _authority.Status.State == AuthorityExecutionState.Running)
        {
            _poll = _authority.InvokeAsync(_ =>
                {
                    _node.PumpLocalAdmission();
                    return Capture();
                }
            );
        }
    }

    private View Capture()
    {
        var challenge = _node.PendingAdmissionChallenge;
        AdmissionChallenge? proof = null;
        if (challenge != null)
        {
            try { proof = _node.CaptureAdmissionChallenge(); }
            catch (InvalidOperationException) { }
        }

        return new View(
            ++_revision,
            _node.HasPendingAdmission,
            _node.AdmissionAccepted,
            challenge != null && _node.CanSubmitAdmission(challenge.Nonce),
            challenge,
            proof,
            _node.AdmissionServerAddress,
            _node.LocalAdmissionError
        );
    }

    private void Apply(View view)
    {
        if (!_disposed && view.Revision >= _view.Revision) { _view = view; }
    }

    private void OnRequired(AdmissionChallengePackage challenge)
    {
        var view = Capture();
        PublishToClient(() =>
            {
                if (_disposed) { return; }

                Apply(view);
                AdmissionRequired?.Invoke(challenge);
            }
        );
    }

    private void OnResult(AdmissionResultPackage result)
    {
        var view = Capture();
        PublishToClient(() =>
            {
                if (_disposed) { return; }

                Apply(view);
                AdmissionResultReceived?.Invoke(result);
            }
        );
    }

    private void PublishToClient(Action action)
    {
        if (_authority == null) { action(); }
        else { Dispatcher.Dispatch(action); }
    }

    private bool OnInterrupted() => _authority == null && (AdmissionTransportInterrupted?.Invoke() ?? false);

    private void Submit(Action<NetNode> operation)
    {
        if (_disposed) { return; }

        if (_authority == null)
        {
            operation(_node);
            return;
        }

        GameManager.ObserveApplicationTask(SubmitAsync(operation));
    }

    private async Task SubmitAsync(Action<NetNode> operation)
    {
        try
        {
            var view = await _authority!.InvokeAsync(_ =>
                {
                    operation(_node);
                    return Capture();
                }
            ).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => Apply(view)).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await Dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed) { _view = _view with { Error = LocalizedText.Literal(error.Message) }; }
                }
            ).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _node.AdmissionRequired -= OnRequired;
        _node.AdmissionResultReceived -= OnResult;
        _node.AdmissionTransportInterrupted -= OnInterrupted;
    }
}
