using System.Runtime.CompilerServices;

using Game.Network;

namespace Survivalcraft.Test.Fixtures;

internal static class NetworkApplicationTestSetup
{
    [ModuleInitializer]
    internal static void InitializeOwner()
    {
        if (!CommonLib.IsNetworkInitialized) { CommonLib.InitializeNetwork(NetNodeOptions.Default, false); }
    }
}
