using System.Security.Cryptography;

using Engine.Core.Security;

using Game.Protocol.Identity;

namespace Survivalcraft.Test.Modding;

internal static class TerritoryTestIdentity
{
    public static PlayerAddress Address()
    {
        return new PlayerAddress(IdentityAddress.Encode(RandomNumberGenerator.GetBytes(32)));
    }
}
