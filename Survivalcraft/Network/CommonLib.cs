using System.Net;
using System.Net.Sockets;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Admission;
using Game.Servers;

namespace Game.Network;

public static class CommonLib
{
    private static NetNode? _net;

    public static bool IsNetworkInitialized => _net != null;

    public static NetNode Net => _net ??
                                 throw new InvalidOperationException(
                                     "Initialize network ownership after loading application settings."
                                 );

    public static void InitializeNetwork(NetNodeOptions options, bool clientPresentation)
    {
        if (_net != null) { throw new InvalidOperationException("Network ownership is already initialized."); }

        _net = new NetNode(
            options,
            clientPresentation,
            Handling.ApplicationMessageHandlers.CreatePreWorld(NodeRole.Inactive, clientPresentation),
            NodeRole.Inactive
        );
        GameManager.ObserveEndpoint(_net);
    }

    public static bool StartServer(
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        ServerAdmissionPolicy? policyOverride = null
    )
    {
        return ServerListener.Start(
            Net,
            Configuration.HostingConfiguration.Preferences,
            LocalIdentityManager.Current,
            new SessionInfo(),
            worldSettings,
            hosting,
            policyOverride
        );
    }

    public static bool StartServer(
        SessionInfo sessionInfo,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        ServerAdmissionPolicy? policyOverride = null
    )
    {
        return ServerListener.Start(
            Net,
            Configuration.HostingConfiguration.Preferences,
            LocalIdentityManager.Current,
            sessionInfo,
            worldSettings,
            hosting,
            policyOverride
        );
    }

    public static string GetInnerIp()
    {
        var ipHost = Dns.GetHostAddresses(Dns.GetHostName());
        foreach (var ip in ipHost)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }

        return ipHost[0].ToString();
    }

    public static bool Resolve(string ip, out IPEndPoint? ep)
    {
        if (Uri.TryCreate("tcp://" + ip, UriKind.Absolute, out var uri))
        {
            var port = uri.Port < 1 ? Configuration.HostingConfiguration.Preferences.ServerPort : uri.Port;
            if (IPAddress.TryParse(uri.Host, out var addr))
            {
                ep = new IPEndPoint(addr, port);
                return true;
            }

            var entry = Dns.GetHostEntry(uri.Host);
            if (entry.AddressList.Length > 0)
            {
                ep = new IPEndPoint(entry.AddressList[0], port);
                return true;
            }
        }

        ep = null;
        return false;
    }

    public static byte[]? GetNowProject(Project project)
    {
        var previousMode = project.SendToClientMode;
        project.SendToClientMode = true;
        try
        {
            var rootNode = new ValuesDictionary();
            var projData = project.Save();
            projData.EntityDataList.EntitiesData.Clear();
            projData.Save(rootNode);
            return rootNode.ToMessagePack();
        }
        finally
        {
            project.SendToClientMode = previousMode;
        }
    }

    /// <summary>
    ///     获取时间戳，单位0.1ms
    /// </summary>
    /// <returns></returns>
    public static long GetMicroSeconds()
    {
        return DateTime.Now.Ticks / 10000;
    }
}
