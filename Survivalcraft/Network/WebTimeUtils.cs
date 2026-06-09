using System.Net;
using System.Net.Sockets;

namespace Game.Network;

/// <summary>
/// 网络时间工具类
/// </summary>
internal class WebTimeUtils
{
    /// <summary>
    /// ntp 服务器域名
    /// </summary>
    private const string _ntpServer = "ntp.aliyun.com";

    private const byte _serverReplyTime = 40;

    /// <summary>
    /// 获取网络时间
    /// </summary>
    public static DateTime GetWebTime()
    {
        try
        {
            var ntpData = new byte[48];
            // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)
            ntpData[0] = 0x1B;
            var addresses = Dns.GetHostEntry(_ntpServer).AddressList;
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(ipEndPoint);
            socket.ReceiveTimeout = 10;
            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();
            ulong intPart = BitConverter.ToUInt32(ntpData, _serverReplyTime);
            ulong fracPart = BitConverter.ToUInt32(ntpData, _serverReplyTime + 4);
            intPart = SwapEndian(intPart);
            fracPart = SwapEndian(fracPart);
            var milliseconds = intPart * 1000 + fracPart * 1000 / 0x100000000UL;
            var webTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
            return webTime.ToLocalTime();
        }
        catch
        {
            DialogsManager.Confirm("网络检查不通过,请确保网络通畅再打开游戏!", _ => { Window.Close(); });
            return new DateTime(0);
        }
    }

    /// <summary>
    /// 小端存储与大端存储的转换
    /// </summary>
    private static uint SwapEndian(ulong x)
    {
        return (uint)(((x & 0x000000ff) << 24) +
                      ((x & 0x0000ff00) << 8) +
                      ((x & 0x00ff0000) >> 8) +
                      ((x & 0xff000000) >> 24));
    }
}
