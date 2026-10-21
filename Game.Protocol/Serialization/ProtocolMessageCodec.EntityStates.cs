using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentSicknessPackage(ComponentSicknessPackage message, ProtocolWriter writer)
    {
        writer.Write(message.EntityId);
        writer.Write(message.SicknessDuration);
    }

    private static ComponentSicknessPackage ReadComponentSicknessPackage(ProtocolReader reader) => new()
        { EntityId = reader.ReadInt32(), SicknessDuration = reader.ReadSingle() };

    private static void WriteComponentMountPackage(ComponentMountPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        writer.Write(message.FromId);
        writer.Write(message.TargetId);
    }

    private static ComponentMountPackage ReadComponentMountPackage(ProtocolReader reader) => new()
    {
        Type = reader.ReadEnum<ComponentMountPackage.EventType>(), FromId = reader.ReadInt32(),
        TargetId = reader.ReadInt32()
    };

    private static void WriteComponentSleepPackage(ComponentSleepPackage message, ProtocolWriter writer)
    {
        writer.Write(message.EntityId);
        writer.WriteEnum(message.Type);
        writer.Write(message.AllowManualWakeup);
        writer.Write(message.Result);
        writer.Write(message.Reason);
    }

    private static ComponentSleepPackage ReadComponentSleepPackage(ProtocolReader reader) => new()
    {
        EntityId = reader.ReadInt32(), Type = reader.ReadEnum<ComponentSleepPackage.EventType>(),
        AllowManualWakeup = reader.ReadBoolean(), Result = reader.ReadBoolean(), Reason = reader.ReadString()
    };

    private static void WriteComponentFluPackage(ComponentFluPackage message, ProtocolWriter writer)
    {
        writer.Write(message.EntityId);
        writer.WriteEnum(message.PackageEventType);
        writer.Write(message.FluOnset);
        writer.Write(message.FluDuration);
        writer.Write(message.CoughDuration);
        writer.Write(message.SneezeDuration);
    }

    private static ComponentFluPackage ReadComponentFluPackage(ProtocolReader reader) => new()
    {
        EntityId = reader.ReadInt32(), PackageEventType = reader.ReadEnum<ComponentFluPackage.EventType>(),
        FluOnset = reader.ReadSingle(), FluDuration = reader.ReadSingle(), CoughDuration = reader.ReadSingle(),
        SneezeDuration = reader.ReadSingle()
    };
}
