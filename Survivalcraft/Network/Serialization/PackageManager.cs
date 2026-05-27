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
    private static IPackage?[] _basePackages = new IPackage[byte.MaxValue];

    public static void Initialize()
    {
        _basePackages = new IPackage[byte.MaxValue];

        var list = typeof(PackageManager).Assembly.GetTypes();
        var type = typeof(IPackage);
        var regList = list
            .Where(item => type.IsAssignableFrom(item) && item is { IsInterface: false, IsAbstract: false }).ToList();

        foreach (var obj in regList.Select(Activator.CreateInstance))
        {
            if (obj is not IPackage package)
            {
                continue;
            }

            RegisterPackage(package);
        }
    }

    public static void RegisterPackage(IPackage package)
    {
        var theID = package.ID;
        if (_basePackages[theID] == null)
        {
            _basePackages[theID] = package;
#if DEBUG
            Log.Information($"注册Package[{package.GetType().Name}]，ID:{theID}");
#endif
        }
        else
        {
            throw new Exception("数据包存在冲突");
        }
    }

    public static void UnRegisterPackage(IPackage package)
    {
        var basePackage = _basePackages[package.ID];
        if (basePackage != null)
        {
            _basePackages[package.ID] = null;
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
                if (_basePackages[id] != null)
                {
                    var obj = Activator.CreateInstance(_basePackages[id]!.GetType());
                    var package = (IPackage)obj!;
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
