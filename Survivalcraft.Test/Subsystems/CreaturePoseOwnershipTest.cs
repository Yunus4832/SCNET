using System.Reflection;

using Engine.Core.Mathematics;

using Game.Components;
using Game.Creatures;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class CreaturePoseOwnershipTest
{
    [Theory]
    [InlineData(CreaturePoseKind.Human)]
    [InlineData(CreaturePoseKind.FourLegged)]
    [InlineData(CreaturePoseKind.FlightlessBird)]
    [InlineData(CreaturePoseKind.Fish)]
    public void AuthorityAttackTimingWorksWithoutAnyRenderingModel(CreaturePoseKind kind)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var pose = Create(project, kind, out _);
        var hits = 0;
        for (var i = 0; i < 10; i++)
        {
            pose.AttackOrder = true;
            pose.Update(.1f);
            if (pose.IsAttackHitMoment) { hits++; }
        }

        Assert.Equal(2, hits);
        Assert.Equal(2, pose.AttackCounter);
    }

    [Fact]
    public void ReplicaCanAnimateButCannotChooseAttackOrProduceAuthorityHitMoments()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var pose = Create(project, CreaturePoseKind.FourLegged, out _);
        pose.AttackOrder = true;
        pose.Update(.1f);
        Assert.False(pose.LastAttackOrder);
        pose.ApplyActionState(true, false, .4f, 0, null, 0);
        pose.Update(.1f);
        Assert.True(pose.LastAttackOrder);
        Assert.False(pose.IsAttackHitMoment);
        Assert.True(pose.AttackPhase >= .5f);
    }

    [Theory]
    [InlineData(CreaturePoseKind.Generic, .95f)]
    [InlineData(CreaturePoseKind.Human, .875f)]
    [InlineData(CreaturePoseKind.Fish, 1f)]
    public void EyeGeometryUsesBodyProfileWithoutModelOrGraphicsResources(CreaturePoseKind kind, float height)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var pose = Create(project, kind, out var creature);
        var body = creature.ComponentBody;
        body.BoxSize = new Vector3(1, 2, 1);
        body.Position = new Vector3(4, 5, 6);
        var expected = body.Position + body.Matrix.Up * height * 2;
        if (kind != CreaturePoseKind.Human) { expected += body.Matrix.Forward * .45f; }

        Assert.Equal(expected, pose.EyePosition);
        body.Position += Vector3.UnitX;
        Assert.Equal(expected + Vector3.UnitX, pose.EyePosition);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, false)]
    [InlineData(RuntimeSide.Client, true)]
    public void ModelsOnlyScheduleOnPresentationWorlds(RuntimeSide side, bool expected)
    {
        using var project = new RuntimeTestProject(side);
        Assert.Equal(
            expected,
            SubsystemUpdate.ShouldScheduleUpdateable(new ComponentHumanModel(), WorldRuntimeContext.For(project))
        );
    }

    private static ComponentCreaturePose Create(
        RuntimeTestProject project,
        CreaturePoseKind kind,
        out ComponentCreature creature
    )
    {
        var pose = new ComponentCreaturePose();
        project.InitializeComponent(pose);
        var body = new ComponentBody();
        body.Initialize(pose.Entity, pose.ValuesDictionary);
        creature = new ComponentCreature
        {
            ComponentBody = body, ComponentHealth = new ComponentHealth { Health = 1 },
            ComponentLocomotion = new ComponentLocomotion { TurnSpeed = 2 }, ComponentCreaturePose = pose
        };
        Set(pose, "_creature", creature);
        Set(pose, "_time", new SubsystemTime());
        typeof(ComponentCreaturePose).GetProperty(nameof(ComponentCreaturePose.Kind))!.SetValue(pose, kind);
        return pose;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
