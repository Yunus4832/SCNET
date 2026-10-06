# Gameplay automation

Read [Doc/Automation.md](../../../../Doc/Automation.md) for the maintained command contract.
Discover the live HTTP command list before use; do not infer argument names from text help.
Check the response's success and code before consuming data. Request state Completed means the
command was handled, not that its gesture or navigation finished. Poll data.Status (not state)
and match the returned action Id; bounded input and navigation have separate status commands.

Use an observe–act–confirm loop:

- Read gameplay context and UI context; wait for ready, loaded terrain and stable Screen.
- Choose the normal input gesture or a bounded navigation destination.
- Poll the action-specific status with a deadline, then confirm actual position, target, inventory or UI.
- Preserve screenshots when layout, hit testing or rendering matters.

For text input, discover and tap the TextBoxWidget, send input/text, then read its Text back.
Unnamed textboxes are discoverable too. After closing a modal panel, wait until gameplay context
reports ModalPanel=null; key processing and panel animations are asynchronous.

Prefer `durationSeconds` for walking, flying and sustained digging. A frame count is not a fixed
time interval: uncapped desktop runs can execute hundreds of frames before the next HTTP request.
Relative mouse delta is delivered once, not every frame of the gesture. Adjust from observed camera
direction and target rather than assuming a sensitivity or directly mutating the camera.

For ordinary first-person targeting, prefer view/look_at with a world point, or view/angles for
absolute/relative yaw and pitch. Wait for CameraType=FppCamera (spawn intro can still be active),
poll view/status for aligned, then confirm the actual raycast target before interacting. Angular
tolerance measures direction error, not each Euler axis separately. View/navigation/input actions
cancel each other; do not issue a mouse adjustment while waiting for view alignment. Keep raw mouse
simulation for testing physical input or controlling camera modes not supported by semantic aiming.

Use normal gestures for jump/double-jump flight, selecting hotbar slots and interacting with targets.
Use navigation for walking across loaded terrain; its y coordinate is feet height, not camera height.
Treat arrived, partial_path, stuck, timeout and cancellation as distinct results. Do not introduce a
second player entity, bypass collision or teleport to make a navigation test pass.

Bound inventory widgets are discoverable targets; unbound placeholders are excluded. Their Slot
metadata identifies InventoryId/index/value/count and
Text supplies the display name. Use exact selectors returned for the current UI, and confirm both
source and destination after drag. A drag_queued response proves only scheduling, not transfer.
Slot indexes are local to their inventory; do not select the first widget with index zero without
checking its inventory and selector. Drag durationSeconds defaults to 0.5 (range 0.1–3 seconds);
poll input/status using its returned action ID before checking the destination.
Never call a final inventory mutation handler to claim UI drag coverage.

Flat worlds can start near the ocean. Check Grounded/Immersion and a screenshot before declaring a
walking fixture ready. A preparation Mod remains appropriate for deterministic terrain or objects
that ordinary interaction cannot efficiently prepare, but not for the operation under test.

Control is shared with the user. Stop or cancel your actions before asking for manual observations,
and do not restart navigation after manual takeover without a new task reason. On teardown, cancel
automation, close the world through the actual menu, then close the application. Application exit
is not necessarily exposed to HTTP; discover it instead of assuming it exists.
