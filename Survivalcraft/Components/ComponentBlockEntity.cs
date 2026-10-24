using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBlockEntity : Component
{
    private SubsystemCharacters _subPlayers = null!;

    public Point3 Coordinates { get; set; }

    public Guid Owner { get; set; }

    public CharacterData? OwnCharacterData => _subPlayers.Characters.Find(x => x.CharacterId == Owner);

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subPlayers = Project.FindSubsystem<SubsystemCharacters>(true)!;
        Coordinates = valuesDictionary.GetValue("Coordinates", Coordinates);
        Owner = valuesDictionary.GetValue("Owner", Owner);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Coordinates", Coordinates);
        valuesDictionary.SetValue("Owner", Owner);
    }
}
