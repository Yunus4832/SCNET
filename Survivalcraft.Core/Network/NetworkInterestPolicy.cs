namespace Game.Network;

/// <summary>在本次世界生命周期中生效的不可变关注范围设置，范围限制由 NetworkTerrainPolicy 应用。</summary>
public sealed record NetworkInterestPolicy(int MaxClientVisibilityRange);
