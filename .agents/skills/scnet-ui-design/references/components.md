# Component and Interaction Choices

## Action panels

Use `ActionPanelWidget` for a Screen's primary and secondary bottom operations.

- Primary actions are frequent and forward-moving, such as connect, start, create, save, or refresh when refresh is central to the page.
- Secondary actions are contextual, destructive, administrative, or less frequent. Do not place navigation to unrelated Screens in the secondary action drawer.
- Order actions by workflow, not alphabetically. Keep destructive actions last unless the user specifies otherwise.
- Use the established positive green treatment for a primary commit/start/connect action and red for delete or destructive removal. Disabled state must remain visually distinct.
- Configure weights to use the available row rather than leaving unused slots. Weight `0` is valid for an absent slot. Choose ratios from visual and task importance; do not blindly use equal fixed button widths on wide screens.
- Keep buttons comfortably large and mutually consistent. Adjust the action-area container and gaps before reducing button height.

Read the current `ActionPanelWidget` implementation and `ActionPanelWidget.xml` before relying on its slot, weight, accessory, or trailing-action behavior.

## Selection controls

- Use `SelectionDrawerWidget` for selectors near Screen edges or where the expanded list should escape its layout container and overlay surrounding content.
- Use `InlineSelectionWidget` inside constrained forms or dialogs when expansion should remain within the component's allocated flow and a popup would visually overflow the container.
- Use a dedicated dialog when the choice needs rich descriptions, confirmation, search, multi-selection, or substantially more space than a compact list.
- Keep the collapsed control and expanded item heights visually related. Ensure separators, borders, and list surfaces end at the same visible width.
- Center short selector text by default. Use ellipsis for long text and verify the selected header refreshes after item or language changes.

Do not reintroduce local clipping or overflow workarounds without checking whether the shared selector already owns popup placement and bounds handling.

## Dialogs and notifications

- Use shared dialog surfaces and rounded borders; do not hand-build a slightly different frame for each feature.
- Prefer `MessageDialog` adaptive height for notification and presentation content. Set its minimum or maximum height only when the content has a real readability constraint; avoid hard-coded per-call heights that merely compensate for the old fixed layout.
- Choose cancel behavior explicitly. Escape/back must dismiss or invoke the intended cancellation path without activating an affirmative action or leaking input to the underlying Screen.
- Use an in-game toast for successful, informational, or transient events that do not require a decision. Do not interrupt play with a confirmation dialog merely to report completion.
- Use dialogs for decisions, destructive confirmation, required input, or errors that the user must acknowledge.

## Inputs, sliders, and checkboxes

- Use shared text box, button, slider, and checkbox styles so visible borders align across a form.
- Preserve an adequate slider track; do not let its value label consume most of the interaction width.
- Keep checkbox and text-box internal padding aligned with buttons and selectors at the shared component level when possible.
- For on-screen hexadecimal or similar keypads, also support physical keyboard input when meaningful. Prevent game shortcuts from firing while an editor owns those keys.

## HUD panels and input ownership

`ComponentGui.ModalPanelWidget` is the single active HUD-panel host used by inventory, clothing, crafting, message, player, and editor panels. It is not the same as a `DialogsManager` dialog.

- Ordinary HUD panels may retain global shortcuts such as pressing the inventory key again to close.
- A panel that edits text or consumes gameplay-mapped keys should implement `IGameInputCapturingWidget` so `ComponentGui.IsGameInputCaptured` suppresses underlying game and message shortcuts.
- Do not hard-code specific editor widget types in `ComponentInput` or `GameWidget`.
- Keep Escape/back behavior deliberate and prevent the same input event from reaching the underlying Screen or panel.

## Shared change boundary

Before adding a local size, margin, color, or input exception, ask:

1. Is the mismatch present in other consumers of this widget?
2. Does the shared component already expose the intended property?
3. Would a shared fix make established Screens more consistent?
4. Which consumers could legitimately depend on the current behavior?

Prefer the shared fix when the rule is universal, then inspect representative consumers. Prefer a page-level adjustment when the difference expresses real page hierarchy rather than compensating for a component defect.
