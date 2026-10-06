using Engine.Core;

namespace ServerLoadTool;

public static class LoadMotion
{
    public static Vector3 Position(Vector3 origin, double seconds, int index, LoadOptions options)
    {
        var distance = seconds * options.Speed;
        if (options.Workload == "shared")
        {
            // Bounded back-and-forth motion, initially all near their actual spawn.
            var phase = distance % 32;
            return origin + new Vector3((float)(phase <= 16 ? phase : 32 - phase), 0, 0);
        }

        if (options.Workload == "explore")
        {
            var angle = (options.Seed * 0.61803398875 + index * 2.39996322973) % (Math.PI * 2);
            return origin + new Vector3((float)(Math.Cos(angle) * distance), 0, (float)(Math.Sin(angle) * distance));
        }

        return origin;
    }
}
