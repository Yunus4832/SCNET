---
name: scnet-network-stability
description: Run repeatable SCNET weak-network, recovery, multiplayer transport, and server-capacity experiments with NetworkDamageTool, isolated instances, structured evidence, and explicit convergence or capacity criteria. Use when diagnosing lag, packet loss, permanent chunk loading, delayed hits, movement jitter, network recovery, queue buildup, or concurrent-player limits; use scnet-debugging alone for ordinary runtime bugs without network impairment or load.
---

# SCNET Network Stability

Run from the repository root. Load and follow `scnet-debugging` for process startup, instance isolation,
runtime logs, evidence preservation, and teardown. Never run an automated experiment against the
default instance.

## Select the experiment

- For latency, jitter, loss, throttling, recovery, chunk convergence, movement, creatures, or hit
  responsiveness, read [references/weak-network.md](references/weak-network.md).
- For maximum concurrent players, server saturation, or scalability, read
  [references/capacity.md](references/capacity.md).
- For either mode, read [references/evidence.md](references/evidence.md) before starting processes.

## Shared requirements

1. State the hypothesis and pass/fail criterion before the run. A process remaining alive is not a
   network-stability result.
2. Build and test `NetworkDamageTool` before an impairment run. Use explicit loopback ports and one
   proxy instance per real client.
3. Record the exact damage profile and seed. Never describe an unseeded random run as reproducible.
4. Separate protocol correctness from experience quality. Permanent missing chunks, lost required
   events, or failure to recover are correctness failures; latency percentiles and visible jitter are
   experience measurements.
5. Change one independent variable at a time for comparison runs. Always retain a no-damage baseline
   from the same build and scenario.
6. Stop all processes cleanly, copy evidence, and remove only instances created by the current run.
7. Report observed limits only for the tested workload and hardware. Do not convert a synthetic
   transport-client result into a claim about the same number of fully simulated GUI players.
