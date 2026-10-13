using System.Net;
using System.Runtime.InteropServices;

using Engine.Network;

using NetworkProbe;

ProbeBinding binding;
string? outputDirectory;
try
{
    binding = ProbeBinding.Parse(ref args, out outputDirectory);
}
catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

if (args.Length < 3 || !Uri.TryCreate(args[1], UriKind.Absolute, out var control) ||
    (args[2] != "auto" && !IPEndPoint.TryParse(args[2], out _)) ||
    (args[0] == "scenario-session" && args[2] != "auto") ||
    (args[0] is "host" or "scenario" or "scenario-relay" or "scenario-session" or "scenario-lease" or "scenario-candidate-lease" ? (args[0] == "host" ? args.Length is < 3 or > 4 : args.Length != 3) : (args[0] is not "client" and not "client-recovery") || (args.Length is < 4 or > 6)))
{
    Console.Error.WriteLine("Usage: NetworkProbe host|scenario|scenario-relay|scenario-session|scenario-lease|scenario-candidate-lease <control URL> <auto|beacon IP:port>");
    Console.Error.WriteLine("       NetworkProbe client|client-recovery <control URL> <auto|beacon IP:port> <join code> [prepare delay seconds] [Disabled|Allowed|Required]");
    Console.Error.WriteLine("       Options: --bind-ipv4 ADDRESS --bind-ipv6 ADDRESS --local-port PORT --output DIRECTORY");
    return 2;
}

using var diagnostics = Environment.GetEnvironmentVariable("SCNET_PROBE_TRACE") == "1" ? new ProbeDiagnostics() : null;
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(args[0] is "scenario-lease" or "scenario-candidate-lease" ? 120 : 45));
await using var parentCancellation = new ProbeParentCancellation(deadline);
ConsoleCancelEventHandler interrupt = (_, context) =>
{
    context.Cancel = true;
    deadline.Cancel();
};
Console.CancelKeyPress += interrupt;
using var termination = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    deadline.Cancel();
});
ProbeReport? report = null;
try
{
    report = new ProbeReport(outputDirectory, args[0], binding);
    var beacon = args[2] == "auto" ? null : IPEndPoint.Parse(args[2]);
    var probe = new ProbeRunner(control, beacon, binding, report);
    if (args[0] is "scenario" or "scenario-relay" or "scenario-session" or "scenario-lease" or "scenario-candidate-lease")
    {
        await ProbeScenario.RunAsync(control, beacon, args[0] is "scenario-relay" or "scenario-lease", args[0] == "scenario-session", args[0] is "scenario-lease" or "scenario-candidate-lease" ? 70 : 0, binding, report, deadline.Token);
    }
    else if (args[0] == "host")
    {
        var holdSeconds = args.Length == 4 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(holdSeconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(holdSeconds, 90);
        deadline.CancelAfter(TimeSpan.FromSeconds(45 + holdSeconds));
        await probe.RunHostAsync(deadline.Token);
    }
    else
    {
        var delaySeconds = args.Length >= 5 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(delaySeconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(delaySeconds, 90);
        deadline.CancelAfter(TimeSpan.FromSeconds(45 + delaySeconds));
        var relayPolicy = args.Length == 6 ? Enum.Parse<RelayPolicy>(args[5], ignoreCase: true) : RelayPolicy.Allowed;
        await probe.RunClientAsync(args[3], TimeSpan.FromSeconds(delaySeconds), relayPolicy, deadline.Token, verifyRecovery: args[0] == "client-recovery");
    }
    report.Complete();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL {exception.GetType().Name}: {exception.Message}");
    report?.Complete(exception);
    return 1;
}
finally
{
    Console.CancelKeyPress -= interrupt;
    report?.Dispose();
}
