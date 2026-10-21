namespace Game.Protocol.Serialization;

public enum CompressionPolicy : byte
{
    None = 0,

    Adaptive = 1,

    Always = 2
}
