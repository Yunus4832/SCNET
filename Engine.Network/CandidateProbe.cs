using System.Net;

using Rendezvous.Protocol;

namespace Engine.Network;

internal sealed class CandidateProbe(SignalMessage candidate, IPEndPoint[] endpoints)
{
    public SignalMessage Candidate { get; } = candidate;

    public IPEndPoint Endpoint => Completion.Task.IsCompletedSuccessfully ? Completion.Task.Result : endpoints[0];

    public IEnumerable<IPEndPoint> SendEndpoints => Completion.Task.IsCompletedSuccessfully
        ? [Completion.Task.Result]
        : endpoints;

    private long _expiresAtTicks = (DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5)).UtcTicks;

    public DateTimeOffset ExpiresAt
    {
        get => new(Interlocked.Read(ref _expiresAtTicks), TimeSpan.Zero);
        set => Interlocked.Exchange(ref _expiresAtTicks, value.UtcTicks);
    }

    private long _ownerExpiresAtTicks;

    public DateTimeOffset? OwnerExpiresAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _ownerExpiresAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        set => Interlocked.Exchange(ref _ownerExpiresAtTicks, value?.UtcTicks ?? 0);
    }

    public void ConfirmReachability(IPEndPoint source)
    {
        ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2);
        Completion.TrySetResult(source);
    }

    public DateTimeOffset NextSendAt { get; set; }

    public TaskCompletionSource<IPEndPoint> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Matches(SignalMessage message, IPEndPoint source) =>
        ExpiresAt > DateTimeOffset.UtcNow &&
        (Completion.Task.IsCompletedSuccessfully ? Completion.Task.Result.Equals(source) : endpoints.Contains(source)) &&
        message.RequestId == Candidate.RequestId && message.SessionId == Candidate.SessionId && message.ParticipantId == Candidate.ParticipantId &&
        message.Challenge == Candidate.Challenge;
}
