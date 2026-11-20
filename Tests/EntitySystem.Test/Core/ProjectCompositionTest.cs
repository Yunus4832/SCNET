using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.Core;

[Collection(ProjectLifecycleCollection.Name)]
public sealed class ProjectCompositionTest
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("Missing.Subsystem", "Missing.Component")]
    [InlineData("throw", "throw")]
    public void ExcludedDefinitionsArePreservedWithoutResolutionOrConstruction(
        string? subsystemClass,
        string? componentClass
    )
    {
        var (database, template) = ProjectTestDatabase.Create();
        AddMember(
            database.MemberSubsystemTemplateType,
            template,
            "ExcludedSubsystem",
            subsystemClass == "throw" ? typeof(ThrowingSubsystem).FullName : subsystemClass
        );
        var entityTemplate = new DatabaseObject(database.EntityTemplateType, "Entity");
        AddMember(
            database.MemberComponentTemplateType,
            entityTemplate,
            "ExcludedComponent",
            componentClass == "throw" ? typeof(ThrowingComponent).FullName : componentClass
        );
        var data = new ProjectData(database, template, null);
        var definition = new ValuesDictionary();
        definition.PopulateFromDatabaseObject(entityTemplate);
        var composition = new SelectedComposition();

        using var project = new Project(database, data, composition: composition);
        using var entity = project.CreateEntity(definition);

        Assert.Empty(project.ReadOnlySubsystems);
        Assert.NotNull(data.ValuesDictionary.GetValue<ValuesDictionary>("ExcludedSubsystem"));
        Assert.NotNull(entity.ValuesDictionary.GetValue<ValuesDictionary>("ExcludedComponent"));
        Assert.Null(entity.FindComponent<ThrowingComponent>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectedMissingClassStillFails(bool subsystem)
    {
        var (database, template) = ProjectTestDatabase.Create();
        var entityTemplate = new DatabaseObject(database.EntityTemplateType, "Entity");
        AddMember(
            subsystem ? database.MemberSubsystemTemplateType : database.MemberComponentTemplateType,
            subsystem ? template : entityTemplate,
            "Selected",
            "Missing.SelectedClass"
        );
        var data = new ProjectData(database, template, null);
        var composition = new SelectedComposition();
        if (subsystem)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new Project(
                    database,
                    data,
                    composition: composition
                )
            );
            Assert.Contains("Missing.SelectedClass", exception.Message);
        }
        else
        {
            using var project = new Project(database, data, composition: composition);
            var definition = new ValuesDictionary();
            definition.PopulateFromDatabaseObject(entityTemplate);
            var exception = Assert.Throws<InvalidOperationException>(() => new Entity(project, definition));
            Assert.Contains("Missing.SelectedClass", exception.Message);
        }
    }

    [Fact]
    public void SameCompositionIsAvailableBeforeConfigurationAndLoadsInitialAndLaterMembers()
    {
        var (database, template) = ProjectTestDatabase.Create();
        AddMember(database.MemberSubsystemTemplateType, template, "SelectedSubsystem", typeof(ProbeSubsystem).FullName);
        var entityTemplate = new DatabaseObject(database.EntityTemplateType, "Entity");
        AddMember(
            database.MemberComponentTemplateType,
            entityTemplate,
            "SelectedComponent",
            typeof(ProbeComponent).FullName
        );
        AddMember(database.MemberComponentTemplateType, entityTemplate, "ExcludedComponent", "Missing.Component");
        var definition = new ValuesDictionary();
        definition.PopulateFromDatabaseObject(entityTemplate);
        var data = new ProjectData(database, template, null);
        data.EntityDataList.EntitiesData.Add(new EntityData { Id = 1, ValuesDictionary = definition });
        var composition = new SelectedComposition();
        using var project = new Project(
            database,
            data,
            world => Assert.Same(composition, world.Composition),
            composition
        );
        using var later = project.CreateEntity(definition);

        Assert.True(project.FindSubsystem<ProbeSubsystem>(true)!.Loaded);
        Assert.True(Assert.Single(project.EntityKeys).FindComponent<ProbeComponent>(true)!.Loaded);
        Assert.True(later.FindComponent<ProbeComponent>(true)!.Loaded);
        Assert.Same(composition, project.Composition);
    }

    [Fact]
    public void DefaultCompositionIncludesAllMembers()
    {
        var (database, template) = ProjectTestDatabase.Create();
        AddMember(database.MemberSubsystemTemplateType, template, "Subsystem", typeof(ProbeSubsystem).FullName);
        using var project = new Project(database, new ProjectData(database, template, null));
        Assert.Null(project.Composition);
        Assert.True(project.FindSubsystem<ProbeSubsystem>(true)!.Loaded);
    }

    private static void AddMember(DatabaseObjectType type, DatabaseObject parent, string name, string? className)
    {
        var member = new DatabaseObject(type, name) { NestingParent = parent };
        member.SetNestedValue("IsOptional", false);
        member.SetNestedValue("LoadOrder", 0);
        if (className != null)
        {
            member.SetNestedValue("Class", className);
        }
    }

    private sealed class SelectedComposition : IProjectComposition
    {
        public bool ShouldCreateSubsystem(ValuesDictionary definition) =>
            !definition.DatabaseObject.Name.StartsWith("Excluded", StringComparison.Ordinal);

        public bool ShouldCreateComponent(ValuesDictionary definition) =>
            !definition.DatabaseObject.Name.StartsWith("Excluded", StringComparison.Ordinal);
    }

    public sealed class ProbeSubsystem : Subsystem
    {
        public bool Loaded { get; private set; }

        public override void Load(ValuesDictionary valuesDictionary) => Loaded = true;
    }

    public sealed class ProbeComponent : Component
    {
        public bool Loaded { get; private set; }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) => Loaded = true;
    }

    public sealed class ThrowingSubsystem : Subsystem
    {
        public ThrowingSubsystem() => throw new InvalidOperationException("Must not construct excluded subsystem.");
    }

    public sealed class ThrowingComponent : Component
    {
        public ThrowingComponent() => throw new InvalidOperationException("Must not construct excluded component.");
    }
}
