using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemRotBlockBehavior : SubsystemPollableBlockBehavior
{
    public const int MaxRot = 1;

    public const float RotPeriod = 60f;

    private bool _isRotEnabled;

    private double _lastRotTime;

    private int _rotStep;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemItemsScanner _subsystemItemsScanner = null!;

    public override int[] HandledBlocks => [];

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
        if (!_isRotEnabled || CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        var rotPeriod = block.GetRotPeriod(value);
        if (rotPeriod <= 0 || pollPass % rotPeriod != 0)
        {
            return;
        }

        var num2 = block.GetDamage(value) + 1;
        value = num2 > 1 ? block.GetDamageDestructionValue(value) : block.SetDamage(value, num2);
        SubsystemTerrain.ChangeCell(x, y, z, value);
    }

    public void ItemsScanned(ReadOnlyList<ScannedItemData> items)
    {
        var num = (int)((_subsystemGameInfo.TotalElapsedGameTime - _lastRotTime) / RotPeriod);
        if (num <= 0)
        {
            return;
        }

        if (_isRotEnabled && CommonLib.WorkType != WorkType.Client)
        {
            foreach (var item in items)
            {
                var num2 = Terrain.ExtractContents(item.Value);
                var block = BlocksManager.Blocks[num2];
                var rotPeriod = block.GetRotPeriod(item.Value);
                if (rotPeriod <= 0)
                {
                    continue;
                }

                var num3 = block.GetDamage(item.Value);
                for (var i = 0; i < num; i++)
                {
                    if (num3 > 1)
                    {
                        break;
                    }

                    if ((i + _rotStep) % rotPeriod == 0)
                    {
                        num3++;
                    }
                }

                _subsystemItemsScanner.TryModifyItem(
                    item,
                    num3 <= 1
                        ? block.SetDamage(item.Value, num3)
                        : block.GetDamageDestructionValue(item.Value)
                );
            }
        }

        _rotStep += num;
        _lastRotTime += num * 60f;
    }
}
