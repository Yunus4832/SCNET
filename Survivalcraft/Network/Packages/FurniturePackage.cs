using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class FurniturePackage : IPackage
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
        TryAddDesignChain,
        RemoveFurnitureDesigns
    }

    public string AddXml = string.Empty;

    public CellFace CellFace;

    public EventType PackageEventType;

    public string FromName = string.Empty;

    public int FurnitureIndex;

    public readonly Dictionary<Point3, int> _pointDict = new();

    public int StartValue;

    public readonly List<int> ToRemoveList = [];

    public byte ID => (byte)PackageType.Furniture;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public FurniturePackage()
    {
    }

    public FurniturePackage(FurnitureSet furnitureSet)
    {
        PackageEventType = EventType.NewFurnitureSet;
        AddXml = furnitureSet.Name;
        FromName = furnitureSet.ImportedFrom;
    }

    public FurniturePackage(List<int> list)
    {
        PackageEventType = EventType.RemoveFurnitureDesigns;
        ToRemoveList.AddRange(list);
    }


    public FurniturePackage(string designName)
    {
        PackageEventType = EventType.DeleteFurnitureSet;
        AddXml = designName;
    }

    public FurniturePackage(string oldName, string newName)
    {
        PackageEventType = EventType.RenameFurnitureSet;
        AddXml = oldName;
        FromName = newName;
    }

    public FurniturePackage(FurnitureSet furnitureSet, int move)
    {
        PackageEventType = EventType.MoveFurnitureSet;
        FurnitureIndex = move;
        AddXml = furnitureSet.Name;
    }

    public FurniturePackage(FurnitureDesign design, FurnitureSet furnitureSet)
    {
        PackageEventType = EventType.AddToFurnitureSet;
        FurnitureIndex = design.Index;
        AddXml = furnitureSet.Name;
    }

    public FurniturePackage(FurnitureDesign design, bool garbageCollectIfNeeded)
    {
        PackageEventType = EventType.TryAddDesignChain;
        FurnitureIndex = design.Index;
        var dict = design.Save();
        AddXml = CommonLib.SerializeVDict(dict);
        StartValue = garbageCollectIfNeeded ? 1 : 0;
    }

    public FurniturePackage(FurnitureDesign design, Dictionary<Point3, int> list, CellFace cellFace, int value,
        bool isRequest = false)
    {
        PackageEventType = isRequest ? EventType.RequestAdd : EventType.Add;
        FurnitureIndex = design.Index;
        var dict = design.Save();
        AddXml = CommonLib.SerializeVDict(dict);
        foreach (var k in list)
        {
            _pointDict.Add(k.Key, k.Value);
        }

        CellFace = cellFace;
        StartValue = value;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.TryAddDesignChain:
                writer.Write(AddXml);
                writer.Write(FurnitureIndex);
                writer.Write(StartValue);
                break;
            case EventType.AddToFurnitureSet:
            case EventType.MoveFurnitureSet:
                writer.Write(AddXml);
                writer.Write(FurnitureIndex);
                break;
            case EventType.RenameFurnitureSet:
                writer.Write(AddXml);
                writer.Write(FromName);
                break;
            case EventType.DeleteFurnitureSet:
                writer.Write(AddXml);
                break;
            case EventType.NewFurnitureSet:
                writer.Write(AddXml);
                writer.Write(!string.IsNullOrEmpty(FromName));
                if (!string.IsNullOrEmpty(FromName))
                {
                    writer.Write(FromName);
                }

                break;
            case EventType.RemoveFurnitureDesigns:
                writer.Write(ToRemoveList.Count);
                foreach (var item in ToRemoveList)
                {
                    writer.Write(item);
                }

                break;
            case EventType.Add:
            case EventType.RequestAdd:
                writer.Write(FurnitureIndex);
                writer.Write(AddXml);
                writer.Write(_pointDict.Count);
                foreach (var k in _pointDict)
                {
                    writer.Write(k.Key);
                    writer.Write(k.Value);
                }

                writer.Write(CellFace);
                writer.Write(StartValue);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        PackageEventType = reader.ReadEnum<EventType>();
        switch (PackageEventType)
        {
            case EventType.TryAddDesignChain:
                AddXml = reader.ReadString();
                FurnitureIndex = reader.ReadInt32();
                StartValue = reader.ReadInt32();
                break;
            case EventType.AddToFurnitureSet:
            case EventType.MoveFurnitureSet:
                AddXml = reader.ReadString();
                FurnitureIndex = reader.ReadInt32();
                break;
            case EventType.RenameFurnitureSet:
                AddXml = reader.ReadString();
                FromName = reader.ReadString();
                break;
            case EventType.DeleteFurnitureSet:
                AddXml = reader.ReadString();
                break;
            case EventType.NewFurnitureSet:
                AddXml = reader.ReadString();
                if (reader.ReadBoolean())
                {
                    FromName = reader.ReadString();
                }

                break;
            case EventType.RemoveFurnitureDesigns:
                var c = reader.ReadInt32();
                for (var i = 0; i < c; i++)
                {
                    ToRemoveList.Add(reader.ReadInt32());
                }

                break;
            case EventType.Add:
            case EventType.RequestAdd:
                FurnitureIndex = reader.ReadInt32();
                AddXml = reader.ReadString();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    _pointDict.Add(reader.ReadPoint3(), reader.ReadInt32());
                }

                CellFace = reader.ReadCellFace();
                StartValue = reader.ReadInt32();
                break;
        }
    }


}
