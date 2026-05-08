namespace Game;

public class RunningAverage(float period)
{
    private int _countValues;

    private readonly long _period = (long)(period * Stopwatch.Frequency);

    private long _startTicks = -1;

    private float _sumValues;

    public float Value { get; private set; }

    public void AddSample(float sample)
    {
        _sumValues += sample;
        _countValues++;
        var timestamp = Stopwatch.GetTimestamp();
        if (_startTicks < 0)
        {
            _startTicks = timestamp;
        }

        if (timestamp < _startTicks + _period)
        {
            return;
        }

        Value = _sumValues / _countValues;
        _sumValues = 0f;
        _countValues = 0;
        _startTicks = timestamp;
    }
}
