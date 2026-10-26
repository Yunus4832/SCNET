using Engine.Network;

using EntitySystem.Core;

using Game.Admission;
using Game.Network.Handling;
using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Owns provisional transports; world sessions are created only after policy and business checks.</summary>
internal sealed class ServerAdmissionController(
    NetNode node,
    Project project,
    ServerAdmissionConfiguration configuration,
    string serverAddress
)
{
    private sealed class Pending(Client client, ConnectionRequestPackage request, AdmissionAttempt attempt)
    {
        internal Client Client { get; } = client;

        internal ConnectionRequestPackage Request { get; } = request;

        internal AdmissionAttempt Attempt { get; } = attempt;

        internal bool Approved { get; set; }

        internal Task<AdmissionResultPackage>? Verification { get; set; }
    }

    private readonly Dictionary<Guid, Pending> _pending = new();

    private readonly Queue<long> _passwordChecks = new();

    private readonly SemaphoreSlim _verifiers = new(2, 2);

    private readonly SemaphoreSlim _verificationSlots = new(32, 32);

    internal void Begin(NetworkConnectionRequest transport, ConnectionRequestPackage request)
    {
        var address = new PlayerAddress(transport.PeerIdentity.Address);
        if (_pending.Count >= 32 ||
            _pending.Values.Any(item => item.Client.PlayerAddress == address) ||
            _pending.Values.Count(item =>
                item.Client.Peer?.TransportEndpoint.Address.Equals(transport.TransportEndpoint.Address) == true
            ) >=
            4)
        {
            transport.Reject(NetNode.CreateUnconnectedPayload(new ConnectionRejectPackage("认证请求过多，请稍后重试")));
            return;
        }

        var peer = transport.Accept();
        if (peer is null) { return; }

        var client = node.ResolveSender(peer: peer)!;
        var attempt = new AdmissionAttempt(
            configuration,
            address.Value,
            serverAddress,
            peer.ChannelBinding.Span,
            DateTimeOffset.UtcNow
        );
        _pending.Add(peer.Id, new Pending(client, request, attempt));
        node.SendWriterFromPackage(attempt.Describe(), peer);
    }

    internal void Submit(Client client, AdmissionProofPackage proof)
    {
        if (client.Peer is not { } peer ||
            !_pending.TryGetValue(peer.Id, out var pending) ||
            !ReferenceEquals(client, pending.Client)) { return; }

        if (proof.Nonce != pending.Attempt.Challenge.Nonce) { return; }

        if (proof.Cancel)
        {
            _pending.Remove(peer.Id);
            node.SendWriterFromPackage(
                new AdmissionResultPackage(pending.Attempt.Challenge.Nonce, AdmissionResultCode.Cancelled),
                peer
            );
            node.RemoveClient(client, "认证已取消");
            return;
        }

        if (pending.Approved || pending.Verification is not null) { return; }

        var now = DateTimeOffset.UtcNow;
        var retry = pending.Attempt.RetryAfterSeconds(now);
        if (retry > 0)
        {
            node.SendWriterFromPackage(
                new AdmissionResultPackage(proof.Nonce, AdmissionResultCode.RateLimited, retry),
                peer
            );
            return;
        }

        if (configuration.Mode == ServerAdmissionMode.Password &&
            proof.Approval.Length == 0 &&
            proof.Password.Length > 0 &&
            now.ToUnixTimeSeconds() < pending.Attempt.Challenge.ExpiresAt)
        {
            while (_passwordChecks.TryPeek(out var timestamp) && timestamp <= now.ToUnixTimeSeconds() - 60)
            {
                _passwordChecks.Dequeue();
            }

            if (_passwordChecks.Count >= 30)
            {
                node.SendWriterFromPackage(
                    new AdmissionResultPackage(pending.Attempt.Challenge.Nonce, AdmissionResultCode.RateLimited, 2),
                    peer
                );
                return;
            }

            _passwordChecks.Enqueue(now.ToUnixTimeSeconds());
        }

        // Count queued work even after its connection disappears; churn cannot bypass the work limit.
        if (!_verificationSlots.Wait(0))
        {
            node.SendWriterFromPackage(
                new AdmissionResultPackage(proof.Nonce, AdmissionResultCode.RateLimited, 2),
                peer
            );
            return;
        }

        // Password derivation and signature verification never block the simulation thread.
        pending.Verification = Task.Run(async () =>
            {
                try
                {
                    await _verifiers.WaitAsync();
                    try { return await pending.Attempt.AuthorizeAsync(proof, DateTimeOffset.UtcNow); }
                    finally { _verifiers.Release(); }
                }
                finally { _verificationSlots.Release(); }
            }
        );
        _ = pending.Verification.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }

    internal void Forget(NetworkConnection peer) => _pending.Remove(peer.Id);

    internal void Update()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var item in _pending.Values.ToArray())
        {
            var peer = item.Client.Peer!;
            if (!peer.IsConnected || now >= item.Attempt.Challenge.ExpiresAt)
            {
                _pending.Remove(peer.Id);
                if (peer.IsConnected) { node.RemoveClient(item.Client, "认证等待超时"); }

                continue;
            }

            if (item.Verification is { IsCompleted: true } verification)
            {
                item.Verification = null;
                var result = verification.IsCompletedSuccessfully
                    ? verification.Result
                    : new AdmissionResultPackage(item.Attempt.Challenge.Nonce, AdmissionResultCode.Denied);
                if (result.Code != AdmissionResultCode.Accepted) { node.SendWriterFromPackage(result, peer); }

                if (result.Code == AdmissionResultCode.Accepted) { item.Approved = true; }
                else if (item.Attempt.IsTerminal || result.Code == AdmissionResultCode.Denied)
                {
                    _pending.Remove(peer.Id);
                    node.RemoveClient(item.Client, "认证已过期或被拒绝");
                    continue;
                }
            }

            if (!item.Approved || node.PendingPeer is not null) { continue; }

            _pending.Remove(peer.Id);
            ConnectionRequestPackageHandler.CompleteAdmission(
                item.Request,
                project,
                node,
                peer,
                item.Attempt.Challenge.Nonce
            );
        }
    }
}
