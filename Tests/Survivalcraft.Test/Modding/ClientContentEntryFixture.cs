using Game.Modding;

namespace Survivalcraft.Test.Modding;

public sealed class ClientContentEntryFixture : IDisposable, IComparer<IMod>
{
    public int Compare(IMod? x, IMod? y) => x is PackageEntrypointMod && y is PackageEntrypointMod ? 0 : -1;

    public void Dispose()
    {
    }
}
