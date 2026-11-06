using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentName : Component
{
    public string Name { get; private set; } = string.Empty;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        Name = valuesDictionary.GetValue<string>("Name");
    }
}
