namespace Game.Content;

internal sealed class ContentServerResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int Code { get; init; }

    public T? Data { get; init; }
}

internal sealed class ContentServerPage<T>
{
    public List<T> Items { get; init; } = [];

    public int Total { get; init; }

    public int PageIndex { get; init; }

    public int PageSize { get; init; }
}
