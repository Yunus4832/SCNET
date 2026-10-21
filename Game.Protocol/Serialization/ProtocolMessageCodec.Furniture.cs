using Engine.Core.Mathematics;

using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteFurniturePackage(FurniturePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.PackageEventType);
        switch (message.PackageEventType)
        {
            case FurniturePackage.EventType.ImportFurnitureSet:
                writer.Write(message.AddXml);
                writer.Write(message.FromName);
                break;
            case FurniturePackage.EventType.DesignChain:
                writer.Write(message.AddXml);
                break;
            case FurniturePackage.EventType.AddToFurnitureSet:
            case FurniturePackage.EventType.MoveFurnitureSet:
                writer.Write(message.AddXml);
                writer.Write(message.FurnitureIndex);
                break;
            case FurniturePackage.EventType.RenameFurnitureSet:
                writer.Write(message.AddXml);
                writer.Write(message.FromName);
                break;
            case FurniturePackage.EventType.DeleteFurnitureSet:
                writer.Write(message.AddXml);
                break;
            case FurniturePackage.EventType.NewFurnitureSet:
                writer.Write(message.AddXml);
                writer.Write(!string.IsNullOrEmpty(message.FromName));
                if (!string.IsNullOrEmpty(message.FromName))
                {
                    writer.Write(message.FromName);
                }

                break;
            case FurniturePackage.EventType.RemoveFurnitureDesigns:
                writer.Write(message.ToRemoveList.Count);
                foreach (var item in message.ToRemoveList)
                {
                    writer.Write(item);
                }

                break;
            case FurniturePackage.EventType.Add:
                writer.Write(message.FurnitureIndex);
                writer.Write(message.AddXml);
                break;
            case FurniturePackage.EventType.RequestAdd:
                writer.Write(message.FurnitureIndex);
                writer.Write(message.AddXml);
                writer.Write(message.PointDict.Count);
                foreach (var k in message.PointDict)
                {
                    writer.Write(k.Key);
                    writer.Write(k.Value);
                }

                writer.Write(message.CellFace);
                writer.Write(message.StartValue);
                break;
        }
    }

    private static FurniturePackage ReadFurniturePackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<FurniturePackage.EventType>();
        switch (type)
        {
            case FurniturePackage.EventType.ImportFurnitureSet:
            case FurniturePackage.EventType.RenameFurnitureSet:
                return new FurniturePackage
                    { PackageEventType = type, AddXml = reader.ReadString(), FromName = reader.ReadString() };
            case FurniturePackage.EventType.DesignChain:
            case FurniturePackage.EventType.DeleteFurnitureSet:
                return new FurniturePackage { PackageEventType = type, AddXml = reader.ReadString() };
            case FurniturePackage.EventType.AddToFurnitureSet:
            case FurniturePackage.EventType.MoveFurnitureSet:
                return new FurniturePackage
                    { PackageEventType = type, AddXml = reader.ReadString(), FurnitureIndex = reader.ReadInt32() };
            case FurniturePackage.EventType.NewFurnitureSet:
                var name = reader.ReadString();
                return new FurniturePackage
                {
                    PackageEventType = type, AddXml = name,
                    FromName = reader.ReadBoolean() ? reader.ReadString() : string.Empty
                };
            case FurniturePackage.EventType.RemoveFurnitureDesigns:
                var length = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 4);
                EnsureRemaining(reader, length * 4);
                var ids = new int[length];
                for (var i = 0; i < length; i++) { ids[i] = reader.ReadInt32(); }

                return new FurniturePackage(ids);
            case FurniturePackage.EventType.Add:
                return new FurniturePackage
                    { PackageEventType = type, FurnitureIndex = reader.ReadInt32(), AddXml = reader.ReadString() };
            case FurniturePackage.EventType.RequestAdd:
                var index = reader.ReadInt32();
                var xml = reader.ReadString();
                var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 16);
                EnsureRemaining(reader, count * 16);
                var cells = new Dictionary<Point3, int>();
                for (var i = 0; i < count; i++)
                {
                    if (!cells.TryAdd(reader.ReadPoint3(), reader.ReadInt32()))
                    {
                        throw new InvalidDataException("Duplicate furniture cell.");
                    }
                }

                return new FurniturePackage
                {
                    PackageEventType = type, FurnitureIndex = index, AddXml = xml, PointDict = cells,
                    CellFace = reader.ReadCellFace(), StartValue = reader.ReadInt32()
                };
            default: throw new InvalidDataException("Unknown furniture mode.");
        }
    }
}
