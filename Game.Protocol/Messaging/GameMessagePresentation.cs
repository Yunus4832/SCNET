namespace Game.Protocol.Messaging;

[Flags]
public enum GameMessagePresentation : byte
{
    None = 0,
    History = 1,
    Overlay = 2,
    Toast = 4,
    Default = History | Overlay
}
