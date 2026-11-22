namespace Game.Identity;

/// <summary>实例私有数据；界面和诊断应使用公开身份摘要。</summary>
public sealed record LocalIdentitySettings
{
    public IReadOnlyList<StoredLocalIdentity> Identities { get; init; } = [];

    public string? SelectedPlayerAddress { get; init; }

    public string? SelectedServerAddress { get; init; }
}
