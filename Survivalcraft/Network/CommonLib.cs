using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Network.Enums;
using Game.Network.Serialization;

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

    public static ComponentPlayer? MainPlayer
    {
        get
        {
            if (Net.Self == null)
            {
                return null;
            }

            var project = GameManager.Project;
            return project?.FindSubsystem<SubsystemPlayers>(true)?.ComponentPlayers
                .FirstOrDefault(p => p.PlayerData.PlayerGUID == Net.Self.GUID);
        }
    }

    public static bool StartServer()
    {
        return Net.StartServer(SettingsManager.ServerPort, SettingsManager.BroadcastPort);
    }

    public static NetDataWriter GetWriter(PackageStreamWriter writer, out int size)
    {
        var data = writer.Data();
        var tmp = new NetDataWriter();
        tmp.PutBytesWithLength(data);
        size = data.Length;
        return tmp;
    }

    public static PackageStreamReader GetReader(NetDataReader reader)
    {
        return new PackageStreamReader(reader.GetBytesWithLength());
    }

    /// <summary>
    /// 写入服务器信息
    /// </summary>
    /// <param name="w"></param>
    public static void WriteServerInfo(NetDataWriter w)
    {
        var subsystemGameInfo = GameManager.Project!.FindSubsystem<SubsystemGameInfo>(true)!;
        var subsystemTimeOfDay = GameManager.Project!.FindSubsystem<SubsystemTimeOfDay>(true)!;

        w.Put(VersionsManager.ProtocolVersion);
        w.Put((ushort)Net.ClientCount);
        w.Put(subsystemGameInfo.WorldSettings.MaxOnlinePlayerCount);
        w.Put((byte)subsystemGameInfo.WorldSettings.GameMode);
        w.Put(!string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.Password));

        w.Put(subsystemGameInfo.WorldSettings.IsNeedCommunityLogin);
        w.Put(subsystemTimeOfDay.CalculateTimeOfDay());
    }

    public static void ReadServerInfo(NetPlayScreen.Connect c, NetDataReader r, Stopwatch s, IPEndPoint ip,
        bool isLocal = false)
    {
        c.Version = r.GetString();
        c.PlayerCount = r.GetUShort();
        c.MaxCount = r.GetUShort();
        c.GameMode = (GameMode)r.GetByte();
        c.HasPassword = r.GetBool();
        c.IsNeedLoginCommunity = r.GetBool();
        c.TimeOfDay = r.GetFloat();
        c.State = NetPlayScreen.ConnectState.Available;
        c.UsedTime = s.ElapsedMilliseconds;
        if (!isLocal)
        {
            return;
        }

        c.Name = "[本地] " + c.Name;
        c.IP = ip.Address + ":" + ip.Port;
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
        if (Uri.TryCreate("http://" + ip, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort ? SettingsManager.ServerPort : uri.Port;
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
        project.SendToClientMode = true;
        var rootNode = new ValuesDictionary();
        var projData = project.Save();
        projData.EntityDataList.EntitiesData.Clear();
        projData.Save(rootNode);
        var data = rootNode.ToMessagePack();
        project.SendToClientMode = false;
        return data;
    }

    public static byte[] Compress(Stream stream)
    {
        stream.Position = 0L;
        var data = new byte[stream.Length];
        stream.ReadExactly(data, 0, (int)stream.Length);
        return Compress(data);
    }

    private static byte[] Compress(byte[] data)
    {
        using var outStream = new MemoryStream();
        using (var zipStream = new DeflateStream(outStream, CompressionMode.Compress))
        {
            zipStream.Write(data, 0, data.Length);
        }

        var streamArray = outStream.ToArray();

        return streamArray;
    }

    public static byte[] Decompress(byte[] inputBytes)
    {
        var outStream = new MemoryStream();
        using (var inputStream = new MemoryStream(inputBytes))
        {
            using (var zipStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            {
                zipStream.CopyTo(outStream);
                zipStream.Close();
            }
        }

        return outStream.ToArray();
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
    /// 获取时间戳，单位0.1ms
    /// </summary>
    /// <returns></returns>
    public static long GetMicroSeconds()
    {
        return DateTime.Now.Ticks / 10000;
    }
}
