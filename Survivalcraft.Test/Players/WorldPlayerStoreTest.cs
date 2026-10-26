using System.Security.Cryptography;
using System.Text;

using Engine.Core.Security;

using EntitySystem.TemplatesDatabase;

using Game.Players;
using Game.Protocol.Identity;

namespace Game.Test.Players;

public sealed class WorldPlayerStoreTest
{
    [Fact]
    public void SubjectAndCharacterRemainIndependentAndSingleOwned()
    {
        var store = new WorldPlayerStore();
        var first = Address("first");
        var second = Address("second");
        var character = Guid.NewGuid();
        var user = store.GetOrCreate(first);
        Assert.Equal(Guid.Empty, user.CharacterId);
        Assert.Same(user, store.GetOrCreate(new PlayerAddress(first.Value)));

        store.BindCharacter(first, character);
        store.BindCharacter(first, character);
        Assert.Throws<InvalidOperationException>(() => store.BindCharacter(second, character));
        Assert.Throws<InvalidOperationException>(() => store.BindCharacter(first, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => store.BindCharacter(first, Guid.Empty));
        Assert.Equal(character, user.CharacterId);
    }

    [Fact]
    public void PermissionsAndBanRoundTripOnSubjectWithoutRemovingCharacterAssets()
    {
        var store = new WorldPlayerStore();
        var address = Address("player");
        var actor = Address("operator");
        var character = Guid.NewGuid();
        var permission = new ResourceId(new ModId("game"), "permissions.manage.standard");
        var user = store.GetOrCreate(address);
        user.DisplayName = "Display nickname";
        user.IsMuted = true;
        user.Permissions.Grant(permission, true);
        store.BindCharacter(address, character);
        var time = DateTimeOffset.Parse("2026-10-22T12:00:00+08:00");
        store.Ban(address, "world rule violation", actor, time);

        var loaded = new WorldPlayerStore();
        loaded.Load(store.Save());
        var restored = Assert.Single(loaded.Entries);
        Assert.Equal(address, restored.Address);
        Assert.Equal(character, restored.CharacterId);
        Assert.Equal(user.DisplayName, restored.DisplayName);
        Assert.True(restored.IsMuted);
        Assert.True(restored.Permissions.CanDelegate(permission));
        Assert.Equal(new PlayerBan("world rule violation", actor, time.ToUniversalTime()), restored.Ban);
        Assert.True(loaded.Unban(address));
        Assert.False(loaded.Unban(address));
        Assert.Null(restored.Ban);
        Assert.Equal(character, restored.CharacterId);
        Assert.True(restored.Permissions.CanDelegate(permission));
    }

    [Fact]
    public void InvalidOwnershipInStoredDataDoesNotPartiallyReplaceSubjects()
    {
        var original = new WorldPlayerStore();
        var retained = original.GetOrCreate(Address("retained"));
        var incoming = new WorldPlayerStore();
        var first = Address("first");
        var second = Address("second");
        var character = Guid.NewGuid();
        incoming.BindCharacter(first, character);
        incoming.GetOrCreate(second);
        var data = incoming.Save();
        data.GetValue<ValuesDictionary>(second.Value).SetValue("CharacterId", character);

        Assert.Throws<InvalidOperationException>(() => original.Load(data));
        Assert.Same(retained, Assert.Single(original.Entries));
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void PlayerAddressRejectsLegacyAndNonCanonicalIdentifiers(string input)
    {
        Assert.Throws<ArgumentException>(() => new PlayerAddress(input));
    }

    private static PlayerAddress Address(string label)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(label));
        return new PlayerAddress(IdentityAddress.Encode(digest));
    }
}
