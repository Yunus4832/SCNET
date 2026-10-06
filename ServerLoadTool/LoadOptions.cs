using System.Globalization;

namespace ServerLoadTool;

public sealed record LoadOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 28887;
    public int Clients { get; init; } = 1;
    public double RampSeconds { get; init; } = 1;
    public double WarmupSeconds { get; init; } = 10;
    public double DurationSeconds { get; init; } = 30;
    public double JoinTimeoutSeconds { get; init; } = 60;
    public string Workload { get; init; } = "idle";
    public int Visibility { get; init; } = 32;
    public int Seed { get; init; } = 1;
    public double Speed { get; init; } = 4;
    public int? ServerPid { get; init; }
    public int? ServerHttpPort { get; init; }
    public string Output { get; init; } = "";

    public static LoadOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--") || i + 1 >= args.Length || !values.TryAdd(args[i][2..], args[i + 1]))
            {
                throw new ArgumentException("Options require unique --name value pairs.");
            }
        }

        string Get(string key, string fallback)
        {
            return values.Remove(key, out var value) ? value : fallback;
        }

        var serverPid = Get("server-pid", "");
        var serverHttpPort = Get("server-http-port", "");
        var options = new LoadOptions
        {
            Host = Get("host", "127.0.0.1"),
            Port = int.Parse(Get("port", "28887"), CultureInfo.InvariantCulture),
            Clients = int.Parse(Get("clients", "1"), CultureInfo.InvariantCulture),
            RampSeconds = double.Parse(Get("ramp", "1"), CultureInfo.InvariantCulture),
            WarmupSeconds = double.Parse(Get("warmup", "10"), CultureInfo.InvariantCulture),
            DurationSeconds = double.Parse(Get("duration", "30"), CultureInfo.InvariantCulture),
            JoinTimeoutSeconds = double.Parse(Get("join-timeout", "60"), CultureInfo.InvariantCulture),
            Visibility = int.Parse(Get("visibility", "32"), CultureInfo.InvariantCulture),
            Seed = int.Parse(Get("seed", "1"), CultureInfo.InvariantCulture),
            Speed = double.Parse(Get("speed", "4"), CultureInfo.InvariantCulture),
            Workload = Get("workload", "idle"),
            Output = Get("output", ""),
            ServerPid = serverPid.Length > 0 ? int.Parse(serverPid, CultureInfo.InvariantCulture) : null,
            ServerHttpPort = serverHttpPort.Length > 0 ? int.Parse(serverHttpPort, CultureInfo.InvariantCulture) : null
        };
        if (values.Count != 0)
        {
            throw new ArgumentException($"Unknown option: {values.Keys.First()}.");
        }

        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535 || Clients is < 1 or > 200 ||
            Visibility is < 32 or > 128 || ServerPid is <= 0 || string.IsNullOrWhiteSpace(Output) ||
            ServerHttpPort is < 1 or > 65535 ||
            Workload is not ("idle" or "shared" or "explore") ||
            !double.IsFinite(RampSeconds) || RampSeconds is < 0 or > 60 ||
            !double.IsFinite(WarmupSeconds) || WarmupSeconds is < 0 or > 3600 ||
            !double.IsFinite(DurationSeconds) || DurationSeconds is < 1 or > 3600 ||
            !double.IsFinite(JoinTimeoutSeconds) || JoinTimeoutSeconds is < 1 or > 600 ||
            !double.IsFinite(Speed) || Speed is < 0 or > 16)
        {
            throw new ArgumentException("Invalid load options. Use --help for bounds; --output is required.");
        }
    }
}
