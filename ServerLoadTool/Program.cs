namespace ServerLoadTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("""
                ServerLoadTool — SCNET protocol load testing only. Use an isolated Creative test server.
                --output DIR (required, empty) --host HOST --port PORT --clients 1..200
                --workload idle|shared|explore --ramp SECONDS_PER_ARRIVAL (0..60)
                --warmup SECONDS (0..3600) --duration SECONDS (1..3600) --join-timeout SECONDS (1..600)
                --visibility BLOCKS (32..128) --speed BLOCKS_PER_SECOND (0..16) --seed INTEGER
                --server-pid PID (optional, local resource sampling)
                --server-http-port PORT (optional, local diagnostics; SCNET_LOAD_TOKEN environment variable required)
                No rendering, Agent control, client physics or compatibility adapters. No privileged commands.
                """);
            return args.Contains("--help") ? 0 : 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        try
        {
            return new LoadRun(LoadOptions.Parse(args)).Execute(cancellation.Token);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }
}
