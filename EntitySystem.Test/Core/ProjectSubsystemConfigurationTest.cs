using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.Core;

public sealed class ProjectSubsystemConfigurationTest
{
    [Fact]
    public void ExplicitSubsystemInputsAreBoundBeforeLoading()
    {
        var (database, data) = CreateProjectData();
        using var project = new Project(
            database,
            data,
            configureSubsystem: subsystem => ((ConfiguredSubsystem)subsystem).Configured = true
        );
        var configured = project.FindSubsystem<ConfiguredSubsystem>(true)!;
        Assert.True(configured.LoadedWithConfiguration);
    }

    [Fact]
    public void AFailedConfigurationStillDisposesTheConstructedSubsystem()
    {
        var (database, data) = CreateProjectData();
        ConfiguredSubsystem? constructed = null;
        Assert.Throws<InvalidOperationException>(() => new Project(
                database,
                data,
                configureSubsystem: subsystem =>
                {
                    constructed = (ConfiguredSubsystem)subsystem;
                    throw new InvalidOperationException("Cannot bind inputs.");
                }
            )
        );
        Assert.NotNull(constructed);
        Assert.True(constructed.Released);
        Assert.False(constructed.LoadedWithConfiguration);
    }

    private static (GameDatabase Database, ProjectData Data) CreateProjectData()
    {
        var (database, template) = ProjectTestDatabase.Create();
        var member = new DatabaseObject(database.MemberSubsystemTemplateType, "Configured")
        {
            NestingParent = template
        };
        member.SetNestedValue("Class", typeof(ConfiguredSubsystem).FullName!);
        member.SetNestedValue("IsOptional", false);
        return (database, new ProjectData(database, template, null));
    }

    public sealed class ConfiguredSubsystem : Subsystem
    {
        public bool Configured { get; set; }

        public bool LoadedWithConfiguration { get; private set; }

        public bool Released { get; private set; }

        public override void Load(ValuesDictionary valuesDictionary) => LoadedWithConfiguration = Configured;

        public override void Dispose() => Released = true;
    }
}
