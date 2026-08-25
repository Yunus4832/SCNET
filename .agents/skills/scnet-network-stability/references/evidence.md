# Network experiment evidence

Create one artifact directory per run. In addition to the evidence required by `scnet-debugging`, keep:

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
