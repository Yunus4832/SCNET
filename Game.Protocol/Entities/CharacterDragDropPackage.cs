using Engine.Core.Mathematics;

using Game.Protocol.Characters;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CharacterDragDropPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.DragDrop;

    [Key(1)] public int InventoryID { get; init; }

    [Key(2)] public int ActiveSlot { get; init; }

    [Key(3)] public Vector3 HitPosition { get; init; }

    [Key(4)] public int Count { get; init; }
}
