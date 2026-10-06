using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServerLoadTool;

public sealed class LoadRun(LoadOptions options)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public int Execute(CancellationToken cancellation)
    {
        if (Directory.Exists(options.Output) && Directory.EnumerateFileSystemEntries(options.Output).Any())
        {
            throw new IOException("Output directory must be empty; preserve each run separately.");
        }

        Directory.CreateDirectory(options.Output);
        var started = DateTimeOffset.UtcNow;
        var manifest = new
        {
            StartedUtc = started,
            Options = options,
            ToolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            ToolBinarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location))),
            GameBinarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Game.Network.NetNode).Assembly.Location))),
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            WorkloadBoundary = "Protocol load only; no rendering, client physics, pathfinding, combat or block interaction. Terrain payload bytes are reassembled, not simulated."
        };
        var manifestNode = JsonSerializer.SerializeToNode(manifest)!;
        File.WriteAllText(Path.Combine(options.Output, "manifest.json"), manifestNode.ToJsonString(_json));
        using var samples = new StreamWriter(Path.Combine(options.Output, "samples.jsonl"));
        using var events = new StreamWriter(Path.Combine(options.Output, "events.jsonl"));
        using var driver = new ProcessProbe(Environment.ProcessId);
        using var server = options.ServerPid.HasValue ? new ProcessProbe(options.ServerPid.Value) : null;
        using var diagnostics = options.ServerHttpPort.HasValue ? new ServerHttpProbe(options.ServerHttpPort.Value) : null;
        var clock = Stopwatch.StartNew();
        var clients = new List<LoadClient>();
        var states = new Dictionary<int, string>();
        var latencies = new List<int>();
        var loopTimes = new List<double>();
        double? readyAt = null;
        double? measurementAt = null;
        double? drainAt = null;
        var lastSample = -1d;
        var previousTick = 0d;
        var phase = "joining";
        string? failure = null;
        var runName = Guid.NewGuid().ToString("N")[..6];
        long baselineSent = 0;
        long baselineReceived = 0;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var tick = clock.Elapsed.TotalSeconds;
                var elapsedMs = Math.Max(1, (int)((tick - previousTick) * 1000));
                previousTick = tick;
                var loopStarted = Stopwatch.GetTimestamp();
                if (clients.Count < options.Clients && tick >= clients.Count * options.RampSeconds)
                {
                    clients.Add(new LoadClient(options, clients.Count, runName));
                }

                foreach (var client in clients)
                {
                    client.Update(elapsedMs, phase is "warmup" or "measurement");
                    if (!states.TryGetValue(client.Index, out var state) || state != client.State)
                    {
                        states[client.Index] = client.State;
                        events.WriteLine(JsonSerializer.Serialize(new { Seconds = tick, client.Index, client.Name, client.State, client.Error }));
                        events.Flush();
                    }
                }

                if (clients.Any(client => client.Error != null))
                {
                    failure = "client_failure";
                    break;
                }

                if (!readyAt.HasValue && clients.Count == options.Clients && clients.All(client => client.State == "playing"))
                {
                    readyAt = tick;
                    phase = "warmup";
                }

                if (readyAt.HasValue && !measurementAt.HasValue && tick - readyAt.Value >= options.WarmupSeconds)
                {
                    measurementAt = tick;
                    phase = "measurement";
                    baselineSent = clients.Sum(client => client.SentBytes);
                    baselineReceived = clients.Sum(client => client.ReceivedBytes);
                }

                if (measurementAt.HasValue && !drainAt.HasValue && tick - measurementAt.Value >= options.DurationSeconds)
                {
                    drainAt = tick;
                    phase = "drain";
                }

                if (drainAt.HasValue)
                {
                    if (clients.All(client => client.PendingChunks == 0) && tick - drainAt.Value >= 3)
                    {
                        break;
                    }

                    if (tick - drainAt.Value > 30)
                    {
                        failure = "chunks_did_not_converge";
                        break;
                    }
                }

                if (tick - lastSample >= 1)
                {
                    lastSample = tick;
                    diagnostics?.Sample();
                    var ping = clients.Where(client => client.State == "playing").Select(client => client.Ping).ToArray();
                    if (phase == "measurement")
                    {
                        latencies.AddRange(ping);
                    }

                    samples.WriteLine(JsonSerializer.Serialize(new
                    {
                        Seconds = tick,
                        Phase = phase,
                        Launched = clients.Count,
                        Playing = clients.Count(client => client.State == "playing"),
                        PendingChunks = clients.Sum(client => client.PendingChunks),
                        CompletedChunks = clients.Sum(client => client.CompletedChunks),
                        ReceivedBytes = clients.Sum(client => client.ReceivedBytes),
                        SentBytes = clients.Sum(client => client.SentBytes),
                        PingMilliseconds = ping,
                        Positions = clients.Select(client => new
                        {
                            client.Index,
                            Position = LoadPosition.From(client.Position),
                            AuthoritativePosition = LoadPosition.From(client.AuthoritativePosition)
                        }),
                        Driver = driver.Capture(tick),
                        Server = server?.Capture(tick),
                        ServerDiagnostics = diagnostics?.Latest,
                        ServerDiagnosticError = diagnostics?.Error
                    }));
                    samples.Flush();
                    Console.WriteLine($"{tick:F1}s {phase}: playing={clients.Count(client => client.State == "playing")}/{options.Clients}, pending chunks={clients.Sum(client => client.PendingChunks)}");
                }

                if (phase == "measurement")
                {
                    loopTimes.Add(Stopwatch.GetElapsedTime(loopStarted).TotalMilliseconds);
                }

                Thread.Sleep(5);
            }

            if (cancellation.IsCancellationRequested)
            {
                failure = "cancelled";
            }
            if (diagnostics != null && (diagnostics.Error != null || !diagnostics.Latest.HasValue))
            {
                failure ??= "server_diagnostics_unavailable";
            }
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
        }
        finally
        {
            manifestNode["ServerWorld"] = JsonSerializer.SerializeToNode(clients.FirstOrDefault()?.World);
            var duration = measurementAt.HasValue ? Math.Min(options.DurationSeconds, clock.Elapsed.TotalSeconds - measurementAt.Value) : 0;
            var summary = new
            {
                Success = failure == null && measurementAt.HasValue && clients.Count == options.Clients,
                Failure = failure,
                ServerDiagnosticError = diagnostics?.Error,
                FinishedUtc = DateTimeOffset.UtcNow,
                MeasuredSeconds = duration,
                MeasurementStartedSeconds = measurementAt,
                RequestedClients = options.Clients,
                PlayingClients = clients.Count(client => client.State == "playing"),
                ApplicationSentBytes = clients.Sum(client => client.SentBytes),
                ApplicationReceivedBytes = clients.Sum(client => client.ReceivedBytes),
                // Includes the drain tail; sample deltas give the exact measurement-only rates.
                PostWarmupSentBytes = clients.Sum(client => client.SentBytes) - baselineSent,
                PostWarmupReceivedBytes = clients.Sum(client => client.ReceivedBytes) - baselineReceived,
                TransportPingP95Milliseconds = Percentile(latencies.Select(value => (double)value), 0.95),
                DriverLoopP95Milliseconds = Percentile(loopTimes, 0.95),
                ChunkTransferP95Milliseconds = Percentile(clients.SelectMany(client => client.ChunkMilliseconds), 0.95),
                PendingChunks = clients.Sum(client => client.PendingChunks),
                Clients = clients.Select(client => new
                {
                    client.Index,
                    client.Name,
                    client.State,
                    client.Error,
                    client.SpawnedMounted,
                    Position = LoadPosition.From(client.Position),
                    AuthoritativePosition = LoadPosition.From(client.AuthoritativePosition),
                    client.PlayingSeconds,
                    client.ReceivedPackages,
                    client.CompletedChunks,
                    client.RetriedChunks,
                    client.PendingChunks
                })
            };
            try
            {
                File.WriteAllText(Path.Combine(options.Output, "manifest.json"), manifestNode.ToJsonString(_json));
                File.WriteAllText(Path.Combine(options.Output, "summary.json"), JsonSerializer.Serialize(summary, _json));
            }
            finally
            {
                foreach (var client in clients)
                {
                    client.Dispose();
                }
            }
        }

        return failure == null && measurementAt.HasValue ? 0 : 1;
    }

    public static double? Percentile(IEnumerable<double> values, double quantile)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? null : sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * quantile) - 1, 0, sorted.Length - 1)];
    }
}
