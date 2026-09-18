# UI Review Checklist

Use the relevant items; do not turn a small change into a mandatory full-application audit.

## Composition

- Does the page retain the established centered, modestly right-weighted visual balance on both compact and wide aspect ratios?
- Do top, side, bottom, description, and action-area margins match comparable Screens?
- Is unused vertical space reduced without making primary controls too small?
- Do visible control edges align across buttons, selectors, sliders, text boxes, and checkboxes?

## Hierarchy and actions

- Are primary and secondary operations separated by frequency and consequence?
- Are action order, weights, enabled state, and green/red semantic colors appropriate?
- Are destructive actions confirmed where data loss is meaningful?
- Does returning from a secondary action restore the primary action layout correctly?

## Text and state

- Are all new strings localized through the repository language workflow?
- Do selected labels and dynamic items refresh after language or source changes?
- Do long names truncate without hiding the meaning or changing row height?
- Are help and description text subordinate and compact?

## Expansion and resizing

- Do selection lists, separators, borders, and item heights remain aligned when expanded?
- Does the UI remain usable when the window narrows and visually balanced when it widens?
- Are controls stable in width relative to their peers rather than drifting independently?
- Do dialog contents adapt between short and long messages without excessive empty space or unnecessary scrolling?

## Input and lifecycle

- Do Escape/back, confirm, cancel, mouse, touch, gamepad, and physical keyboard inputs reach exactly one intended owner?
- Does a HUD editor suppress conflicting gameplay shortcuts while remaining usable with external keyboards?
- Does returning from another Screen preserve or refresh the correct UI state without duplicating dialogs or actions?

## Change scope

- Was the fix made at the correct owner: Screen, style, or shared widget?
- If a shared component changed, were representative Screens inspected for deformation or changed interaction?
- Were unrelated visual cleanups kept out of the change?
- Was validation proportional to risk, with runtime visual verification requested when static inspection cannot prove layout quality?
