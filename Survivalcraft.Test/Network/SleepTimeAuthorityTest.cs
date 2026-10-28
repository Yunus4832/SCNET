using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Components;
using Game.Managers;
using Game.Network;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SleepTimeAuthorityTest
{
    [Theory]
    [InlineData(WorldRole.RemoteClient, false, false)]
    [InlineData(WorldRole.RemoteClient, true, false)]
    [InlineData(WorldRole.Server, false, true)]
    [InlineData(WorldRole.Server, true, false)]
    [InlineData(WorldRole.SinglePlayer, false, true)]
    [InlineData(WorldRole.SinglePlayer, true, false)]
    public void ClientNeverAcceleratesSleepFromItsPartialPlayerSet(
        WorldRole mode,
        bool includeAwakePlayer,
        bool accelerated
    )
    {
        var previous = GameManager.Role;
        CommonLib.Net.Role = mode;
        try
        {
            var players = new SubsystemCharacters();
            var list = (List<ComponentCharacter>)typeof(SubsystemCharacters)
                .GetField("_componentCharacters", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(players)!;
            var sleeper = new ComponentSleep();
            typeof(ComponentSleep).GetField("_sleepFactor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(sleeper, 1f);
            list.Add(
                new ComponentCharacter
                {
                    ComponentHealth = new ComponentHealth { Health = 1f },
                    ComponentSleep = sleeper,
                    ComponentGui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui))
                }
            );
            if (includeAwakePlayer)
            {
                list.Add(
                    new ComponentCharacter
                    {
                        ComponentHealth = new ComponentHealth { Health = 1f },
                        ComponentSleep = new ComponentSleep(),
                        ComponentGui = (ComponentGui)RuntimeHelpers.GetUninitializedObject(typeof(ComponentGui))
                    }
                );
            }

            var updates = new SubsystemUpdate { UpdatesPerFrame = 20 };
            var time = new SubsystemTime { FixedTimeStep = 0.05f };
            typeof(SubsystemTime).GetField("_subsystemCharacters", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(time, players);
            typeof(SubsystemTime).GetField("_subsystemUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(time, updates);

            time.NextFrame();

            Assert.Equal(accelerated ? 20 : 1, updates.UpdatesPerFrame);
            Assert.Equal(accelerated ? 0.05f : null, time.FixedTimeStep);
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }
}
