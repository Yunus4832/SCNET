# Weak-network experiments

## Topology

Use an explicit remote session through one proxy:

```text
GUI client -> 127.0.0.1:28989 NetworkDamageTool -> 127.0.0.1:28987 Headless server
```

Build and verify the proxy first:

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

## Profiles

Run the no-damage baseline before the damaged profile. Useful starting profiles are:

| Profile | One-way latency | Jitter | Loss | Downlink |
|---|---:|---:|---:|---:|
| baseline | 0 ms | 0 ms | 0% | unlimited |
| mild | 50 ms | 20 ms | 2% | 4 Mbps |
| poor-wifi | 120 ms | 80 ms | 8% | 1.5 Mbps |

The first proxy version applies a fixed profile for the whole process. Use separate runs for baseline
and damaged comparisons. A full outage/recovery scenario requires restarting the proxy with the same
listen port or extending the tool with scripted phases; report that limitation rather than claiming
an outage-recovery result from steady loss.

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
