using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Subsystems;

public class SubsystemSpikesBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private static readonly Random _sharedRandom = new();

    private Vector3? _closestSoundToPlay;

    private readonly Dictionary<ComponentCreature, double> _lastInjuryTimes = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks => [86];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!_closestSoundToPlay.HasValue)
        {
            return;
        }

        _subsystemAudio.PlaySound(
            "Audio/Spikes",
            0.7f,
            _sharedRandom.Float(-0.1f, 0.1f),
            _closestSoundToPlay.Value,
            4f,
            true
        );
        _closestSoundToPlay = null;
    }

    public bool RetractExtendSpikes(int x, int y, int z, bool extend)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (BlocksManager.Blocks[num] is not SpikedPlankBlock)
        {
            return false;
        }

        var data = SpikedPlankBlock.SetSpikesState(Terrain.ExtractData(cellValue), extend);
        var value = Terrain.ReplaceData(cellValue, data);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        var vector = new Vector3(x, y, z);
        var num2 = _subsystemAudio.CalculateListenerDistance(vector);
        if (!_closestSoundToPlay.HasValue ||
            num2 < _subsystemAudio.CalculateListenerDistance(_closestSoundToPlay.Value))
        {
            _closestSoundToPlay = vector;
        }

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
        if (CommonLib.WorkType != WorkType.Client)
        {
            componentCreature.ComponentHealth.Injure(0.1f, null, false, LanguageControl.Get(GetType().Name, 0));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
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
