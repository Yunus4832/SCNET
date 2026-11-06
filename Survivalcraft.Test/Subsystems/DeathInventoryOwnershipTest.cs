using System.Reflection;

using EntitySystem.Core;

using Game.Components;
using Game.Creatures;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class DeathInventoryOwnershipTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, 1)]
    [InlineData(RuntimeSide.Client, 0)]
    public void DeathUpdatesInvokeCustomInventoryDropsOnlyOnAuthority(RuntimeSide side, int expectedDrops)
    {
        // Exercise simulation and replica bookkeeping without constructing presentation resources.

        using var project = new RuntimeTestProject(side);
        var body = new ComponentBody();
        var sounds = new ComponentCreatureSounds();
        var creature = new ComponentCreature { ComponentBody = body, ComponentCreatureSounds = sounds };
        var health = new ComponentHealth
        {
            Health = 0, Air = 1, AirCapacity = 100,
            BreathingMode = BreathingMode.Water, FallResilience = 100, CorpseDuration = 100
        };
        project.InitializeComponent(body);
        project.InitializeComponent(sounds);
        project.InitializeComponent(creature);
        project.InitializeComponent(health);
        body.Initialize(health.Entity, health.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(health.Entity)!).Add(body);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var inventory = new DropProbe();
        inventory.Initialize(health.Entity, health.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(health.Entity)!).Add(inventory);
        Set(health, "_componentCreature", creature);
        Set(health, "_componentOnFire", new ComponentOnFire());
        Set(health, "_subsystemTime", new SubsystemTime());
        Set(health, "_subsystemGameInfo", new SubsystemGameInfo { TotalElapsedGameTime = 10 });
        Set(health, "_lastHealth", 1f);

        health.Update(0);
        health.Update(0);

        Assert.Equal(expectedDrops, inventory.Drops);
        Assert.Equal(side == RuntimeSide.Authority ? 10d : null, health.DeathTime);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private sealed class DropProbe : ComponentInventoryBase
    {
        public int Drops { get; private set; }

        public override void DropAllItems(Engine.Core.Mathematics.Vector3 position) => Drops++;
    }
}
