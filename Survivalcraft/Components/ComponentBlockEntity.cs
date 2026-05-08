using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBlockEntity : Component
{
    private SubsystemPlayers _subPlayers = null!;

    public Point3 Coordinates { get; set; }

    public Guid Owner { get; set; }

    public PlayerData? OwnPlayerData => _subPlayers.PlayersData.Find(x => x.PlayerGUID == Owner);

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        Coordinates = valuesDictionary.GetValue("Coordinates", Coordinates);
        Owner = valuesDictionary.GetValue("Owner", Owner);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Coordinates", Coordinates);
        valuesDictionary.SetValue("Owner", Owner);
    }
}
