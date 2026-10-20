using Game.Network.Transport;

namespace Game.Network.Handling;

public class PackageManager
{
    private sealed class PackageRegistration
    {
        public required string Name { get; init; }

        public required Func<ProtocolReader, IProtocolMessage> Read { get; init; }
    }

    private static PackageRegistration?[] _packageRegistrations = new PackageRegistration[byte.MaxValue + 1];

    public static void Initialize()
    {
        _packageRegistrations = new PackageRegistration?[byte.MaxValue + 1];

        RegisterBuiltInPackages();
    }

    private static void RegisterBuiltInPackages()
    {
        RegisterProtocolPackage<ServerInfoPackage, ServerInfoPackageHandler>(PackageType.ServerInfo);
        RegisterProtocolPackage<ConnectionRequestPackage, ConnectionRequestPackageHandler>(PackageType
            .ConnectionRequest);
        RegisterProtocolPackage<ConnectionRejectPackage, ConnectionRejectPackageHandler>(PackageType.ConnectionReject);
        RegisterProtocolPackage<ClientPackage, ClientPackageHandler>(PackageType.Client);
        RegisterProtocolPackage<PickablePackage, PickablePackageHandler>(PackageType.Pickable);
        RegisterProtocolPackage<SubsystemBodyPackage, SubsystemBodyPackageHandler>(PackageType.SubsystemBody);
        RegisterProtocolPackage<SubsystemTerrainPackage, SubsystemTerrainPackageHandler>(PackageType.SubsystemTerrain);
        RegisterProtocolPackage<SubsystemTimePackage, SubsystemTimePackageHandler>(PackageType.SubsystemTime);
        RegisterProtocolPackage<SubsystemSkyPackage, SubsystemSkyPackageHandler>(PackageType.SubsystemSky);
        RegisterProtocolPackage<SubsystemWeatherPackage, SubsystemWeatherPackageHandler>(PackageType.SubsystemWeather);
        RegisterProtocolPackage<SubsystemElectricityPackage, SubsystemElectricityPackageHandler>(PackageType
            .SubsystemElectricity);
        RegisterPlayerPackages();
        RegisterProtocolPackage<ComponentInventoryPackage, ComponentInventoryPackageHandler>(PackageType
            .ComponentInventory);
        RegisterProtocolPackage<ComponentVitalStatPackage, ComponentVitalStatPackageHandler>(PackageType
            .ComponentVitalStat);
        RegisterProtocolPackage<ComponentClothingPackage, ComponentClothingPackageHandler>(PackageType
            .ComponentClothing);
        RegisterProtocolPackage<ComponentBehaviorPackage, ComponentBehaviorPackageHandler>(PackageType
            .ComponentBehavior);
        RegisterProtocolPackage<ComponentHealthPackage, ComponentHealthPackageHandler>(PackageType.ComponentHealth);
        RegisterProtocolPackage<ComponentMountPackage, ComponentMountPackageHandler>(PackageType.ComponentMount);
        RegisterProtocolPackage<ComponentSicknessPackage, ComponentSicknessPackageHandler>(PackageType
            .ComponentSickness);
        RegisterProtocolPackage<ComponentFluPackage, ComponentFluPackageHandler>(PackageType.ComponentFlu);
        RegisterProtocolPackage<ComponentOnFirePackage, ComponentOnFirePackageHandler>(PackageType.ComponentOnFire);
        RegisterProtocolPackage<ComponentSleepPackage, ComponentSleepPackageHandler>(PackageType.ComponentSleep);
        RegisterProtocolPackage<ComponentFurnacePackage, ComponentFurnacePackageHandler>(PackageType.ComponentFurnace);
        RegisterProtocolPackage<PlayerDataPackage, PlayerDataPackageHandler>(PackageType.PlayerData);
        RegisterProtocolPackage<MessagePackage, MessagePackageHandler>(PackageType.Message);
        RegisterProtocolPackage<EntityPackage, EntityPackageHandler>(PackageType.Entity);
        RegisterProtocolPackage<ProjectilePackage, ProjectilePackageHandler>(PackageType.Projectile);
        RegisterProtocolPackage<FurniturePackage, FurniturePackageHandler>(PackageType.Furniture);
        RegisterProtocolPackage<ExplosionsPackage, ExplosionsPackageHandler>(PackageType.Explosion);
        RegisterProtocolPackage<MovingBlockPackage, MovingBlockPackageHandler>(PackageType.MovingBlockSet);
        RegisterProtocolPackage<BlockEditPackage, BlockEditPackageHandler>(PackageType.BlockEdit);
        RegisterProtocolPackage<SignBlockPackage, SignBlockPackageHandler>(PackageType.SignBlock);
        RegisterProtocolPackage<DispenserPackage, DispenserPackageHandler>(PackageType.Dispenser);
        RegisterProtocolPackage<EditableBlockPackage, EditableBlockPackageHandler>(PackageType.EditableBlock);
        RegisterProtocolPackage<GroupManagePackage, GroupManagePackageHandler>(PackageType.GroupManage);
        RegisterProtocolPackage<SubsystemSeasonPackage, SubsystemSeasonPackageHandler>(PackageType.SubsystemSeason);
        RegisterProtocolPackage<CommandPackage, CommandPackageHandler>(PackageType.Command);
        RegisterProtocolPackage<PlayerListPackage, PlayerListPackageHandler>(PackageType.PlayerList);
        RegisterProtocolPackage<OnlinePlayerStatePackage, OnlinePlayerStatePackageHandler>(
            PackageType.OnlinePlayerState);
        RegisterProtocolPackage<BootstrapPackage, BootstrapPackageHandler>(PackageType.Bootstrap);
        RegisterProtocolPackage<ConnectionPhaseAckPackage, ConnectionPhaseAckPackageHandler>(
            PackageType.ConnectionPhaseAck);
        RegisterProtocolPackage<InitialWorldSnapshotPackage, InitialWorldSnapshotPackageHandler>(
            PackageType.InitialWorldSnapshot);
        RegisterProtocolPackage<ChunkStateResetPackage, ChunkStateResetPackageHandler>(PackageType.ChunkStateReset);
        RegisterProtocolPackage<ModEnvelopePackage, ModEnvelopePackageHandler>(PackageType.ModEnvelope);
    }

