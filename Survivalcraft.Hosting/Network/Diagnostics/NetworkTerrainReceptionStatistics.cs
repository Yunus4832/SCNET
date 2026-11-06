namespace Game.Network.Diagnostics;

public readonly record struct NetworkTerrainReceptionStatistics(
    int PendingTransferCount,
    long PendingTransferBytes,
    long FragmentsReceived,
    long SnapshotsDecoded,
    long FullChunkRequestsSent,
    long MissingFragmentRequestsSent
);
