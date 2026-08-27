namespace Game.Content;

internal sealed class ContentServerResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int Code { get; init; }

    public T? Data { get; init; }
}
