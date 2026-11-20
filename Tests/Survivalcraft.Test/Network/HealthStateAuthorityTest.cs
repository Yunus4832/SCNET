using System.Reflection;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class HealthStateAuthorityTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, 5f)]
    [InlineData(RuntimeSide.Client, 99f)]
    public void FluStateIsAppliedOnlyOnClient(RuntimeSide side, float expected)
    {
        using var project = new RuntimeTestProject(side);
        var flu = new ComponentFlu { FluDuration = 5f };
        Register(project, flu);

        new ComponentFluPackageHandler().Handle(
            new ComponentFluPackage { EntityId = 42, FluDuration = 99f },
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );

        Assert.Equal(expected, flu.FluDuration);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, 5f)]
    [InlineData(RuntimeSide.Client, 0f)]
    public void SicknessStateIsAppliedOnlyOnClient(RuntimeSide side, float expected)
    {
        using var project = new RuntimeTestProject(side);
        var sickness = new ComponentSickness { SicknessDuration = 5f };
        Register(project, sickness);

        new ComponentSicknessPackageHandler().Handle(
            new ComponentSicknessPackage { EntityId = 42, SicknessDuration = 0f },
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );

        Assert.Equal(expected, sickness.SicknessDuration);
    }

    [Fact]
    public void AuthorityRejectsStaminaWithoutAnOwnedPlayerSession()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var vitals = new ComponentVitalStats { Stamina = .5f };
        Register(project, vitals);

        new ComponentVitalStatPackageHandler().Handle(
            new VitalStatePackage { EntityId = 42, Stamina = 1f },
            new PackageReceiveContext(null, WorldRuntimeContext.For(project), null)
        );

        Assert.Equal(.5f, vitals.Stamina);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityRejectsClientStaminaEvenForTheOwningSession(bool ownsCharacter)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var world = WorldRuntimeContext.For(project);
        var characters = new SubsystemCharacters();
        project.AddSubsystem(characters);
        var vitals = new ComponentVitalStats { Stamina = .5f };
        Register(project, vitals);
        var component = new ComponentCharacter();
        project.InitializeComponent(component);
        component.Entity.EntityId = 42;
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, project);
        player.CharacterId = Guid.NewGuid();
        player.ComponentCharacter = component;
        ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                "_characters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(characters)!).Add(player);
        var owner = new Client(null, 1, Guid.NewGuid(), player.CharacterId);
        WorldNetworkBinding.For(world).RequireNode().AddClient(owner);
        var other = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid());

        new ComponentVitalStatPackageHandler().Handle(
            new VitalStatePackage { EntityId = 42, Stamina = 1f },
            new PackageReceiveContext(null, world, ownsCharacter ? owner : other)
        );

        Assert.Equal(.5f, vitals.Stamina);
    }

    private static void Register(RuntimeTestProject project, Component component)
    {
        project.InitializeComponent(component);
        component.Entity.EntityId = 42;
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(component.Entity)!).Add(component);
        ((Dictionary<Entity, bool>)typeof(Project).GetField(
                "entityDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(project)!).Add(component.Entity, true);
    }
}
