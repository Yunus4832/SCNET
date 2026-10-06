using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class FurniturePackage : IPackage
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

    public string AddXml = string.Empty;

    public CellFace CellFace;

    public EventType PackageEventType;

    public string FromName = string.Empty;

    public int FurnitureIndex;

    public readonly Dictionary<Point3, int> PointDict = new();

    public int StartValue;

    public readonly List<int> ToRemoveList = [];

    public byte ID => (byte)PackageType.Furniture;




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

    public FurniturePackage(FurnitureDesign design)
    {
        PackageEventType = EventType.DesignChain;
        AddXml = SerializeDesigns(design.ListChain());
    }

    public FurniturePackage(List<FurnitureDesign> designs, string setName)
    {
        PackageEventType = EventType.ImportFurnitureSet;
        AddXml = SerializeDesigns(designs);
        FromName = setName;
    }

    private static string SerializeDesigns(List<FurnitureDesign> chain)
    {
        var dict = new ValuesDictionary();
        for (var i = 0; i < chain.Count; i++)
        {
            var node = chain[i].Save();
            node.SetValue("NetworkIndex", chain[i].Index);
            node.SetValue("LinkedDesign", chain.IndexOf(chain[i].LinkedDesign!));
            dict.SetValue(i.ToString(System.Globalization.CultureInfo.InvariantCulture), node);
        }

        return CommonLib.SerializeVDict(dict);
    }

    internal List<FurnitureDesign> ReadDesignChain(SubsystemTerrain? terrain)
    {
        var values = CommonLib.ReadVDict(AddXml);
        if (values.Count > 65535)
        {
            throw new InvalidOperationException("Furniture design count exceeds capacity.");
        }

        var chain = new List<FurnitureDesign>();
        for (var i = 0; i < values.Count; i++)
        {
            var node = values.GetValue<ValuesDictionary>(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (node.GetValue<int>("Resolution") is < 2 or > FurnitureDesign.MaxResolution)
            {
                throw new InvalidOperationException("Invalid furniture resolution.");
            }

            chain.Add(new FurnitureDesign(node.GetValue<int>("NetworkIndex"), terrain, node));
        }

        foreach (var node in chain)
        {
            if (node.LoadTimeLinkedDesignIndex < -1 || node.LoadTimeLinkedDesignIndex >= chain.Count)
            {
                throw new InvalidOperationException("Invalid furniture design link index.");
            }

            if (node.LoadTimeLinkedDesignIndex >= 0)
            {
                node.LinkedDesign = chain[node.LoadTimeLinkedDesignIndex];
            }
        }

        return chain;
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
            PointDict.Add(k.Key, k.Value);
        }

        CellFace = cellFace;
        StartValue = value;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.ImportFurnitureSet:
                writer.Write(AddXml);
                writer.Write(FromName);
                break;
            case EventType.DesignChain:
                writer.Write(AddXml);
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
                writer.Write(FurnitureIndex);
                writer.Write(AddXml);
                break;
            case EventType.RequestAdd:
                writer.Write(FurnitureIndex);
                writer.Write(AddXml);
                writer.Write(PointDict.Count);
                foreach (var k in PointDict)
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
            case EventType.ImportFurnitureSet:
                AddXml = reader.ReadString();
                FromName = reader.ReadString();
                break;
            case EventType.DesignChain:
                AddXml = reader.ReadString();
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
                FurnitureIndex = reader.ReadInt32();
                AddXml = reader.ReadString();
                break;
            case EventType.RequestAdd:
                FurnitureIndex = reader.ReadInt32();
                AddXml = reader.ReadString();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    PointDict.Add(reader.ReadPoint3(), reader.ReadInt32());
                }

                CellFace = reader.ReadCellFace();
                StartValue = reader.ReadInt32();
                break;
        }
    }
}
