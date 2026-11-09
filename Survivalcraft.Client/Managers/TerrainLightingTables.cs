namespace Game.Managers;

/// <summary>One immutable publication of render brightness lookup tables.</summary>
public sealed class TerrainLightingTables
{
    private readonly float[] _values;

    private readonly float[] _faces;

    public ReadOnlySpan<float> Values => _values;

    public ReadOnlySpan<float> Faces => _faces;

    internal TerrainLightingTables(float brightness)
    {
        _values = new float[16];
        _faces = new float[96];
        var ambient = MathUtils.Lerp(0f, 0.1f, brightness);
        for (var i = 0; i < 16; i++)
        {
            _values[i] = MathUtils.Saturate(MathUtils.Lerp(ambient, 1f, MathUtils.Pow(i / 15f, 1.25f)));
        }

        for (var face = 0; face < 6; face++)
        {
            var directional = LightingManager.CalculateLighting(CellFace.FaceToVector3(face));
            for (var i = 0; i < 16; i++) { _faces[i + 16 * face] = _values[i] * directional; }
        }
    }
}
