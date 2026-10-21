using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentHealthPackage(ComponentHealthPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case ComponentHealthPackage.EventType.RequestInjure:
                writer.Write(message.TargetId);
                writer.Write(message.AttackerId);
                writer.Write(message.Health);
                writer.Write(message.IgnoreInvulnerability);
                writer.Write(message.Amount);
                writer.Write(message.Cause);
                writer.WriteEnum(message.InjureRequestType);
                break;
            case ComponentHealthPackage.EventType.Injure:
                writer.Write(message.TargetId);
                writer.Write(message.AttackerId);
                writer.Write(message.Health);
                writer.Write(message.IgnoreInvulnerability);
                writer.Write(message.Amount);
                writer.Write(message.Cause);
                break;
            case ComponentHealthPackage.EventType.HitResult:
                writer.Write(message.Position);
                writer.Write(message.Velocity);
                writer.Write(message.Color);
                writer.Write(message.Text);
                break;
            case ComponentHealthPackage.EventType.SyncHealth:
                writer.Write(message.TargetId);
                writer.Write(message.Health);
                writer.Write(message.Cause);
                break;
            case ComponentHealthPackage.EventType.Damage:
                writer.Write(message.TargetId);
                writer.Write(message.Health);
                break;
        }
    }

    private static ComponentHealthPackage ReadComponentHealthPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ComponentHealthPackage.EventType>();
        switch (type)
        {
            case ComponentHealthPackage.EventType.Injure:
            case ComponentHealthPackage.EventType.RequestInjure:
                var message = new ComponentHealthPackage
                {
                    Type = type,
                    TargetId = reader.ReadInt32(),
                    AttackerId = reader.ReadInt32(),
                    Health = reader.ReadSingle(),
                    IgnoreInvulnerability = reader.ReadBoolean(),
                    Amount = reader.ReadSingle(),
                    Cause = reader.ReadString(),
                    InjureRequestType = type == ComponentHealthPackage.EventType.RequestInjure
                        ? reader.ReadEnum<ComponentHealthPackage.RequestInjureType>()
                        : ComponentHealthPackage.RequestInjureType.Unknown
                };
                if (!Enum.IsDefined(message.InjureRequestType))
                {
                    throw new InvalidDataException("Unknown injury request type.");
                }

                return message;
            case ComponentHealthPackage.EventType.HitResult:
                return new ComponentHealthPackage(
                    reader.ReadVector3(),
                    reader.ReadVector3(),
                    reader.ReadColor(),
                    reader.ReadString()
                );
            case ComponentHealthPackage.EventType.SyncHealth:
                return new ComponentHealthPackage
                {
                    Type = type, TargetId = reader.ReadInt32(), Health = reader.ReadSingle(),
                    Cause = reader.ReadString()
                };
            case ComponentHealthPackage.EventType.Damage:
                return new ComponentHealthPackage
                    { Type = type, TargetId = reader.ReadInt32(), Health = reader.ReadSingle() };
            default: throw new InvalidDataException("Unknown health message mode.");
        }
    }
}
