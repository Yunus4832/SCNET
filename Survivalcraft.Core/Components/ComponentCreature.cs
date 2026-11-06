using Engine.Core.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreature : Component
{
    private string[] _killVerbs = [];

    protected SubsystemCharacterStats subsystemCharacterStats = null!;

    public ComponentBody ComponentBody { get; set; } = null!;

    public ComponentHealth ComponentHealth { get; set; } = null!;

    public ComponentSpawn ComponentSpawn { get; set; } = null!;

    public ComponentCreaturePose ComponentCreaturePose { get; set; } = null!;

    public ComponentCreatureSounds ComponentCreatureSounds { get; set; } = null!;

    public ComponentLocomotion ComponentLocomotion { get; set; } = null!;

    public virtual CharacterStats? CharacterStats => null;

    public bool ConstantSpawn { get; set; }

    public CreatureCategory Category { get; set; }

    public LocalizedText DisplayName { get; set; } = LocalizedText.Empty;

    public ReadOnlyList<string> KillVerbs => new(_killVerbs);

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        ComponentBody = Entity.FindComponent<ComponentBody>(true)!;
        ComponentHealth = Entity.FindComponent<ComponentHealth>(true)!;
        ComponentSpawn = Entity.FindComponent<ComponentSpawn>(true)!;
        ComponentCreatureSounds = Entity.FindComponent<ComponentCreatureSounds>(true)!;
        ComponentCreaturePose = Entity.FindComponent<ComponentCreaturePose>(true)!;
        ComponentLocomotion = Entity.FindComponent<ComponentLocomotion>(true)!;
        subsystemCharacterStats = Project.FindSubsystem<SubsystemCharacterStats>(true)!;
        ConstantSpawn = valuesDictionary.GetValue<bool>("ConstantSpawn");
        Category = valuesDictionary.GetValue<CreatureCategory>("Category");
        var displayName = valuesDictionary.GetValue<string>("DisplayName");
        DisplayName = LocalizedText.Literal(displayName);
        if (displayName.StartsWith('[') && displayName.EndsWith(']'))
        {
            var lp = displayName.Substring(1, displayName.Length - 2)
                .Split([":"], StringSplitOptions.RemoveEmptyEntries);
            DisplayName = LocalizedText.Database("DisplayName", lp[1], lp[1]);
        }

        _killVerbs =
            HumanReadableConverter.ValuesListFromString<string>(',', valuesDictionary.GetValue<string>("KillVerbs"));
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
