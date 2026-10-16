using EntitySystem.TemplatesDatabase;


namespace Game.Subsystems;

public class SubsystemElectricBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemElectricity _subsystemElectricity = null!;

    public override ResourceId[] HandledBlocks =>
    [ResourceId.Parse("game:wire"), ResourceId.Parse("game:not_gate"), ResourceId.Parse("game:and_gate"), ResourceId.Parse("game:or_gate"), ResourceId.Parse("game:xor_gate"), ResourceId.Parse("game:nand_gate"), ResourceId.Parse("game:nor_gate"), ResourceId.Parse("game:delay_gate"), ResourceId.Parse("game:adjustable_delay_gate"), ResourceId.Parse("game:sr_latch"), ResourceId.Parse("game:random_generator"), ResourceId.Parse("game:digital_to_analog_converter"), ResourceId.Parse("game:analog_to_digital_converter"), ResourceId.Parse("game:sound_generator"), ResourceId.Parse("game:battery"), ResourceId.Parse("game:lightbulb"), ResourceId.Parse("game:switch"), ResourceId.Parse("game:button"), ResourceId.Parse("game:counter"), ResourceId.Parse("game:real_time_clock"), ResourceId.Parse("game:memory_bank"), ResourceId.Parse("game:truth_table_circuit"), ResourceId.Parse("game:pressure_plate"), ResourceId.Parse("game:photodiode"), ResourceId.Parse("game:motion_detector"), ResourceId.Parse("game:led"), ResourceId.Parse("game:multicolored_led"), ResourceId.Parse("game:one_led"), ResourceId.Parse("game:four_led"), ResourceId.Parse("game:seven_segment_display"), ResourceId.Parse("game:wooden_door"), ResourceId.Parse("game:iron_door"), ResourceId.Parse("game:cell_door"), ResourceId.Parse("game:wooden_trapdoor"), ResourceId.Parse("game:cell_trapdoor"), ResourceId.Parse("game:wooden_fence_gate"), ResourceId.Parse("game:iron_fence_gate"), ResourceId.Parse("game:spiked_plank"), ResourceId.Parse("game:christmas_tree"), ResourceId.Parse("game:wooden_posted_sign"), ResourceId.Parse("game:wooden_attached_sign"), ResourceId.Parse("game:iron_posted_sign"), ResourceId.Parse("game:iron_attached_sign"), ResourceId.Parse("game:small_gunpowder_keg"), ResourceId.Parse("game:medium_gunpowder_keg"), ResourceId.Parse("game:large_gunpowder_keg"), ResourceId.Parse("game:small_incendiary_keg"), ResourceId.Parse("game:medium_incendiary_keg"), ResourceId.Parse("game:large_incendiary_keg"), ResourceId.Parse("game:detonator"), ResourceId.Parse("game:wire_through_planks"), ResourceId.Parse("game:wire_through_stone"), ResourceId.Parse("game:wire_through_bricks"), ResourceId.Parse("game:wire_through_semiconductor"), ResourceId.Parse("game:wire_through_cobblestone"), ResourceId.Parse("game:thermometer"), ResourceId.Parse("game:hygrometer"), ResourceId.Parse("game:target"), ResourceId.Parse("game:dispenser"), ResourceId.Parse("game:furniture"), ResourceId.Parse("game:piston")];

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        _subsystemElectricity.OnElectricElementBlockGenerated(x, y, z);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        _subsystemElectricity.OnElectricElementBlockAdded(x, y, z);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        _subsystemElectricity.OnElectricElementBlockRemoved(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        _subsystemElectricity.OnElectricElementBlockModified(x, y, z);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        _subsystemElectricity.OnChunkDiscarding(chunk);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        for (var i = 0; i < 6; i++)
        {
            _subsystemElectricity.GetElectricElement(x, y, z, i)
                ?.OnNeighborBlockChanged(new CellFace(x, y, z, i), neighborX, neighborY, neighborZ);
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var x = raycastResult.CellFace.X;
        var y = raycastResult.CellFace.Y;
        var z = raycastResult.CellFace.Z;
        for (var i = 0; i < 6; i++)
        {
            var electricElement = _subsystemElectricity.GetElectricElement(x, y, z, i);
            if (electricElement != null)
            {
                return electricElement.OnInteract(raycastResult, componentMiner);
            }
        }

        return false;
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        var x = cellFace.X;
        var y = cellFace.Y;
        var z = cellFace.Z;
        var num = 0;
        ElectricElement? electricElement;
        while (true)
        {
            if (num >= 6)
            {
                return;
            }

            electricElement = _subsystemElectricity.GetElectricElement(x, y, z, num);
            if (electricElement != null)
            {
                break;
            }

            num++;
        }

        electricElement.OnCollide(cellFace, velocity, componentBody);
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        var x = cellFace.X;
        var y = cellFace.Y;
        var z = cellFace.Z;
        var num = 0;
        ElectricElement? electricElement;
        while (true)
        {
            if (num >= 6)
            {
                return;
            }

            electricElement = _subsystemElectricity.GetElectricElement(x, y, z, num);
            if (electricElement != null)
            {
                break;
            }

            num++;
        }

        electricElement.OnHitByProjectile(cellFace, worldItem);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemElectricity = Project.FindSubsystem<SubsystemElectricity>(true)!;
    }
}
