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
2. Build `ServerLoadTool` and run `dotnet test ServerLoadTool.Test`. Reuse evidence while code is unchanged.
3. Start an isolated unmodded Creative server with fixed terrain/seed and explicit ports. Record build,
   world, hardware and whether terrain was already generated. Do not change the game runtime modes.
4. Keep a real GUI canary for user-visible behavior. Begin with one load client and confirm Playing
   and terrain convergence before increasing population. Count the canary against the world limit.
5. Run a bounded workload, for example:

```bash
dotnet run --project ServerLoadTool --no-build -- \
  --host 127.0.0.1 --port 31987 --clients 4 --workload shared \
  --ramp 1 --warmup 10 --duration 30 --visibility 32 --seed 1 \
  --output .agent-work/load-test/artifacts/shared-4
```

Add `--server-pid` for same-host resources and `--server-http-port` for diagnostics; supply the token
only through `SCNET_LOAD_TOKEN`. Verify that they refer to the actual target, never record credentials.
Do not overwrite an output directory or repeat a failed scenario without inspecting its summary/events.

## Interpret results

- Compare workloads separately: idle, bounded shared-area movement, and explore.
- Inspect the process exit code, `summary.json` and per-client errors. A connection is not Playing;
  a terrain frame or fragment is not a completed chunk.
- Use measurement samples for bandwidth and tick-counter deltas. Maximum tick values cover the
  server process lifetime; transport ping is not hit-response latency or application command latency.
- Track driver CPU and loop time as well as server CPU. Driver saturation invalidates a server limit.
- Step client counts conservatively. Stop increasing when tick objectives, convergence, memory bounds,
  disconnect rate or canary experience fails. Distinguish world capacity rejection from saturation.
- Do not describe synthetic flight snapshots as full physics, combat, collision or Agent gameplay.
  Report tested workload capacity only; leave unmeasured limits unknown.
- Compare fresh instances for cold runs, or explicitly preserve equal cache/creation history for warm
  runs. Repeated populations leave offline players and terrain in reused worlds.

Stop load clients normally, then stop the server/canary, preserve relevant logs, and remove only
instances created for the experiment. The tool itself disconnects clients but never deletes server data.
