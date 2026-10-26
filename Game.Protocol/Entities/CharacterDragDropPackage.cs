using Engine.Core.Mathematics;

using Game.Protocol.Characters;

namespace Game.Protocol.Entities;

public sealed record CharacterDragDropPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.DragDrop;

    public int InventoryID { get; init; }

    public int ActiveSlot { get; init; }

    public Vector3 HitPosition { get; init; }

    public int Count { get; init; }
}
