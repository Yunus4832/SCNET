using Engine.Core.Mathematics;
using Engine.Media.Images;

using Game.World.Presentation;

namespace Survivalcraft.Test.World;

public sealed class SeasonPresentationTest
{
    [Theory]
    [InlineData(0.125f, 255, 220, 120)]
    [InlineData(0.375f, 255, 170, 90)]
    [InlineData(0.625f, 190, 220, 255)]
    [InlineData(0.875f, 170, 235, 170)]
    public void UninitializedGradientRetainsTheFourDefaultSeasonColors(float year, int r, int g, int b)
    {
        Assert.Equal(new Color(r, g, b), new SeasonPresentation().GetTimeOfYearColor(year));
    }

    [Fact]
    public void GradientUsesTheOriginalRoundClampAndFirstRowAndOwnsItsPixels()
    {
        var presentation = new SeasonPresentation();
        using (var image = new Image(4, 2))
        {
            image.SetPixel(0, 0, Color.Red);
            image.SetPixel(1, 0, Color.Green);
            image.SetPixel(2, 0, Color.Blue);
            image.SetPixel(3, 0, Color.White);
            for (var x = 0; x < image.Width; x++) { image.SetPixel(x, 1, Color.Black); }

            presentation.InitializeGradient(image);
            image.SetPixel(0, 0, Color.Black);
        }

        Assert.Equal(Color.Red, presentation.GetTimeOfYearColor(-1f));
        Assert.Equal(Color.Red, presentation.GetTimeOfYearColor(0f));
        Assert.Equal(Color.Green, presentation.GetTimeOfYearColor(0.25f));
        Assert.Equal(Color.Blue, presentation.GetTimeOfYearColor(0.5f));
        Assert.Equal(Color.White, presentation.GetTimeOfYearColor(1f));
        Assert.Equal(Color.White, presentation.GetTimeOfYearColor(2f));
        Assert.Equal(new Color(255, 220, 120), new SeasonPresentation().GetTimeOfYearColor(0.125f));
    }
}
