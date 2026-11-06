using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class DispenserSoundCodecTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DispenserResultSoundRoundTripsWithoutAConfigurationRequest(bool shot)
    {
        var sound = new DispenserSoundPackage { Position = new Vector3(1, 2, 3), Shot = shot };
        Assert.Equal(sound, ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(sound)));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                sound with
                {
                    Position = new Vector3(float.NaN, 0, 0)
                }
            )
        );
    }
}
