using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemRotBlockBehavior : SubsystemPollableBlockBehavior
{
    public const int MaxRot = 1;

    public const float RotPeriod = 60f;

    private bool _isRotEnabled;

    private double _lastRotTime;

    private int _rotStep;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemItemsScanner _subsystemItemsScanner = null!;

    public override ResourceId[] HandledBlocks => [];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemItemsScanner = Project.FindSubsystem<SubsystemItemsScanner>(true)!;
        _lastRotTime = valuesDictionary.GetValue<double>("LastRotTime");
        _rotStep = valuesDictionary.GetValue<int>("RotStep");
        _subsystemItemsScanner.ItemsScanned += ItemsScanned;
        _isRotEnabled = _subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
                        _subsystemGameInfo.WorldSettings.GameMode != GameMode.Adventure;
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        valuesDictionary.SetValue("LastRotTime", _lastRotTime);
        valuesDictionary.SetValue("RotStep", _rotStep);
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        if (!_isRotEnabled || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var contents = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
        var rotPeriod = block.GetRotPeriod(value);
        if (rotPeriod <= 0 || pollPass % rotPeriod != 0)
        {
            return;
        }

        var newDamage = block.GetDamage(value) + 1;
        value = newDamage > 1 ? block.GetDamageDestructionValue(value) : block.SetDamage(value, newDamage);
        SubsystemTerrain.ChangeCell(x, y, z, value);
    }

    public void ItemsScanned(ReadOnlyList<ScannedItemData> items)
    {
        var elapsedPeriods = (int)((_subsystemGameInfo.TotalElapsedGameTime - _lastRotTime) / RotPeriod);
        if (elapsedPeriods <= 0)
        {
            return;
        }

        if (_isRotEnabled && WorldRuntimeContext.For(Project).IsAuthority)
        {
            foreach (var item in items)
            {
                var contents = Terrain.ExtractContents(item.Value);
                var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
                var rotPeriod = block.GetRotPeriod(item.Value);
                if (rotPeriod <= 0)
                {
                    continue;
                }

                var currentDamage = block.GetDamage(item.Value);
                for (var i = 0; i < elapsedPeriods; i++)
                {
                    if (currentDamage > 1)
                    {
                        break;
                    }

                    if ((i + _rotStep) % rotPeriod == 0)
                    {
                        currentDamage++;
                    }
                }

                _subsystemItemsScanner.TryModifyItem(
                    item,
                    currentDamage <= 1
                        ? block.SetDamage(item.Value, currentDamage)
                        : block.GetDamageDestructionValue(item.Value)
                );
            }
        }

        _rotStep += elapsedPeriods;
        _lastRotTime += elapsedPeriods * 60f;
    }
}
