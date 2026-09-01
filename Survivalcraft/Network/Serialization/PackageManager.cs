using System.Net;

using Game.Network.Packages;
using Game.Network.Packages.Handlers;

using LiteNetLib;
using LiteNetLib.Utils;

namespace Game.Network.Serialization;

public enum PackageType : byte
{
    ServerInfo,
    ConnectionRequest,
    ConnectionReject,
    Client,
    Pickable,
    SubsystemBody,
    SubsystemTerrain,
    SubsystemTime,
    SubsystemSky,
    SubsystemWeather,
    SubsystemElectricity,
    SubsystemPlayers,
    ComponentPlayer,
    ComponentInventory,
    ComponentVitalStat,
    ComponentClothing,
    ComponentBehavior,
    ComponentHealth,
    ComponentMount,
    ComponentSickness,
    ComponentFlu,
    ComponentOnFire,
    ComponentSleep,
    ComponentFurnace,
    PlayerData,
    Message,
    Entity,
    Projectile,
    Territoriy,
    Furniture,
    Explosion,
    MovingBlockSet,
    BlockEdit, // 打开某些方块或动物实体的背包，比如：箱子，熔炉，发射器
    SignBlock,

    Dispenser,
    EditableBlock,
    GroupManage,
    SubsystemSeason,
    Command,
    PlayerList,
    OnlinePlayerState,
    Bootstrap,
    ConnectionPhaseAck,
    InitialWorldSnapshot,
    PlayerJoined,

    ModPackage = 255 // 为后面的mod数据传输做保留
}

public class PackageManager
{
    private sealed class PackageRegistration
    {
        public required string Name { get; init; }

        public required Func<IPackage> Create { get; init; }
    }

    private static PackageRegistration?[] _packageRegistrations = new PackageRegistration[byte.MaxValue + 1];

    public static void Initialize()
    {
        _packageRegistrations = new PackageRegistration?[byte.MaxValue + 1];

        RegisterBuiltInPackages();
    }

