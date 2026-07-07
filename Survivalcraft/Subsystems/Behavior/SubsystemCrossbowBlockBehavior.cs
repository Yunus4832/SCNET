using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemCrossbowBlockBehavior : SubsystemBlockBehavior
{
    private const string _typeName = "SubsystemCrossbowBlockBehavior";

    private readonly Dictionary<ComponentMiner, double> _aimStartTimes = new();

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly ArrowBlock.ArrowType[] _supportedArrowTypes =
    [
        ArrowBlock.ArrowType.IronBolt,
        ArrowBlock.ArrowType.DiamondBolt,
        ArrowBlock.ArrowType.ExplosiveBolt
    ];

    public override int[] HandledBlocks => [];

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        componentPlayer.ComponentGui.ModalPanelWidget = componentPlayer.ComponentGui.ModalPanelWidget == null
            ? new CrossbowWidget(inventory, slotIndex)
            : null;
        return true;
    }

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        var inventory = componentMiner.Inventory;
        var activeSlotIndex = inventory.ActiveSlotIndex;
        if (activeSlotIndex < 0)
        {
            return false;
        }

        var slotValue = inventory.GetSlotValue(activeSlotIndex);
        var slotCount = inventory.GetSlotCount(activeSlotIndex);
        var num = Terrain.ExtractContents(slotValue);
        var data = Terrain.ExtractData(slotValue);
        if (num != 200 || slotCount <= 0)
        {
            return false;
        }

        var draw = CrossbowBlock.GetDraw(data);
        if (!_aimStartTimes.TryGetValue(componentMiner, out var value))
        {
            value = _subsystemTime.GameTime;
            _aimStartTimes[componentMiner] = value;
        }

        var num2 = (float)(_subsystemTime.GameTime - value);
        var num3 = (float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0);
        var v = ((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.01f : 0.03f) +
                 0.15f * MathUtils.Saturate((num2 - 2.5f) / 6f)) * new Vector3
        {
            X = SimplexNoise.OctavedNoise(num3, 2f, 3, 2f, 0.5f),
            Y = SimplexNoise.OctavedNoise(num3 + 100f, 2f, 3, 2f, 0.5f),
            Z = SimplexNoise.OctavedNoise(num3 + 200f, 2f, 3, 2f, 0.5f)
        };
        aim.Direction = Vector3.Normalize(aim.Direction + v);
        switch (state)
        {
            case AimState.InProgress:
            {
                if (num2 >= 10f)
                {
                    componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
                    return true;
                }

                var componentFirstPersonModel =
                    componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
                if (componentFirstPersonModel != null)
                {
                    componentMiner.ComponentPlayer?.ComponentAimingSights.ShowAimingSights(aim.Position,
                        aim.Direction);
                    componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.22f, 0.15f, 0.1f);
                    componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
                }

                componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.3f;
                componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder =
                    new Vector3(-0.08f, -0.1f, 0.07f);
                componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder =
                    new Vector3(-1.55f, 0f, 0f);
                break;
            }
            case AimState.Cancelled:
                _aimStartTimes.Remove(componentMiner);
                break;
            case AimState.Completed:
            {
                var arrowType = CrossbowBlock.GetArrowType(data);
                if (draw != 15)
                {
                    componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                        LanguageManager.Get(_typeName, 0), Color.White, true, false);
                }
                else if (!arrowType.HasValue)
                {
                    componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                        LanguageManager.Get(_typeName, 1), Color.White, true, false);
                }
                else
                {
                    var vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
                                 componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
                                 componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
                    var v2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
                    var value2 = Terrain.MakeBlockValue(192, 0,
                        ArrowBlock.SetArrowType(0, arrowType.Value));
                    var s = 38f;
                    if (_subsystemProjectiles.FireProjectile(value2, vector, s * v2, Vector3.Zero,
                            componentMiner.ComponentCreature) != null)
                    {
                        data = CrossbowBlock.SetArrowType(data, null);
                        _subsystemAudio.PlaySound("Audio/Bow", 1f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);
                    }
                }

                if (CommonLib.WorkType != WorkType.Client)
                {
                    inventory.RemoveSlotItems(activeSlotIndex, 1);
                    var value3 = Terrain.MakeBlockValue(num, 0, CrossbowBlock.SetDraw(data, 0));
                    inventory.AddSlotItems(activeSlotIndex, value3, 1);
                    componentMiner.DamageActiveTool(1);
                }

                if (draw > 0)
                {
                    _subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, _random.Float(-0.1f, 0.1f),
                        componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
                }

                _aimStartTimes.Remove(componentMiner);
                return true;
            }
        }

        return false;
    }

    public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
    {
        var num = Terrain.ExtractContents(value);
        var arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
        if (num != 192 || !_supportedArrowTypes.Contains(arrowType))
        {
            return 0;
        }

        var data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
        var arrowType2 = CrossbowBlock.GetArrowType(data);
        var draw = CrossbowBlock.GetDraw(data);
        if (!arrowType2.HasValue && draw == 15)
        {
            return 1;
        }

        return 0;
    }

    public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count,
        int processCount, out int processedValue, out int processedCount)
    {
        if (processCount == 1)
        {
            var arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
            var data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
            processedValue = 0;
            processedCount = 0;
            inventory.RemoveSlotItems(slotIndex, 1);
            inventory.AddSlotItems(slotIndex,
                Terrain.MakeBlockValue(200, 0, CrossbowBlock.SetArrowType(data, arrowType)), 1);
        }
        else
        {
            processedValue = value;
            processedCount = count;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        base.Load(valuesDictionary);
    }
}
