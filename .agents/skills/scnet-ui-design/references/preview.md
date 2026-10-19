# Isolated UI Preview

## Choose the smallest sufficient environment

Use `UiPreviewTool` for layout, localization, clipping, visual state, and scaling checks that do not need a live world. Use `scnet-debugging` for actual clicks, keyboard focus, touch, gameplay, networking, or lifecycle behavior. A tiny wording change does not automatically require rendering every language and viewport.

The shared engineering method is: use production rendering, construct explicit repeatable inputs, inspect observable output, assert the relevant invariants, and state the evidence boundary. Shader tests follow the same method but require geometry, camera, depth, and pixel assertions; do not expand this UI workflow into a general shader test framework.

Read [the tool README](../../../../UiPreviewTool/README.md) for the canonical CLI, scenario interface, output schema, and platform requirements. The tool briefly opens a real graphics window; PNG output is offscreen, but a usable desktop graphics environment is still required.

## Prepare a scenario

1. Use `--xml` for a self-contained widget tree. Do not assume every Screen or XML asset is independent of the world.
2. For C# constructors and dynamic data, build a small external assembly implementing `IUiPreviewScenario`. It may register required Mods and construct empty/populated lists, selections, long names, or disabled actions. Keep fixtures under `.agent-work/<task>/`; keep reusable production functionality in its owning project.
3. Keep scenario constructors lightweight; create widgets and GPU resources in `Create`, after resource initialization. Return a new tree per language and dispose fixture-owned resources explicitly.
4. Prefer normal constructors and explicit fixture data. If a narrow rendering fixture needs stubs or private-state preparation, isolate and disclose them. Do not add game APIs solely to make a preview work, or claim such a fixture verifies real interaction or world lifecycle.
5. Prepare only states affected by the change. For a list/editor, an empty list, selected populated row, long text, and disabled action are often more useful than another default screenshot.

No game instance is needed. Do not launch a full server/client lab for a layout-only question unless isolation cannot represent the behavior.

## Render and inspect

From the repository root, an example starting point is:

```bash
dotnet run --project UiPreviewTool -- \
  --xml UiPreviewTool/Samples/Form.xml \
  --culture zh-CN,en-US,pt-PT,ru-RU \
  --size 960x640 --scale 1 \
  --output .agent-work/ui-layout/artifacts/preview-01 --strict
```

Replace the sample with the actual XML or external factory. Use a new output directory for each meaningful run; the tool refuses to overwrite existing evidence. PowerShell accepts the same CLI parameters; use one line or its native continuation syntax.

- Confirm command exit and report contents. Exit 3 is a layout-check failure, not a successful preview; exit 1 is a runtime/resource failure.
- Inspect PNGs with the image-viewing tool. Use the XML tree to correlate visible problems with actual bounds, text, enabled state, and clipping. A PNG's existence is insufficient evidence.
- Check the relevant language and viewport cases. Long translations, compact viewports, or a non-default scale deserve additional runs when they are implicated by the change.
- Treat `Clipped` as information: scrolling and ellipsis can be intentional. Automatic checks cover non-finite bounds and root overflow/compression, not every overlap, truncation, or aesthetic issue.
- Remember that the tool centers the root, uses a logical viewport derived from scale, and performs layout without `Update()` or input. It does not simulate the full game host's transitions or animation.
- Resolve fixture/bootstrap failures before repeating the scenario. Virtualized lists may need repeated layout passes; the tool already owns those passes, so do not invent per-preview startup delays.

For assertions beyond the built-in report, use the relevant bounds and state from the layout XML or a focused scenario test. Do not broadly reject every overlap: container backgrounds, overlays, and selection surfaces intentionally overlap their contents.

## Finish

Use screenshots and reports to correct the UI, then rerun the smallest invalidated case. Reuse evidence for unaffected states. Keep the current acceptance summary and exact artifact paths in `.agent-work` when the work spans multiple runs.

Report what was rendered and which checks passed. Separate visual checks from real-game input/network verification. Close temporary probe processes; leave a user-requested interactive game environment alone. Do not publish artifacts, change profiles, or restart the user's running test environment solely to take an isolated preview.
