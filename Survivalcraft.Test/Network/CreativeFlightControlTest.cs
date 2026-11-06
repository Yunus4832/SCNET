using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Characters;
using Game.Components;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Handling.Presentation;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class CreativeFlightControlTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlightIntentSurvivesActualPayloadRoundTripAndUnchangedRetry(bool lookDirected)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var player = CreatePlayer(project, false);
        player.ApplyRemoteFlightControl(lookDirected);
        _ = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        var package = CharacterPackageBuilder.Capture(player, ComponentCharacterPackage.CharacterAction.BodyUpdate);
        var decoded =
            Assert.IsType<CharacterStatePackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(package)));
        Assert.Equal(lookDirected, decoded.UseLookDirectedCreativeFlight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityAcceptsFlightFactOnlyFromTheOwningConnection(bool spoofSender)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).RequireNode();
        var owner = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var stranger = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(owner);
        node.AddClient(stranger);
        var player = CreatePlayer(project, false);
        player.CharacterData.CharacterId = owner.CharacterId;
        var characters = new SubsystemCharacters();
        project.AddSubsystem(characters);
        ((List<CharacterData>)Field(characters, "_characters")!).Add(player.CharacterData);
        new ComponentCharacterPackageHandler().Handle(
            new CharacterStatePackage { ControllerClientId = owner.ID, UseLookDirectedCreativeFlight = true },
            new PackageReceiveContext(node, node.Runtime, spoofSender ? stranger : owner)
        );
        Assert.Equal(!spoofSender, player.UseLookDirectedCreativeFlight);
    }

    [Fact]
    public void LocalPredictionOwnsItsFlightFactAndIgnoresRemoteEcho()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var player = CreatePlayer(project, true);
        player.SetControlInput(new CharacterControlInput { UseLookDirectedCreativeFlight = true });
        player.ApplyRemoteFlightControl(false);
        Assert.True(player.UseLookDirectedCreativeFlight);
        player.SetControlInput(new CharacterControlInput { UseLookDirectedCreativeFlight = false });
        player.ApplyRemoteFlightControl(true);
        Assert.False(player.UseLookDirectedCreativeFlight);
    }

    [Fact]
    public void TouchFlightFollowsLookPitchWhileKeyboardFlightStaysHorizontal()
    {
        var look = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        var touch = ComponentLocomotion.GetCreativeFlightForward(look, true);
        var keyboard = ComponentLocomotion.GetCreativeFlightForward(look, false);
        Assert.True(touch.Y > look.Y);
        Assert.True(touch.Z > 0f);
        Assert.InRange(touch.Length(), .99999f, 1.00001f);
        Assert.True(Vector3.DistanceSquared(Vector3.UnitZ, keyboard) < 1e-12f);
        var horizontalTouch = ComponentLocomotion.GetCreativeFlightForward(Vector3.UnitZ, true);
        Assert.InRange(horizontalTouch.Y, .09950f, .09951f);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PositionResultCancelsOnlyLocalPresentationAndDoesNotRequireObserverInput(bool local)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var player = CreatePlayer(project, local);
        ComponentInput? input = null;
        if (local)
        {
            input = new ComponentInput();
            ((List<Component>)Field(player.Entity, "_components")!).Add(input);
            SetField(input.Navigation, "_player", player);
            SetField(input.ViewControl, "_player", player);
        }

        var position = new Vector3(7f, 80f, -2f);
        typeof(ClientComponentCharacterPackageHandler)
            .GetMethod("ApplyAuthoritativePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new ClientComponentCharacterPackageHandler(), [player, position, Vector3.Zero]);
        Assert.Equal(position, player.ComponentBody.Position);
        if (input is not null)
        {
            Assert.Equal("cancelled", Field(input.Navigation, "_status"));
            Assert.Equal("teleported", Field(input.Navigation, "_reason"));
            Assert.Equal("cancelled", Field(input.ViewControl, "_status"));
            Assert.Equal("teleported", Field(input.ViewControl, "_reason"));
        }
    }

    private static ComponentCharacter CreatePlayer(RuntimeTestProject project, bool local)
    {
        var data = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        SetField(data, "<Project>k__BackingField", project);
        typeof(CharacterData).GetProperty(nameof(CharacterData.IsLocalCharacter))!.SetValue(data, local);
        var locomotion = new ComponentLocomotion();
        var player = new ComponentCharacter
        {
            CharacterData = data,
            ComponentBody = new ComponentBody
                { Rotation = Quaternion.Identity, Velocity = Vector3.Zero, Locomotion = locomotion },
            ComponentLocomotion = locomotion
        };
        project.InitializeComponent(player);
        data.ComponentCharacter = player;
        return player;
    }

    private static object? Field(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }
}
