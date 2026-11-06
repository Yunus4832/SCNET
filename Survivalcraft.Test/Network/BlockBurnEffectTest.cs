using Engine.Core.Mathematics;

using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class BlockBurnEffectTest
{
    [Theory]
    [InlineData(BlockBurnEffectKind.Debris)]
    [InlineData(BlockBurnEffectKind.Burning)]
    [InlineData(BlockBurnEffectKind.FluidSizzle)]
    public void BurnEffectRoundTripPreservesPresentationAndUsesEffectTransport(BlockBurnEffectKind kind)
    {
        var message = new BlockBurnEffectPackage { Position = new Vector3(-1.5f, 65f, 2.5f), Kind = kind };
        ProtocolMessageValidation.Validate(message);
        var decoded =
            Assert.IsType<BlockBurnEffectPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        Assert.Equal(message, decoded);
        Assert.Equal(PackageTransportPolicy.Effect, PackageTransportPolicy.Get(message));
    }

    [Fact]
    public void BurnEffectRejectsUnknownKind()
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(
                new BlockBurnEffectPackage { Kind = (BlockBurnEffectKind)255 }
            )
        );
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void BurnEffectRejectsNonFinitePositions(float coordinate)
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(
                new BlockBurnEffectPackage { Position = new Vector3(coordinate, 0, 0) }
            )
        );
    }
}
