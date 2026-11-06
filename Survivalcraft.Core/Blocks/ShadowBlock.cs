namespace Game.Blocks;

public class ShadowBlock : Block
{
    public static int SetShadowStrength(int data, int shadowStrength)
    {
        shadowStrength = MathUtils.Clamp(shadowStrength, -128, 128);
        return shadowStrength + 128;
    }
}
