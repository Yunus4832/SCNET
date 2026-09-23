---
name: scnet-gui-atlas-management
description: Compose SCNET gameplay HUD button textures, manage GUI atlas SVG and PNG sources, import historical premultiplied images, generate the atlas, and validate visual or alpha-sensitive replacements. Use when adding, replacing, extracting, reorganizing, or diagnosing resources under GuiAtlasTool/Assets or the generated GUI atlas; do not use gameplay button composition for general Widget, Screen, Dialog, or ActionPanel buttons.
---

# SCNET GUI Atlas Management

Treat `GuiAtlasTool/Assets/` as the source of truth. `Content/Assets/Atlases/AtlasTexture.png` and
`Atlas.txt` are ignored build products; never edit or commit them as source assets.

## Choose the source format

- Prefer SVG for new GUI assets by default. It keeps geometry editable, scales predictably, and
  works with the maintained gameplay-button composition workflow.
- Use PNG selectively for small icons when final-size pixel alignment, hinting, line weight, or
  antialiasing has been visually compared and the raster result is better. This includes status
  symbols, cursors, compact flags, and small glyphs recovered from a verified atlas.
- A nominally large canvas does not make an icon suitable for SVG when the visible symbol occupies
  only a small area. Judge the rendered feature size.
- Do not keep SVG and PNG files with the same stem. The filename stem is the atlas entry name.

## Preserve the Alpha contract

Source PNG files must contain standard straight (unassociated) RGBA. The generator premultiplies
every SVG and PNG exactly once when writing the atlas.

- Normalize an ordinary straight-alpha PNG with `prepare-png`.
- Pass `--premultiplied` when importing a PNG recovered from an existing game atlas or another
  source known to contain premultiplied channels.
- Use `extract` for an existing generated atlas; it reads the mapping, crops requested entries, and
  automatically converts the premultiplied pixels back to straight RGBA.
- Do not manually copy a premultiplied crop into `Assets/`, and do not change the generator to accept
  mixed Alpha conventions.

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  input.png GuiAtlasTool/Assets/EntryName.png

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-png \
  historical.png GuiAtlasTool/Assets/EntryName.png --premultiplied

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- extract \
  AtlasTexture.png Atlas.txt GuiAtlasTool/Assets EntryName AnotherEntry
```

These commands refuse to replace files unless `--overwrite` is present. Resolve and inspect a
same-name source-format replacement before authorizing the overwrite; remove the superseded source
format in the same change.

## Compose gameplay HUD buttons

`compose-game-button` is only for textured HUD controls used while playing, such as inventory,
weather, mount, and sneak buttons. It is not a general UI button generator. Do not use it for
Widget styles or buttons in Screens, Dialogs, ActionPanel, or other layout-driven UI.

Use it instead of manually joining a gameplay button frame and an SVG icon in Inkscape. It selects
a maintained frame template, preserves the icon aspect ratio, fits its `viewBox` into the button
safe area, and creates normal and pressed assets as one pair:

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- compose-game-button \
  floating path/to/Icon.svg NewButton
```

The button type is `floating`, `left-attached`, or `right-attached`. Use `--scale`, `--offset-x`,
and `--offset-y` only for optical-size or optical-centering corrections after inspecting the
default result. The offsets use SVG design units. Use `--asset-directory` for an isolated trial;
the default output is `GuiAtlasTool/Assets/`. The command refuses to replace either state unless
`--overwrite` is present.

Button frame templates under `GuiAtlasTool/Templates/Buttons/` are tool-owned exports. Keep the
Inkscape project independent: redraw or approve a frame there, export it once into the matching
normal and pressed templates, then validate generated buttons. Do not make the command read an
external Inkscape project at runtime.

The command intentionally accepts SVG icons only. When a small icon has a demonstrated reason to
remain PNG, keep it as a pixel-sensitive atlas source; do not route it through this command or
convert it to an SVG wrapper. A future raster compositor would be a separate workflow with explicit
target-size, resampling, straight-Alpha input, and normal/pressed output rules.

## Generate and validate

Generate the atlas directly when focused inspection is useful:

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- generate \
  GuiAtlasTool/Assets \
  Content/Assets/Atlases/AtlasTexture.png \
  Content/Assets/Atlases/Atlas.txt
```

After changing sources or the tool:

1. Build `GuiAtlasTool` and generate the atlas.
2. Confirm the entry dimensions and mapping name are unchanged unless the change is intentional.
3. For a recovered PNG, compare the generated atlas crop with its premultiplied reference. Exact
   equality is preferred; a maximum channel difference of one can result from reversible integer
   rounding and is visually equivalent.
4. Inspect transparent edges, target-size line weight, normal/pressed pairs, and any attached-edge
   orientation affected by the asset.
5. Run `git diff --check`. When C# changed, also use `scnet-code-style` and proportional build
   validation.

Update `GuiAtlasTool/README.md`, `Doc/BuildAndConfig.md`, and `Directory.Build.targets` together when
the source contract, commands, paths, or build integration changes. Use `scnet-ui-design` as well
when the work changes layout, interaction semantics, or shared UI component behavior rather than
only atlas resource management.
