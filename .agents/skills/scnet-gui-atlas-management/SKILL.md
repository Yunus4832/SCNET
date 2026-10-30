---
name: scnet-gui-atlas-management
description: Compose SCNET gameplay HUD button textures, manage GUI atlas SVG, PNG and WebP sources, import historical premultiplied images, generate the atlas, and validate visual or alpha-sensitive replacements. Use when adding, replacing, extracting, reorganizing, or diagnosing resources under GuiAtlasTool/Assets or the generated GUI atlas; do not use gameplay button composition for general Widget, Screen, Dialog, or ActionPanel buttons.
---

# SCNET GUI Atlas Management

Treat `GuiAtlasTool/Assets/` as the source of truth. `Content/Assets/Atlases/AtlasTexture.webp` and
`Atlas.txt` are ignored build products; never edit or commit them as source assets.

## Choose the source format

- Prefer lossless WebP for raster GUI assets. Use SVG for editable geometry, scalable icons, and
  the maintained gameplay-button composition workflow.
- Use PNG selectively for small icons when final-size pixel alignment, hinting, line weight, or
  antialiasing has been visually compared and the raster result is better. This includes status
  symbols, cursors, compact flags, and small glyphs recovered from a verified atlas.
- A nominally large canvas does not make an icon suitable for SVG when the visible symbol occupies
  only a small area. Judge the rendered feature size.
- Do not keep SVG, PNG and WebP files with the same stem. The filename stem is the atlas entry name.

## Preserve the Alpha contract

All source PNG/WebP files and the generated lossless WebP atlas contain straight (unassociated) RGBA.
Do not premultiply authoring assets or the atlas output. SVG entries use their declared dimensions
and are composed with the raster entries into one atlas.

- Prefer lossless WebP for new raster assets; PNG remains supported. Keep editable geometry in SVG.
- Never keep PNG, WebP or SVG files with the same stem: the stem is the entry name.
- Ordinary straight-alpha files can be placed directly into `Assets/`; no preparation is required.
- `prepare-image input.png output.webp` optionally converts a raster file to default lossless WebP.
- Only when importing an explicitly known historical premultiplied file, use `prepare-image` with
  `--premultiplied` to undo that representation once. Do not infer alpha representation from pixels.
- `extract` reads the current straight-alpha atlas and directly crops entries into WebP files.
  It does not unpremultiply. Historical premultiplied atlases must first be explicitly normalized.

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-image \
  input.png GuiAtlasTool/Assets/EntryName.webp

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- prepare-image \
  historical.png GuiAtlasTool/Assets/EntryName.webp --premultiplied

dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- extract \
  AtlasTexture.webp Atlas.txt GuiAtlasTool/Assets EntryName AnotherEntry
```

Conversion/extraction refuse replacements without `--overwrite`. Inspect a same-name replacement
and remove the superseded source format in the same change. Atlas generation accepts PNG, WebP and
SVG sources and only permits `.webp` image output. Still images only; animated raster inputs are rejected.

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
remain raster, keep it as a pixel-sensitive atlas source; do not route it through this command or
convert it to an SVG wrapper. A future raster compositor would be a separate workflow with explicit
target-size, resampling, straight-Alpha input, and normal/pressed output rules.

## Generate and validate

Generate the atlas directly when focused inspection is useful:

```bash
dotnet run --project GuiAtlasTool/GuiAtlasTool.csproj -- generate \
  GuiAtlasTool/Assets \
  Content/Assets/Atlases/AtlasTexture.webp \
  Content/Assets/Atlases/Atlas.txt
```

After changing sources or the tool:

1. Build `GuiAtlasTool` and generate the atlas.
2. Confirm the entry dimensions and mapping name are unchanged unless the change is intentional.
3. Verify straight-alpha raster inputs survive atlas encoding/extraction with identical visible pixels
   and alpha. Compare engine-premultiplied crops with the previous rendered reference when migrating
   known premultiplied assets; integer conversion can produce a channel difference of one.
4. Inspect transparent edges, target-size line weight, normal/pressed pairs, and any attached-edge
   orientation affected by the asset.
5. Run `git diff --check`. When C# changed, also use `scnet-code-style` and proportional build
   validation.

Update `GuiAtlasTool/README.md`, `Doc/BuildAndConfig.md`, and `Directory.Build.targets` together when
the source contract, commands, paths, or build integration changes. Use `scnet-ui-design` as well
when the work changes layout, interaction semantics, or shared UI component behavior rather than
only atlas resource management.
