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

    private float _delay;

    private float _duration;

    private string _largeText = string.Empty;

    private string _message = string.Empty;

    private MessageMode _messageMode;

    private byte _messageType;

    private string _playerName = string.Empty;

    private string _smallText = string.Empty;

    private readonly List<byte> _toClients = [];

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
        _messageMode = MessageMode.BaseMessage;
        _message = message;
        _playerName = playerName;
        _messageType = type;
        _toClients.AddRange(toClients);
    }

    // 发送大字消息，可以做服务器公告用
    public MessagePackage(string largeText, string smallText, float duration, float delay)
    {
        _messageMode = MessageMode.LargeMessage;
        _largeText = largeText;
        _smallText = smallText;
        _duration = duration;
        _delay = delay;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_messageMode);
        switch (_messageMode)
        {
            case MessageMode.BaseMessage:
                writer.Write(_messageType);
                writer.Write((byte)_toClients.Count);
                for (byte i = 0; i < _toClients.Count; i++)
                {
                    writer.Write(_toClients[i]);
                }

                writer.Write(_message);
                if (string.IsNullOrEmpty(_playerName))
                {
                    writer.Write(false); // 这里不要布尔判断，而是直接写入string.Empty可能会好一点
                }
                else
                {
                    writer.Write(true);
                    writer.Write(_playerName);
                }

                break;
            case MessageMode.LargeMessage:
                writer.Write(_largeText);
                writer.Write(_smallText);
                writer.Write(_duration);
                writer.Write(_delay);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _messageMode = reader.ReadEnum<MessageMode>();
        switch (_messageMode)
        {
            case MessageMode.BaseMessage:
                _messageType = reader.ReadByte();
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    _toClients.Add(reader.ReadByte());
                }

                _message = reader.ReadString();
                if (reader.ReadBoolean())
                {
                    _playerName = reader.ReadString();
                }

                break;
            case MessageMode.LargeMessage:
                _largeText = reader.ReadString();
                _smallText = reader.ReadString();
                _duration = reader.ReadSingle();
                _delay = reader.ReadSingle();
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (_messageMode)
        {
            case MessageMode.BaseMessage:
                var gameWidgets = project.FindSubsystem<SubsystemGameWidgets>(true)!;
                const bool external = false;
                gameWidgets.AddNetMessage(_message, _playerName, _messageType, _toClients, external);
                if (!isServer || From == null)
                {
                    break;
                }

                _playerName = From.PlayerData.Name;
                var flag = project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList
                    .Contains(From.GUID.ToString());
                if (!flag)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case MessageMode.LargeMessage:
                if (isServer)
                {
                    break;
                }

                foreach (var player in project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData)
                {
                    player.ComponentPlayer?.ComponentGui?.DisplayLargeMessage(
                        _largeText,
                        _smallText,
                        _duration,
                        _delay
                    );
                }

                break;
        }
    }
}
