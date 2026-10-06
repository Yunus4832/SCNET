# Gameplay fixtures and lifecycle evidence

## Drive the behavior being tested

Use a temporary Mod fixture when normal commands or Engine automation cannot prepare a repeatable
scenario efficiently. Keep it outside production APIs and record its source/package identity.
Use normal game operations to prepare terrain, move a participant, or invoke an interaction entry
point, then observe the resulting simulation. State which inputs are fixture-driven.

Do not directly set the result being tested: injecting a voltage, replicated cache, inventory result,
or moving-block trajectory cannot prove its production calculation or propagation works. If UI
submission is part of the hypothesis, submit through discovered Engine controls, not the handler's
final mutation method. A normal API test is useful but is not automatically an end-to-end UI test.

## Validate the fixture's assumptions first

Check the actual world and mode before a long comparison. A fixed seed does not prove a player has
no initial mount, that adjacent terrain is loaded, or that an inventory reports finite stack counts.
Confirm relevant participant IDs, dependency objects, loaded contents, and baseline state.

Choose predicates from the semantics under test. Terrain contents can be ready before rendering
light is fully valid. Creative inventory slot counts can represent unlimited availability rather
than the number just picked up. Compare the slot value/design reference when that is the contract.

Separate connection readiness, scenario prerequisites, action completion, and observed convergence.
Give each needed prerequisite a deadline. For a controlled loaded-world experiment, require ready
topology before its clock starts; this does not prove cold-load latency is acceptable.

When a checker or fixture is wrong, preserve its failed result and explain the correction. Define
the corrected criterion before rerunning; do not silently move windows or loosen tolerances to pass.
Distinguish a product failure, a fixture failure, and a run that never exercised the intended event.

## Cover the relevant lifecycle boundary

For state referenced by inventory, resources, or saved players, test the boundary changed by the
implementation: for example create/use, leave/return, disconnect, save, reload, and reconnect.
Do not require this entire sequence for unrelated rendering or parser changes.

A dictionary registration is not proof that the corresponding entity is attached or its inventory
is available to scanners. During restore, verify both serialized references and live entity readiness.
An offline record can remain while its owner reconnects: ignoring it too early can delete referenced
resources, while considering its stale values after the live entity is ready can prevent collection.

For despawn or pickup, correlate object identity with authoritative state and the resulting inventory;
a client count falling to zero alone cannot distinguish pickup, destruction, or loss of interest.
For persistence, inspect saved references and definitions, then reload through the normal path.

## Keep observation and teardown attributable

Sample often enough to cover the predefined stable window and record transient states separately.
Use a bounded wait for the actual last required sample, not an unrelated elapsed-time counter.
Scope cumulative instance logs to this run while preserving the complete original evidence safely.

Retain exact process/session handles and obtain real exit status. After an observation timeout or
lost handle, inspect the same process; do not restart or invent an exit code. Verify graceful save
when persistence is part of the test. Capture the prior Mod profile before changing it and restore
that profile after stopping the session, rather than assuming a global default.
