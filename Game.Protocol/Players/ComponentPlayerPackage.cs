namespace Game.Protocol.Players;

public abstract record ComponentPlayerPackage : IProtocolMessage
{
    [Flags]
    public enum ChangFlag : byte
    {
        None = 0,
        ParentBodyChange = 1,
        LookAnglesChange = 2,
        LadderChange = 4,
        PositionChange = 8,
        RotationChange = 16,
        VelocityChange = 32,
        ChildLookAnglesChange = 64,
        SneakChange = 128
    }

    public enum PlayerAction
    {
        BodyUpdate,
        InteractEvent,
        AimEvent,
        DigEvent,
        Hit,
        CreativeFlyChange,
        IntoPlaying,
        Restart,
        AddExperience,
        Drop,
        DragDrop,
        SyncStat,
        PositionSet
    }

    public byte FromPlayerId { get; init; }
    public abstract PlayerAction Type { get; }
    public byte ID => (byte)PackageType.ComponentPlayer;
}
