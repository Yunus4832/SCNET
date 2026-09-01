using System.Globalization;
using System.Net;

namespace NetworkDamageTool;

public sealed record LinkImpairmentOptions(
    int LatencyMilliseconds,
    int JitterMilliseconds,
    double LossProbability,
    int BandwidthKilobitsPerSecond)
{
    public override string ToString() =>
        $"latency={LatencyMilliseconds}ms,jitter={JitterMilliseconds}ms," +
        $"loss={LossProbability:P1},bandwidth={BandwidthKilobitsPerSecond}kbps";
}

public sealed record DamageProxyOptions(
    IPEndPoint ListenEndPoint,
    IPEndPoint TargetEndPoint,
    int Seed,
    LinkImpairmentOptions Upstream,
    LinkImpairmentOptions Downstream,
    string? EventsPath,
    TimeSpan? Duration)
{
    public static DamageProxyOptions Parse(string[] args)
    {
        var options = ParseOptions(args[0] == "run" ? args[1..] : args);
        var listen = ParseEndPoint(Required(options, "listen"));
        var target = ParseEndPoint(Required(options, "target"));
        var seed = ParseInt(options, "seed", 1, int.MinValue, int.MaxValue);
        var upstream = ParseLink(options, "up");
        var downstream = ParseLink(options, "down");
        var durationSeconds = ParseInt(options, "duration-seconds", 0, 0, int.MaxValue);
        return new DamageProxyOptions(
            listen,
            target,
            seed,
            upstream,
            downstream,
            options.GetValueOrDefault("events"),
            durationSeconds == 0 ? null : TimeSpan.FromSeconds(durationSeconds));
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
                          Usage:
                            dotnet run --project NetworkDamageTool -- run \
                              --listen 127.0.0.1:28989 --target 127.0.0.1:28987 [options]

                          Common options:
                            --seed N                  Deterministic random seed (default: 1)
                            --events PATH             Write one JSONL statistics record per second
                            --duration-seconds N      Stop automatically; 0 runs until Ctrl+C

                          Direction options (replace DIR with up or down):
                            --DIR-latency-ms N        Fixed one-way delay
                            --DIR-jitter-ms N         Uniform +/- jitter
                            --DIR-loss P              Drop probability from 0 to 1
                            --DIR-bandwidth-kbps N    Link bandwidth; 0 means unlimited
                          """);
    }

    private static LinkImpairmentOptions ParseLink(Dictionary<string, string> options, string prefix) =>
        new(
            ParseInt(options, $"{prefix}-latency-ms", 0, 0, 60_000),
            ParseInt(options, $"{prefix}-jitter-ms", 0, 0, 60_000),
            ParseDouble(options, $"{prefix}-loss", 0, 0, 1),
            ParseInt(options, $"{prefix}-bandwidth-kbps", 0, 0, int.MaxValue));

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Expected --option value near '{args[index]}'.");
            }

            var key = args[index][2..];
            if (!result.TryAdd(key, args[index + 1]))
            {
                throw new ArgumentException($"Option '--{key}' was specified more than once.");
            }
        }

        return result;
    }

    private static string Required(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"Missing required option '--{name}'.");

    private static int ParseInt(
        Dictionary<string, string> options,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (!options.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"Invalid value for '--{name}': '{value}'.");
        }

        return parsed;
    }

    private static double ParseDouble(
        Dictionary<string, string> options,
        string name,
        double defaultValue,
        double minimum,
        double maximum)
    {
        if (!options.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed) || parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"Invalid value for '--{name}': '{value}'.");
        }

        return parsed;
    }

    private static IPEndPoint ParseEndPoint(string value)
    {
        if (!IPEndPoint.TryParse(value, out var endPoint))
        {
            throw new ArgumentException($"Invalid IPv4 endpoint '{value}'.");
        }

        if (endPoint.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException("The first proxy version supports IPv4 endpoints only.");
        }

        return endPoint;
    }
}
