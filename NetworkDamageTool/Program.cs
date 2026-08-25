using NetworkDamageTool;

if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
{
    DamageProxyOptions.PrintHelp();
    return 0;
}

try
{
    var options = DamageProxyOptions.Parse(args);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    if (options.Duration is { } duration)
    {
        cancellation.CancelAfter(duration);
    }

    await using var proxy = new DamageProxy(options);
    Console.WriteLine($"Network damage proxy listening on {options.ListenEndPoint}, forwarding to {options.TargetEndPoint}.");
    Console.WriteLine($"Seed={options.Seed}; upstream={options.Upstream}; downstream={options.Downstream}");
    await proxy.RunAsync(cancellation.Token);
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"ERROR: {exception.Message}");
    return 1;
}
