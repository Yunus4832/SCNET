using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Admission;

using LiteNetLib.Utils;

namespace Game.Network;

public static class CommonLib
{
    public const int DisconnectTimeout = 10000;

    public static readonly NetNode Net = new();

    public static Texture2D? BlockTexture;

    public static bool IsOfflineMode;

    public static WorkType WorkType { get; set; }

    public static bool IsWorkTypeValid()
    {
        return WorkType == GetCurrentWorkType();
    }

    private static WorkType GetCurrentWorkType()
    {
        if (Net.IsServer)
        {
            return WorkType.Server;
        }

        return Net.CurrentStage == NetNode.Stage.Connected ? WorkType.Client : WorkType.Local;
    }

    public static ComponentCharacter? LocalCharacter
    {
        get
        {
            if (Net.Self == null)
            {
                return null;
            }

            var project = GameManager.Project;
            return project?.FindSubsystem<SubsystemCharacters>(true)?.ComponentCharacters
                .FirstOrDefault(p => p.CharacterData.CharacterId == Net.Self.CharacterId);
        }
    }

    public static bool StartServer(WorldSettings worldSettings, ServerAdmissionConfiguration? policyOverride = null)
    {
        var started = Net.StartServer(
            SettingsManager.Current.ServerPort,
            SettingsManager.Current.BroadcastPort,
            worldSettings.MaxOnlinePlayerCount,
            worldSettings.P2P with
            {
                Name = string.IsNullOrEmpty(worldSettings.P2P.Name) ? worldSettings.Name : worldSettings.P2P.Name
            },
            policyOverride
        );
        if (started) { Net.ActiveHostSession = null; }

        return started;
    }

    public static bool StartServer(
        SessionInfo sessionInfo,
        WorldSettings worldSettings,
        ServerAdmissionConfiguration? policyOverride = null
    )
    {
        var serverPort = sessionInfo.ServerPort > 0
            ? sessionInfo.ServerPort
            : SettingsManager.Current.ServerPort;
        int? broadcastPort = sessionInfo.BroadcastPort switch
        {
            < 0 => null,
            > 0 => sessionInfo.BroadcastPort,
            _ => SettingsManager.Current.BroadcastPort
        };
        var effectivePolicy = policyOverride ??
                              sessionInfo.AdmissionOverrides.Apply(
                                  ServerAdmissionConfiguration.Load(
                                      GameManager.Project!.ExtensionData.Get("game", "admission")
                                  )
                              );
        var started = Net.StartServer(
            serverPort,
            broadcastPort,
            worldSettings.MaxOnlinePlayerCount,
            sessionInfo.P2POverrides.Apply(worldSettings.P2P, worldSettings.Name),
            effectivePolicy
        );
        if (started) { Net.ActiveHostSession = sessionInfo; }

        return started;
    }

    public static NetDataWriter GetWriter(
        ProtocolWriter writer,
        out int size,
        CompressionPolicy compressionPolicy = CompressionPolicy.Adaptive
    )
    {
        var data = writer.Data(compressionPolicy);
        var tmp = new NetDataWriter();
        tmp.PutBytesWithLength(data);
        size = data.Length;
        return tmp;
    }

    public static ProtocolReader GetReader(ReadOnlyMemory<byte> data) => ProtocolPacketCodec.CreateReader(data);

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
            var port = uri.Port < 1 ? SettingsManager.Current.ServerPort : uri.Port;
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

    public static string SerializeVDict(ValuesDictionary dict)
    {
        var elem = new XElement("Values");
        dict.Save(elem);
        return XmlUtils.SaveXmlToString(elem, true);
    }

    public static ValuesDictionary ReadVDict(string str)
    {
        var dict = new ValuesDictionary();
        var elem = XmlUtils.LoadXmlFromString(str, true);
        dict.ApplyOverrides(elem);
        return dict;
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
