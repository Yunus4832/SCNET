namespace Engine.Network;

/// <summary>Delivery semantics independent of the transport implementation.</summary>
public enum DeliveryMode
{
    ReliableOrdered = 0,

    ReliableUnordered = 1,

    Sequenced = 2,

    Unreliable = 3
}
