using EntitySystem.Core;

namespace Game.Components.Behavior;

public abstract class ComponentBehavior : Component
{
    protected readonly StateMachine stateMachine = new();

    public abstract float ImportanceLevel { get; }

    public virtual bool IsActive { get; set; }

    public virtual string DebugInfo => string.Empty;
}
