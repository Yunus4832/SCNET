namespace Game.Network.Transport;

public interface IMessageConnection : IConnectionLiveness, IDisposable
{
    bool IsInProcess { get; }

    /// <summary>返回 Accepted 时接管后续的多接收方投递任务；返回 Backpressured、Closed 或发生异常时，调用方仍持有该任务。临时背压须以返回值报告。</summary>
    ConnectionSendResult TrySendOwned(OutboundPackage message);

    void Disconnect(LocalizedText reason);
}
