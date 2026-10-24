using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Security;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Players;
using Game.Protocol.Identity;
using Game.Subsystems;

using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Network;

[Collection(NetworkWorkTypeCollection.Name)]
public sealed class CharacterCreationFlowTest
{
    [Fact]
    public void RejectionsOnlyCompleteTheMatchingRequestAndPermitRetry()
    {
        var previousType = CommonLib.WorkType;
        var previousServer = CommonLib.Net.Server;
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty, null);
        var pending = (List<OutboundPackage>)typeof(NetNode).GetField(
            "_pendingPackages",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!.GetValue(CommonLib.Net)!;
        var ids = new HashSet<Guid>();
        try
        {
            CommonLib.WorkType = WorkType.Client;
            CommonLib.Net.Server = server;
            var players = new SubsystemCharacters();
            var profile = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
            profile.Name = "Tester";
            profile.CharacterSkinName = "$Male1";
            var failures = new List<CharacterCreationError>();
            players.CharacterCreationRejected += failures.Add;
            var first = players.RequestOwnCharacterCreation(profile);
            ids.Add(first);
            Assert.NotEqual(Guid.Empty, first);
            Assert.Equal(first, players.RequestOwnCharacterCreation(profile));
            players.RejectCharacterCreation(Guid.NewGuid(), CharacterCreationError.NameUnavailable);
            Assert.Empty(failures);
            Assert.Equal(first, players.PendingCharacterCreationId);
            players.RejectCharacterCreation(first, CharacterCreationError.NameUnavailable);
            Assert.Equal([CharacterCreationError.NameUnavailable], failures);
            Assert.Equal(Guid.Empty, players.PendingCharacterCreationId);
            var second = players.RequestOwnCharacterCreation(profile);
            ids.Add(second);
            Assert.NotEqual(first, second);
            players.RejectCharacterCreation(first, CharacterCreationError.InvalidProfile);
            Assert.Equal(second, players.PendingCharacterCreationId);
            players.ClearCharacterCreationRequest();
            Assert.Equal(Guid.Empty, players.PendingCharacterCreationId);
        }
        finally
        {
            pending.RemoveAll(item =>
                item.Package is CharacterDataPackage package && ids.Contains(package.CreationRequestId)
            );
            CommonLib.Net.Server = previousServer;
            CommonLib.WorkType = previousType;
        }
    }

    [Fact]
    public void AuthorityReportsInvalidExistingAndOfflineDuplicateProfilesWithoutMutatingOwnership()
    {
        var players = new SubsystemCharacters();
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var store = new WorldPlayerStore();
        var user = store.GetOrCreate(address);
        var sender = new Client(null, 1, Guid.NewGuid(), Guid.Empty, null) { PlayerAddress = address };
        var request = new CharacterDataPackage
        {
            Type = CharacterDataPackage.DataType.Create, CreationRequestId = Guid.NewGuid(),
            CharacterName = "Tester", SkinName = "$Male1", CharacterSex = ProtocolEnum.Encode(CharacterSex.Male)
        };
        Assert.Null(CharacterDataPackageHandler.ValidateCreation(request, sender, players, store, out var name, out _));
        Assert.Equal("Tester", name);
        Assert.Equal(
            CharacterCreationError.Unavailable,
            CharacterDataPackageHandler.ValidateCreation(
                request with { CreationRequestId = Guid.Empty },
                sender,
                players,
                store,
                out _,
                out _
            )
        );
        Assert.Equal(
            CharacterCreationError.InvalidProfile,
            CharacterDataPackageHandler.ValidateCreation(
                request with { CharacterName = "!" },
                sender,
                players,
                store,
                out _,
                out _
            )
        );
        var data = new ValuesDictionary();
        data.SetValue("Name", "TESTER");
        var recordType = typeof(SubsystemCharacters).GetNestedType("OfflineCharacterData", BindingFlags.NonPublic)!;
        var record = Activator.CreateInstance(recordType, data, new ValuesDictionary())!;
        var offline = (IDictionary)typeof(SubsystemCharacters).GetField(
            "_offlineCharacters",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!.GetValue(players)!;
        offline.Add(Guid.NewGuid(), record);
        Assert.Equal(
            CharacterCreationError.NameUnavailable,
            CharacterDataPackageHandler.ValidateCreation(request, sender, players, store, out _, out _)
        );
        Assert.Equal(Guid.Empty, user.CharacterId);
        store.BindCharacter(address, Guid.NewGuid());
        Assert.Equal(
            CharacterCreationError.ExistingCharacter,
            CharacterDataPackageHandler.ValidateCreation(request, sender, players, store, out _, out _)
        );
        store.Ban(address, "rule", null, DateTimeOffset.UtcNow);
        Assert.Equal(
            CharacterCreationError.Unavailable,
            CharacterDataPackageHandler.ValidateCreation(request, sender, players, store, out _, out _)
        );
    }

    [Theory]
    [InlineData(CharacterCreationError.Unavailable)]
    [InlineData(CharacterCreationError.ExistingCharacter)]
    [InlineData(CharacterCreationError.InvalidProfile)]
    [InlineData(CharacterCreationError.NameUnavailable)]
    public void FailureRoundTripsItsCorrelationAndReason(CharacterCreationError error)
    {
        var package = new CharacterDataPackage
        {
            Type = CharacterDataPackage.DataType.CreationRejected, CreationRequestId = Guid.NewGuid(),
            CreationError = error
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var decoded = Assert.IsType<CharacterDataPackage>(ProtocolMessageCodec.Read(package.ID, reader));
        Assert.Equal(package.CreationRequestId, decoded.CreationRequestId);
        Assert.Equal(error, decoded.CreationError);
    }
}
