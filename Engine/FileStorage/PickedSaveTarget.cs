namespace Engine.FileStorage;

public sealed record PickedSaveTarget(
    string Name,
    Func<CancellationToken, Task<Stream>> OpenWriteAsync);
