namespace Game.Protocol.Messaging;

public enum GameMessageKind : byte
{
    Chat = 0,

    System = 1,

    Command = 2,

    Sign = 3
}
