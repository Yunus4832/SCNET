namespace Engine.Network;

/// <summary>定义独立于具体传输实现的投递语义。</summary>
public enum DeliveryMode
{
    ReliableOrdered = 0,

    ReliableUnordered = 1,

    Sequenced = 2,

    Unreliable = 3
}
