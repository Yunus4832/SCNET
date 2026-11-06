using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding;
using Game.Network;
using Game.Network.Handling;
using Game.World;

namespace Survivalcraft.Test.Fixtures;

/// <summary>A world binding for focused tests that intentionally omit the full subsystem graph.</summary>
internal sealed class RuntimeTestProject : Project
{
    private int _nextEntityTemplate;

    public RuntimeTestProject(RuntimeSide side, WorldRuntimeContext? runtime = null, bool bindRuleHooks = true)
    {
        var names = new[]
        {
            "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
            "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
        };
        var types = names.Select(name =>
            new DatabaseObjectType(name, name, name, 0, name == "Parameter", false, 64, false)
        ).ToArray();
        foreach (var type in types)
        {
            type.InitializeRelations(types, null, types[^1]);
        }

        var root = new DatabaseObject(types[0], "Root");
        GameDatabase = new GameDatabase(new Database(root, types));
        ProjectTemplate = new DatabaseObject(types[1], "TestWorld") { NestingParent = root };
        if (runtime is null)
        {
            var node = new NetNode(
                NetNodeOptions.Default,
                side == RuntimeSide.Client,
                PackageHandlerCatalog.Empty,
                side == RuntimeSide.Authority ? NodeRole.Authority : NodeRole.Client
            );
            runtime = node.Runtime;
        }

        if (runtime.Side != side)
        {
            throw new ArgumentException("Runtime side does not match the test world.", nameof(runtime));
        }

        runtime.BindProject(this);
        if (bindRuleHooks && !WorldRuleHooks.TryGet(runtime, out _)) { BindNoModRules(); }
    }

    public void BindNoModRules() => RuleHookTestEnvironment.BindEmpty(this);

    public void BindRuleHooks(GameplayHooks gameplay, BlockBehaviorHooks blocks) =>
        _ = new WorldRuleHooks(WorldRuntimeContext.For(this), gameplay, blocks);

    public void InitializeComponent(Component component)
    {
        var entityDefinition = new DatabaseObject(GameDatabase.EntityTemplateType, $"TestEntity{_nextEntityTemplate++}")
        {
            NestingParent = ProjectTemplate.NestingParent
        };
        var entityValues = new ValuesDictionary();
        entityValues.PopulateFromDatabaseObject(entityDefinition);
        var entity = new Entity(this, entityValues);
        var componentDefinition = new DatabaseObject(GameDatabase.MemberComponentTemplateType, component.GetType().Name)
        {
            NestingParent = entityDefinition
        };
        var componentValues = new ValuesDictionary();
        componentValues.PopulateFromDatabaseObject(componentDefinition);
        component.Initialize(entity, componentValues);
    }

    public void AddSubsystem(Subsystem subsystem)
    {
        InitializeSubsystem(subsystem);
        subsystems.Add(subsystem);
    }

    public void InitializeSubsystem(Subsystem subsystem)
    {
        var definition = new DatabaseObject(GameDatabase.MemberSubsystemTemplateType, subsystem.GetType().Name)
        {
            NestingParent = ProjectTemplate
        };
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(definition);
        subsystem.Initialize(this, values);
    }
}
