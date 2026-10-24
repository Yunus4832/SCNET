using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using Engine.Core.Runtime;
using Engine.Core.Security;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game;
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
    [InlineData(RunModeType.Gui, false)]
    [InlineData(RunModeType.Gui, true)]
    [InlineData(RunModeType.HeadlessServer, false)]
    [InlineData(RunModeType.HeadlessServer, true)]
    public void DisconnectBeforeFirstSpawnPreservesCharacterAndResumesSpawn(RunModeType mode, bool reload)
    {
        var previousMode = RunMode.Value;
        var previousWorkType = CommonLib.WorkType;
        var previousClients = CommonLib.Net.Clients;
        var previousGarbageCollect = SettingsManager.Current.AutoGarbageCollect;
        try
        {
            RunMode.Value = mode;
            CommonLib.WorkType = WorkType.Server;
            CommonLib.Net.Clients = [];
            SettingsManager.Current.AutoGarbageCollect = false;
            CharacterSkinsManager.Initialize();
            using var project = new CharacterProject();
            var owner = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
            var character = new CharacterData(project)
            {
                OwnerAddress = owner, Name = "BeforeSpawn", CharacterSkinName = "$Male1", Level = 3.5f
            };
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
            SettingsManager.Current.AutoGarbageCollect = previousGarbageCollect;
            CommonLib.Net.Clients = previousClients;
            CommonLib.WorkType = previousWorkType;
            RunMode.Value = previousMode;
        }
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

        public CharacterProject()
        {
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
                    Players, Characters, terrain, new SubsystemTime(), new SubsystemGameWidgets(),
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
