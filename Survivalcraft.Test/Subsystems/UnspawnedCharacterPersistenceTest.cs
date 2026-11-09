using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using Engine.Core.Security;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Managers;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;
using Game.Terrains;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class UnspawnedCharacterPersistenceTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectBeforeFirstSpawnPreservesCharacterAndResumesSpawn(bool reload)
    {
        var previousNodeRole = CommonLib.Net.Role;
        var previousClients = CommonLib.Net.Clients.Values.ToArray();
        var previousGarbageCollect = Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect;
        try
        {
            CommonLib.Net.Role = NodeRole.Authority;
            Fixtures.ConnectionTestEnvironment.SetRoster(CommonLib.Net, []);
            Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect = false;
            CharacterSkinsManager.Initialize();
            using var project = new CharacterProject();
            var owner = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
            var character = Fixtures.CharacterTestEnvironment.Create(project, owner);
            character.Name = "BeforeSpawn";
            character.CharacterSkinName = "$Male1";
            character.Level = 3.5f;
            var characterId = character.CharacterId;
            project.Characters.AddCharacterData(character);
            Assert.Equal(characterId, project.Players.Store.Find(owner)!.CharacterId);
            Assert.Null(character.ComponentCharacter);

            project.Characters.MakeCharacterOffline(characterId, false);
            Assert.Empty(project.Characters.Characters);
            Assert.False(project.Characters.CharacterList[characterId].IsOnline);
            Assert.False(project.Characters.IsCharacterNameAvailable("BEFORESPAWN"));
            Assert.Empty(project.Characters.GetOfflineInventoryValues());

            using var restarted = new CharacterProject();
            var target = project;
            if (reload)
            {
                var saved = new ValuesDictionary();
                project.Characters.Save(saved);
                var record = saved.GetValue<ValuesDictionary>("OfflineCharacters")
                    .GetValue<ValuesDictionary>(characterId.ToString("N"));
                Assert.False(record.ContainsKey("Entity"));
                var xml = new XElement("Characters");
                saved.Save(xml);
                var reloaded = new ValuesDictionary();
                reloaded.ApplyOverrides(XElement.Parse(xml.ToString()));
                restarted.Players.Store.Load(project.Players.Store.Save());
                restarted.Characters.Load(reloaded);
                target = restarted;
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.True(target.Characters.MakeCharacterOnline(characterId, out var restored, out var entity));
                Assert.NotNull(restored);
                Assert.Null(entity);
                Assert.Equal(characterId, restored.CharacterId);
                Assert.Equal(owner, restored.OwnerAddress);
                Assert.Equal("BeforeSpawn", restored.Name);
                Assert.Equal(3.5f, restored.Level);
                Assert.Equal(-1.0, restored.FirstSpawnTime);
                Assert.Equal("FirstUpdate", restored.CurrentState);
                Assert.Equal(characterId, target.Players.Store.Find(owner)!.CharacterId);
                Assert.Single(target.Characters.Characters);
                target.Characters.MakeCharacterOffline(characterId, false);
            }
        }
        finally
        {
            Game.Configuration.HostingConfiguration.Preferences.AutoGarbageCollect = previousGarbageCollect;
            Fixtures.ConnectionTestEnvironment.SetRoster(CommonLib.Net, previousClients);
            CommonLib.Net.Role = previousNodeRole;
        }
    }

    [Fact]
    public void ReplicaUpdatesCannotDriveSpawnOrRestartBusiness()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(NodeRole.Client);
        using var character = new CharacterData(project) { SpawnPosition = new(100, 70, 100) };
        character.Update();
        Assert.Equal("AwaitingEntity", character.CurrentState);
        Assert.Equal(new Engine.Core.Mathematics.Vector3(100, 70, 100), character.SpawnPosition);
        Assert.Throws<InvalidOperationException>(() => character.SpawnPlayer(
                character.SpawnPosition,
                CharacterData.SpawnMode.Respawn
            )
        );
        Assert.Throws<InvalidOperationException>(character.NetRestart);
    }

    [Fact]
    public void CharacterPersistenceDoesNotContainInputDeviceSelection()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject();
        using var character = Fixtures.CharacterTestEnvironment.Create(
            project,
            new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        );
        var values = new ValuesDictionary();
        character.Save(values);
        Assert.False(values.ContainsKey("InputDevice"));
    }

    [Fact]
    public void ExistingReplicaInstallsRepeatedAuthorityStateWithoutRunningSpawnBusiness()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(NodeRole.Client);
        using var character = Fixtures.CharacterTestEnvironment.Create(
            project,
            new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        );
        var installed = new Game.Components.ComponentCharacter();
        character.ComponentCharacter = installed;
        var values = new ValuesDictionary();
        character.Save(values);
        values.SetValue("Level", 7.5f);
        values.SetValue("FirstSpawnTime", 12.0);
        values.SetValue("LastSpawnTime", 40.0);
        values.SetValue("SpawnsCount", 3);
        character.ApplyReplicaState(values);
        character.ApplyReplicaState(values);
        character.Update();
        Assert.Equal(7.5f, character.Level);
        Assert.Equal(12.0, character.FirstSpawnTime);
        Assert.Equal(40.0, character.LastSpawnTime);
        Assert.Equal(3, character.SpawnsCount);
        Assert.Equal("Installed", character.CurrentState);
        Assert.Same(installed, character.ComponentCharacter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplicaStateCannotReplaceStableCharacterIdentity(bool replaceOwner)
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(NodeRole.Client);
        using var character = Fixtures.CharacterTestEnvironment.Create(
            project,
            new PlayerAddress(IdentityAddress.Encode(new byte[32]))
        );
        var values = new ValuesDictionary();
        character.Save(values);
        if (replaceOwner)
        {
            values.SetValue("OwnerAddress", IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray()));
        }
        else { values.SetValue("CharacterId", Guid.NewGuid()); }

        Assert.Throws<InvalidDataException>(() => character.ApplyReplicaState(values));
    }

    [Fact]
    public void AuthorityRejectsReplicaStateInstallation()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject();
        using var character = new CharacterData(project);
        Assert.Throws<InvalidOperationException>(() => character.ApplyReplicaState(new ValuesDictionary()));
    }

    [Fact]
    public void RemoteReplicaNeverEntersLocalViewPreparationOrRendering()
    {
        CharacterSkinsManager.Initialize();
        using var project = new CharacterProject(NodeRole.Client);
        using var character = new CharacterData(project);
        character.ComponentCharacter = new Game.Components.ComponentCharacter();
        using var view = new CharacterPresentation(new SubsystemCharacterPresentation(), character);
        // No graphics or view services exist: a remote replica must return before using them.
        view.Attach(Widgets.CharacterViewLifetimeTest.CreateWidget(view));
        view.Update();
        Assert.Equal(CharacterViewState.Inactive, view.State);
        Assert.False(view.IsReadyForPlaying);
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
            new NetNode(
                NetNodeOptions.Default,
                true,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                role
            ).Runtime.BindProject(this);
            Fixtures.RuleHookTestEnvironment.BindEmpty(this);
            var terrain = new SubsystemTerrain { Terrain = new Terrain() };
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(
                terrain,
                new TerrainDemandManager(terrain.Terrain, Game.Terrains.Distribution.TerrainContentRole.Authority)
            );
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
