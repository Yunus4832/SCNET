using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public abstract class Subsystem : IDisposable
{
    private bool _initialized;

    public Project Project
    {
        get => _initialized ? field : throw new InvalidOperationException("Subsystem was not initialized.");
        private set;
    } = null!;

    public ValuesDictionary ValuesDictionary
    {
        get => _initialized ? field : throw new InvalidOperationException("Subsystem was not initialized.");
        private set;
    } = null!;

    public virtual void Dispose()
    {
    }

    public virtual void OnEntityAdded(Entity entity)
    {
    }

    public virtual void OnEntityRemoved(Entity entity)
    {
    }

    public virtual void Load(ValuesDictionary valuesDictionary)
    {
    }

    public virtual void Save(ValuesDictionary valuesDictionary)
    {
    }

    public void Initialize(Project project, ValuesDictionary valuesDictionary)
    {
        if (valuesDictionary.DatabaseObject.Type != project.GameDatabase.MemberSubsystemTemplateType)
        {
            throw new InvalidOperationException("ValuesDictionary has invalid type.");
        }

        Project = project;
        ValuesDictionary = valuesDictionary;
        _initialized = true;
    }
}
