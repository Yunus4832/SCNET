using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
[Union(0, typeof(FurnitureDesignAddedPackage))]
[Union(1, typeof(FurnitureDesignAddRequestPackage))]
[Union(2, typeof(FurnitureDesignChainPackage))]
[Union(3, typeof(FurnitureDesignsRemovedPackage))]
[Union(4, typeof(FurnitureSetAddDesignPackage))]
[Union(5, typeof(FurnitureSetCreatedPackage))]
[Union(6, typeof(FurnitureSetDeletedPackage))]
[Union(7, typeof(FurnitureSetImportPackage))]
[Union(8, typeof(FurnitureSetMovePackage))]
[Union(9, typeof(FurnitureSetRenamedPackage))]
public abstract record FurniturePackage : IProtocolMessage
{
    public enum EventType
    {
        RequestAdd = 0,

        Add = 1,

        NewFurnitureSet = 2,

        DeleteFurnitureSet = 3,

        RenameFurnitureSet = 4,

        MoveFurnitureSet = 5,

        AddToFurnitureSet = 6,

        DesignChain = 7,

        RemoveFurnitureDesigns = 8,

        ImportFurnitureSet = 9
    }

    [IgnoreMember] public abstract EventType PackageEventType { get; }
}
