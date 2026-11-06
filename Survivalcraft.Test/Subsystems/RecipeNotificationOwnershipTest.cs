using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class RecipeNotificationOwnershipTest
{
    [Fact]
    public void AuthorityRecipeNoticeUsesOwnerTransportWithoutGuiDependencies()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var runtime = WorldRuntimeContext.For(authority);
        var node = WorldNetworkBinding.For(runtime).RequireNode();
        var clientNode = WorldNetworkBinding.For(client).RequireNode();
        var id = Guid.NewGuid();
        var owner = new Client(null, 1, Guid.NewGuid(), id)
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(owner);
        var server = new Client(Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(node, owner, clientNode, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        typeof(CharacterData).GetField("<Project>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(data, authority);
        data.CharacterId = id;
        var character = new ComponentCharacter { CharacterData = data };
        authority.InitializeComponent(character);
        var inventory = new TestInventory();
        authority.InitializeComponent(inventory);
        inventory.Notify(character, "Recipe needs more experience");
        MessagePackage? received = null;
        for (var i = 0; i < 10 && received is null; i++)
        {
            typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(node, null);
            WorldNetworkBinding.For(clientNode.Runtime).Inbox.Drain(
                10,
                envelope => received = Assert.IsType<MessagePackage>(envelope.Package)
            );
        }

        Assert.NotNull(received);
        Assert.Equal(GameMessageKind.System, received.GameMessage.Kind);
        Assert.Equal(GameMessagePresentation.Toast, received.GameMessage.Presentation);
    }

    [Theory]
    [InlineData(RuntimeSide.Client, false)]
    [InlineData(RuntimeSide.Authority, true)]
    public void ClientOrClosedInventoryCannotPublishRecipeNotices(RuntimeSide side, bool close)
    {
        using var project = new RuntimeTestProject(side);
        var inventory = new TestInventory();
        project.InitializeComponent(inventory);
        if (close) { WorldRuntimeContext.For(project).Close(); }

        inventory.Notify(new ComponentCharacter(), "No GUI or player connection should be touched");
    }

    private sealed class TestInventory : ComponentInventoryBase
    {
        internal void Notify(ComponentCharacter character, string message) =>
            PublishRecipeMessage(
                character,
                LocalizedText.Literal(message),
                GameMessageTone.Normal
            );
    }
}
