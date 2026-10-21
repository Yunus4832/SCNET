namespace Game.Protocol.Messaging;

public enum MessageTextStyle : byte
{
    Normal = 0,

    Sender = 1,

    System = 2,

    Team = 3,

    Success = 4,

    Error = 5,

    Warning = 6,

    Accent = 7
}
