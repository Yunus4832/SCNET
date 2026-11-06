using EntitySystem.TemplatesDatabase;

namespace Game.Spawning;

public class SpawnEntityData
{
    public bool ConstantSpawn;

    public ValuesDictionary? Data;

    public Vector3 Position;

    public string TemplateName = string.Empty;
}
