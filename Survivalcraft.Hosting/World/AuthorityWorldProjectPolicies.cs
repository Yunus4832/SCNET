using Game.Celebrations;
using Game.Spawning;

namespace Game.World;

/// <summary>在加载任何权威端子系统之前绑定本世界实际使用的宿主策略。</summary>
public sealed record AuthorityWorldProjectPolicies(
    CreatureSpawnPolicy Spawn,
    NetworkInterestPolicy Interest,
    NewYearCelebrationPolicy Celebration,
    Func<bool> ShouldCollectAfterRosterChange
);
