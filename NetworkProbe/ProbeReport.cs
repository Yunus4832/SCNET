using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Engine.Network;

namespace NetworkProbe;

internal sealed class ProbeReport : IDisposable
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly StreamWriter? _events;
    private readonly StreamWriter? _samples;
    private readonly Lock _gate = new();
    private string _phase = "starting";

    public string? DirectoryPath { get; }

    public ProbeReport(string? directory, string mode, ProbeBinding binding)
    {
        if (directory is null)
        {
            return;
        }
        DirectoryPath = Path.GetFullPath(directory);
        if (Directory.Exists(DirectoryPath) || File.Exists(DirectoryPath))
        {
            throw new IOException("Probe output directory must not already exist.");
        }
        Directory.CreateDirectory(DirectoryPath);
        _events = new StreamWriter(new FileStream(Path.Combine(DirectoryPath, "events.jsonl"), FileMode.CreateNew))
        {
            AutoFlush = true
        };
        _samples = new StreamWriter(new FileStream(Path.Combine(DirectoryPath, "samples.jsonl"), FileMode.CreateNew))
        {
            AutoFlush = true
        };
        var binaries = new[] { typeof(ProbeReport).Assembly.Location, typeof(NetworkHost).Assembly.Location }
            .Select(path => new { Name = Path.GetFileName(path), Sha256 = HashFile(path) }).ToArray();
        WriteFile("manifest.json", new
        {
            Mode = mode,
            StartedAt = DateTimeOffset.UtcNow,
            ProcessId = Environment.ProcessId,
            Binding = new { IPv4 = binding.IPv4Address.ToString(), IPv6 = binding.IPv6Address.ToString(), binding.Port },
            Binaries = binaries
        });
    }

    public void Record(string phase, object? details = null, bool advancePhase = true)
    {
        lock (_gate)
        {
            if (advancePhase)
            {
                _phase = phase;
            }
            _events?.WriteLine(JsonSerializer.Serialize(new
            {
                Phase = phase,
                ElapsedMilliseconds = _elapsed.Elapsed.TotalMilliseconds,
                Utc = DateTimeOffset.UtcNow,
                Details = details
            }));
        }
    }

    public void Complete(Exception? failure = null)
    {
        if (DirectoryPath is null)
        {
            return;
        }
        var rendezvous = failure as RendezvousException;
        WriteFile("summary.json", new
        {
            Passed = failure is null,
            LastPhase = _phase,
            ElapsedMilliseconds = _elapsed.Elapsed.TotalMilliseconds,
            Failure = failure is null ? null : new
            {
                Type = failure.GetType().Name,
                failure.Message,
                Operation = rendezvous?.Operation,
                Kind = rendezvous?.Kind.ToString(),
                Code = rendezvous?.ErrorCode,
                Status = rendezvous?.StatusCode
            }
        });
    }

    public void Sample(object details, string? phase = null)
    {
        lock (_gate)
        {
            _samples?.WriteLine(JsonSerializer.Serialize(new
            {
                Phase = phase ?? _phase,
                ElapsedMilliseconds = _elapsed.Elapsed.TotalMilliseconds,
                Utc = DateTimeOffset.UtcNow,
                Details = details
            }));
        }
    }

    private void WriteFile(string name, object value)
    {
        using var file = new FileStream(Path.Combine(DirectoryPath!, name), FileMode.CreateNew);
        JsonSerializer.Serialize(file, value, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    public void Dispose()
    {
        _events?.Dispose();
        _samples?.Dispose();
    }
}
