using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSoundMaterials : Subsystem
{
    private ValuesDictionary _footstepSoundsValuesDictionary = null!;

    private ValuesDictionary _impactsSoundsValuesDictionary = null!;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public void PlayImpactSound(int value, Vector3 position, float loudnessMultiplier)
    {
        var num = Terrain.ExtractContents(value);
        var soundMaterialName = BlocksManager.Blocks[num].GetSoundMaterialName(_subsystemTerrain, value);
        if (string.IsNullOrEmpty(soundMaterialName))
        {
            return;
        }

        var value2 = _impactsSoundsValuesDictionary.GetValue(soundMaterialName, string.Empty);
        if (string.IsNullOrEmpty(value2))
        {
            return;
        }

        var pitch = _random.Float(-0.2f, 0.2f);
        _subsystemAudio.PlayRandomSound(value2, 0.5f * loudnessMultiplier, pitch, position,
            5f * loudnessMultiplier, true);
    }

    public bool PlayFootstepSound(ComponentCreature componentCreature, float loudnessMultiplier)
    {
        var footstepSoundMaterialName = GetFootstepSoundMaterialName(componentCreature);
        if (string.IsNullOrEmpty(footstepSoundMaterialName))
        {
            return false;
        }

        var value = componentCreature.ComponentCreatureSounds.ValuesDictionary
            .GetValue<ValuesDictionary>("CustomFootstepSounds").GetValue(footstepSoundMaterialName, string.Empty);
        if (string.IsNullOrEmpty(value))
        {
            value = _footstepSoundsValuesDictionary.GetValue(footstepSoundMaterialName, string.Empty);
        }

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var pitch = _random.Float(-0.2f, 0.2f);
        _subsystemAudio.PlayRandomSound(value, 0.75f * loudnessMultiplier, pitch,
            componentCreature.ComponentBody.Position, 2f * loudnessMultiplier, true);
        if (componentCreature is not ComponentPlayer
            {
                ComponentVitalStats.Wetness: > 0f
            }
                componentPlayer)
        {
            return true;
        }

        var value2 = _footstepSoundsValuesDictionary.GetValue("Squishy", string.Empty);
        if (string.IsNullOrEmpty(value2))
        {
            return true;
        }

        var volume = 0.7f * loudnessMultiplier *
                     MathUtils.Pow(componentPlayer.ComponentVitalStats.Wetness, 4f);
        _subsystemAudio.PlayRandomSound(value2, volume, pitch,
            componentCreature.ComponentBody.Position, 2f * loudnessMultiplier, true);

        return true;
    }

    public string GetFootstepSoundMaterialName(ComponentCreature componentCreature)
    {
        var position = componentCreature.ComponentBody.Position;
        if (componentCreature.ComponentBody is
            { ImmersionDepth: > 0.2f, ImmersionFluidBlock: WaterBlock })
        {
            return "Water";
        }

        if (componentCreature.ComponentLocomotion.LadderValue.HasValue)
        {
            return Terrain.ExtractContents(componentCreature.ComponentLocomotion.LadderValue.Value) == 59
                ? "WoodenLadder"
                : "MetalLadder";
        }

        var cellValue = _subsystemTerrain.Terrain.GetCellValue(Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y + 0.1f), Terrain.ToCell(position.Z));
        var num = Terrain.ExtractContents(cellValue);
        var soundMaterialName = BlocksManager.Blocks[num].GetSoundMaterialName(_subsystemTerrain, cellValue);
        if (string.IsNullOrEmpty(soundMaterialName) && componentCreature.ComponentBody.StandingOnValue.HasValue)
        {
            soundMaterialName = BlocksManager
                .Blocks[Terrain.ExtractContents(componentCreature.ComponentBody.StandingOnValue.Value)]
                .GetSoundMaterialName(_subsystemTerrain, componentCreature.ComponentBody.StandingOnValue.Value);
        }

        return !string.IsNullOrEmpty(soundMaterialName) ? soundMaterialName : string.Empty;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _impactsSoundsValuesDictionary = valuesDictionary.GetValue<ValuesDictionary>("ImpactSounds");
        _footstepSoundsValuesDictionary = valuesDictionary.GetValue<ValuesDictionary>("FootstepSounds");
    }
}
