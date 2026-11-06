using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Celebrations;

public sealed class SubsystemNewYearCelebrationHosting : Subsystem, IUpdateable
{
    private NewYearCelebrationPolicy? _policy;

    private SubsystemFireworksBlockBehavior _fireworks = null!;

    private bool _loaded;

    private bool _disposed;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public UpdateOrder UpdateOrder => UpdateOrder.Default - 1;

    public void Configure(NewYearCelebrationPolicy policy)
    {
        if (_loaded || _disposed)
        {
            throw new InvalidOperationException("Celebration hosting must be configured before loading.");
        }

        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (_loaded || _disposed || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Celebration hosting requires one live authority world.");
        }

        if (_policy is null)
        {
            throw new InvalidOperationException("Celebration hosting clock and year preferences are not configured.");
        }

        _fireworks = Project.FindSubsystem<SubsystemFireworksBlockBehavior>(true)!;
        _loaded = true;
    }

    public void Update(float dt)
    {
        TryStartAtCalendarTick(Time.PeriodicEvent(5.0, 0.0));
    }

    internal bool TryStartAtCalendarTick(bool calendarTick)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || !calendarTick) { return false; }

        var policy = _policy ?? throw new InvalidOperationException("Celebration hosting is not loaded.");
        if (_fireworks.IsNewYearCelebrationActive ||
            !policy.TryGetCelebrationYear(out var year) ||
            _fireworks.TryStartNewYearCelebration() is not { } character)
        {
            return false;
        }

        policy.RememberCelebration(year);
        if (character.CharacterData.ControllerConnection is { } connection &&
            character.CharacterData.CharacterId != Guid.Empty)
        {
            NetworkSender.SendTo(
                connection,
                new NewYearCelebrationAnnouncementPackage { CharacterId = character.CharacterData.CharacterId }
            );
        }

        return true;
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _policy = null;
        _fireworks = null!;
        base.Dispose();
    }
}
