using System.Reflection;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class SignEditAuthorityTest
{
    [Fact]
    public void EditingQueuesARequestAndWaitsForAuthoritativeData()
    {
        using var authorityProject = new RuntimeTestProject(RuntimeSide.Authority);
        using var clientProject = new RuntimeTestProject(RuntimeSide.Client);
        var authority = WorldNetworkBinding.For(authorityProject).Node!;
        var client = WorldNetworkBinding.For(clientProject).Node!;
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        var server = new Client(Guid.Empty);
        authority.AddClient(player);
        client.AddClient(server);
        client.Server = server;
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, server);
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var signs = AttachSigns(clientProject, GameMode.Survival);
        var point = new Point3(1, 65, 2);
        var original = Data("original");
        signs.SetSignData(point, original.Lines, original.Colors, original.Url);
        var requests = new List<ReceivedPackage>();
        authority.OnReceive += (_, messages) => requests.AddRange(messages);

        var presentation = AttachPresentation(clientProject);
        presentation.SubmitEdit(point, Data("requested"));

        Assert.Equal("original", signs.GetSignData(point)!.Lines[0]);
        Assert.Empty(requests);
        client.Update();
        authority.Update();
        var request = Assert.IsType<SignBlockPackage>(Assert.Single(requests).Package);
        Assert.Equal("requested", request.Lines[0]);
        Assert.Equal(point, request.Point);
        Assert.Equal("original", signs.GetSignData(point)!.Lines[0]);
        var approved = Data("approved");
        new SignBlockPackageHandler().Handle(
            new SignBlockPackage(point, approved.Lines, approved.Colors, approved.Url),
            new PackageReceiveContext(client, client.Runtime, server)
        );
        Assert.Equal("approved", signs.GetSignData(point)!.Lines[0]);
    }

    [Fact]
    public void AdventureAuthorityRejectsBothOpeningAndSavingBeforeAccessingWorldData()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var signs = AttachSigns(project, GameMode.Adventure);
        var runtime = WorldRuntimeContext.For(project);
        var point = new Point3(1, 65, 2);
        var original = Data("original");
        signs.SetSignData(point, original.Lines, original.Colors, original.Url);
        var actor = new Client(Guid.Empty);
        var context = new PackageReceiveContext(WorldNetworkBinding.For(runtime).Node, runtime, actor);
        // No terrain or interest subsystem: read-only rejection must precede those dependencies.
        new BlockEditPackageHandler().Handle(new OpenSignEditorPackage { Point3 = point }, context);
        var edit = Data("rejected");
        new SignBlockPackageHandler().Handle(new SignBlockPackage(point, edit.Lines, edit.Colors, edit.Url), context);

        Assert.Equal("original", signs.GetSignData(point)!.Lines[0]);
        Assert.Equal(0, WorldNetworkBinding.For(runtime).Node!.PendingPackageCount);
    }

    [Fact]
    public void AuthorityCannotOpenAnEditorOrSubmitAClientEdit()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var signs = AttachSigns(project, GameMode.Survival);
        var presentation = AttachPresentation(project);
        presentation.OpenEditor(null!, new Point3(1, 65, 2));
        Assert.Throws<InvalidOperationException>(() => presentation.SubmitEdit(new Point3(1, 65, 2), Data("invalid")));
    }

    private static SignData Data(string text) => new() { Lines = [text, "", "", ""] };

    private static SubsystemSignBlockBehavior AttachSigns(RuntimeTestProject project, GameMode mode)
    {
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { GameMode = mode } };
        project.AddSubsystem(info);
        var signs = new SubsystemSignBlockBehavior();
        project.AddSubsystem(signs);
        typeof(SubsystemSignBlockBehavior).GetField(
                "_subsystemGameInfo",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .SetValue(signs, info);
        return signs;
    }

    private static SubsystemSignBlockPresentation AttachPresentation(RuntimeTestProject project)
    {
        var presentation = new SubsystemSignBlockPresentation();
        project.InitializeSubsystem(presentation);
        return presentation;
    }
}
