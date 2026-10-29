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

## Button appearance and sizing

Use the existing role-specific baseline rather than choosing a new shade or height for each Screen.
These values describe the current implementation; the linked widget/style remains authoritative if it changes.

| Role | Current baseline | Owner/reference |
| --- | --- | --- |
| Ordinary form/dialog button | Outer height 60 | [ButtonStyle_160x60](../../../../Content/Assets/Styles/ButtonStyle_160x60.xml), [ButtonStyle_310x60](../../../../Content/Assets/Styles/ButtonStyle_310x60.xml) |
| Top tab | Outer height 56, shared horizontal margins | [ScreenTopTabButton](../../../../Content/Assets/Styles/ScreenTopTabButton.xml), [ScreenTopTabs](../../../../Content/Assets/Styles/ScreenTopTabs.xml) |
| Bottom action bar | Outer height 56 | [ActionPanelWidget](../../../../Content/Assets/Widgets/ActionPanelWidget.xml) |
| Positive action | RGB 50, 150, 35 for both center and bevel | [PlayScreen action colors](../../../../Survivalcraft/Screens/PlayScreen.cs), [HostedWorldScreen](../../../../Content/Assets/Screens/HostedWorldScreen.xml) |
| Destructive action | RGB 150, 50, 35 for both center and bevel | [ManageIdentitiesScreen action colors](../../../../Survivalcraft/Screens/ManageIdentitiesScreen.cs), [HostedWorldScreen](../../../../Content/Assets/Screens/HostedWorldScreen.xml) |

- Use green for forward actions such as start/run, join, install, login, and making an identity the default. Use red for delete, stop, or forgetting saved credentials. Ordinary selectors, cancel/back, and routine dialog confirmation can retain the standard neutral treatment; do not make every affirmative dialog button green.
- Set both `CenterColor` and `BevelColor` when applying a semantic color. Do not introduce a near-matching green/red or recolor label text as a substitute. Enabled active tabs retain legible light text; checking/pressing a tab should not turn its text black.
- Use `ScreenTopTabButton`/`ScreenTopTabs` for peer top tabs. Do not give management pages taller tabs than neighboring Screens. Match button heights within a row; use an existing compact dialog/grid pattern when that role needs it, rather than treating every button as height 60.
- Dimensions above are logical outer layout units, not physical display pixels. [BevelledButtonContents](../../../../Content/Assets/Widgets/BevelledButtonContents.xml) currently adds a 6-unit inset on each side, so outer heights 60/56 have visible heights 48/44. Compare actual visible borders against fields and selectors before adding offsets; equal outer sizes alone do not prove alignment.
- Keep disabled actions visibly disabled. Recover space by reducing unnecessary panel height and gaps before shrinking touch targets or text.

## Selection controls

- Use `SelectionDrawerWidget` for selectors near Screen edges or where the expanded list should escape its layout container and overlay surrounding content.
- Use `InlineSelectionWidget` inside constrained forms or dialogs when expansion should remain within the component's allocated flow and a popup would visually overflow the container.
- Use a dedicated dialog when the choice needs rich descriptions, confirmation, search, multi-selection, or substantially more space than a compact list.
- Keep the collapsed control and expanded item heights visually related. Ensure separators, borders, and list surfaces end at the same visible width.
- Center short selector text by default. Use ellipsis for long text and verify the selected header refreshes after item or language changes.

Do not reintroduce local clipping or overflow workarounds without checking whether the shared selector already owns popup placement and bounds handling.

## List-selection dialogs

Use `ListSelectionDialog` for a lightweight choice that takes effect immediately. The current design keeps the title inside the shared surface, uses a compact title-to-list gap, and has no row dividers or extra confirmation bar.

- Plain text choices are centered. Use `CreateTextItem(name, description, wrapDescription)` for a consistent centered name/details layout; a service endpoint can go on its own subordinate line. Server identity choices keep the name and abbreviated identifier together on one line. Simple choices use a narrower panel than descriptive or illustrated lists.
- Pass `currentItem` when the caller knows the active business value. Its rounded, borderless green fill identifies the current option independently of transient click selection. Keep the fill clearly distinguishable from the black panel and omit a separate right-side tick. Opening the dialog must not invoke the selection callback. The dialog initially brings the current item into view.
- Keep rows equal in height within a list and fit complete rows into the available viewport when possible. `ContentSize` sets preferred panel width and maximum list height; available space still takes precedence. Do not restore oversized fixed panels or erase user scrolling each frame.
- Text rows use ellipsis; descriptions can wrap with a bounded line count. Custom illustrated rows remain supported through the widget factory and should retain readable image/text grouping within the shared item padding.
- Selection closes the dialog; Escape/back or a tap outside cancels without invoking the callback. A tap on the internal title is not an outside tap. Keep these behaviors distinct from marking the current item.

## Dialogs and notifications

- Use shared dialog surfaces and rounded borders; do not hand-build a slightly different frame for each feature.
- Prefer `MessageDialog` adaptive height for notification and presentation content. Set its minimum or maximum height only when the content has a real readability constraint; avoid hard-coded per-call heights that merely compensate for the old fixed layout.
- Choose cancel behavior explicitly. Escape/back must dismiss or invoke the intended cancellation path without activating an affirmative action or leaking input to the underlying Screen.
- Use an in-game toast for successful, informational, or transient events that do not require a decision. Do not interrupt play with a confirmation dialog merely to report completion.
- Use dialogs for decisions, destructive confirmation, required input, or errors that the user must acknowledge.

## Input-dialog composition

- For simple name/address/code/password dialogs, prefer centered fields with placeholders over a separate left-hand label column. Keep fields equal in width and center them with the title and bottom action group. Existing `ContentRepositoryDialog`, `JoinCodeDialog`, and `PasswordDialog` are the references for this composition.
- Place field purpose in the placeholder when the title alone does not identify it. A password dialog can use the first placeholder for its length/format and the second for repeat entry; keep masks and validation unchanged.
- Omit a separate label for a self-explanatory choice button, such as the relay-policy button in `JoinCodeDialog`. Keep its selected text understandable on its own and align its visible border with the fields.
- Placeholders disappear after entry. Keep a persistent label or short description when users must identify similar filled values, understand units, or consult essential instructions. This recommendation is for simple dialogs, not a blanket removal of labels from settings Screens or complex forms.
- Reuse localized field strings where they still express the purpose. Keep long instructions out of titles: use a concise title plus a placeholder or subordinate description. Do not shrink every title to accommodate one long translation.
- Determine placeholder visibility from display content, including IME composition (`TextBoxWidget.HasDisplayText` in the current implementation), rather than focus or committed text alone. Show the hint for an empty focused field, hide it while entering/composing text, and restore it after clearing. Placeholder text is display-only and must not enter saved values, validation, password masks, or callbacks.
- Reuse the existing placeholder composition before introducing another input abstraction. Check empty, prefilled, focused, filled/cleared, password-confirmation, optional remember-password, and error states as applicable. A render fixture can verify layout and prepared states; use real interaction when claiming IME or soft-keyboard correctness.

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
