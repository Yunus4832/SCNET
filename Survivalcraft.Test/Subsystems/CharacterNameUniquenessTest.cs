using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Security;

using EntitySystem.TemplatesDatabase;

using Game.Characters;
using Game.Network;
using Game.Protocol.Identity;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

[Collection(NetworkWorkTypeCollection.Name)]
public sealed class CharacterNameUniquenessTest
{
    [Fact]
    public void ActiveAndOfflineNamesAreReservedCaseInsensitively()
    {
        var subsystem = new SubsystemCharacters();
        var active = AddActive(subsystem, "Alice");
        var offlineId = AddOffline(subsystem, "Bob");
        Assert.False(subsystem.IsCharacterNameAvailable("ALICE"));
        Assert.False(subsystem.IsCharacterNameAvailable("bob"));
        Assert.True(subsystem.IsCharacterNameAvailable("alice", active.CharacterId));
        Assert.True(subsystem.IsCharacterNameAvailable("BOB", offlineId));
        Assert.False(subsystem.IsCharacterNameAvailable("Bob", active.CharacterId));
        Assert.True(subsystem.IsCharacterNameAvailable("Charlie"));
        Assert.False(subsystem.IsCharacterNameAvailable(string.Empty));
    }

    [Fact]
    public void ActiveRenameSupersedesStaleOfflineCopyForTheSameCharacter()
    {
        var subsystem = new SubsystemCharacters();
        var characterId = AddOffline(subsystem, "BeforeRename");
        var active = AddActive(subsystem, "AfterRename");
        active.CharacterId = characterId;
        Assert.True(subsystem.IsCharacterNameAvailable("BeforeRename"));
        Assert.False(subsystem.IsCharacterNameAvailable("AFTERRENAME"));
        Assert.True(subsystem.IsCharacterNameAvailable("AfterRename", characterId));
    }

    [Fact]
    public void ClientPrevalidationIncludesOfflineNamesFromThePublicRoster()
    {
        var previous = CommonLib.WorkType;
        try
        {
            CommonLib.WorkType = WorkType.Client;
            var subsystem = new SubsystemCharacters();
            var id = Guid.NewGuid();
            subsystem.ApplyCharacterList(
                [new CharacterListEntry(id, new PlayerAddress(IdentityAddress.Encode(new byte[32])), "Offline", false)]
            );
            Assert.False(subsystem.IsCharacterNameAvailable("OFFLINE"));
            Assert.True(subsystem.IsCharacterNameAvailable("Offline", id));
            Assert.True(subsystem.IsCharacterNameAvailable("Available"));
        }
        finally { CommonLib.WorkType = previous; }
    }

    private static CharacterData AddActive(SubsystemCharacters subsystem, string name)
    {
        var player = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        player.CharacterId = Guid.NewGuid();
        player.Name = name;
        var players = (List<CharacterData>)typeof(SubsystemCharacters)
            .GetField("_characters", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(subsystem)!;
        players.Add(player);
        return player;
    }

    private static Guid AddOffline(SubsystemCharacters subsystem, string name)
    {
        var data = new ValuesDictionary();
        data.SetValue("Name", name);
        var recordType = typeof(SubsystemCharacters).GetNestedType("OfflineCharacterData", BindingFlags.NonPublic)!;
        var record = Activator.CreateInstance(recordType, data, new ValuesDictionary())!;
        var offline = (IDictionary)typeof(SubsystemCharacters)
            .GetField("_offlineCharacters", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(subsystem)!;
        var characterId = Guid.NewGuid();
        offline.Add(characterId, record);
        return characterId;
    }
}
