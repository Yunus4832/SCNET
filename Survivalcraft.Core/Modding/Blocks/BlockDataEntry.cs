namespace Game.Modding.Blocks;

/// <summary>已读取的规则和资源元数据文本，不保留宿主的读取回调。</summary>
public sealed record BlockDataEntry(ResourceId Id, string Data);
