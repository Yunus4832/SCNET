using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBlockBehaviors : Subsystem
{
    private const string _typeName = nameof(SubsystemBlockBehaviors);

    private readonly List<SubsystemBlockBehavior> _blockBehaviors = [];

    private SubsystemBlockBehavior[][] _blockBehaviorsByContents = [];

    public ReadOnlyList<SubsystemBlockBehavior> BlockBehaviors => new(_blockBehaviors);

    public SubsystemBlockBehavior[] GetBlockBehaviors(int contents, ComponentMiner? miner = null, Point3? point = null)
    {
        if (!point.HasValue ||
            !SubsystemTerritoryBlockBehavior.CheckIsInTerritoriy(
                point.Value.X,
                point.Value.Z,
                out Territoriy? territoriy) ||
            miner != null && SubsystemTerritoryBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy!) ||
            territoriy!.AllowBlockBehavior)
        {
            return _blockBehaviorsByContents[contents];
        }

        miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
            LanguageManager.Get(_typeName, 1),
            Color.Yellow,
            false,
            true
        );
        return [];
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _blockBehaviorsByContents = new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
        var dictionary = new Dictionary<int, List<SubsystemBlockBehavior>>();
        for (var i = 0; i < _blockBehaviorsByContents.Length; i++)
        {
            dictionary[i] = [];
            var array = BlocksManager.Blocks[i].Behaviors.Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var text in array)
            {
                var item = Project.FindSubsystem<SubsystemBlockBehavior>(text.Trim(), true)!;
                dictionary[i].Add(item);
            }
        }

        foreach (var item2 in Project.FindSubsystems<SubsystemBlockBehavior>())
        {
            _blockBehaviors.Add(item2);
            var handledBlocks = item2.HandledBlocks;
            foreach (var key in handledBlocks)
            {
                dictionary[key].Add(item2);
            }
        }

        for (var k = 0; k < _blockBehaviorsByContents.Length; k++)
        {
            _blockBehaviorsByContents[k] = dictionary[k].ToArray();
        }
    }
}