    private static void RegisterBuiltInPackages()
    {
        RegisterBuiltInPackage<ServerInfoPackage, ServerInfoPackageHandler>(PackageType.ServerInfo);
        RegisterBuiltInPackage<ConnectionRequestPackage, ConnectionRequestPackageHandler>(PackageType
            .ConnectionRequest);
        RegisterBuiltInPackage<ConnectionRejectPackage, ConnectionRejectPackageHandler>(PackageType.ConnectionReject);
        RegisterBuiltInPackage<ClientPackage, ClientPackageHandler>(PackageType.Client);
        RegisterBuiltInPackage<PickablePackage, PickablePackageHandler>(PackageType.Pickable);
        RegisterBuiltInPackage<SubsystemBodyPackage, SubsystemBodyPackageHandler>(PackageType.SubsystemBody);
        RegisterBuiltInPackage<SubsystemTerrainPackage, SubsystemTerrainPackageHandler>(PackageType.SubsystemTerrain);
        RegisterBuiltInPackage<SubsystemTimePackage, SubsystemTimePackageHandler>(PackageType.SubsystemTime);
        RegisterBuiltInPackage<SubsystemSkyPackage, SubsystemSkyPackageHandler>(PackageType.SubsystemSky);
        RegisterBuiltInPackage<SubsystemWeatherPackage, SubsystemWeatherPackageHandler>(PackageType.SubsystemWeather);
        RegisterBuiltInPackage<SubsystemElectricityPackage, SubsystemElectricityPackageHandler>(PackageType
            .SubsystemElectricity);
        RegisterBuiltInPackage<SubsystemPlayersPackage, SubsystemPlayersPackageHandler>(PackageType.SubsystemPlayers);
        RegisterBuiltInPackage<ComponentPlayerPackage, ComponentPlayerPackageHandler>(PackageType.ComponentPlayer);
        RegisterBuiltInPackage<ComponentInventoryPackage, ComponentInventoryPackageHandler>(PackageType
            .ComponentInventory);
        RegisterBuiltInPackage<ComponentVitalStatPackage, ComponentVitalStatPackageHandler>(PackageType
            .ComponentVitalStat);
        RegisterBuiltInPackage<ComponentClothingPackage, ComponentClothingPackageHandler>(PackageType
            .ComponentClothing);
        RegisterBuiltInPackage<ComponentBehaviorPackage, ComponentBehaviorPackageHandler>(PackageType
            .ComponentBehavior);
        RegisterBuiltInPackage<ComponentHealthPackage, ComponentHealthPackageHandler>(PackageType.ComponentHealth);
        RegisterBuiltInPackage<ComponentMountPackage, ComponentMountPackageHandler>(PackageType.ComponentMount);
        RegisterBuiltInPackage<ComponentSicknessPackage, ComponentSicknessPackageHandler>(PackageType
            .ComponentSickness);
        RegisterBuiltInPackage<ComponentFluPackage, ComponentFluPackageHandler>(PackageType.ComponentFlu);
        RegisterBuiltInPackage<ComponentOnFirePackage, ComponentOnFirePackageHandler>(PackageType.ComponentOnFire);
        RegisterBuiltInPackage<ComponentSleepPackage, ComponentSleepPackageHandler>(PackageType.ComponentSleep);
        RegisterBuiltInPackage<ComponentFurnacePackage, ComponentFurnacePackageHandler>(PackageType.ComponentFurnace);
        RegisterBuiltInPackage<PlayerDataPackage, PlayerDataPackageHandler>(PackageType.PlayerData);
        RegisterBuiltInPackage<MessagePackage, MessagePackageHandler>(PackageType.Message);
        RegisterBuiltInPackage<EntityPackage, EntityPackageHandler>(PackageType.Entity);
        RegisterBuiltInPackage<ProjectilePackage, ProjectilePackageHandler>(PackageType.Projectile);
        RegisterBuiltInPackage<TerritoriyPackage, TerritoriyPackageHandler>(PackageType.Territoriy);
        RegisterBuiltInPackage<FurniturePackage, FurniturePackageHandler>(PackageType.Furniture);
        RegisterBuiltInPackage<ExplosionsPackage, ExplosionsPackageHandler>(PackageType.Explosion);
        RegisterBuiltInPackage<MovingBlockPackage, MovingBlockPackageHandler>(PackageType.MovingBlockSet);
        RegisterBuiltInPackage<BlockEditPackage, BlockEditPackageHandler>(PackageType.BlockEdit);
        RegisterBuiltInPackage<SignBlockPackage, SignBlockPackageHandler>(PackageType.SignBlock);
        RegisterBuiltInPackage<DispenserPackage, DispenserPackageHandler>(PackageType.Dispenser);
        RegisterBuiltInPackage<EditableBlockPackage, EditableBlockPackageHandler>(PackageType.EditableBlock);
        RegisterBuiltInPackage<GroupManagePackage, GroupManagePackageHandler>(PackageType.GroupManage);
        RegisterBuiltInPackage<SubsystemSeasonPackage, SubsystemSeasonPackageHandler>(PackageType.SubsystemSeason);
        RegisterBuiltInPackage<CommandPackage, CommandPackageHandler>(PackageType.Command);
        RegisterBuiltInPackage<PlayerListPackage, PlayerListPackageHandler>(PackageType.PlayerList);
        RegisterBuiltInPackage<OnlinePlayerStatePackage, OnlinePlayerStatePackageHandler>(
            PackageType.OnlinePlayerState);
        RegisterBuiltInPackage<BootstrapPackage, BootstrapPackageHandler>(PackageType.Bootstrap);
        RegisterBuiltInPackage<ConnectionPhaseAckPackage, ConnectionPhaseAckPackageHandler>(
            PackageType.ConnectionPhaseAck);
        RegisterBuiltInPackage<InitialWorldSnapshotPackage, InitialWorldSnapshotPackageHandler>(
            PackageType.InitialWorldSnapshot);
        RegisterBuiltInPackage<PlayerJoinedPackage, PlayerJoinedPackageHandler>(PackageType.PlayerJoined);
        RegisterBuiltInPackage<ModEnvelopePackage, ModEnvelopePackageHandler>(PackageType.ModPackage);
    }

    public static void RegisterPackage(Func<IPackage> factory)
    {
        var package = factory();
        RegisterPackage(package.ID, package.GetType().Name, package.GetType(), factory);
    }