    public static void RegisterPackage<TPackage>(Func<TPackage> factory, IPackageHandler<TPackage> handler)
        where TPackage : IProtocolMessage
    {
        var message = factory();
        if (!ProtocolMessageCodec.IsDefined(message.ID) || handler.PackageType != typeof(TPackage))
        { throw new ArgumentException("The handler must match a defined protocol message."); }
        if (_packageRegistrations[message.ID] != null) { throw new InvalidOperationException("Duplicate protocol message ID."); }
        _packageRegistrations[message.ID] = new PackageRegistration
        { Name = typeof(TPackage).Name, Read = reader => ProtocolMessageCodec.Read(message.ID, reader) };
        PackageDispatcher.Register(handler);
    }

    private static THandler RegisterProtocolPackage<TPackage, THandler>(PackageType id)
        where TPackage : IProtocolMessage
        where THandler : IPackageHandler<TPackage>, new()
    {
        var key = (byte)id;
        if (_packageRegistrations[key] != null)
        {
            throw new InvalidOperationException($"Duplicate protocol message ID: {key}.");
        }
        _packageRegistrations[key] = new PackageRegistration
        {
            Name = typeof(TPackage).Name,
            Read = reader => ProtocolMessageCodec.Read(key, reader)
        };
        var handler = new THandler();
        PackageDispatcher.Register(handler);
        return handler;
    }

    private static void RegisterPlayerPackages()
    {
        var handler = RegisterProtocolPackage<ComponentPlayerPackage, ComponentPlayerPackageHandler>(PackageType.ComponentPlayer);
        PackageDispatcher.Register(typeof(PlayerStatePackage), handler);
        PackageDispatcher.Register(typeof(PlayerInteractionPackage), handler);
        PackageDispatcher.Register(typeof(PlayerAimPackage), handler);
        PackageDispatcher.Register(typeof(PlayerDigPackage), handler);
        PackageDispatcher.Register(typeof(PlayerHitPackage), handler);
        PackageDispatcher.Register(typeof(PlayerFlightPackage), handler);
        PackageDispatcher.Register(typeof(PlayerReadyPackage), handler);
        PackageDispatcher.Register(typeof(PlayerRestartPackage), handler);
        PackageDispatcher.Register(typeof(PlayerExperiencePackage), handler);
        PackageDispatcher.Register(typeof(PlayerDropPackage), handler);
        PackageDispatcher.Register(typeof(PlayerDragDropPackage), handler);
        PackageDispatcher.Register(typeof(PlayerStatisticsPackage), handler);
        PackageDispatcher.Register(typeof(PlayerPositionPackage), handler);
    }

