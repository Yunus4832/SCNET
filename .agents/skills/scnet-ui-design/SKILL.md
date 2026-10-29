---
name: scnet-ui-design
description: "Design, implement, preview, or review SCNET game and mod UI layouts, widgets, dialogs, responsive behavior, and interaction styling. Use UiPreviewTool for isolated visual checks of C# or XML components; do not use for ContentServer web UI or shader-only testing."
---

# SCNET UI Design

Preserve a coherent game UI across desktop, Android, window resizing, and localization. Treat existing shared widgets and style assets as the source of truth for concrete dimensions and colors; this skill records design intent and selection criteria; references identify current style baselines without creating a second source of UI constants.

## Working method

1. Inspect the target Screen or panel and at least one current comparable implementation before choosing a layout.
2. Identify whether the problem comes from the page, a shared style, or a shared widget. Prefer fixing the shared owner when the behavior should be consistent everywhere, then review its consumers for regressions.
3. Preserve the established visual hierarchy and interaction semantics. Do not solve one screenshot with arbitrary offsets, duplicated controls, or per-Screen exceptions when a reusable rule exists.
4. Check the relevant compact/wide layouts, long localized text, enabled/disabled states, and expanded controls. Prefer `UiPreviewTool` for visual evidence when the component can be isolated; use the game when correctness depends on input, world state, or networking.
5. Keep visual cleanup separate from unrelated gameplay behavior. Follow the repository C# style and proportional validation skills when implementation changes require them.

## Required references

- Read [references/layout.md](references/layout.md) when changing Screen composition, margins, sizing, alignment, responsive behavior, labels, or form rows.
- Read [references/components.md](references/components.md) when choosing or changing actions, selectors, dialogs, sliders, text boxes, HUD panels, colors, or input ownership.
- Read [references/preview.md](references/preview.md) when preparing isolated render scenarios or inspecting preview screenshots and layout reports.
- Read [references/review-checklist.md](references/review-checklist.md) before completing any material UI change or UI review.

## Visual validation

Use the maintained `UiPreviewTool` rather than recreating window, graphics, font, atlas, and screenshot bootstrap code. The tool provides rendering and basic layout checks; this skill provides scenario selection and visual judgment. Its CLI and factory contract are documented in [UiPreviewTool](../../../UiPreviewTool/README.md).

Choose checks proportional to the change. A successful screenshot or exit code does not prove interaction correctness. State which data was fixture-built and which behavior was actually exercised.

## Core preferences

- Aim for an overall centered composition with a modest rightward visual bias on wide screens, not a column pinned to the right edge. Balance secondary information on the left when the page naturally supports it.
- Reuse the shared Screen area styles and standard widgets. Neighboring Screens should not visibly jump in top margin, content width, description height, or bottom action spacing.
- Align the visible edges of controls, not only their outer layout boxes. Internal padding, labels, checkboxes, sliders, and text fields must form a stable pixel-level column when presented together.
- Prefer centered, placeholder-led input dialogs over a separate left label column; keep persistent labels where users need them after entering a value. See the component guidance for exceptions and input-state checks.
- Reuse semantic button colors and role-specific heights from existing styles; top tabs and action bars should remain compact and mutually consistent.
- Keep controls comfortably usable without wasting vertical space. Favor consistent heights and compact gaps over shrinking primary buttons or leaving large dead bands.
- Design for localization and resizing from the start. Use concise localized labels, ellipsis where identity remains clear, and stable widths where changing window size would otherwise make adjacent controls drift.
- Preserve the user's explicit design direction over these defaults. If a request conflicts with an existing preference, implement the request and update this skill only when the new choice is intended as a reusable convention.
