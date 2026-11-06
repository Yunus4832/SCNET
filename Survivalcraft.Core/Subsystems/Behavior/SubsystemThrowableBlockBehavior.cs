using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemThrowableBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemProjectiles _subsystemProjectiles = null!;

    public event Action<WeaponAimRequest>? AimPresentationRequested;

    public event Action<WeaponSoundRequest>? SoundRequested;

    public override ResourceId[] HandledBlocks => [];

    public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
    {
        switch (state)
        {
            case AimState.InProgress:
            {
                componentMiner.ComponentCreature.ComponentCreaturePose.AimHandAngleOrder = 3.2f;
                var block2 = WorldContentBinding.For(Project).Catalog
                    .Blocks[Terrain.ExtractContents(componentMiner.ActiveBlockValue)];
                AimPresentationRequested?.Invoke(
                    new WeaponAimRequest(
                        componentMiner,
                        aim,
                        new Vector3(0f, 0.35f, 0.17f),
                        block2 is SpearBlock ? new Vector3(-1.5f, 0f, 0f) : null
                    )
                );

                if (block2 is SpearBlock)
                {
                    componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemOffsetOrder =
                        new Vector3(0f, -0.25f, 0f);
                    componentMiner.ComponentCreature.ComponentCreaturePose.InHandItemRotationOrder =
                        new Vector3(3.14159f, 0f, 0f);
                }

                break;
            }
            case AimState.Completed:
            {
                var vector = componentMiner.ComponentCreature.ComponentCreaturePose.EyePosition +
                             componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.4f;
                var v = Vector3.Normalize(vector + aim.Direction * 10f - vector);
                var activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
                var slotValue = componentMiner.Inventory.GetSlotValue(activeSlotIndex);
                var slotCount = componentMiner.Inventory.GetSlotCount(activeSlotIndex);
                var num = Terrain.ExtractContents(slotValue);
                var block = WorldContentBinding.For(Project).Catalog.Blocks[num];
                if (slotCount <= 0)
                {
                    return true;
                }

                var num2 = block.ProjectileSpeed;
                if (componentMiner.ComponentCharacter != null)
                {
                    num2 *= 0.5f * (componentMiner.ComponentCharacter.ComponentLevel.StrengthFactor - 1f) + 1f;
                }

                if (_subsystemProjectiles.FireProjectile(
                        slotValue,
                        vector,
                        v * num2,
                        _random.Vector3(5f, 10f),
                        componentMiner.ComponentCreature
                    ) ==
                    null)
                {
                    return true;
                }

                if (WorldRuntimeContext.For(Project).IsAuthority)
                {
                    componentMiner.Inventory.RemoveSlotItems(activeSlotIndex, 1);
                }

                RequestSound(
                    "Audio/Throw",
                    _random.Float(0.2f, 0.3f),
                    _random.Float(-0.2f, 0.2f),
                    aim.Position,
                    2f,
                    true
                );
                componentMiner.Poke(false);

                return true;
            }
        }

        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        base.Load(valuesDictionary);
    }

    private void RequestSound(
        string name,
        float volume,
        float pitch,
        Vector3 position,
        float minDistance,
        bool autoDelay
    )
    {
        var request = new WeaponSoundRequest(name, volume, pitch, position, minDistance, autoDelay ? null : 0f);
        SoundRequested?.Invoke(request);
    }

    public override void Dispose()
    {
        AimPresentationRequested = null;
        SoundRequested = null;
        base.Dispose();
    }
}
