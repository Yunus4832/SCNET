---
name: scnet-network-probe
description: Operate SCNET NetworkProbe for transport-only signaling, direct/relay paths, session lookup, preparation leases, cancellation and calibrated NAT experiments. Prefer Linux with the skill-owned Python/Bash helpers; use scnet-network-stability for real-game weak-network behavior and scnet-server-load-testing for capacity.
---

# SCNET Network Probe

Use Linux for these experiments. The helpers require Python 3, and isolated NAT/address-family
experiments additionally use Linux namespaces, nftables and related kernel facilities. They are
Agent maintenance tools under this skill, not user-facing commands or probe release contents.
Do not port the helpers to Windows merely to complete a routine probe investigation; use an
authorized Linux host, or report that the requested experiment cannot run in the available environment.

Read [NetworkProbe/README.md](../../../NetworkProbe/README.md) for the native .NET probe contract.
Read [references/experiments.md](references/experiments.md) only for the selected local, NAT,
address-family, MTU or loss experiment. Use `scnet-agent-workspace` for retained evidence under
`.agent-work/<task-id>/artifacts/<run-id>/`.

## Choose and run the experiment

- State the hypothesis and acceptance criterion. Run only the relevant scenario, not every matrix.
- Build NetworkProbe and RendezvousServer with `dotnet build`; reuse unchanged build evidence.
  For code regressions select relevant Rendezvous.Protocol.Test, Engine.Network.Test or
  RendezvousServer.Test tests directly.
- For ordinary loopback use `scripts/local-lab.py`; it owns temporary signaling and probe processes,
  checks readiness and convergence, and saves logs/results. `--mode` accepts `scenario`,
  `scenario-relay`, `scenario-session`, `scenario-lease` or `scenario-candidate-lease`.
  Cancellation uses `--cancel-after-path` with either lease mode, not a separate mode value.
  See the reference for supported options.
- For isolated address families use `scripts/local-lab.sh`; for calibrated NAT use
  `scripts/nat-lab.sh`. Never invoke the namespace-mutating Python implementation directly.
  Wrappers verify that networking changes occur outside the host namespace.
- For real cross-network testing, run the native `host` and `client` on different exits with an
  authorized signaling service. Public signaling requires trusted HTTPS. Use `scnet-debugging`
  when the hypothesis needs actual game clients/devices rather than the transport-only probe.

## Inspect and finish

A successful scenario requires exit 0, passing parent/host/client summaries, reliable echo/completion,
and final host/client samples with HostStopped true and every Resources counter zero. Path preparation
alone is not connection success. For intentional cancellation or blocked paths require the intended
failure category and the same cleanup evidence.

Preserve run configuration, topology, assembly hashes and both endpoints' records. Inspect Path and
AddressFamily: HolePunch alone does not prove IPv6, and loopback PASS does not prove NAT traversal.
A NAT result requires the helper's independent mapping/filter calibration, not just successful Engine
traffic. Do not treat historical matrix results as a newly verified run.

Helpers stop owned processes on success/failure; still inspect their exit status and cleanup evidence.
Stop any extra processes or containers started for the run. Keep private keys and tickets out of
retained evidence. Distinguish missing Linux facilities or runtime dependencies from protocol failures.
