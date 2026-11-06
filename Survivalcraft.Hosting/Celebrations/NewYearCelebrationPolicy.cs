namespace Game.Celebrations;

public sealed class NewYearCelebrationPolicy(
    TimeProvider clock,
    Func<int> readLastCelebratedYear,
    Action<int> rememberYear
)
{
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly Func<int> _readLastCelebratedYear =
        readLastCelebratedYear ?? throw new ArgumentNullException(nameof(readLastCelebratedYear));

    private readonly Action<int> _rememberYear = rememberYear ?? throw new ArgumentNullException(nameof(rememberYear));

    public bool TryGetCelebrationYear(out int year)
    {
        var now = _clock.GetLocalNow();
        year = now.Year;
        return now.Year > _readLastCelebratedYear() &&
               now.Month == 1 &&
               now.Day == 1 &&
               now.Hour == 0 &&
               now.Minute < 59;
    }

    public void RememberCelebration(int year) => _rememberYear(year);
}