    public static void RegisterPackage(PackageType packageType, Func<IPackage> factory)
    {
        var package = factory();
        var packageID = (byte)packageType;
        if (package.ID != packageID)
        {
            throw new InvalidOperationException(
                $"数据包ID不匹配，注册ID:{packageID}，Package[{package.GetType().Name}] ID:{package.ID}");
        }

        RegisterPackage(packageID, package.GetType().Name, package.GetType(), factory);
    }

    private static void RegisterBuiltInPackage<TPackage, THandler>(PackageType packageType)
        where TPackage : IPackage, new()
        where THandler : IPackageHandler<TPackage>, new()
    {
        var package = new TPackage();
        var packageID = (byte)packageType;
        if (package.ID != packageID)
        {
            throw new InvalidOperationException(
                $"数据包ID不匹配，注册ID:{packageID}，Package[{typeof(TPackage).Name}] ID:{package.ID}");
        }

        RegisterPackage(packageID, typeof(TPackage).Name, typeof(TPackage), () => new TPackage(), new THandler());
    }

    public static void RegisterPackage(IPackage package)
    {
        var packageType = package.GetType();
        RegisterPackage(package.ID, packageType.Name, packageType,
            () => (IPackage)Activator.CreateInstance(packageType)!);
    }

    private static void RegisterPackage(
        byte packageID,
        string packageName,
        Type packageType,
        Func<IPackage> factory,
        IPackageHandler? handler = null
    )
    {
        if (_packageRegistrations[packageID] == null)
        {
            _packageRegistrations[packageID] = new PackageRegistration
            {
                Name = packageName,
                Create = factory
            };
            if (handler != null)
            {
                PackageDispatcher.Register(handler);
            }
            else
            {
                PackageDispatcher.RegisterLegacyHandler(packageType);
            }

            Log.Debug($"注册Package[{packageName}]，ID:{packageID}");
        }
        else
        {
            throw new Exception("数据包存在冲突");
        }
    }

    public static void UnRegisterPackage(IPackage package)
    {
        var basePackage = _packageRegistrations[package.ID];
        if (basePackage == null)
        {
            return;
        }

        _packageRegistrations[package.ID] = null;
        PackageDispatcher.Unregister(package.GetType());
    }

    public static T DecodePackage<T>(
        NetNode? netNode,
        NetDataReader reader,
        NetPeer? netPeer = null,
        ConnectionRequest? request = null,
        IPEndPoint? iPEndPoint = null
    ) where T : class
    {
        var obj = DecodePackages(netNode, reader, netPeer, request, iPEndPoint)[0];
        return (T)obj;
    }

    public static List<IPackage> DecodePackages(
        NetNode? netNode,
        NetDataReader reader,
        NetPeer? netPeer = null,
        ConnectionRequest? request = null,
        IPEndPoint? iPEndPoint = null
    )
    {
        Client? from = null;
        if (netNode != null)
        {
            from = netNode.Clients.Values
                .FirstOrDefault(client => (client.Peer != null && client.Peer == netPeer) ||
                                          (client.Request != null && client.Request == request) ||
                                          (client.IPPoint != null &&
                                           Equals(client.IPPoint, iPEndPoint)));
        }

        if (from == null)
        {
            if (netPeer != null)
            {
                from = new Client(netPeer);
            }

            if (request != null)
            {
                from = new Client(request);
            }

            if (iPEndPoint != null)
            {
                from = new Client(iPEndPoint);
            }
        }

        var packages = new List<IPackage>();
        byte packageID = 99;
        if (reader.AvailableBytes == 0)
        {
            return packages;
        }

        try
        {
            var packageReader = CommonLib.GetReader(reader);
            IPackage? last = null;
            while (packageReader.BaseStream.Position < packageReader.BaseStream.Length)
            {
                var check = packageReader.ReadByte();
                if (check != 0x88)
                {
                    Log.Information("解包验证出错，上个包:" + (last == null ? "无" : last.GetType().Name));
                    break;
                }

                var id = packageReader.ReadByte();
                packageID = id;
                var registration = _packageRegistrations[id];
                if (registration != null)
                {
                    var package = registration.Create();
                    last = package;
                    package.From = from!;
                    package.ReadData(packageReader);
                    packages.Add(package);
                }
                else
                {
                    Log.Information(string.Format($"不存在的包ID:{id}"));
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            Log.Information("接收到不规范的数据包" + packageID);
        }

        return packages;
    }
}
