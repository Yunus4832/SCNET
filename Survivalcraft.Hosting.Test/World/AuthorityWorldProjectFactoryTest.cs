using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Blocks.Collision;
using Game.Celebrations;
using Game.Hosting;
using Game.Modding.Blocks;
using Game.Network;
using Game.Protocol;
using Game.Spawning;
using Game.World;

namespace Survivalcraft.Hosting.Test.World;

public sealed class AuthorityWorldProjectFactoryTest
{
    [Fact]
    public void FileOperationsExcludeWorldLoadingAndOverlappingOperationsUntilReleased()
    {
        var root = "world-operation-" + Guid.NewGuid().ToString("N");
        Engine.Core.FileStorage.Storage.RegisterFileSystemRoot(root, Path.GetTempPath());
        var directory = root + ":/world-operation-test";
        var activity = new WorldProjectActivity();
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using (activity.BeginFileOperation(directory))
        {
            Assert.Throws<InvalidOperationException>(() => activity.Track(world, directory));
            Assert.Throws<InvalidOperationException>(() => activity.BeginFileOperation(directory + "/Regions"));
        }

        activity.Track(world, directory);
        Assert.Throws<InvalidOperationException>(() => activity.BeginFileOperation(directory));
        world.Close();
        using var released = activity.BeginFileOperation(directory);
    }

    [Fact]
    public void HostCreatesAndReleasesAnActualProjectUsingPreparedCoreContentWithoutApplicationTypes()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var activity = new WorldProjectActivity();
        var root = "world-activity-" + Guid.NewGuid().ToString("N");
        Engine.Core.FileStorage.Storage.RegisterFileSystemRoot(root, Path.GetTempPath());
        var directory = root + ":/scnet-world-activity-test";
        activity.Track(world, directory);
        var catalog = Catalog();
        var released = 0;
        _ = new WorldContentBinding(
            world,
            catalog,
            () =>
            {
                released++;
                catalog.Dispose();
            }
        );
        var (database, template) = Database();
        var xml = new XElement("Project", new XAttribute("Name", template.Name), new XElement("BlockRegistry"));
        var written = 0;
        var project = AuthorityWorldProjectFactory.Create(
            world,
            catalog,
            database,
            xml,
            new ValuesDictionary(),
            Policies(),
            metadata =>
            {
                Assert.Same(xml, metadata);
                Assert.NotNull(metadata.Element("BlockRegistry"));
                written++;
            }
        );

        Assert.Same(project, world.Project);
        Assert.Same(catalog, WorldContentBinding.For(project).Catalog);
        Assert.Equal(1, written);
        Assert.Equal(0, released);
        Assert.True(activity.IsDirectoryInUse(directory));
        Assert.Contains(project, activity.Projects);
        world.Close();
        Assert.Equal(0, released);
        Assert.True(activity.IsDirectoryInUse(directory));
        Assert.Empty(activity.Projects);
        project.Dispose();
        Assert.Equal(1, released);
        Assert.False(activity.IsDirectoryInUse(directory));
        Assert.DoesNotContain(
            typeof(AuthorityWorldProjectFactory).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Survivalcraft" or "Survivalcraft.Client"
        );
    }

    [Fact]
    public void AForeignPreparedCatalogCannotEnterAnotherWorldAndFailedConstructionReleasesTheRealOwner()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var owned = Catalog();
        using var foreign = Catalog();
        var released = 0;
        _ = new WorldContentBinding(
            world,
            owned,
            () =>
            {
                released++;
                owned.Dispose();
            }
        );
        var (database, template) = Database();
        Assert.Throws<InvalidOperationException>(() => AuthorityWorldProjectFactory.Create(
                world,
                foreign,
                database,
                new XElement("Project", new XAttribute("Name", template.Name)),
                new ValuesDictionary(),
                Policies(),
                _ => throw new InvalidOperationException("Must not persist foreign content.")
            )
        );
        Assert.True(world.IsClosed);
        Assert.Equal(1, released);
        Assert.False(world.HasProjectResources);
    }

    private static AuthorityWorldProjectPolicies Policies() => new(
        new CreatureSpawnPolicy(100, 16, 32, 8, 96, 100, 16, 96, 30f, 60f),
        new NetworkInterestPolicy(128),
        new NewYearCelebrationPolicy(TimeProvider.System, () => 2026, _ => { }),
        () => false
    );

    private static BlockContentCatalog Catalog()
    {
        var registry = BlockRuntimeCatalog.Compile(
            [new(ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock()))]
        );
        var catalog = new BlockContentCatalog(registry);
        catalog.Initialize(new CollisionGeometryCatalog([]), null, new XElement("Recipes"));
        return catalog;
    }

    private static (GameDatabase, DatabaseObject) Database()
    {
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
        var template = new DatabaseObject(types[1], "Authority") { NestingParent = root };
        return (new GameDatabase(new Database(root, types)), template);
    }
}
