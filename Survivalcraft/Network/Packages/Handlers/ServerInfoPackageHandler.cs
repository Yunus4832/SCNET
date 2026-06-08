using Game.Network.Enums;
using Game.Network.Serialization;

using static Game.Screens.NetPlayScreen;

namespace Game.Network.Packages;

public partial class ServerInfoPackage
{
    internal void HandleCore(NetNode? netNode, bool isServer)
    {
        if (RequestInfo)
        {
            if (From?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(new ServerInfoPackage(false), From.IPPoint);
            }
        }
        else
        {
            var p = ScreensManager.FindScreen<NetPlayScreen>("NetPlay", true)!;
            var c = new Connect
            {
                State = ConnectState.Avaliable,
                IP = From?.IPPoint?.ToString() ?? string.Empty
            };
            c.Name = c.IP;
            c.GameMode = GameMode;
            c.HasPassword = NeedPasswd;
            c.IsNeedLoginCommunity = NeedLogin;
            c.MaxCount = MaxPlayerCount;
            c.PlayerCount = ClientCount;
            c.FromBroadcast = From?.IsLocalRemote ?? false;
            c.FromLocal = false;
            c.FromCommunity = false;
            c.UsedTime = Ping;
            c.Version = Version;
            c.TimeOfDay = TimeOfDay;
            c.ModServerAddress = ModServerAddress;
            c.Season = Season;
            c.TimeOfSeason = TimeOfSeason;
            if (IpToDNS.TryGetValue(c.IP, out var dns))
            {
                c.IP = dns;
            }

            if (DNSToName.TryGetValue(c.IP, out var name))
            {
                c.Name = name;
            }

            if (p.CheckConnectExists(c, out var found))
            {
                found!.State = c.State;
                found.IP = string.IsNullOrEmpty(dns) ? found.IP : c.IP;
                found.Name = string.IsNullOrEmpty(name) ? found.Name : c.Name;
                found.GameMode = c.GameMode;
                found.HasPassword = c.HasPassword;
                found.IsNeedLoginCommunity = c.IsNeedLoginCommunity;
                found.MaxCount = c.MaxCount;
                found.PlayerCount = c.PlayerCount;
                found.FromBroadcast = c.FromBroadcast;
                found.UsedTime = c.UsedTime;
                found.Version = c.Version;
                found.TimeOfDay = c.TimeOfDay;
                found.ModServerAddress = ModServerAddress;
                found.Season = c.Season;
                found.TimeOfSeason = c.TimeOfSeason;
            }
            else
            {
                if (From is not null && From.IsLocalRemote) //局域网
                {
                    if (p.CheckSaveConnectExists(c, out var f))
                    {
                        ModsManager.SaveConnects.Remove(f!);
                    }

                    ModsManager.SaveConnects.Add(c);
                }
            }

            p.UpdateList();
        }
    }
}

public sealed class ServerInfoPackageHandler : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, NetNode? netNode, bool isServer)
    {
        package.HandleCore(netNode, isServer);
    }
}
