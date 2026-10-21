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
        BodyUpdate = 0,

        InteractEvent = 1,

        AimEvent = 2,

        DigEvent = 3,

        Hit = 4,

        CreativeFlyChange = 5,

        IntoPlaying = 6,

        Restart = 7,

        AddExperience = 8,

        Drop = 9,

        DragDrop = 10,

        SyncStat = 11,

        PositionSet = 12
    }

    public byte FromPlayerId { get; init; }

    public abstract PlayerAction Type { get; }

    public byte ID => (byte)PackageType.ComponentPlayer;
}
