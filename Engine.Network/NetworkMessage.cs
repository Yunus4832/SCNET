namespace Engine.Network;

public sealed record NetworkMessage(ReadOnlyMemory<byte> Payload, DeliveryMode Delivery);
