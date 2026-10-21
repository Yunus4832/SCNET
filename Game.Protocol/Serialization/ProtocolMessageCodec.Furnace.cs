using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentFurnacePackage(ComponentFurnacePackage message, ProtocolWriter writer)
    {
        writer.Write(message.FurnaceDataList.Count);
        foreach (var item in message.FurnaceDataList)
        {
            writer.Write(item.EntityID);
            writer.Write(item.FireTimeRemaining);
            writer.Write(item.SmeltingProgress);
            writer.Write(item.HeatLevel);
        }
    }

    private static ComponentFurnacePackage ReadComponentFurnacePackage(ProtocolReader reader)
    {
        var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 16);
        EnsureRemaining(reader, count * 16);
        var data = new ComponentFurnacePackage.FurnacePackageData[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new ComponentFurnacePackage.FurnacePackageData
            {
                EntityID = reader.ReadInt32(), FireTimeRemaining = reader.ReadSingle(),
                SmeltingProgress = reader.ReadSingle(), HeatLevel = reader.ReadSingle()
            };
        }

        return new ComponentFurnacePackage { FurnaceDataList = data };
    }
}
