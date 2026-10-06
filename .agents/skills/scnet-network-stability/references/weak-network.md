# Weak-network experiments

## Topology

Use an explicit remote session through one proxy:

```text
GUI client -> 127.0.0.1:28989 NetworkDamageTool -> 127.0.0.1:28987 Headless server
```

Build and verify the proxy first when current-build evidence is absent or invalidated:

```bash
dotnet test NetworkDamageTool.Test/NetworkDamageTool.Test.csproj
dotnet build Survivalcraft.Linux/Survivalcraft.Linux.csproj
```

Start the server and client with the isolated-instance commands in the `scnet-debugging` multiplayer
reference. Start the proxy before the client:

```bash
dotnet run --no-build --project NetworkDamageTool -- run \
  --listen 127.0.0.1:28989 \
  --target 127.0.0.1:28987 \
  --seed 12345 \
  --up-latency-ms 120 --up-jitter-ms 50 --up-loss 0.05 \
  --down-latency-ms 120 --down-jitter-ms 50 --down-loss 0.05 \
  --down-bandwidth-kbps 1500 \
  --events <artifact-directory>/proxy-events.jsonl
```

Point `--connect` at port `28989`, not the server port. Local broadcast discovery is outside this
experiment.

The proxy keeps separate upstream sockets for discovery and connection UDP endpoints from the first
client IP, preserving response ownership even when replies arrive late. All routes share each
direction's impairment and bandwidth budget. Use one proxy per real client, including clients on the
same IP; restart it between client runs to clear routes (maximum 64 endpoints).

## Profiles

Run the no-damage baseline before the damaged profile. Useful starting profiles are:

| Profile | One-way latency | Jitter | Loss | Downlink |
|---|---:|---:|---:|---:|
| baseline | 0 ms | 0 ms | 0% | unlimited |
| mild | 50 ms | 20 ms | 2% | 4 Mbps |
| poor-wifi | 120 ms | 80 ms | 8% | 1.5 Mbps |

Use separate runs for baseline and damaged comparisons. For a short outage without changing UDP
routes, add `--up-outage-start-seconds 30 --up-outage-duration-seconds 5` and the matching `down`
options. Times are measured since the proxy run began, not since Game readiness; declare a startup
deadline so the outage does not accidentally test bootstrap instead. The start is inclusive and the
end exclusive. Traffic received during the window, and previously delayed traffic whose forwarding
falls inside it, is dropped rather than released later. Outside it the normal direction profile
resumes. Preserve actual drop/forward evidence and application-level convergence, not just survival.

### Align timed impairment with the target event

Do not estimate a return, reconnect, or load interruption from typical application startup duration.
Use a common experiment clock or event-correlated orchestration when their overlap is the hypothesis.
Keep proxy startup, playable readiness, and controlled scenario start as separate timestamps.

With the tool's current JSONL, proxy start can be derived from `timestamp - elapsedMs`. A temporary
fixture and orchestration can share a future start time on the same host; declare a loaded-state
prerequisite and startup deadline, then schedule actions and the proxy outage relative to that time.
This is a test strategy, not a built-in game startup option. For different hosts, account for clock
offset or use acknowledgements rather than assuming their UTC clocks agree.

Verify the actual target-event timestamp lies inside the configured outage and inspect forwarding
and drops in each affected direction. A recovered client after an outage that ended before its
return proves a different scenario. Report data correctness and scenario coverage separately.

If introducing a readiness gate or changing the time origin, record the new experiment contract,
rerun its corresponding baseline, and retain the earlier failure. Do not reinterpret a controlled
loaded-state recovery test as proof that the original cold-load latency problem is fixed.

## Chunk convergence scenario

Use the same world, spawn, visibility distance, movement path, and observation duration in every run.
Record:

- time from client connection to playable state;
- proxy upstream/downstream datagrams, bytes, and drops;
- client/server disconnects and errors;
- permanently missing visible chunks after network conditions normalize;
- server pending chunk-request count and oldest request age when those metrics are available.

A chunk correctness run fails when a chunk remains inside the active area but never reaches loaded and
valid state within the declared recovery window. Visual blank space alone is insufficient evidence:
correlate the coordinate with client and server lifecycle events when instrumentation exists.

## Movement, creatures, and hits

For experience measurements, preserve the same input script and duration. Prefer numeric evidence:

- movement snapshot inter-arrival and correction distance;
- creature snapshot inter-arrival and freeze duration;
- hit input-to-server-result latency P50/P95/P99;
- ping and packet-loss time series.

Do not combine these into one “lag” number. A correctness fix for chunks does not prove movement or hit
responsiveness improved.
