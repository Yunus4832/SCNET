using System.Diagnostics;
using System.IO.Pipes;
using System.Net;

namespace NetworkProbe;

internal static class ProbeScenario
{
    public static async Task RunAsync(Uri control, IPEndPoint? beacon, bool requireRelay, bool useSessionLocator, int preparationDelaySeconds, ProbeBinding binding, ProbeReport report, CancellationToken cancellationToken)
    {
        using var host = Start("host", control, beacon, binding, report.DirectoryPath, preparationDelaySeconds: preparationDelaySeconds);
        Child? client = null;
        var output = new List<Task<string>>();
        var errors = new List<Task<string>> { host.Process.StandardError.ReadToEndAsync() };
        try
        {
            const string prefix = "READY joinCode=";
            string ready;
            while (true)
            {
                var line = await host.Process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    throw new InvalidOperationException("Host exited before reporting readiness.");
                }
                Console.WriteLine($"host: {line}");
                if (line.StartsWith(prefix, StringComparison.Ordinal) && line.Length >= prefix.Length + 6)
                {
                    ready = line;
                    break;
                }
            }
            report.Record("host-ready");
            var code = useSessionLocator
                ? ready.Split("sessionId=")[1]
                : ready.Substring(prefix.Length, 6);
            client = Start("client", control, beacon, binding, report.DirectoryPath, code, requireRelay, preparationDelaySeconds);
            output.Add(host.Process.StandardOutput.ReadToEndAsync());
            output.Add(client.Process.StandardOutput.ReadToEndAsync());
            errors.Add(client.Process.StandardError.ReadToEndAsync());
            var hostExit = host.Process.WaitForExitAsync();
            var clientExit = client.Process.WaitForExitAsync();
            var firstExit = await Task.WhenAny(hostExit, clientExit).WaitAsync(cancellationToken);
            var firstProcess = firstExit == hostExit ? host : client;
            if (firstProcess.Process.ExitCode != 0)
            {
                var role = firstExit == hostExit ? "host" : "client";
                throw new InvalidOperationException($"Probe {role} failed with exit code {firstProcess.Process.ExitCode}.");
            }
            await Task.WhenAll(hostExit, clientExit).WaitAsync(cancellationToken);
            if (host.Process.ExitCode != 0 || client.Process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Probe exit codes: host={host.Process.ExitCode}, client={client.Process.ExitCode}");
            }
        }
        finally
        {
            try
            {
                if (client is not null)
                {
                    await StopAsync(client);
                }
            }
            finally
            {
                await StopAsync(host);
                foreach (var stream in output)
                {
                    Console.Write(await stream);
                }
                foreach (var stream in errors)
                {
                    Console.Error.Write(await stream);
                }
                client?.Dispose();
            }
        }
        Console.WriteLine("PASS separate-process scenario (local topology)");
        report.Record("scenario-complete");
    }

    private static Child Start(string role, Uri control, IPEndPoint? beacon, ProbeBinding binding, string? outputDirectory, string? joinCode = null, bool requireRelay = false, int preparationDelaySeconds = 0)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("No process executable.");
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(typeof(ProbeScenario).Assembly.Location);
        }
        start.ArgumentList.Add(role);
        start.ArgumentList.Add(control.AbsoluteUri);
        start.ArgumentList.Add(beacon?.ToString() ?? "auto");
        if (joinCode is not null)
        {
            start.ArgumentList.Add(joinCode);
            start.ArgumentList.Add(preparationDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(requireRelay ? "Required" : "Allowed");
        }
        else if (preparationDelaySeconds > 0)
        {
            start.ArgumentList.Add(preparationDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        start.ArgumentList.Add("--bind-ipv4");
        start.ArgumentList.Add(binding.IPv4Address.ToString());
        start.ArgumentList.Add("--bind-ipv6");
        start.ArgumentList.Add(binding.IPv6Address.ToString());
        if (outputDirectory is not null)
        {
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(Path.Combine(outputDirectory, role));
        }
        var name = "scnet-probe-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        start.Environment[ProbeParentCancellation.environmentKey] = name;
        try
        {
            var connected = pipe.WaitForConnectionAsync();
            var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start probe process.");
            return new Child(process, pipe, connected);
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    private static async Task StopAsync(Child child)
    {
        try
        {
            if (!child.Process.HasExited)
            {
                try
                {
                    await child.Connected.WaitAsync(TimeSpan.FromSeconds(2));
                    await child.Pipe.WriteAsync(new byte[] { 1 });
                    await child.Pipe.FlushAsync();
                }
                catch (Exception exception) when (exception is IOException or TimeoutException)
                {
                    // A child that has already lost its control channel still has a bounded exit budget.
                }
                try
                {
                    await child.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
                }
                catch (TimeoutException)
                {
                    if (!child.Process.HasExited)
                    {
                        child.Process.Kill(entireProcessTree: true);
                    }
                }
            }
            await child.Process.WaitForExitAsync();
        }
        finally
        {
            child.Pipe.Dispose();
            try
            {
                await child.Connected;
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }
    }

    private sealed class Child(Process process, NamedPipeServerStream pipe, Task connected) : IDisposable
    {
        public Process Process { get; } = process;

        public NamedPipeServerStream Pipe { get; } = pipe;

        public Task Connected { get; } = connected;

        public void Dispose()
        {
            Pipe.Dispose();
            Process.Dispose();
        }
    }
}
