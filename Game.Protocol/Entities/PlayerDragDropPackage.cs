using Engine.Core;

using Game.Protocol.Players;

namespace Game.Protocol.Entities;

public sealed record PlayerDragDropPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.DragDrop;
    public int InventoryID { get; init; }
    public int ActiveSlot { get; init; }
    public Vector3 HitPosition { get; init; }
    public int Count { get; init; }
}
