using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentCharacterPresentation : Component
{
    private ComponentCharacter _character = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemTerrain _terrain = null!;

    private bool _subscribed;

    private bool _aimHintIssued;

    private bool _aimHintPending;

    private int _generation;

    public CharacterPresentation CharacterView =>
        Project.FindSubsystem<SubsystemCharacterPresentation>(true)!.Get(
            Entity.FindComponent<ComponentCharacter>(true)!.CharacterData
        );

    public GameWidget GameWidget => CharacterView.GameWidget;

    public ContainerWidget GuiWidget => GameWidget.GuiWidget;

    public ViewWidget ViewWidget => GameWidget.ViewWidget;

    public DragHostWidget? DragHostWidget => field ??= GameWidget.Children.Find<DragHostWidget>(false);

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        Subscribe();
    }

    private bool CanPresent => _subscribed &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation &&
                               _character.IsLocallyControlled;

    private void OnSteedSpeedFeedback(bool fast)
    {
        if (!CanPresent) { return; }

        var male = _character.CharacterData.CharacterSex == CharacterSex.Male;
        var sound = fast ? male ? "Audio/Creatures/MaleYellFast" : "Audio/Creatures/FemaleYellFast" :
            male ? "Audio/Creatures/MaleYellSlow" : "Audio/Creatures/FemaleYellSlow";
        _audio.PlayRandomSound(sound, .75f, 0f, _character.ComponentBody.Position, 2f, false);
    }

    private void OnCreativeBlockPicked(int value)
    {
        if (!CanPresent) { return; }

        var block = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(
            Terrain.ExtractContents(value)
        );
        _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
            block.GetDisplayName(_terrain, value),
            Color.White,
            false,
            false
        );
        _audio.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f, 0f);
    }

    private void OnAimHintRequested()
    {
        if (!CanPresent || _aimHintIssued || _aimHintPending) { return; }

        _aimHintPending = true;
        var generation = _generation;
        WorldRuntimeContext.For(Project).QueueTimeDelayedExecution(
            Time.FrameStartTime + 3,
            () =>
            {
                if (generation != _generation) { return; }

                _aimHintPending = false;
                if (!CanPresent || _aimHintIssued || !_character.IsAiming || _character.ComponentBody.IsSneaking)
                {
                    return;
                }

                _aimHintIssued = true;
                _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(nameof(ComponentCharacter), 1),
                    Color.White,
                    true,
                    true
                );
            }
        );
    }

    private void Subscribe()
    {
        if (_character is null || _subscribed) { return; }

        _character.SteedSpeedFeedback += OnSteedSpeedFeedback;
        _character.CreativeBlockPicked += OnCreativeBlockPicked;
        _character.AimHintRequested += OnAimHintRequested;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_character is null || !_subscribed) { return; }

        _character.SteedSpeedFeedback -= OnSteedSpeedFeedback;
        _character.CreativeBlockPicked -= OnCreativeBlockPicked;
        _character.AimHintRequested -= OnAimHintRequested;
        _subscribed = false;
        _aimHintPending = false;
        _generation++;
    }

    public override void OnEntityAdded() => Subscribe();
    public override void OnEntityRemoved() => Unsubscribe();
    public override void Dispose() => Unsubscribe();
}
