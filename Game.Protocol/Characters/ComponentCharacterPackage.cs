using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
[Union(0, typeof(CharacterAimPackage))]
[Union(1, typeof(CharacterDigPackage))]
[Union(2, typeof(CharacterExperiencePackage))]
[Union(3, typeof(CharacterFlightPackage))]
[Union(4, typeof(CharacterHitPackage))]
[Union(5, typeof(CharacterPlacePackage))]
[Union(6, typeof(CharacterPositionPackage))]
[Union(7, typeof(CharacterRestartPackage))]
[Union(8, typeof(CharacterStatePackage))]
[Union(9, typeof(CharacterStatisticsPackage))]
[Union(10, typeof(CharacterUsePackage))]
[Union(11, typeof(Connection.CharacterReadyPackage))]
[Union(12, typeof(Entities.CharacterDragDropPackage))]
[Union(13, typeof(Entities.CharacterDropPackage))]
public abstract record ComponentCharacterPackage : IProtocolMessage
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

    public enum CharacterAction
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

    [Key(0)] public byte ControllerSessionId { get; init; }

    [IgnoreMember] public abstract CharacterAction Type { get; }
}
