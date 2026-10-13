namespace Engine.Network;

/// <summary>Confirms transport delivery, not application processing of the payload.</summary>
public sealed class DeliveryReceipt
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal DeliveryReceipt()
    {
    }

    public Guid Id { get; } = Guid.NewGuid();

    public Task Completion => _completion.Task;

    internal void Confirm() => _completion.TrySetResult();

    internal void Fail() => _completion.TrySetException(new IOException("Connection ended before transport confirmation."));
}
