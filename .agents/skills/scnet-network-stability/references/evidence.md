# Network experiment evidence

Create one artifact directory per run, normally `.agent-work/<task-id>/artifacts/<run-id>/` under
`scnet-agent-workspace`. In addition to the evidence required by `scnet-debugging`, keep:

```text
<artifact-directory>/
  manifest.json
  proxy-events.jsonl
  proxy.stdout.log
  server.log
  client.log
  process-metrics.jsonl
  summary.json
  report.md
```

`manifest.json` records the Git revision and dirty state, exact commands, timestamps, instance names,
ports, world, visibility, profile, seed, workload, duration, hardware summary, and pass/fail criteria.
Do not include tokens, passwords, or unrelated settings.

`proxy-events.jsonl` is produced by `NetworkDamageTool --events`. Preserve it even when the run fails.
Capture server and client logs from their instance log directories after all processes stop.

For capacity runs, `process-metrics.jsonl` should include timestamp, client count, server CPU, working
set, thread count, and any available tick/network counters. Preserve the load-client process metrics
separately so test-driver saturation is not mistaken for server saturation.

`summary.json` contains machine-readable outcomes and percentile measurements. `report.md` states:

1. hypothesis and scenario;
2. baseline and damaged/load results;
3. first correctness failure or first breached service objective;
4. whether queues converged after new work stopped;
5. tested scope and untested boundaries;
6. recommended optimization tied directly to observed evidence.

Never infer a cache hit, retransmission, or permanent chunk from packet counts alone. Application-level
instrumentation or correlated logs are required for those claims.

## Comparable performance evidence

Record production and fixture identities separately, including a binary hash when a dirty tree makes
the Git revision insufficient. Pair the same workload, seed, visibility, participant/dependency
counts, observation windows, and relevant warm-up history. Confirm these conditions actually held;
for example an unexpected spawn boat changes an allegedly player-only workload.

Report upstream and downstream independently, together with CPU, allocation/GC, queue backlog,
and routing/serialization costs when available. State CPU units (for example one-core equivalent)
and absolute changes as well as percentages, especially with a small baseline. Bandwidth savings
alone do not prove overall performance improved. Missing old counters remain missing; do not infer
packet-type costs from total proxy traffic or invent matching historical measurements.

Identify terrain-loading tails, diagnostic overhead, and unequal warm-up as confounders rather than
claiming a pure steady-state or AOI-only causal result. Add complexity such as caches only when the
measured bottleneck justifies it. Keep reliable-event latency separate from eventual convergence:
after stopping new work, observe whether backlog drains and final authority matches. Passing that
recovery test does not erase a previously failed latency objective.
