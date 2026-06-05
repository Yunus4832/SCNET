using System.Net;

using Game.Network.Packages;

using LiteNetLib;
using LiteNetLib.Utils;

namespace Game.Network.Serialization;

public enum PackageType : byte
{
    ServerInfo,
    ConnectionRequest,
    ConnectionReject,
    Project,
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
        RegisterPackage(PackageType.ServerInfo, () => new ServerInfoPackage());
        RegisterPackage(PackageType.ConnectionRequest, () => new ConnectionRequestPackage());
        RegisterPackage(PackageType.ConnectionReject, () => new ConnectionRejectPackage());
        RegisterPackage(PackageType.Project, () => new ProjectPackage());
        RegisterPackage(PackageType.Client, () => new ClientPackage());
        RegisterPackage(PackageType.Pickable, () => new PickablePackage());
        RegisterPackage(PackageType.SubsystemBody, () => new SubsystemBodyPackage());
        RegisterPackage(PackageType.SubsystemTerrain, () => new SubsystemTerrainPackage());
        RegisterPackage(PackageType.SubsystemTime, () => new SubsystemTimePackage());
        RegisterPackage(PackageType.SubsystemSky, () => new SubsystemSkyPackage());
        RegisterPackage(PackageType.SubsystemWeather, () => new SubsystemWeatherPackage());
        RegisterPackage(PackageType.SubsystemElectricity, () => new SubsystemElectricityPackage());
        RegisterPackage(PackageType.SubsystemPlayers, () => new SubsystemPlayersPackage());
        RegisterPackage(PackageType.ComponentPlayer, () => new ComponentPlayerPackage());
        RegisterPackage(PackageType.ComponentInventory, () => new ComponentInventoryPackage());
        RegisterPackage(PackageType.ComponentVitalStat, () => new ComponentVitalStatPackage());
        RegisterPackage(PackageType.ComponentClothing, () => new ComponentClothingPackage());
        RegisterPackage(PackageType.ComponentBehavior, () => new ComponentBehaviorPackage());
        RegisterPackage(PackageType.ComponentHealth, () => new ComponentHealthPackage());
        RegisterPackage(PackageType.ComponentMount, () => new ComponentMountPackage());
        RegisterPackage(PackageType.ComponentSickness, () => new ComponentSicknessPackage());
        RegisterPackage(PackageType.ComponentFlu, () => new ComponentFluPackage());
        RegisterPackage(PackageType.ComponentOnFire, () => new ComponentOnFirePackage());
        RegisterPackage(PackageType.ComponentSleep, () => new ComponentSleepPackage());
        RegisterPackage(PackageType.ComponentFurnace, () => new ComponentFurnacePackage());
        RegisterPackage(PackageType.PlayerData, () => new PlayerDataPackage());
        RegisterPackage(PackageType.Message, () => new MessagePackage());
        RegisterPackage(PackageType.Entity, () => new EntityPackage());
        RegisterPackage(PackageType.Projectile, () => new ProjectilePackage());
        RegisterPackage(PackageType.Territoriy, () => new TerritoriyPackage());
        RegisterPackage(PackageType.Furniture, () => new FurniturePackage());
        RegisterPackage(PackageType.Explosion, () => new ExplosionsPackage());
        RegisterPackage(PackageType.MovingBlockSet, () => new MovingBlockPackage());
        RegisterPackage(PackageType.BlockEdit, () => new BlockEditPackage());
        RegisterPackage(PackageType.SignBlock, () => new SignBlockPackage());
        RegisterPackage(PackageType.Dispenser, () => new DispenserPackage());
        RegisterPackage(PackageType.EditableBlock, () => new EditableBlockPackage());
        RegisterPackage(PackageType.GroupManage, () => new GroupManagePackage());
        RegisterPackage(PackageType.SubsystemSeason, () => new SubsystemSeasonPackage());
    }

    public static void RegisterPackage(Func<IPackage> factory)
    {
        var package = factory();
        RegisterPackage(package.ID, package.GetType().Name, factory);
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

        RegisterPackage(packageID, package.GetType().Name, factory);
    }

    public static void RegisterPackage(IPackage package)
    {
        var packageType = package.GetType();
        RegisterPackage(package.ID, packageType.Name, () => (IPackage)Activator.CreateInstance(packageType)!);
    }

    private static void RegisterPackage(byte packageID, string packageName, Func<IPackage> factory)
    {
        if (_packageRegistrations[packageID] == null)
        {
            _packageRegistrations[packageID] = new PackageRegistration
            {
                Name = packageName,
                Create = factory
            };
#if DEBUG
            Log.Information($"注册Package[{packageName}]，ID:{packageID}");
#endif
        }
        else
        {
            throw new Exception("数据包存在冲突");
        }
    }

    public static void UnRegisterPackage(IPackage package)
    {
        var basePackage = _packageRegistrations[package.ID];
        if (basePackage != null)
        {
            _packageRegistrations[package.ID] = null;
        }
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
            var preader = CommonLib.GetReader(reader);
            IPackage? last = null;
            while (preader.BaseStream.Position < preader.BaseStream.Length)
            {
                var check = preader.ReadByte();
                if (check != 0x88)
                {
                    Log.Information("解包验证出错，上个包:" + (last == null ? "无" : last.GetType().Name));
                    break;
                }

                var id = preader.ReadByte();
                packageID = id;
                var registration = _packageRegistrations[id];
                if (registration != null)
                {
                    var package = registration.Create();
                    last = package;
                    package.From = from!;
                    package.ReadData(preader);
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
