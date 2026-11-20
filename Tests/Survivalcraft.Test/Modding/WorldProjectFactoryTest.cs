using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Network;
using Game.Widgets;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldProjectFactoryTest
{
    [Fact]
    public void AClientFactoryCannotConsumeAnAuthorityWorld()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        using var mods = GameModRuntime.Start();
        var runtime = WorldRuntimeContext.For(project);
        Assert.Throws<InvalidOperationException>(() =>
            WorldProjectFactory.CreateClient(
                runtime,
                mods,
                project.GameDatabase,
                [],
                new CanvasWidget(),
                BlockIndexMapping.Empty,
                null
            )
        );
        Assert.False(runtime.IsClosed);
        Assert.False(project.IsDisposed);
    }

    [Fact]
    public void ClientSnapshotFailureReleasesOnlyItsOwnResources()
    {
        Dispatcher.Initialize();
        ContentManager.Initialize();
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var mods = GameModRuntime.Start();
        mods.InitializeContentData();
        var client = new NetNode(
            NetNodeOptions.Default,
            false,
            Game.Network.Handling.ApplicationMessageHandlers.CreatePreWorld(
                NodeRole.Client,
                false
            ),
            NodeRole.Client
        ).Runtime;
        var payload = new ValuesDictionary().ToMessagePack();
        var error = Assert.Throws<InvalidOperationException>(() =>
            WorldProjectFactory.CreateClient(
                client,
                mods,
                authority.GameDatabase,
                payload,
                new CanvasWidget(),
                BlockIndexMapping.Empty,
                null
            )
        );
        Assert.Equal("Project template guid or name must be specified.", error.Message);
        Assert.True(client.IsClosed);
        Assert.Empty(mods.WorldCatalogs);
        Assert.False(authority.IsDisposed);
        Assert.False(WorldRuntimeContext.For(authority).IsClosed);
    }

    [Fact]
    public void AClientSnapshotBuildsASeparateProjectInTheSameDatabase()
    {
        Dispatcher.Initialize();
        ContentManager.Initialize();
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var mods = GameModRuntime.Start();
        mods.InitializeContentData();
        AddMetadataSubsystem(authority, "GameInfo");
        AddMetadataSubsystem(authority, "Views");
        var clientRuntime = new NetNode(
            NetNodeOptions.Default,
            false,
            Game.Network.Handling.ApplicationMessageHandlers.CreatePreWorld(
                NodeRole.Client,
                false
            ),
            NodeRole.Client
        ).Runtime;
        using var client = WorldProjectFactory.CreateClient(
            clientRuntime,
            mods,
            authority.GameDatabase,
            CommonLib.GetNowProject(authority)!,
            new CanvasWidget(),
            BlockIndexMapping.Empty,
            null
        );
        Assert.NotSame(authority, client);
        Assert.Same(authority.GameDatabase, client.GameDatabase);
        Assert.Same(clientRuntime, WorldRuntimeContext.For(client));
        Assert.Same(client, clientRuntime.Project);
        Assert.False(authority.IsDisposed);
    }

    private static void AddMetadataSubsystem(RuntimeTestProject project, string name)
    {
        var definition = new DatabaseObject(project.GameDatabase.MemberSubsystemTemplateType, name)
        {
            NestingParent = project.ProjectTemplate
        };
        _ = new DatabaseObject(project.GameDatabase.ParameterType, "Class", typeof(MetadataSubsystem).FullName!)
        {
            NestingParent = definition
        };
        _ = new DatabaseObject(project.GameDatabase.ParameterType, "IsOptional", false)
        {
            NestingParent = definition
        };
    }

    public sealed class MetadataSubsystem : Subsystem;
}
