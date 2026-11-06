using System.Reflection;

using Game.Components;
using Game.Network;
using Game.Subsystems;
using Game.World;
using Game.World.Timing;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SleepTimeAuthorityTest
{
    [Theory]
    [InlineData(RuntimeSide.Client, NodeRole.Client, false, false)]
    [InlineData(RuntimeSide.Client, NodeRole.Client, true, false)]
    [InlineData(RuntimeSide.Authority, NodeRole.Authority, false, true)]
    [InlineData(RuntimeSide.Authority, NodeRole.Authority, true, false)]
    [InlineData(RuntimeSide.Client, NodeRole.Authority, false, false)]
    [InlineData(RuntimeSide.Authority, NodeRole.Client, false, true)]
    public void ClientNeverAcceleratesSleepFromItsPartialPlayerSet(
        RuntimeSide side,
        NodeRole mode,
        bool includeAwakePlayer,
        bool accelerated
    )
    {
        var previous = CommonLib.Net.Role;
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
                    ComponentSleep = sleeper
                }
            );
            if (includeAwakePlayer)
            {
                list.Add(
                    new ComponentCharacter
                    {
                        ComponentHealth = new ComponentHealth { Health = 1f },
                        ComponentSleep = new ComponentSleep()
                    }
                );
            }

            var updates = new SubsystemUpdate { UpdatesPerFrame = 20 };
            var time = new SubsystemTime { FixedTimeStep = 0.05f };
            using var project = new RuntimeTestProject(side);
            project.InitializeSubsystem(time);
            updates.UpdatesPerFrame = time.NextFrame(0f, new CharacterSleepPopulationSource(players).ReadPopulation);

            Assert.Equal(accelerated ? 20 : 1, updates.UpdatesPerFrame);
            Assert.Equal(accelerated ? 0.05f : null, time.FixedTimeStep);
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }
}
