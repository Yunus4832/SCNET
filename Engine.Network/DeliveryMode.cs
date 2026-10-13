namespace Engine.Network;

/// <summary>Delivery semantics independent of the transport implementation.</summary>
public enum DeliveryMode
{
    ReliableOrdered,
    ReliableUnordered,
    Sequenced,
    Unreliable
}
