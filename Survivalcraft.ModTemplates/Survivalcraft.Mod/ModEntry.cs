using Game.Modding;

namespace Survivalcraft.ModTemplate;

public sealed class ModEntry : IMod
{
    public void Configure(IModContext context)
    {
        context.Extensions.Register(
            "examples",
            new ResourceId(context.Manifest.ModId, "example"),
            "Hello from Survivalcraft.ModTemplate");
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }
}
