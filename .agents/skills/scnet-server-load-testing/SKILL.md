---
name: scnet-server-load-testing
description: Run SCNET ServerLoadTool thin protocol clients to establish server load baselines and compare idle, shared-area movement, or distributed exploration. Use for simple server concurrency and capacity measurements; not for Agent gameplay, UI automation, or replacing real multiplayer clients.
---

# SCNET Server Load Testing

Read [Doc/ServerLoadTesting.md](../../../Doc/ServerLoadTesting.md) for the tool contract.
Use `scnet-debugging` for isolated server/canary startup and teardown, and `scnet-agent-workspace`
for retained outputs. Use `scnet-network-stability` for capacity criteria or impaired networks.

## Establish a baseline

1. Confirm that the target is an authorized, disposable test server. This tool creates players and
   generates terrain; a request to benchmark does not authorize stressing an unrelated deployment.
2. Build `ServerLoadTool` and run `dotnet test Tests/ServerLoadTool.Test/`. Reuse evidence while code is unchanged.
3. Start an isolated unmodded Creative server with fixed terrain/seed and explicit ports. Record build,
   world, hardware and whether terrain was already generated. Do not change the game runtime modes.
4. Keep a real GUI canary for user-visible behavior. Begin with one load client and confirm Playing
   and terrain convergence before increasing population. Count the canary against the world limit.
5. Run a bounded workload, for example:

```bash
dotnet run --project Tools/ServerLoadTool --no-build -- \
  --host 127.0.0.1 --port 31987 --clients 4 --workload shared \
  --ramp 1 --warmup 10 --duration 30 --visibility 32 --seed 1 \
  --output .agent-work/load-test/artifacts/shared-4
```

Add `--server-pid` for same-host resources and `--server-http-port` for diagnostics; supply the token
only through `SCNET_LOAD_TOKEN`. Verify that they refer to the actual target, never record credentials.
Do not overwrite an output directory or repeat a failed scenario without inspecting its summary/events.

## Interpret results

Inspect the exit code, summary and client errors. Compare measurements for the same workload.
Use the [output reference](../../../Doc/ServerLoadTesting.md#输出与解读) for counter and latency
definitions. Keep driver saturation, admission rejection and server saturation distinct.

Increase population only while tick, convergence, memory, disconnect and GUI observation criteria
hold. Use fresh instances for cold runs and record existing terrain and players for warm runs.
Report capacity for the measured workload.

Stop load clients normally, then stop the server/canary, preserve relevant logs, and remove only
instances created for the experiment. The tool itself disconnects clients but never deletes server data.
