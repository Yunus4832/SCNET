namespace Game.Protocol.Entities;

public sealed record ComponentSicknessPackage : IProtocolMessage
{
    public int EntityId { get; init; }
    public float SicknessDuration { get; init; }
    public byte ID => (byte)PackageType.ComponentSickness;
}
