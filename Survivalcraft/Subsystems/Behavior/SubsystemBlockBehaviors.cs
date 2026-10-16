using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Subsystems;

public class SubsystemBlockBehaviors : Subsystem
{
    private readonly List<SubsystemBlockBehavior> _blockBehaviors = [];

    private SubsystemBlockBehavior[][] _blockBehaviorsByContents = [];

    public ReadOnlyList<SubsystemBlockBehavior> BlockBehaviors => new(_blockBehaviors);

    public SubsystemBlockBehavior[] GetBlockBehaviors(int contents) => _blockBehaviorsByContents[contents];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _blockBehaviors.AddRange(Project.FindSubsystems<SubsystemBlockBehavior>());
        _blockBehaviorsByContents = BuildTable(CurrentModRuntime.Value!.RequireWorldCatalog(), _blockBehaviors,
            name => Project.FindSubsystem<SubsystemBlockBehavior>(name, true)!);
    }

    internal static SubsystemBlockBehavior[][] BuildTable(BlockContentCatalog catalog,
        IEnumerable<SubsystemBlockBehavior> behaviors, Func<string, SubsystemBlockBehavior> findByName)
    {
        var table = Enumerable.Range(0, BlockRuntimeCatalog.Capacity)
            .Select(_ => new List<SubsystemBlockBehavior>()).ToArray();
        foreach (var block in catalog.RegisteredBlocks)
        {
            foreach (var name in block.Behaviors.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var behavior = findByName(name.Trim());
                if (!table[block.BlockIndex].Contains(behavior))
                {
                    table[block.BlockIndex].Add(behavior);
                }
            }
        }

        foreach (var behavior in behaviors)
        {
            foreach (var id in behavior.HandledBlocks)
            {
                var index = catalog.GetBlock(id).BlockIndex;
                if (!table[index].Contains(behavior))
                {
                    table[index].Add(behavior);
                }
            }
        }

        return table.Select(list => list.ToArray()).ToArray();
    }
}
