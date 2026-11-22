namespace Game.Modding.Content;

/// <summary>供 CPU 侧或客户端资源管理者使用的已冻结注册项。</summary>
public sealed record RegisteredContentResource(
    ResourceId Id,
    string RelativePath,
    string ContentPath,
    Func<Stream> OpenRead
);
