using Engine.Serialization;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreature : Component
{
    private string[] _killVerbs = [];

    protected SubsystemPlayerStats subsystemPlayerStats = null!;

    public ComponentBody ComponentBody { get; set; } = null!;

    public ComponentHealth ComponentHealth { get; set; } = null!;

    public ComponentSpawn ComponentSpawn { get; set; } = null!;

    public ComponentCreatureModel ComponentCreatureModel { get; set; } = null!;

    public ComponentCreatureSounds ComponentCreatureSounds { get; set; } = null!;

    public ComponentLocomotion ComponentLocomotion { get; set; } = null!;

    public virtual PlayerStats? PlayerStats => null;

    public bool ConstantSpawn { get; set; }

    public CreatureCategory Category { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ReadOnlyList<string> KillVerbs => new(_killVerbs);

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        ComponentBody = Entity.FindComponent<ComponentBody>(true)!;
        ComponentHealth = Entity.FindComponent<ComponentHealth>(true)!;
        ComponentSpawn = Entity.FindComponent<ComponentSpawn>(true)!;
        ComponentCreatureSounds = Entity.FindComponent<ComponentCreatureSounds>(true)!;
        ComponentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true)!;
        ComponentLocomotion = Entity.FindComponent<ComponentLocomotion>(true)!;
        subsystemPlayerStats = Project.FindSubsystem<SubsystemPlayerStats>(true)!;
        ConstantSpawn = valuesDictionary.GetValue<bool>("ConstantSpawn");
        Category = valuesDictionary.GetValue<CreatureCategory>("Category");
        DisplayName = valuesDictionary.GetValue<string>("DisplayName");
        if (DisplayName.StartsWith('[') && DisplayName.EndsWith(']'))
        {
            var lp = DisplayName.Substring(1, DisplayName.Length - 2)
                .Split([":"], StringSplitOptions.RemoveEmptyEntries);
            DisplayName = LanguageControl.GetDatabase("DisplayName", lp[1]);
        }

        _killVerbs = HumanReadableConverter.ValuesListFromString<string>(',', valuesDictionary.GetValue<string>("KillVerbs"));
        if (_killVerbs.Length == 0)
        {
            throw new InvalidOperationException("Must have at least one KillVerb");
        }

        if (!MathUtils.IsPowerOf2((long)Category))
        {
            throw new InvalidOperationException("A single category must be assigned for creature.");
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("ConstantSpawn", ConstantSpawn);
    }
}
