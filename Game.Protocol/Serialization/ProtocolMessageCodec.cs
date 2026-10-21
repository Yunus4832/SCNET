using Game.Protocol.Blocks;
using Game.Protocol.Commands;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Messaging;
using Game.Protocol.Mods;
using Game.Protocol.Players;
using Game.Protocol.Terrain;
using Game.Protocol.World;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    public static IProtocolMessage Read(byte id, ProtocolReader reader) => (PackageType)id switch
    {
        PackageType.ComponentOnFire => ReadComponentOnFirePackage(reader),
        PackageType.ComponentBehavior => ReadComponentBehaviorPackage(reader),
        PackageType.Command => ReadCommandPackage(reader),
        PackageType.ComponentInventory => ReadComponentInventoryPackage(reader),
        PackageType.Furniture => ReadFurniturePackage(reader),
        PackageType.EditableBlock => ReadEditableBlockPackage(reader),
        PackageType.Pickable => ReadPickablePackage(reader),
        PackageType.Projectile => ReadProjectilePackage(reader),
        PackageType.Message => ReadMessagePackage(reader),
        PackageType.GroupManage => ReadGroupManagePackage(reader),
        PackageType.PlayerData => ReadPlayerDataPackage(reader),
        PackageType.Explosion => ReadExplosionsPackage(reader),
        PackageType.ComponentHealth => ReadComponentHealthPackage(reader),
        PackageType.MovingBlockSet => ReadMovingBlockPackage(reader),
        PackageType.ComponentVitalStat => ReadComponentVitalStatPackage(reader),
        PackageType.ComponentClothing => ReadComponentClothingPackage(reader),
        PackageType.ComponentFurnace => ReadComponentFurnacePackage(reader),
        PackageType.SubsystemElectricity => ReadSubsystemElectricityPackage(reader),
        PackageType.SignBlock => ReadSignBlockPackage(reader),
        PackageType.BlockEdit => ReadBlockEditPackage(reader),
        PackageType.Dispenser => ReadDispenserPackage(reader),
        PackageType.ComponentSickness => ReadComponentSicknessPackage(reader),
        PackageType.ModEnvelope => ReadModEnvelopePackage(reader),
        PackageType.PlayerList => ReadPlayerListPackage(reader),
        PackageType.OnlinePlayerState => ReadOnlinePlayerStatePackage(reader),
        PackageType.ComponentMount => ReadComponentMountPackage(reader),
        PackageType.ComponentSleep => ReadComponentSleepPackage(reader),
        PackageType.SubsystemWeather => ReadSubsystemWeatherPackage(reader),
        PackageType.ComponentFlu => ReadComponentFluPackage(reader),
        PackageType.ComponentPlayer => ReadPlayer(reader),
        PackageType.SubsystemTerrain => ReadTerrain(reader),
        PackageType.SubsystemBody => ReadBody(reader),
        PackageType.ServerInfo => ReadServerInfo(reader),
        PackageType.Client => ReadClient(reader),
        PackageType.Bootstrap => ReadBootstrap(reader),
        PackageType.InitialWorldSnapshot => ReadInitialSnapshot(reader),
        PackageType.Entity => ReadEntity(reader),
        PackageType.ConnectionRequest => ReadConnectionRequest(reader),
        PackageType.ConnectionReject => new ConnectionRejectPackage(reader.ReadString()),
        PackageType.ConnectionPhaseAck => ReadPhaseAck(reader),
        PackageType.ChunkStateReset => new ChunkStateResetPackage(reader.ReadPoint2()),
        PackageType.SubsystemTime => new SubsystemTimePackage(reader.ReadDouble(), reader.ReadDouble()),
        PackageType.SubsystemSky => new SubsystemSkyPackage(reader.ReadVector3()),
        PackageType.SubsystemSeason => new SubsystemSeasonPackage(reader.ReadSingle()),
        _ => throw new InvalidDataException($"Unknown game message ID: {id}.")
    };

    public static void Write(IProtocolMessage message, ProtocolWriter writer)
    {
        switch (message)
        {
            case ComponentOnFirePackage valueComponentOnFirePackage:
                WriteComponentOnFirePackage(valueComponentOnFirePackage, writer);
                break;
            case ComponentBehaviorPackage valueComponentBehaviorPackage:
                WriteComponentBehaviorPackage(valueComponentBehaviorPackage, writer);
                break;
            case CommandPackage command:
                WriteCommandPackage(command, writer);
                break;
            case ComponentInventoryPackage inventory:
                WriteComponentInventoryPackage(inventory, writer);
                break;
            case FurniturePackage furniture:
                WriteFurniturePackage(furniture, writer);
                break;
            case EditableBlockPackage editableBlock:
                WriteEditableBlockPackage(editableBlock, writer);
                break;
            case PickablePackage valuePickablePackage:
                WritePickablePackage(valuePickablePackage, writer);
                break;
            case ProjectilePackage valueProjectilePackage:
                WriteProjectilePackage(valueProjectilePackage, writer);
                break;
            case MessagePackage valueMessagePackage:
                WriteMessagePackage(valueMessagePackage, writer);
                break;
            case GroupManagePackage valueGroupManagePackage:
                WriteGroupManagePackage(valueGroupManagePackage, writer);
                break;
            case PlayerDataPackage valuePlayerDataPackage:
                WritePlayerDataPackage(valuePlayerDataPackage, writer);
                break;
            case ExplosionsPackage explosions:
                WriteExplosionsPackage(explosions, writer);
                break;
            case ComponentHealthPackage valueComponentHealthPackage:
                WriteComponentHealthPackage(valueComponentHealthPackage, writer);
                break;
            case MovingBlockPackage valueMovingBlockPackage:
                WriteMovingBlockPackage(valueMovingBlockPackage, writer);
                break;
            case ComponentVitalStatPackage valueComponentVitalStatPackage:
                WriteComponentVitalStatPackage(valueComponentVitalStatPackage, writer);
                break;
            case ComponentClothingPackage valueComponentClothingPackage:
                WriteComponentClothingPackage(valueComponentClothingPackage, writer);
                break;
            case ComponentFurnacePackage valueComponentFurnacePackage:
                WriteComponentFurnacePackage(valueComponentFurnacePackage, writer);
                break;
            case SubsystemElectricityPackage valueSubsystemElectricityPackage:
                WriteSubsystemElectricityPackage(valueSubsystemElectricityPackage, writer);
                break;
            case SignBlockPackage valueSignBlockPackage:
                WriteSignBlockPackage(valueSignBlockPackage, writer);
                break;
            case BlockEditPackage valueBlockEditPackage:
                WriteBlockEditPackage(valueBlockEditPackage, writer);
                break;
            case DispenserPackage valueDispenserPackage:
                WriteDispenserPackage(valueDispenserPackage, writer);
                break;
            case ComponentSicknessPackage valueComponentSicknessPackage:
                WriteComponentSicknessPackage(valueComponentSicknessPackage, writer);
                break;
            case ModEnvelopePackage valueModEnvelopePackage:
                WriteModEnvelopePackage(valueModEnvelopePackage, writer);
                break;
            case PlayerListPackage valuePlayerListPackage:
                WritePlayerListPackage(valuePlayerListPackage, writer);
                break;
            case OnlinePlayerStatePackage valueOnlinePlayerStatePackage:
                WriteOnlinePlayerStatePackage(valueOnlinePlayerStatePackage, writer);
                break;
            case ComponentMountPackage valueComponentMountPackage:
                WriteComponentMountPackage(valueComponentMountPackage, writer);
                break;
            case ComponentSleepPackage valueComponentSleepPackage:
                WriteComponentSleepPackage(valueComponentSleepPackage, writer);
                break;
            case SubsystemWeatherPackage valueSubsystemWeatherPackage:
                WriteSubsystemWeatherPackage(valueSubsystemWeatherPackage, writer);
                break;
            case ComponentFluPackage valueComponentFluPackage:
                WriteComponentFluPackage(valueComponentFluPackage, writer);
                break;
            case ComponentPlayerPackage player:
                WritePlayer(player, writer);
                break;
            case SubsystemTerrainPackage terrain:
                WriteTerrain(terrain, writer);
                break;
            case SubsystemBodyPackage body:
                WriteBody(body, writer);
                break;
            case ServerInfoPackage serverInfo:
                WriteServerInfo(serverInfo, writer);
                break;
            case BootstrapPackage bootstrap:
                WriteBootstrap(bootstrap, writer);
                break;
            case InitialWorldSnapshotPackage snapshot:
                WriteInitialSnapshot(snapshot, writer);
                break;
            case EntityPackage entities:
                WriteEntity(entities, writer);
                break;
            case ClientPackage clients:
                WriteClient(clients, writer);
                break;
            case ConnectionRequestPackage request:
                writer.Write(request.Magic);
                writer.Write(request.ProtocolVersion);
                writer.Write(request.TmpToken);
                writer.Write(request.MultiplayerClientId);
                writer.Write(request.ModDataHash);
                break;
            case ConnectionRejectPackage reject:
                writer.Write(reject.Reason);
                break;
            case ConnectionPhaseAckPackage ack:
                writer.Write(ack.Epoch);
                writer.WriteEnum(ack.Phase);
                break;
            case ChunkStateResetPackage reset:
                writer.Write(reset.Chunk);
                break;
            case SubsystemTimePackage time:
                writer.Write(time.Time);
                writer.Write(time.TimeOfDayOffset);
                break;
            case SubsystemSkyPackage sky:
                writer.Write(sky.LightningStrikePosition);
                break;
            case SubsystemSeasonPackage season:
                writer.Write(season.TimeOfYear);
                break;
            default:
                throw new ArgumentException($"Unknown game message type: {message.GetType().Name}.", nameof(message));
        }
    }

    public static bool IsDefined(byte id) => (PackageType)id is
        PackageType.ComponentOnFire or
        PackageType.ComponentBehavior or
        PackageType.Command or
        PackageType.ComponentInventory or
        PackageType.Furniture or
        PackageType.EditableBlock or
        PackageType.Pickable or
        PackageType.Projectile or
        PackageType.Message or
        PackageType.GroupManage or
        PackageType.PlayerData or
        PackageType.Explosion or
        PackageType.ComponentHealth or
        PackageType.MovingBlockSet or
        PackageType.ComponentVitalStat or
        PackageType.ComponentClothing or
        PackageType.ComponentFurnace or
        PackageType.SubsystemElectricity or
        PackageType.SignBlock or
        PackageType.BlockEdit or
        PackageType.Dispenser or
        PackageType.ComponentSickness or
        PackageType.ModEnvelope or
        PackageType.PlayerList or
        PackageType.OnlinePlayerState or
        PackageType.ComponentMount or
        PackageType.ComponentSleep or
        PackageType.SubsystemWeather or
        PackageType.ComponentFlu or
        PackageType.ComponentPlayer or
        PackageType.SubsystemTerrain or
        PackageType.SubsystemBody or
        PackageType.ServerInfo or
        PackageType.Client or
        PackageType.Bootstrap or
        PackageType.InitialWorldSnapshot or
        PackageType.Entity or
        PackageType.ConnectionRequest or
        PackageType.ConnectionReject or
        PackageType.ConnectionPhaseAck or
        PackageType.ChunkStateReset or
        PackageType.SubsystemTime or
        PackageType.SubsystemSky or
        PackageType.SubsystemSeason;

    private static ConnectionPhaseAckPackage ReadPhaseAck(ProtocolReader reader)
    {
        var epoch = reader.ReadGuid();
        var phase = reader.ReadEnum<ConnectionPhase>();
        return Enum.IsDefined(phase)
            ? new ConnectionPhaseAckPackage(epoch, phase)
            : throw new InvalidDataException("Unknown connection phase.");
    }

    private static ConnectionRequestPackage ReadConnectionRequest(ProtocolReader reader)
    {
        var magic = reader.ReadInt32();
        var version = reader.ReadInt32();
        var token = reader.ReadGuid();
        var identity = reader.ReadGuid();
        return new ConnectionRequestPackage(token, version, identity, reader.ReadString(), magic);
    }
}
