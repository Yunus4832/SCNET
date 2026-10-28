namespace Engine.Network.Transport;

internal sealed class PendingDelivery(int bytes, DeliveryReceipt? receipt)
{
    public Guid Id { get; } = Guid.NewGuid();

    public int Bytes { get; } = bytes;

    public DeliveryReceipt? Receipt { get; } = receipt;
}
