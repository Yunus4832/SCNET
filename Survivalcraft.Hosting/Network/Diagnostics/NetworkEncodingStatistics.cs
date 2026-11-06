namespace Game.Network.Diagnostics;

public readonly record struct NetworkEncodingStatistics(
    long Batches,
    long Packages,
    long RawBytes,
    long EncodedBytes,
    double SerializationMilliseconds,
    double FrameEncodingMilliseconds
);
