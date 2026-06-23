using static Game.Screens.NetPlayScreen;

namespace Game.Network.Packages.Handlers;

public sealed class ServerInfoPackageHandler : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, NetNode? netNode, bool isServer)
    {
        if (package.RequestInfo)
        {
            if (package.From?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(new ServerInfoPackage(false), package.From.IPPoint);
            }
        }
        else
        {
            var p = ScreensManager.FindScreen<NetPlayScreen>("NetPlay", true)!;
            var c = new Connect
            {
                State = ConnectState.Available,
                IP = package.From?.IPPoint?.ToString() ?? string.Empty
            };
            c.Name = c.IP;
            c.GameMode = package.GameMode;
            c.HasPassword = package.NeedPasswd;
            c.IsNeedLoginCommunity = package.NeedLogin;
            c.MaxCount = package.MaxPlayerCount;
            c.PlayerCount = package.ClientCount;
            c.FromBroadcast = package.From?.IsLocalRemote ?? false;
            c.FromLocal = false;
            c.FromCommunity = false;
            c.UsedTime = package.Ping;
            c.Version = package.Version;
            c.TimeOfDay = package.TimeOfDay;
            c.ModServerAddress = package.ModServerAddress;
            c.RequiredModProfile = package.RequiredModProfile;
            c.Season = package.Season;
            c.TimeOfSeason = package.TimeOfSeason;
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
                found.ModServerAddress = package.ModServerAddress;
                found.RequiredModProfile = package.RequiredModProfile;
                found.Season = c.Season;
                found.TimeOfSeason = c.TimeOfSeason;
            }
            else
            {
                if (package.From is not null && package.From.IsLocalRemote) //局域网
                {
                    if (p.CheckSaveConnectExists(c, out var f))
                    {
                        ConnectionDirectory.Saved.Remove(f!);
                    }

                    ConnectionDirectory.Saved.Add(c);
                }
            }

            p.UpdateList();
        }
    }
}
