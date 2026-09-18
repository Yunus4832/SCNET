# Layout and Responsive Composition

## Screen structure

Start from the shared styles in `Content/Assets/Styles`:

- `ScreenContentArea` for the primary content container.
- `ScreenDescriptionArea` for compact explanatory text near the bottom.
- `ScreenBottomActionArea` or `ScreenActionPanel` for bottom operations.
- `ScreenBottomActions` for simple legacy button rows when an `ActionPanelWidget` is not warranted.
- `Area`, `DialogArea`, and `TextBoxArea` for established surface treatment.

Inspect their current XML instead of copying dimensions into new page markup. If multiple Screens need the same correction, update or introduce a narrowly named shared style and walk its consumers.

## Visual center and wide screens

SCNET must work on desktop windows and mobile displays wider than 16:9.

- Keep the composition recognizably centered, but allow the visual center to sit modestly right of the geometric center for right-hand reach and the existing SCNET character.
- Do not move every field to the far right. Use left-side labels, descriptions, previews, or navigation to balance the page when meaningful.
- Avoid fixed offsets that only fit one aspect ratio. Prefer containers, alignment, stable control widths, and weighted whitespace.
- When a wide layout exposes large empty regions, first reconsider grouping and visual balance; do not automatically stretch every control across the screen.

`WorldOptions` is a useful reference for a right-weighted composition that still uses the left side. It is a reference, not a template that must be copied literally.

## Form rows and alignment

- Give peer controls a common visible leading edge and, when appropriate, a common interactive width.
- Account for internal margins. A button, slider, text box, checkbox, and selection widget can share an outer X coordinate yet still look misaligned.
- Prefer correcting shared widget padding or styles when the mismatch affects every use. Review other Screens after global changes rather than preserving a known misalignment with local offsets.
- Keep labels stable when inline controls change state. Do not center an entire row in a way that makes its label jump as selected text or control width changes.
- Slider tracks should remain comfortably long and visually comparable with same-level buttons. Explanatory value text may extend beyond the track; do not sacrifice most of the drag range to reserve excessive label width.
- Use stable control widths for forms. Allow layout compression when space is genuinely insufficient, but do not let routine window resizing make one row visibly shorter than its peers.

## Vertical rhythm

- Keep the top content margin, content-to-description gap, description-to-action gap, and bottom inset consistent across neighboring Screens.
- Explanatory text is subordinate: use a smaller font and compact reserved height so it does not compete with the main content or force the action bar upward.
- Bottom action buttons should remain comfortably sized. Reduce unused container height and gaps before shrinking the buttons themselves.
- Large editor panels may use available space to make tables and controls readable, but their internal rows should still follow a deliberate rhythm.

## Text and localization

- All user-facing text must use the localization system and update correctly when the language changes during the process lifetime.
- Do not cache localized selected text separately from the refreshed item collection.
- Test mentally or visually with longer translations, not only concise Chinese strings.
- Use single-line ellipsis for source names, server names, and similar identifiers when truncation preserves recognition. Prefer truncation over arbitrary data-model length limits unless the protocol or domain truly requires a limit.
- Use the same punctuation convention as sibling entries. Explanatory menu descriptions generally omit a terminal full stop when their peers do.
