# Isolated UI Preview

## Choose the smallest sufficient environment

Use `UiPreviewTool` for layout, localization, clipping, visual state, and scaling checks that do not need a live world. Use `scnet-debugging` for actual clicks, keyboard focus, touch, gameplay, networking, or lifecycle behavior. A tiny wording change does not automatically require rendering every language and viewport.

Use [the tool README](../../../../Tools/UiPreviewTool/README.md) for setup, CLI options and output
formats. Choose only the languages, viewports and visual states affected by the change.

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
dotnet run --project Tools/UiPreviewTool -- \
  --xml Tools/UiPreviewTool/Samples/Form.xml \
  --culture zh-CN,en-US,pt-PT,ru-RU \
  --size 960x640 --scale 1 \
  --output .agent-work/ui-layout/artifacts/preview-01 --strict
```

Replace the sample with the actual XML or external factory. Use a new output directory for each meaningful run; the tool refuses to overwrite existing evidence. PowerShell accepts the same CLI parameters; use one line or its native continuation syntax.

- Check the exit code and read `preview.xml` plus the affected language reports. Use the [exit-code and layout reference](../../../../Tools/UiPreviewTool/README.md#输出与判定) to interpret failures.
- Inspect PNGs with the image-viewing tool. Use the XML tree to correlate visible problems with actual bounds, text, enabled state, and clipping. A PNG's existence is insufficient evidence.
- Treat `Clipped` as information: scrolling and ellipsis can be intentional. Automatic checks cover non-finite bounds and root overflow/compression, not every overlap, truncation, or aesthetic issue.
- Remember that the tool centers the root, uses a logical viewport derived from scale, and performs layout without `Update()` or input. It does not simulate the full game host's transitions or animation.
- Resolve fixture/bootstrap failures before repeating the scenario. Virtualized lists may need repeated layout passes; the tool already owns those passes, so do not invent per-preview startup delays.

For assertions beyond the built-in report, use the relevant bounds and state from the layout XML or a focused scenario test. Do not broadly reject every overlap: container backgrounds, overlays, and selection surfaces intentionally overlap their contents.

## Finish

Use screenshots and reports to correct the UI, then rerun the smallest invalidated case. Reuse evidence for unaffected states. Keep the current acceptance summary and exact artifact paths in `.agent-work` when the work spans multiple runs.

Report what was rendered and which checks passed. Separate visual checks from real-game input/network verification. Close temporary probe processes; leave a user-requested interactive game environment alone. Do not publish artifacts, change profiles, or restart the user's running test environment solely to take an isolated preview.