    public static void WriteMessage(IProtocolMessage message, ProtocolWriter writer) => ProtocolMessageCodec.Write(message, writer);

    public static ClientState MinimumState(IProtocolMessage message) => message switch
    {
        ModEnvelopePackage envelope => envelope.RequiredState,
        CommandPackage => ClientState.ProjectLoaded,
        ComponentInventoryPackage => ClientState.ProjectLoaded,
        FurniturePackage => ClientState.ProjectLoaded,
        EditableBlockPackage => ClientState.ProjectLoaded,
        PickablePackage or ProjectilePackage => ClientState.ProjectLoaded,
        PlayerDataPackage => ClientState.Connected,
        MessagePackage or GroupManagePackage => ClientState.ProjectLoaded,
        ComponentBehaviorPackage => ClientState.Playing,
        ComponentHealthPackage or ComponentOnFirePackage or MovingBlockPackage or ExplosionsPackage => ClientState.ProjectLoaded,
        ComponentVitalStatPackage or SubsystemElectricityPackage => ClientState.Playing,
        ComponentClothingPackage or ComponentFurnacePackage or SignBlockPackage or BlockEditPackage => ClientState.ProjectLoaded,
        DispenserPackage or ComponentSicknessPackage or PlayerListPackage or OnlinePlayerStatePackage or ComponentMountPackage or ComponentSleepPackage or SubsystemWeatherPackage or ComponentFluPackage => ClientState.ProjectLoaded,
        SubsystemTimePackage or SubsystemSkyPackage or ChunkStateResetPackage or SubsystemBodyPackage => ClientState.Playing,
        SubsystemSeasonPackage or EntityPackage or ServerInfoPackage or SubsystemTerrainPackage or ComponentPlayerPackage => ClientState.ProjectLoaded,
        _ => ClientState.NotConnected
    };

    public static void UnRegisterPackage(IProtocolMessage package)
    {
        var basePackage = _packageRegistrations[package.ID];
        if (basePackage == null)
        {
            return;
        }

        _packageRegistrations[package.ID] = null;
        PackageDispatcher.Unregister(package.GetType());
    }

    public static (T Package, PackageReceiveContext Context) DecodePackage<T>(
        NetNode? netNode,
        ReadOnlyMemory<byte> payload,
        Client? sender = null
    ) where T : class, IProtocolMessage
    {
        var obj = DecodePackages(netNode, payload, sender)[0];
        return ((T)obj.Package, obj.Context);
    }

    public static List<ReceivedPackage> DecodePackages(
        NetNode? netNode,
        ReadOnlyMemory<byte> payload,
        Client? sender = null
    )
    {
        var packages = new List<ReceivedPackage>();
        var context = new PackageReceiveContext(netNode, netNode?.IsServer ?? false, sender);
        byte packageID = 99;
        if (payload.IsEmpty)
        {
            return packages;
        }

        try
        {
            using var packageReader = CommonLib.GetReader(payload);
            IProtocolMessage? last = null;
            while (packageReader.BaseStream.Position < packageReader.BaseStream.Length)
            {
                var check = packageReader.ReadByte();
                if (check != 0x88)
                {
                    throw new InvalidDataException("Invalid message marker after " + last?.GetType().Name);
                }

                var id = packageReader.ReadByte();
                packageID = id;
                var registration = _packageRegistrations[id];
                if (registration != null)
                {
                    var package = registration.Read(packageReader);
                    last = package;
                    packages.Add(new ReceivedPackage(package, context));
                }
                else
                {
                    throw new InvalidDataException($"Unknown message ID: {id}.");
                }
            }
        }
        catch (Exception e)
        {
            packages.Clear();
            Log.Error(e.ToString());
            Log.Information("接收到不规范的数据包" + packageID);
        }

        return packages;
    }
}
