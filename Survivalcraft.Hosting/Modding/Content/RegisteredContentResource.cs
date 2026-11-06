namespace Game.Modding.Content;

/// <summary>One frozen resource registration consumed by actual CPU or client resource owners.</summary>
public sealed record RegisteredContentResource(
    ResourceId Id,
    string RelativePath,
    string ContentPath,
    Func<Stream> OpenRead
);
