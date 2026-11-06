namespace Game.Network.Transport;

public interface IMessageConnection : IConnectionLiveness, IDisposable
{
    bool IsInProcess { get; }

    /// <summary>
    /// Accepted takes ownership of one fanout continuation. Backpressured, Closed and exceptions leave it with the caller.
    /// Transient backpressure must be returned as a result, rather than thrown as a transport exception.
    /// </summary>
    ConnectionSendResult TrySendOwned(OutboundPackage message);

    void Disconnect(LocalizedText reason);
}
