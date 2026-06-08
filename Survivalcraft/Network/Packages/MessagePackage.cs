using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class MessagePackage : IPackage
{
    public enum MessageMode : byte
    {
        BaseMessage, // 普通消息
        LargeMessage // 大字消息，也就是开局显示的那几条消息
    }

    public float Delay;

    public float Duration;

    public string LargeText = string.Empty;

    public string Message = string.Empty;

    public MessageMode PackageMessageMode;

    public byte MessageType;

    public string PlayerName = string.Empty;

    public string SmallText = string.Empty;

    public readonly List<byte> ToClients = [];

    public byte ID => (byte)PackageType.Message;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public MessagePackage()
    {
    }

    public MessagePackage(string playerName, string message, byte type, List<byte> toClients)
    {
        PackageMessageMode = MessageMode.BaseMessage;
        Message = message;
        PlayerName = playerName;
        MessageType = type;
        ToClients.AddRange(toClients);
    }

    // 发送大字消息，可以做服务器公告用
    public MessagePackage(string largeText, string smallText, float duration, float delay)
    {
        PackageMessageMode = MessageMode.LargeMessage;
        LargeText = largeText;
        SmallText = smallText;
        Duration = duration;
        Delay = delay;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageMessageMode);
        switch (PackageMessageMode)
        {
            case MessageMode.BaseMessage:
                writer.Write(MessageType);
                writer.Write((byte)ToClients.Count);
                for (byte i = 0; i < ToClients.Count; i++)
                {
                    writer.Write(ToClients[i]);
                }

                writer.Write(Message);
                if (string.IsNullOrEmpty(PlayerName))
                {
                    writer.Write(false); // 这里不要布尔判断，而是直接写入string.Empty可能会好一点
                }
                else
                {
                    writer.Write(true);
                    writer.Write(PlayerName);
                }

                break;
            case MessageMode.LargeMessage:
                writer.Write(LargeText);
                writer.Write(SmallText);
                writer.Write(Duration);
                writer.Write(Delay);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        PackageMessageMode = reader.ReadEnum<MessageMode>();
        switch (PackageMessageMode)
        {
            case MessageMode.BaseMessage:
                MessageType = reader.ReadByte();
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    ToClients.Add(reader.ReadByte());
                }

                Message = reader.ReadString();
                if (reader.ReadBoolean())
                {
                    PlayerName = reader.ReadString();
                }

                break;
            case MessageMode.LargeMessage:
                LargeText = reader.ReadString();
                SmallText = reader.ReadString();
                Duration = reader.ReadSingle();
                Delay = reader.ReadSingle();
                break;
        }
    }


}
