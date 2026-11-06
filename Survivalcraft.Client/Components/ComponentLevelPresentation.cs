using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentLevelPresentation : Component, IUpdateable
{
    private static readonly (double Delay, float Pitch)[] _levelUpNotes =
    [
        (0, -.2f), (.15, -.03333333f), (.3, 142f / (339f * (float)Math.PI)), (.45, 23f / 60f),
        (.75, -.03333333f), (.9, 23f / 60f)
    ];

    private ComponentLevel _level = null!;

    private ComponentCharacter _character = null!;

    private SubsystemAudio _audio = null!;

    private readonly Random _random = new();

    private float? _lastLevelTextValue;

    private int _lastLevelLanguageRevision = -1;

    private bool _subscribed;

    private int _generation;

    public string ResolveFactorDescription(ComponentLevel.Factor factor)
    {
        return factor.ClothingValue is { } value
            ? Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(Terrain.ExtractContents(value))
                .GetDisplayName(null, value)
            : factor.Description is { } description
                ? Localization.ClientLanguage.Resolver.Resolve(description, factor.DescriptionArguments ?? [])
                : string.Empty;
    }

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _level = Entity.FindComponent<ComponentLevel>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        Subscribe();
    }

    public void Update(float dt)
    {
        if (!IsAddedToProject || !CanPresent) { return; }

        var level = MathUtils.Floor(_character.CharacterData.Level);
        if (_lastLevelTextValue.HasValue &&
            _lastLevelTextValue.Value.CloseTo(level) &&
            _lastLevelLanguageRevision == Localization.ClientLanguage.Revision) { return; }

        _character.Entity.FindComponent<ComponentGui>(true)!.LevelLabelWidget.Text = string.Format(
            Localization.ClientLanguage.Catalog.Get(ComponentLevel.Name, 2),
            level
        );
        _lastLevelTextValue = level;
        _lastLevelLanguageRevision = Localization.ClientLanguage.Revision;
    }

    private bool CanPresent => _subscribed &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation &&
                               _character.IsLocallyControlled;

    private void OnExperienceReceived(int levelUps, bool playSound)
    {
        if (!CanPresent) { return; }

        if (playSound)
        {
            _audio.PlaySound(
                "Audio/ExperienceCollected",
                .2f,
                _random.Float(-.1f, .4f),
                _character.ComponentBody.Position,
                2f,
                false
            );
        }

        var generation = _generation;
        var startTime = Time.FrameStartTime + .5;
        for (var i = 0; i < levelUps; i++)
        {
            Queue(
                startTime,
                generation,
                () => _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(ComponentLevel.Name, 1),
                    Color.White,
                    true,
                    false
                )
            );
            foreach (var note in _levelUpNotes)
            {
                Queue(
                    startTime + note.Delay,
                    generation,
                    () => _audio.PlaySound(
                        "Audio/ExperienceCollected",
                        1f,
                        note.Pitch,
                        _character.ComponentBody.Position,
                        2f,
                        false
                    )
                );
            }
        }
    }

    private void Queue(double time, int generation, Action action)
    {
        WorldRuntimeContext.For(Project).QueueTimeDelayedExecution(
            time,
            () =>
            {
                if (generation == _generation && CanPresent) { action(); }
            }
        );
    }

    private void Subscribe()
    {
        if (_level is null || _subscribed) { return; }

        _level.ExperienceReceived += OnExperienceReceived;
        _subscribed = true;
        _lastLevelTextValue = null;
    }

    private void Unsubscribe()
    {
        if (_level is null || !_subscribed) { return; }

        _level.ExperienceReceived -= OnExperienceReceived;
        _subscribed = false;
        _generation++;
    }

    public override void OnEntityAdded() => Subscribe();
    public override void OnEntityRemoved() => Unsubscribe();
    public override void Dispose() => Unsubscribe();
}
