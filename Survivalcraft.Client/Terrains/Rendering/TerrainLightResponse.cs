using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.Terrains.Rendering;

/// <summary>为每个可能的环境天光强度记录量化的顶点亮度。</summary>
public readonly record struct TerrainLightResponse(
    Color Levels0To3,
    Color Levels4To7,
    Color Levels8To11,
    Color Levels12To15
)
{
    private static readonly float[] _baseBrightness = Enumerable.Range(0, 16)
        .Select(level => MathUtils.Pow(level / 15f, 1.25f)).ToArray();

    private static readonly float[] _responses = CreateResponses();

    private static readonly TerrainLightResponse[] _channelResponses = CreateChannelResponses();

    internal static VertexDeclaration VertexDeclaration { get; } = new(
        new VertexElement(0, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate1),
        new VertexElement(4, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate2),
        new VertexElement(8, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate3),
        new VertexElement(12, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate4)
    );

    public static TerrainLightResponse FullBright { get; } = new(Color.White, Color.White, Color.White, Color.White);

    public static TerrainLightResponse FromChannels(TerrainLightChannels channels) =>
        _channelResponses[channels.Packed];

    private static float[] CreateResponses()
    {
        var responses = new float[4096];
        for (var packed = 0; packed < 256; packed++)
        {
            var channels = TerrainLightChannels.FromPacked((byte)packed);
            for (var daylight = 0; daylight < 16; daylight++)
            {
                responses[(packed << 4) + daylight] =
                    _baseBrightness[channels.GetEffectiveLight(new TerrainLightEnvironment(daylight))];
            }
        }

        return responses;
    }

    private static TerrainLightResponse[] CreateChannelResponses()
    {
        var responses = new TerrainLightResponse[256];
        for (var packed = 0; packed < responses.Length; packed++)
        {
            ReadOnlySpan<TerrainLightChannels> samples = [TerrainLightChannels.FromPacked((byte)packed)];
            ReadOnlySpan<float> weights = [1f];
            responses[packed] = FromSamples(samples, weights);
        }

        return responses;
    }

    public static TerrainLightResponse FromSamples(
        ReadOnlySpan<TerrainLightChannels> samples,
        ReadOnlySpan<float> weights
    )
    {
        if (samples.IsEmpty || samples.Length != weights.Length)
        {
            throw new ArgumentException("Light samples and interpolation weights must have the same nonzero length.");
        }

        Span<byte> levels = stackalloc byte[16];
        for (var daylight = 0; daylight < levels.Length; daylight++)
        {
            var brightness = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                brightness += weights[i] * _responses[(samples[i].Packed << 4) + daylight];
            }

            levels[daylight] = (byte)MathUtils.Round(255f * MathUtils.Saturate(brightness));
        }

        return new TerrainLightResponse(
            new Color(levels[0], levels[1], levels[2], levels[3]),
            new Color(levels[4], levels[5], levels[6], levels[7]),
            new Color(levels[8], levels[9], levels[10], levels[11]),
            new Color(levels[12], levels[13], levels[14], levels[15])
        );
    }

    public float GetBrightness(float daylight, float brightnessSetting)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(daylight, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(daylight, 15f);
        if (float.IsNaN(daylight)) { throw new ArgumentOutOfRangeException(nameof(daylight)); }

        var lower = (int)daylight;
        var upper = Math.Min(lower + 1, 15);
        var response = MathUtils.Lerp(GetResponse(lower), GetResponse(upper), daylight - lower);
        var ambient = MathUtils.Lerp(0f, 0.1f, brightnessSetting);
        return MathUtils.Lerp(ambient, 1f, response);
    }

    private float GetResponse(int daylight)
    {
        var levels = (daylight >> 2) switch
        {
            0 => Levels0To3,
            1 => Levels4To7,
            2 => Levels8To11,
            _ => Levels12To15
        };
        return (byte)(levels.PackedValue >> (8 * (daylight & 3))) / 255f;
    }
}
