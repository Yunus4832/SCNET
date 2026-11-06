using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Components;

public sealed class ComponentLocomotionPresentation : Component
{
    private ComponentLocomotion _locomotion = null!;

    private ComponentCreatureSoundsPresentation _sounds = null!;

    private ComponentCharacter? _character;

    private bool _subscribed;

    private double _shoesWarningTime;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _locomotion = Entity.FindComponent<ComponentLocomotion>(true)!;
        _sounds = Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>();
        Subscribe();
    }

    private bool CanPresent => _subscribed &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation;

    private void PlayFootstep(float loudness)
    {
        if (CanPresent) { _sounds.PlayFootstepSound(loudness); }
    }

    private void ShowFootwearHint()
    {
        if (!CanPresent ||
            _character is not { IsLocallyControlled: true } ||
            !Time.PeriodicEvent(10, 0) ||
            (_shoesWarningTime != 0 && Time.FrameStartTime - _shoesWarningTime <= 300)) { return; }

        var value = _character.ComponentClothing.GetClothes(ClothingSlot.Feet).LastOrDefault();
        if (Terrain.ExtractContents(value) == BuiltInBlockRegistry.GetIndex<ClothingBlock>() &&
            WorldContentBinding.For(Project).Catalog.Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value)).MovementSpeedFactor >
            1f) { return; }

        _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
            Localization.ClientLanguage.Catalog.Get(_locomotion.GetType().Name, 0),
            Color.White,
            true,
            true
        );
        _shoesWarningTime = Time.FrameStartTime;
    }

    private void Subscribe()
    {
        if (_locomotion is null || _subscribed) { return; }

        _locomotion.FootstepRequested += PlayFootstep;
        _locomotion.FootwearHintRequested += ShowFootwearHint;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_locomotion is null || !_subscribed) { return; }

        _locomotion.FootstepRequested -= PlayFootstep;
        _locomotion.FootwearHintRequested -= ShowFootwearHint;
        _subscribed = false;
    }

    public override void OnEntityAdded() => Subscribe();
    public override void OnEntityRemoved() => Unsubscribe();
    public override void Dispose() => Unsubscribe();
}
