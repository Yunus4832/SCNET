using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemSpikesBlockBehavior : SubsystemBlockBehavior
{
    private readonly Dictionary<ComponentCreature, double> _lastInjuryTimes = new();

    private SubsystemSpatialSoundRequests _soundRequests = null!;

    private SubsystemTime _subsystemTime = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:spiked_plank")];

    public bool RetractExtendSpikes(int x, int y, int z, bool extend)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[num] is not SpikedPlankBlock)
        {
            return false;
        }

        var data = SpikedPlankBlock.SetSpikesState(Terrain.ExtractData(cellValue), extend);
        var value = Terrain.ReplaceData(cellValue, data);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        _soundRequests.RequestNearestSound("game:spikes", "Audio/Spikes", .7f, .1f, new Vector3(x, y, z), 4f, true);

        return true;
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        var data = Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
        if (!SpikedPlankBlock.GetSpikesState(data))
        {
            return;
        }

        var mountingFace = SpikedPlankBlock.GetMountingFace(data);
        if (cellFace.Face != mountingFace)
        {
            return;
        }

        var componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
        if (componentCreature == null)
        {
            return;
        }

        _lastInjuryTimes.TryGetValue(componentCreature, out var value);
        if (!(_subsystemTime.GameTime - value > 1.0))
        {
            return;
        }

        _lastInjuryTimes[componentCreature] = _subsystemTime.GameTime;
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            componentCreature.ComponentHealth.Injure(0.1f, null, false, new LocalizedText(GetType().Name, "0", "0"));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
    }

    public override void OnEntityRemoved(Entity entity)
    {
        var componentCreature = entity.FindComponent<ComponentCreature>();
        if (componentCreature != null)
        {
            _lastInjuryTimes.Remove(componentCreature);
        }
    }
}
