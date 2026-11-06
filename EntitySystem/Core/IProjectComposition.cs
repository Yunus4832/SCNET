using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

/// <summary>Selects project members before runtime type resolution and construction.</summary>
/// <remarks>Implementations must keep decisions stable for the project lifetime and leave definitions unchanged.</remarks>
public interface IProjectComposition
{
    bool ShouldCreateSubsystem(ValuesDictionary definition);

    bool ShouldCreateComponent(ValuesDictionary definition);
}
