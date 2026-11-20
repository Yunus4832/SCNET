using System.Reflection;

using EntitySystem.Core;

using Game.Blocks;
using Game.Components;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class CreatureFirePresentationTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityAdvancesAndExtinguishesFireWithoutPresentationResources(bool immersed)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var fire = new ComponentOnFire();
        var body = new ComponentBody();
        project.InitializeComponent(fire);
        body.Initialize(fire.Entity, fire.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fire.Entity)!).Add(body);
        fire.Entity.IsAddedToProject = true;
        typeof(ComponentOnFire).GetProperty("ComponentBody", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, body);
        typeof(ComponentOnFire).GetField("_subsystemTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, new SubsystemTime());
        typeof(ComponentOnFire).GetField("_nextCheckTime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, double.MaxValue);
        if (immersed)
        {
            body.ImmersionFactor = 1f;
            body.ImmersionFluidBlock = new WaterBlock();
        }

        fire.SetOnFire(null, 2f);
        fire.Update(1f);
        Assert.Equal(!immersed, fire.IsOnFire);
        fire.Update(1f);

        Assert.False(fire.IsOnFire);
    }

    [Fact]
    public void ClientWaitsForAuthorityExtinguishInsteadOfUsingItsOwnWaterCollision()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var fire = new ComponentOnFire();
        project.InitializeComponent(fire);
        fire.Entity.IsAddedToProject = true;
        typeof(ComponentOnFire).GetProperty("ComponentBody", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, new ComponentBody { ImmersionFactor = 1f, ImmersionFluidBlock = new WaterBlock() });
        fire.ApplyFireState(null, 4f);
        fire.Extinguish();
        fire.Update(1f);
        Assert.True(fire.IsOnFire);
        Assert.Equal(3f, fire.FireDuration);
        fire.ApplyFireState(null, 0f);
        Assert.False(fire.IsOnFire);
    }

    [Fact]
    public void AuthoritySnapshotIncludesRemainingDurationAndExplicitExtinguish()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var fire = new ComponentOnFire();
        project.InitializeComponent(fire);
        var body = new ComponentBody();
        body.Initialize(fire.Entity, fire.ValuesDictionary);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fire.Entity)!).Add(body);
        fire.SetOnFire(null, 5f);
        fire.SetOnFire(null, 2f);
        Assert.Equal(5f, ((EntityFirePackage)EntityStatePackageBuilder.Capture(fire)).Duration);
        fire.Extinguish();
        Assert.Equal(0f, ((EntityFirePackage)EntityStatePackageBuilder.Capture(fire)).Duration);
    }
}
