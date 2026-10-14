namespace Game.Commands;

[Flags]
public enum CommandResultPresentation : byte
{
    Silent = 0,
    History = 1,
    Overlay = 2,
    Toast = 4,
    Default = History | Overlay
}
