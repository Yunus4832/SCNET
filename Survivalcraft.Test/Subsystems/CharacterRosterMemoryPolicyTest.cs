using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Core.Security;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class CharacterRosterMemoryPolicyTest
{
    [Theory]
    [InlineData(NodeRole.Client)]
    [InlineData(NodeRole.Authority)]
    public void RealRosterOperationsCollectAtCompletedMutationAndReadCurrentPolicy(NodeRole role)
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(role);
        var enabled = true;
        var observed = new List<int>();
        using var policy = new CharacterRosterMemoryPolicy(
            WorldRuntimeContext.For(project),
            project.Characters,
            () => enabled,
            () => observed.Add(project.Characters.Characters.Count)
        );
        var character = Fixtures.CharacterTestEnvironment.Create(
            project,
            new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        );
        character.Name = "MemoryPolicy";
        project.Characters.AddCharacterData(character);
        enabled = false;
        project.Characters.RemoveCharacterData(character);
        Assert.Equal([1], observed);
        enabled = true;
        project.Characters.AddCharacterData(character);
        project.Characters.RemoveCharacterData(character);
        Assert.Equal([1, 1, 0], observed);
        policy.Dispose();
        project.Characters.AddCharacterData(character);
        Assert.Equal([1, 1, 0], observed);
    }

    [Fact]
    public void WorldsHaveIndependentPoliciesAndClosedWorldDoesNotCollect()
    {
        CharacterSkinsManager.Initialize();
        using var first = new CharacterProject(NodeRole.Client);
        using var second = new CharacterProject(NodeRole.Client);
        var firstCount = 0;
        var secondCount = 0;
        var runtime = WorldRuntimeContext.For(first);
        runtime.OwnWorldResource(
            new CharacterRosterMemoryPolicy(runtime, first.Characters, () => true, () => firstCount++)
        );
        using var otherPolicy = new CharacterRosterMemoryPolicy(
            WorldRuntimeContext.For(second),
            second.Characters,
            () => true,
            () => secondCount++
        );
        var firstCharacter = new CharacterData(first) { Name = "First" };
        var secondCharacter = new CharacterData(second) { Name = "Second" };
        first.Characters.AddCharacterData(firstCharacter);
        second.Characters.AddCharacterData(secondCharacter);
        Assert.Equal(1, firstCount);
        Assert.Equal(1, secondCount);
        runtime.Close();
        first.Characters.RemoveCharacterData(firstCharacter);
        second.Characters.RemoveCharacterData(secondCharacter);
        Assert.Equal(1, firstCount);
        Assert.Equal(2, secondCount);
    }

    [Fact]
    public void PolicyBoundBeforeLoadObservesInitialCharacters()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(NodeRole.Client);
        var collected = 0;
        using var policy = new CharacterRosterMemoryPolicy(
            WorldRuntimeContext.For(project),
            project.Characters,
            () => true,
            () => collected++
        );
        using var character = Fixtures.CharacterTestEnvironment.Create(
            project,
            new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        );
        character.Name = "Loaded";
        var saved = new ValuesDictionary();
        character.Save(saved);
        project.Characters.Load(
            new ValuesDictionary
            {
                { "GlobalSpawnPosition", Vector3.Zero },
                { "Characters", new ValuesDictionary { { "0", saved } } }
            }
        );
        Assert.Single(project.Characters.Characters);
        Assert.Equal(1, collected);
    }

    private sealed class CharacterProject : Project, IDisposable
    {
        public SubsystemPlayers Players { get; } = new();

        public SubsystemCharacters Characters { get; } = new();

        public new void Dispose()
        {
            // Dependencies are lookup-only fixtures; no terrain thread or renderer was initialized.
            Characters.Dispose();
            subsystems.Clear();
            base.Dispose();
        }

        public CharacterProject(NodeRole role = NodeRole.Authority)
        {
            new NetNode(NetNodeOptions.Default, false, Game.Network.Handling.PackageHandlerCatalog.Empty, role).Runtime
                .BindProject(this);
            Fixtures.RuleHookTestEnvironment.BindEmpty(this);
            var updater = (TerrainUpdater)RuntimeHelpers.GetUninitializedObject(typeof(TerrainUpdater));
            SetField(updater, "_pendingLocations", new Dictionary<int, TerrainUpdateLocation?>());
            var parametersField = typeof(TerrainUpdater).GetField(
                "_updateParameters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            var parameters = Activator.CreateInstance(parametersField.FieldType)!;
            parametersField.FieldType.GetField("Locations")!
                .SetValue(parameters, new Dictionary<int, TerrainUpdateLocation>());
            parametersField.SetValue(updater, parameters);
            var terrain = new SubsystemTerrain { TerrainUpdater = updater };
            subsystems.AddRange(
                [
                    Players, Characters, terrain, new SubsystemTime(), new SubsystemMessages(),
                    new SubsystemGameInfo(), new SubsystemSky()
                ]
            );
            foreach (var subsystem in subsystems) { WorldSubsystemTestSupport.Attach(this, subsystem); }

            SetField(Characters, "_subsystemTerrain", terrain);
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
