using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Protocol;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Hosting.Test.World;

public sealed class WorldProjectPersistenceTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionSubscriberCanDisposeOwnerWithoutWaitingForItsOwnNotification(bool failIo)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "world");
            if (failIo) { File.WriteAllText(target, "not a directory"); }

            using var project = new PersistenceProject(target);
            var disposedFromNotification = false;
            var saving = Task.Run(() =>
                {
                    using var owner = new WorldProjectPersistence(project);
                    owner.SaveCompleted += _ =>
                    {
                        owner.Dispose();
                        disposedFromNotification = true;
                    };
                    var pending = owner.SaveAsync();
                    while (!pending.IsCompleted)
                    {
                        owner.Pump();
                        Thread.Sleep(1);
                    }

                    Assert.Equal(failIo, pending.GetAwaiter().GetResult().Failure != null);
                }
            );
            await saving.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(disposedFromNotification);
            Assert.Throws<InvalidOperationException>(() => WorldProjectPersistence.For(project));
            if (!failIo)
            {
                Assert.True(WorldProjectPersistence.IsProjectFileValid(project.DirectoryName + "/Project.xml"));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DirectProjectDisposalNotifiesStoppedOnceAfterCleanupWithoutSaving(bool requestStop, bool failCleanup)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-direct-stop-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var project = new PersistenceProject(directory);
            var lifecycle = new SubsystemWorldLifecycle();
            project.Add(lifecycle);
            lifecycle.Load(new ValuesDictionary());
            var released = false;
            project.Add(
                new ReleaseProbe(() =>
                    {
                        released = true;
                        if (failCleanup) { throw new InvalidOperationException("Cleanup failed."); }
                    }
                )
            );
            var stopped = 0;
            lifecycle.Stopped += _ =>
            {
                Assert.True(project.IsDisposed);
                Assert.True(project.Node.Runtime.IsClosed);
                Assert.True(released);
                stopped++;
            };
            lifecycle.BindStopRequest(() => Task.CompletedTask);
            if (requestStop) { lifecycle.RequestStop(); }

            if (failCleanup) { Assert.Throws<AggregateException>(project.Dispose); }
            else { project.Dispose(); }

            Assert.Equal(1, stopped);
            Assert.False(File.Exists(Path.Combine(directory, "Project.xml")));
            project.Dispose();
            Assert.Equal(1, stopped);
            Assert.False(File.Exists(Path.Combine(directory, "Project.xml")));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void DisposingOnlyTheLifecycleDetachesItsFutureStopNotification()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-detached-stop-" + Guid.NewGuid().ToString("N"));
        using var project = new PersistenceProject(directory);
        var lifecycle = new SubsystemWorldLifecycle();
        project.Add(lifecycle);
        lifecycle.Load(new ValuesDictionary());
        var stopped = 0;
        lifecycle.Stopped += _ => stopped++;

        lifecycle.Dispose();
        lifecycle.Dispose();
        Assert.False(project.IsDisposed);
        Assert.False(project.Node.Runtime.IsClosed);
        Assert.Throws<ObjectDisposedException>(lifecycle.RequestStop);
        project.Dispose();
        Assert.Equal(0, stopped);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task StopStillDisposesTheProjectWhenAWorldCloseSubscriberFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-stop-" + Guid.NewGuid().ToString("N"));
        PersistenceProject? project = null;
        WorldRuntimeContext? originalWorld = null;
        var released = false;
        var stopped = 0;
        var owner = new AuthorityWorldExecutor(
            _ =>
            {
                project = new PersistenceProject(directory);
                var lifecycle = new SubsystemWorldLifecycle();
                project.Add(lifecycle);
                lifecycle.Load(new ValuesDictionary());
                project.Add(new ReleaseProbe(() => released = true));
                lifecycle.Stopped += _ => stopped++;
                project.Node.Runtime.Lifetime.Register(() =>
                    throw new InvalidOperationException("Close observer failed.")
                );
                originalWorld = project.Node.Runtime;
                return originalWorld;
            },
            false
        );
        try
        {
            await owner.Ready.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<Exception>(() => owner.StopAsync(true));
            Assert.True(project!.IsDisposed);
            Assert.True(originalWorld!.IsClosed);
            Assert.True(released);
            Assert.Equal(1, stopped);
        }
        finally
        {
            try { await owner.DisposeAsync(); }
            catch (Exception) { }

            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopRequiresSuccessfulSaveAndFailureCanBeRetried(bool failIo)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-stop-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "world");
        if (failIo) { File.WriteAllText(target, "not a directory"); }

        PersistenceProject? project = null;
        WorldRuntimeContext? originalWorld = null;
        var stopped = false;
        var failed = false;
        var owner = new AuthorityWorldExecutor(
            _ =>
            {
                project = new PersistenceProject(target);
                var lifecycle = new SubsystemWorldLifecycle();
                project.Add(lifecycle);
                lifecycle.Load(new ValuesDictionary());
                lifecycle.Stopped += _ => stopped = true;
                lifecycle.Persistence.SaveFailed += _ => failed = true;
                originalWorld = project.Node.Runtime;
                return originalWorld;
            },
            false
        );
        try
        {
            await owner.Ready.WaitAsync(TimeSpan.FromSeconds(5));
            var stop = owner.StopAsync(true);
            if (failIo) { await Assert.ThrowsAsync<WorldSaveException>(() => stop); }
            else { await stop.WaitAsync(TimeSpan.FromSeconds(5)); }

            Assert.Equal(failIo, failed);
            Assert.Equal(!failIo, stopped);
            Assert.Equal(!failIo, project!.IsDisposed);
            Assert.Equal(!failIo, originalWorld!.IsClosed);
            if (failIo)
            {
                Assert.Equal(AuthorityExecutionState.Running, owner.Status.State);
                File.Delete(target);
                await owner.StopAsync(true).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(stopped);
                Assert.True(project.IsDisposed);
            }

            Assert.True(WorldProjectPersistence.IsProjectFileValid(project.DirectoryName + "/Project.xml"));
        }
        finally
        {
            await owner.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }

    private sealed class ReleaseProbe(Action release) : Subsystem
    {
        public override void Dispose() => release();
    }

    private sealed class PersistenceProject : Project
    {
        public NetNode Node { get; } = new(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        public string DirectoryName { get; }

        public PersistenceProject(string directory)
        {
            var rootName = "persistence-" + Guid.NewGuid().ToString("N");
            Engine.Core.FileStorage.Storage.RegisterFileSystemRoot(rootName, Path.GetDirectoryName(directory)!);
            DirectoryName = rootName + ":/" + Path.GetFileName(directory);
            var names = new[]
            {
                "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
                "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
            };
            var types = names.Select(name => new DatabaseObjectType(
                    name,
                    name,
                    name,
                    0,
                    name == "Parameter",
                    false,
                    64,
                    false
                )
            ).ToArray();
            foreach (var type in types) { type.InitializeRelations(types, null, types[^1]); }

            var root = new DatabaseObject(types[0], "Root");
            GameDatabase = new GameDatabase(new Database(root, types));
            ProjectTemplate = new DatabaseObject(types[1], "Persistence") { NestingParent = root };
            Node.Runtime.BindProject(this);
            var catalog = new BlockContentCatalog(
                BlockRuntimeCatalog.Compile(
                    [new(ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock()))]
                )
            );
            _ = new WorldContentBinding(Node.Runtime, catalog, catalog.Dispose);
            Add(
                new SubsystemGameInfo
                    { WorldSettings = new WorldSettings { Name = "Persistence" }, DirectoryName = DirectoryName }
            );
        }

        public void Add(Subsystem subsystem)
        {
            var definition = new DatabaseObject(GameDatabase.MemberSubsystemTemplateType, subsystem.GetType().Name)
                { NestingParent = ProjectTemplate };
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            subsystem.Initialize(this, values);
            subsystems.Add(subsystem);
        }
    }
}
