using Engine.Media.Images;

namespace Game.World.Presentation;

public sealed class SeasonPresentation
{
    private Color[]? _gradient;

    public void InitializeGradient(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var colors = new Color[image.Width];
        for (var x = 0; x < colors.Length; x++) { colors[x] = image.GetPixel(x, 0); }

        Volatile.Write(ref _gradient, colors);
    }

    public string GetTimeOfYearName(float timeOfYear)
    {
        SeasonCalendar.TimeOfYearToSeason(timeOfYear, out var season, out var timeOfSeason);
        var part = timeOfSeason switch { < 0.25f => 0, >= 0.75f => 2, _ => 1 };
        return Localization.ClientLanguage.Catalog.Get(nameof(SubsystemSeasons), (int)season * 3 + part);
    }

    public Color GetTimeOfYearColor(float timeOfYear)
    {
        var gradient = Volatile.Read(ref _gradient);
        if (gradient is null)
        {
            SeasonCalendar.TimeOfYearToSeason(timeOfYear, out var season, out _);
            return season switch
            {
                Season.Summer => new Color(255, 220, 120),
                Season.Autumn => new Color(255, 170, 90),
                Season.Winter => new Color(190, 220, 255),
                _ => new Color(170, 235, 170)
            };
        }

        var x = (int)MathUtils.Clamp(MathUtils.Round(timeOfYear * gradient.Length), 0f, gradient.Length - 1);
        return gradient[x];
    }
}
