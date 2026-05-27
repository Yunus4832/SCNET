using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public class ComponentLoot : Component, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private bool _lootDropped;

    private List<Loot> _lootList = [];

    private List<Loot> _lootOnFireList = [];

    private readonly Random _random = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPickables _subsystemPickables = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_lootDropped ||
            !_componentCreature.ComponentHealth.DeathTime.HasValue ||
            !(_subsystemGameInfo.TotalElapsedGameTime >= _componentCreature.ComponentHealth.DeathTime.Value +
                _componentCreature.ComponentHealth.CorpseDuration))
        {
            return;
        }

        var num = _componentCreature.Entity.FindComponent<ComponentOnFire>()?.IsOnFire ?? false;
        _lootDropped = true;

        //客户端禁止掉落
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        foreach (var item in num ? _lootOnFireList : _lootList)
        {
            if (_random.Float(0f, 1f) < item.Probability)
            {
                var num2 = _random.Int(item.MinCount, item.MaxCount);
                for (var i = 0; i < num2; i++)
                {
                    var position = (_componentCreature.ComponentBody.BoundingBox.Min +
                                    _componentCreature.ComponentBody.BoundingBox.Max) / 2f;
                    _subsystemPickables.AddPickable(item.Value, 1, position, null, null);
                }
            }
        }
    }

    public static List<Loot> ParseLootList(ValuesDictionary lootVd)
    {
        var list = (from string value in lootVd.Values select ParseLoot(value)).ToList();
        list.Sort((l1, l2) => l1.Value - l2.Value);
        return list;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _lootDropped = valuesDictionary.GetValue<bool>("LootDropped");
        _lootList = ParseLootList(valuesDictionary.GetValue<ValuesDictionary>("Loot"));
        _lootOnFireList = ParseLootList(valuesDictionary.GetValue<ValuesDictionary>("LootOnFire"));
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("LootDropped", _lootDropped);
    }

    public static Loot ParseLoot(string lootString)
    {
        var array = lootString.Split([";"], StringSplitOptions.None);
        if (array.Length < 3)
        {
            throw new InvalidOperationException("Invalid loot string.");
        }

        var v = CraftingRecipesManager.DecodeResult(array[0]);
        Loot result = default;
        result.Value = v;
        result.MinCount = int.Parse(array[1], CultureInfo.InvariantCulture);
        result.MaxCount = int.Parse(array[2], CultureInfo.InvariantCulture);
        result.Probability = array.Length >= 4 ? float.Parse(array[3], CultureInfo.InvariantCulture) : 1f;
        return result;
    }

    public struct Loot
    {
        public int Value;

        public int MinCount;

        public int MaxCount;

        public float Probability;
    }
}
