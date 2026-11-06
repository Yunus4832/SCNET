using Engine.Core.Mathematics;

namespace BeaconMod.Client.Presentation;

public static class BeaconPalette
{
    public static readonly Color[] Colors =
    [
        new(180, 245, 255), new(255, 100, 95), new(255, 180, 65), new(255, 245, 105),
        new(110, 255, 145), new(95, 175, 255), new(190, 130, 255), new(255, 140, 210)
    ];

    public static Color GetColor(int data) => Colors[data & 7];

    public static Color GetEmitterColor(int data) => Color.Lerp(GetColor(data), Color.White, 0.8f);
}
