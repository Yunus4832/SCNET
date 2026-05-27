using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemThrowableBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    public override int[] HandledBlocks => [];

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        switch (state)
        {
            case AimState.InProgress:
            {
                componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 3.2f;
                var block2 = BlocksManager.Blocks[Terrain.ExtractContents(componentMiner.ActiveBlockValue)];
                var componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
                if (componentFirstPersonModel != null)
                {
                    componentMiner.ComponentPlayer?.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
                    componentFirstPersonModel.ItemOffsetOrder = new Vector3(0f, 0.35f, 0.17f);
                    if (block2 is SpearBlock)
                    {
                        componentFirstPersonModel.ItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
                    }
                }

                if (block2 is SpearBlock)
                {
                    componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder =
                        new Vector3(0f, -0.25f, 0f);
                    componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder =
                        new Vector3(3.14159f, 0f, 0f);
                }

                break;
            }
            case AimState.Completed:
            {
                var vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
                             componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.4f;
                var v = Vector3.Normalize(vector + aim.Direction * 10f - vector);
                var activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
                var slotValue = componentMiner.Inventory.GetSlotValue(activeSlotIndex);
                var slotCount = componentMiner.Inventory.GetSlotCount(activeSlotIndex);
                var num = Terrain.ExtractContents(slotValue);
                var block = BlocksManager.Blocks[num];
                if (slotCount <= 0)
                {
                    return true;
                }

                var num2 = block.ProjectileSpeed;
                if (componentMiner.ComponentPlayer != null)
                {
                    num2 *= 0.5f * (componentMiner.ComponentPlayer.ComponentLevel.StrengthFactor - 1f) + 1f;
                }

                if (_subsystemProjectiles.FireProjectile(slotValue, vector, v * num2, _random.Vector3(5f, 10f),
                        componentMiner.ComponentCreature) == null)
                {
                    return true;
                }

                if (CommonLib.WorkType != WorkType.Client)
                {
                    componentMiner.Inventory.RemoveSlotItems(activeSlotIndex, 1);
                }

                _subsystemAudio.PlaySound("Audio/Throw", _random.Float(0.2f, 0.3f),
                    _random.Float(-0.2f, 0.2f), aim.Position, 2f, true);
                componentMiner.Poke(false);

                return true;
            }
        }

        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        base.Load(valuesDictionary);
    }
}
