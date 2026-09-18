---
name: scnet-ui-design
description: "Design, implement, or review SCNET game UI layouts, shared widgets, dialogs, action areas, responsive behavior, and interaction styling. Use for changes under Survivalcraft UI code or Content/Assets UI XML; do not use for ContentServer web UI."
---

# SCNET UI Design

Preserve a coherent game UI across desktop, Android, window resizing, and localization. Treat existing shared widgets and style assets as the source of truth for concrete dimensions and colors; this skill records design intent and selection criteria, not duplicated constants.

## Working method

1. Inspect the target Screen or panel and at least one current comparable implementation before choosing a layout.
2. Identify whether the problem comes from the page, a shared style, or a shared widget. Prefer fixing the shared owner when the behavior should be consistent everywhere, then review its consumers for regressions.
3. Preserve the established visual hierarchy and interaction semantics. Do not solve one screenshot with arbitrary offsets, duplicated controls, or per-Screen exceptions when a reusable rule exists.
4. Check compact and wide layouts, long localized text, enabled/disabled states, and any expanded selector or secondary action state affected by the change.
5. Keep visual cleanup separate from unrelated gameplay behavior. Follow the repository C# style and proportional validation skills when implementation changes require them.

## Required references

- Read [references/layout.md](references/layout.md) when changing Screen composition, margins, sizing, alignment, responsive behavior, labels, or form rows.
- Read [references/components.md](references/components.md) when choosing or changing actions, selectors, dialogs, sliders, text boxes, HUD panels, colors, or input ownership.
- Read [references/review-checklist.md](references/review-checklist.md) before completing any material UI change or UI review.

## Core preferences

- Aim for an overall centered composition with a modest rightward visual bias on wide screens, not a column pinned to the right edge. Balance secondary information on the left when the page naturally supports it.
- Reuse the shared Screen area styles and standard widgets. Neighboring Screens should not visibly jump in top margin, content width, description height, or bottom action spacing.
- Align the visible edges of controls, not only their outer layout boxes. Internal padding, labels, checkboxes, sliders, and text fields must form a stable pixel-level column when presented together.
- Keep controls comfortably usable without wasting vertical space. Favor consistent heights and compact gaps over shrinking primary buttons or leaving large dead bands.
- Design for localization and resizing from the start. Use concise localized labels, ellipsis where identity remains clear, and stable widths where changing window size would otherwise make adjacent controls drift.
- Preserve the user's explicit design direction over these defaults. If a request conflicts with an existing preference, implement the request and update this skill only when the new choice is intended as a reusable convention.
