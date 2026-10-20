using Game.Protocol.World;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteSubsystemWeatherPackage(SubsystemWeatherPackage message, ProtocolWriter writer)
    {
        writer.Write(message.PrecipitationStartTime);
        writer.Write(message.PrecipitationEndTime);
        writer.Write(message.LightningIntensity);
        writer.Write(message.FogStartTime);
        writer.Write(message.FogRampTime);
        writer.Write(message.FogEndTime);
    }

    private static SubsystemWeatherPackage ReadSubsystemWeatherPackage(ProtocolReader reader) => new()
    { PrecipitationStartTime = reader.ReadDouble(), PrecipitationEndTime = reader.ReadDouble(), LightningIntensity = reader.ReadSingle(), FogStartTime = reader.ReadDouble(), FogRampTime = reader.ReadSingle(), FogEndTime = reader.ReadDouble() };
}
