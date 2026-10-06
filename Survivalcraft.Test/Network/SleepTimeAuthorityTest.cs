using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Components;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SleepTimeAuthorityTest
{
    [Theory]
    [InlineData(WorkType.Client, false, false)]
    [InlineData(WorkType.Client, true, false)]
    [InlineData(WorkType.Server, false, true)]
    [InlineData(WorkType.Server, true, false)]
    [InlineData(WorkType.Local, false, true)]
    [InlineData(WorkType.Local, true, false)]
    public void ClientNeverAcceleratesSleepFromItsPartialPlayerSet(WorkType mode, bool includeAwakePlayer,
        bool accelerated)
    {
        var previous = CommonLib.WorkType;
        CommonLib.WorkType = mode;
        try
        {
            var players = new SubsystemPlayers();
            var list = (List<ComponentPlayer>)typeof(SubsystemPlayers)
                .GetField("_componentPlayers", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(players)!;
            var sleeper = new ComponentSleep();
            typeof(ComponentSleep).GetField("_sleepFactor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(sleeper, 1f);
            list.Add(new ComponentPlayer
            {
                ComponentHealth = new ComponentHealth { Health = 1f },
                ComponentSleep = sleeper,
                ComponentGui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui))
            });
            if (includeAwakePlayer)
            {
                list.Add(new ComponentPlayer
                {
                    ComponentHealth = new ComponentHealth { Health = 1f },
                    ComponentSleep = new ComponentSleep(),
                    ComponentGui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui))
                });
            }

            var updates = new SubsystemUpdate { UpdatesPerFrame = 20 };
            var time = new SubsystemTime { FixedTimeStep = 0.05f };
            typeof(SubsystemTime).GetField("_subsystemPlayers", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(time, players);
            typeof(SubsystemTime).GetField("_subsystemUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(time, updates);

            time.NextFrame();

            Assert.Equal(accelerated ? 20 : 1, updates.UpdatesPerFrame);
            Assert.Equal(accelerated ? 0.05f : (float?)null, time.FixedTimeStep);
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
    }
}
