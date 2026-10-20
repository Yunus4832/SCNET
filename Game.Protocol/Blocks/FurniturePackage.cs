using System.Collections.ObjectModel;

using Engine.Core;

using Game.Protocol.Terrain;

namespace Game.Protocol.Blocks;

public sealed record FurniturePackage : IProtocolMessage
{
    public enum EventType
    {
        RequestAdd,
        Add,
        NewFurnitureSet,
        DeleteFurnitureSet,
        RenameFurnitureSet,
        MoveFurnitureSet,
        AddToFurnitureSet,
        DesignChain,
        RemoveFurnitureDesigns,
        ImportFurnitureSet
    }
    public string AddXml { get; init; } = string.Empty;
    public CellFace CellFace { get; init; }
    public EventType PackageEventType { get; init; }
    public string FromName { get; init; } = string.Empty;
    public int FurnitureIndex { get; init; }
    public IReadOnlyDictionary<Point3, int> PointDict { get; init; } = ReadOnlyDictionary<Point3, int>.Empty;
    public int StartValue { get; init; }
    public IReadOnlyList<int> ToRemoveList { get; init; } = [];
    public byte ID => (byte)PackageType.Furniture;
    public FurniturePackage() { }
    public FurniturePackage(IEnumerable<int> ids) { PackageEventType = EventType.RemoveFurnitureDesigns; ToRemoveList = ids.ToArray(); }
    public FurniturePackage(string name) { PackageEventType = EventType.DeleteFurnitureSet; AddXml = name; }
    public FurniturePackage(string oldName, string newName) { PackageEventType = EventType.RenameFurnitureSet; AddXml = oldName; FromName = newName; }
}
