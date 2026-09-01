using System.Text;

namespace Game;

public struct Profiler : IDisposable
{
    public class Metric
    {
        public readonly RunningAverage AverageHitCount = new(5f);

        public readonly RunningAverage AverageTime = new(5f);

        public int HitCount;

        public long MaxTicks;

        public string Name = string.Empty;

        public long TotalTicks;
    }

    private static readonly Dictionary<string, Metric> _metrics;

    private static readonly List<Metric> _sortedMetrics = [];

    private static bool _sortNeeded;

    private readonly long _startTicks;

    private Metric? _metric;

    public static bool Enabled = true;

    public static int MaxNameLength { get; private set; }

    public static ReadOnlyList<Metric> ReadOnlyMetrics
    {
        get
        {
            if (!_sortNeeded)
            {
                return new ReadOnlyList<Metric>(_sortedMetrics);
            }

            _sortedMetrics.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
            _sortNeeded = false;

            return new ReadOnlyList<Metric>(_sortedMetrics);
        }
    }

    public Profiler(string name)
    {
        if (Enabled)
        {
            if (!_metrics.TryGetValue(name, out _metric))
            {
                _metric = new Metric
                {
                    Name = name
                };
                MaxNameLength = MathUtils.Max(MaxNameLength, name.Length);
                _metrics.Add(name, _metric);
                _sortedMetrics.Add(_metric);
                _sortNeeded = true;
            }

            _startTicks = Stopwatch.GetTimestamp();
        }
        else
        {
            _startTicks = 0L;
            _metric = null;
        }
    }

    static Profiler()
    {
        _metrics = new Dictionary<string, Metric>();
    }

    public void Dispose()
    {
        if (_metric == null)
        {
            throw new InvalidOperationException("Profiler.Dispose called without a matching constructor.");
        }

        var num = Stopwatch.GetTimestamp() - _startTicks;
        _metric.TotalTicks += num;
        _metric.MaxTicks = MathUtils.Max(_metric.MaxTicks, num);
        _metric.HitCount++;
        _metric = null;
    }

    public static void Sample()
    {
        foreach (var metric in ReadOnlyMetrics)
        {
            var sample = metric.TotalTicks / (float)Stopwatch.Frequency;
            metric.AverageHitCount.AddSample(metric.HitCount);
            metric.AverageTime.AddSample(sample);
            metric.HitCount = 0;
            metric.TotalTicks = 0L;
            metric.MaxTicks = 0L;
        }
    }

    public static void ReportAverage(Metric metric, StringBuilder text)
    {
        var num = MaxNameLength + 2;
        var length = text.Length;
        text.Append(metric.Name);
        text.Append('.', Math.Max(1, num - text.Length + length));
        text.AppendNumber(metric.AverageHitCount.Value, 2);
        text.Append('x');
        text.Append('.', Math.Max(1, num + 9 - text.Length + length));
        FormatTimeSimple(text, metric.AverageTime.Value);
    }

    public static void ReportFrame(Metric metric, StringBuilder text)
    {
        var num = MaxNameLength + 2;
        var length = text.Length;
        text.Append(metric.Name);
        text.Append('.', Math.Max(1, num - text.Length + length));
        FormatTimeSimple(text, metric.TotalTicks / (float)Stopwatch.Frequency);
    }

    public static void ReportAverage(StringBuilder text)
    {
        foreach (var metric in ReadOnlyMetrics)
        {
            ReportAverage(metric, text);
            text.Append('\n');
        }
    }

    public static void ReportFrame(StringBuilder text)
    {
        foreach (var metric in ReadOnlyMetrics)
        {
            ReportFrame(metric, text);
            text.Append('\n');
        }
    }

    public static void FormatTimeSimple(StringBuilder text, float time)
    {
        text.AppendNumber(time * 1000f, 3);
        text.Append("ms");
    }

    public static void FormatTime(StringBuilder text, float time)
    {
        if (time >= 1f)
        {
            text.AppendNumber(time, 2);
            text.Append('s');
        }
        else if (time >= 0.1f)
        {
            text.AppendNumber(time * 1000f, 0);
            text.Append("ms");
        }
        else if (time >= 0.01f)
        {
            text.AppendNumber(time * 1000f, 1);
            text.Append("ms");
        }
        else if (time >= 0.001f)
        {
            text.AppendNumber(time * 1000f, 2);
            text.Append("ms");
        }
        else if (time >= 0.0001f)
        {
            text.AppendNumber(time * 1000000f, 0);
            text.Append("us");
        }
        else if (time >= 1E-05f)
        {
            text.AppendNumber(time * 1000000f, 1);
            text.Append("us");
        }
        else if (time >= 1E-06f)
        {
            text.AppendNumber(time * 1000000f, 2);
            text.Append("us");
        }
        else if (time >= 1E-07f)
        {
            text.AppendNumber(time * 1E+09f, 0);
            text.Append("ns");
        }
        else if (time >= 1E-08f)
        {
            text.AppendNumber(time * 1E+09f, 1);
            text.Append("ns");
        }
        else
        {
            text.AppendNumber(time * 1E+09f, 2);
            text.Append("ns");
        }
    }
}
