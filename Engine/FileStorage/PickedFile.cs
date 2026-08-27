namespace Engine.FileStorage;

public sealed record PickedFile(
    string Name,
    string? ContentType,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);
