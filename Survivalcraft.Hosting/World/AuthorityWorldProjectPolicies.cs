using Game.Celebrations;
using Game.Spawning;

namespace Game.World;

/// <summary>Actual per-world host policies, bound before any authority subsystem is loaded.</summary>
public sealed record AuthorityWorldProjectPolicies(
    CreatureSpawnPolicy Spawn,
    NetworkInterestPolicy Interest,
    NewYearCelebrationPolicy Celebration,
    Func<bool> ShouldCollectAfterRosterChange
);
