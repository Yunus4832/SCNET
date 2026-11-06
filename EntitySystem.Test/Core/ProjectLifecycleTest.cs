using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.Core;

[Collection(ProjectLifecycleCollection.Name)]
public sealed class ProjectLifecycleTest
{
    [Fact]
    public void ConfigurationRunsBeforeSubsystemLoading()
    {
        var (database, template) = ProjectTestDatabase.Create();
        var probe = new DatabaseObject(database.MemberSubsystemTemplateType, "Probe") { NestingParent = template };
        probe.SetNestedValue("Class", typeof(ProbeSubsystem).FullName!);
        probe.SetNestedValue("IsOptional", false);
        var data = new ProjectData(database, template, null);

        using var project = new Project(
            database,
            data,
            world =>
                world.ExtensionData.Get("test", "context").SetValue("configured", true)
        );

        Assert.True(project.FindSubsystem<ProbeSubsystem>(true)!.SawConfiguration);
    }

    [Fact]
    public void DisposingOneWorldDoesNotRemoveOtherWorldLoadObservers()
    {
        var (database, template) = ProjectTestDatabase.Create();
        var loaded = new List<Project>();
        Project.OnProjectLoad += OnLoaded;
        try
        {
            var first = new Project(database, new ProjectData(database, template, null));
            first.Dispose();
            using var second = new Project(database, new ProjectData(database, template, null));

            Assert.Equal(new[] { first, second }, loaded);
        }
        finally
        {
            Project.OnProjectLoad -= OnLoaded;
        }

        void OnLoaded(Project project)
        {
            loaded.Add(project);
        }
    }

    [Fact]
    public void FailedConstructionStillReleasesTheConfiguredOwner()
    {
        var (database, template) = ProjectTestDatabase.Create();
        var released = false;
        Project? failed = null;

        Assert.Throws<InvalidOperationException>(() => new Project(
                database,
                new ProjectData(database, template, null),
                world =>
                {
                    failed = world;
                    world.Disposed += _ => released = true;
                    throw new InvalidOperationException("Configuration failed.");
                }
            )
        );

        Assert.True(released);
        Assert.True(failed!.IsDisposed);
    }

    [Fact]
    public void DisposalAttemptsAllResourcesAndObserversEvenWhenOneFails()
    {
        var calls = new List<string>();
        var project = new DisposalProject();
        project.Add(
            new DisposalProbe(() =>
                {
                    calls.Add("failing resource");
                    throw new InvalidOperationException("Resource failed.");
                }
            )
        );
        project.Add(new DisposalProbe(() => calls.Add("remaining resource")));
        project.Disposed += _ =>
        {
            calls.Add("failing observer");
            throw new InvalidOperationException("Observer failed.");
        };
        project.Disposed += _ => calls.Add("remaining observer");

        var exception = Assert.Throws<AggregateException>(project.Dispose);
        project.Dispose();

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(
            new[]
            {
                "failing resource", "remaining resource", "failing observer", "remaining observer"
            },
            calls
        );
    }

    public sealed class ProbeSubsystem : Subsystem
    {
        public bool SawConfiguration { get; private set; }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            SawConfiguration = Project.ExtensionData.Get("test", "context").GetValue<bool>("configured");
        }
    }

    private sealed class DisposalProject : Project
    {
        public void Add(Subsystem subsystem)
        {
            subsystems.Add(subsystem);
        }
    }

    private sealed class DisposalProbe(Action dispose) : Subsystem
    {
        public override void Dispose()
        {
            dispose();
        }
    }
}
