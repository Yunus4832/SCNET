namespace Game.Modding.Blocks;

/// <summary>宿主提供的资源注册项，在 Core 目录初始化之前完成实例化。</summary>
public sealed record BlockDataRegistration(Func<string> Read);
