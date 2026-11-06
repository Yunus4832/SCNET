using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;
using Game.Modding;
using Game.Network;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldRuntimeAuthorityTest
{
    [Fact]
    public void ServerDiscoveryUsesTheRequestedAuthorityAndRejectsClientWorlds()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { MaxOnlinePlayerCount = 12 } };
        authority.AddSubsystem(info);
        authority.AddSubsystem(new SubsystemSeasons());
        authority.AddSubsystem(new SubsystemTimeOfDay { SubsystemGameInfo = info });
        var node = WorldNetworkBinding.For(authority).RequireNode();
        for (byte id = 1; id <= 2; id++)
        {
            node.AddClient(
                new Client(null, id, Guid.NewGuid(), Guid.Empty)
                {
                    PlayerAddress = new Game.Protocol.Identity.PlayerAddress(
                        Engine.Core.Security.IdentityAddress.Encode(new byte[32])
                    )
                }
            );
        }

        var previousClients = CommonLib.Net.Clients.Values.ToArray();
        try
        {
            ConnectionTestEnvironment.SetRoster(CommonLib.Net, []);
            var snapshot = ServerInfoPackageBuilder.Capture(authority, null, []);
            Assert.Equal(2, snapshot.ClientCount);
            Assert.Equal(12, snapshot.MaxPlayerCount);
            Assert.Throws<InvalidOperationException>(() => ServerInfoPackageBuilder.Capture(client, null, []));
        }
        finally
        {
            ConnectionTestEnvironment.SetRoster(CommonLib.Net, previousClients);
        }
    }

    [Fact]
    public void PersistentVoltageUsesItsOwnAuthorityEvenWhenTheApplicationRoleDisagrees()
    {
        var previous = CommonLib.Net.Role;
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var authoritativeElectricity = new SubsystemElectricity();
        var replicaElectricity = new SubsystemElectricity();
        authority.InitializeSubsystem(authoritativeElectricity);
        client.InitializeSubsystem(replicaElectricity);
        var point = new Point3(1, 2, 3);
        try
        {
            CommonLib.Net.Role = NodeRole.Client;
            authoritativeElectricity.WritePersistentVoltage(point, 0.5f);
            Assert.Equal(0.5f, authoritativeElectricity.ReadPersistentVoltage(point));
            CommonLib.Net.Role = NodeRole.Authority;
            replicaElectricity.WritePersistentVoltage(point, 0.75f);
            Assert.Null(replicaElectricity.ReadPersistentVoltage(point));
            Assert.Equal(0.5f, authoritativeElectricity.ReadPersistentVoltage(point));
        }
        finally
        {
            CommonLib.Net.Role = previous;
        }
    }

    [Fact]
    public void RecoveryAndInjuryHooksBelongToTheAuthorityRatherThanTheApplicationMode()
    {
        var previousRole = CommonLib.Net.Role;

        var calls = 0;
        using var mods = GameModRuntime.Start(
            [new ModDescriptor(new ModManifest("test.damage", "Damage", "1.0.0"), () => new DamageProbe(_ => calls++))]
        );
        using var authority = new RuntimeTestProject(RuntimeSide.Authority, bindRuleHooks: false);
        authority.BindRuleHooks(mods.Gameplay, mods.BlockBehaviors);
        using var client = new RuntimeTestProject(RuntimeSide.Client, bindRuleHooks: false);
        client.BindRuleHooks(mods.Gameplay, mods.BlockBehaviors);
        var authoritativeHealth = new ComponentHealth { Health = 0.5f };
        var replicaHealth = new ComponentHealth { Health = 0.5f };
        authority.InitializeComponent(authoritativeHealth);
        client.InitializeComponent(replicaHealth);
        try
        {
            CommonLib.Net.Role = NodeRole.Client;
            authoritativeHealth.Heal(0.2f);
            authoritativeHealth.Injure(0.1f, null, true, LocalizedText.Literal("probe"));
            Assert.Equal(0.7f, authoritativeHealth.Health);
            Assert.Equal(1, calls);

            CommonLib.Net.Role = NodeRole.Authority;
            replicaHealth.Heal(0.2f);
            replicaHealth.Injure(0.1f, null, true, LocalizedText.Literal("probe"));
            Assert.Equal(0.5f, replicaHealth.Health);
            Assert.Equal(1, calls);
        }
        finally
        {
            CommonLib.Net.Role = previousRole;
        }
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, false, false, false, 1, 0)]
    [InlineData(RuntimeSide.Authority, true, true, false, 1, 0)]
    [InlineData(RuntimeSide.Authority, true, false, false, 0, 0)]
    [InlineData(RuntimeSide.Authority, true, true, true, 1, 0)]
    [InlineData(RuntimeSide.Authority, true, false, true, 0, 0)]
    [InlineData(RuntimeSide.Authority, false, false, true, 1, 0)]
    [InlineData(RuntimeSide.Client, false, false, false, 0, 0)]
    [InlineData(RuntimeSide.Client, true, true, false, 0, 1)]
    [InlineData(RuntimeSide.Client, true, false, false, 0, 0)]
    [InlineData(RuntimeSide.Client, true, true, true, 0, 1)]
    [InlineData(RuntimeSide.Client, true, false, true, 0, 0)]
    [InlineData(RuntimeSide.Client, false, false, true, 0, 0)]
    public void EnvironmentalDamageIsAppliedOrReportedByItsSimulationOwner(
        RuntimeSide side,
        bool playerControlled,
        bool local,
        bool mounted,
        int expectedApplications,
        int expectedRequests
    )
    {
        var calls = 0;
        using var mods = GameModRuntime.Start(
            [new ModDescriptor(new ModManifest("test.damage", "Damage", "1.0.0"), () => new DamageProbe(_ => calls++))]
        );
        using var project = new RuntimeTestProject(side, bindRuleHooks: false);
        project.BindRuleHooks(mods.Gameplay, mods.BlockBehaviors);
        var node = WorldNetworkBinding.For(project).RequireNode();
        node.Server = new Client(Guid.Empty);
        var health = new ComponentHealth { Health = 0.5f };
        project.InitializeComponent(health);
        health.Entity.EntityId = 11;
        ComponentCharacter? controller = null;
        if (playerControlled)
        {
            var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, local);
            controller = new ComponentCharacter { CharacterData = data };
        }

        var body = new ComponentBody();
        if (mounted)
        {
            _ = new ComponentBody { ParentBody = body, Character = controller };
        }
        else
        {
            body.Character = controller;
        }

        var creature = new ComponentCreature { ComponentBody = body };
        typeof(ComponentHealth).GetField("_componentCreature", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(health, creature);

        {
            health.ApplyEnvironmentalDamage(
                0.1f,
                null,
                LocalizedText.Literal("probe"),
                ComponentHealthPackage.RequestInjureType.Fall
            );

            Assert.Equal(expectedApplications, calls);
            Assert.Equal(expectedRequests, node.PendingPackageCount);
        }
    }

    private sealed class DamageProbe(Action<CreatureInjuringContext> observe) : IMod
    {
        public void Configure(IModContext context)
        {
            context.Gameplay.OnCreatureInjuring(injury =>
                {
                    observe(injury);
                    injury.Cancel = true;
                }
            );
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}
