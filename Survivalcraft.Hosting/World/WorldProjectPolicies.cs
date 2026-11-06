using Game.Celebrations;
using Game.Configuration;
using Game.Spawning;

namespace Game.World;

public static class WorldProjectPolicies
{
    public static (CreatureSpawnPolicy Spawn, NetworkInterestPolicy Interest) Capture(HostPreferences settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return (new CreatureSpawnPolicy(
            settings.CreatureTotalLimit,
            settings.CreatureAreaLimit,
            settings.CreatureMaxPlayerAreaLimit,
            settings.CreatureMaxPointLimit,
            settings.CreatureAreaRadius,
            settings.CreatureTotalLimitConstant,
            settings.CreatureAreaLimitConstant,
            settings.CreatureAreaRadiusConstant,
            settings.CreatureSpawnIntervalTime,
            settings.CreatureConstantSpawnIntervalTime
        ), new NetworkInterestPolicy(settings.MaxClientVisibilityRange));
    }

    public static AuthorityWorldProjectPolicies CaptureAuthority(HostPreferences settings)
    {
        var policies = Capture(settings);
        return new AuthorityWorldProjectPolicies(
            policies.Spawn,
            policies.Interest,
            new NewYearCelebrationPolicy(
                TimeProvider.System,
                () => settings.NewYearCelebrationLastYear,
                year => settings.NewYearCelebrationLastYear = year
            ),
            () => settings.AutoGarbageCollect
        );
    }
}
