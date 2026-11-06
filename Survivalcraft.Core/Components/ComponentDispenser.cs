using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentDispenser : ComponentInventoryBase
{
    private ComponentBlockEntity _componentBlockEntity = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public void Dispense()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var coordinates = _componentBlockEntity.Coordinates;
        var data = Terrain.ExtractData(
            _subsystemTerrain.Terrain.GetCellValue(coordinates.X, coordinates.Y, coordinates.Z)
        );
        var direction = DispenserBlock.GetDirection(data);
        var mode = DispenserBlock.GetMode(data);
        var num = 0;
        int slotValue;
        while (true)
        {
            if (num >= SlotsCount)
            {
                return;
            }

            slotValue = GetSlotValue(num);
            var slotCount = GetSlotCount(num);
            if (slotValue != 0 && slotCount > 0)
            {
                break;
            }

            num++;
        }

        var removed = RemoveSlotItems(num, 1);
        for (var i = 0; i < removed; i++) { DispenseItem(coordinates, direction, slotValue, mode); }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _componentBlockEntity = Entity.FindComponent<ComponentBlockEntity>(true)!;
    }

    public void DispenseItem(Point3 point, int face, int value, DispenserBlock.Mode mode)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        while (true)
        {
            var vector = CellFace.FaceToVector3(face);
            var position = new Vector3(point.X + 0.5f, point.Y + 0.5f, point.Z + 0.5f) + 0.6f * vector;
            if (mode == DispenserBlock.Mode.Dispense)
            {
                const float s = 1.8f;
                _subsystemPickables.AddPickable(value, 1, position, s * (vector + sharedRandom.Vector3(0.2f)), null);
                PublishSound(position, false);
                return;
            }

            var s2 = sharedRandom.Float(39f, 41f);
            if (_subsystemProjectiles.FireProjectile(
                    value,
                    position,
                    s2 * (vector + sharedRandom.Vector3(0.025f) + new Vector3(0f, 0.05f, 0f)),
                    Vector3.Zero,
                    _componentBlockEntity.OwnCharacterData?.ComponentCharacter
                ) !=
                null)
            {
                PublishSound(position, true);
            }
            else
            {
                mode = DispenserBlock.Mode.Dispense;
                continue;
            }

            break;
        }
    }

    private void PublishSound(Vector3 position, bool shot)
    {
        NetworkSender.SendNearPoint(
            Project,
            position,
            24f,
            new DispenserSoundPackage { Position = position, Shot = shot }
        );
    }
}
