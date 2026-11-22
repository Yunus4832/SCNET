namespace Engine.Network;

/// <summary>确认传输层已投递数据，应用层可能尚未处理载荷。</summary>
public sealed class DeliveryReceipt
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal DeliveryReceipt()
    {
    }

    public Guid Id { get; } = Guid.NewGuid();

    public Task Completion => _completion.Task;

    internal void Confirm() => _completion.TrySetResult();

    internal void Fail() =>
        _completion.TrySetException(new IOException("Connection ended before transport confirmation."));
}
