---
name: scnet-language-management
description: Manage SCNET localization files through the repository C# LanguageTool. Use when adding, updating, removing, renaming, listing, reading, or validating keys in Content/Assets/Lang/*.json, and avoid direct JSON text edits for localization changes.
---

# SCNET Language Management

Use the repository tool instead of manually editing `Content/Assets/Lang/*.json`.

Run commands from the repository root. Prefer these navigation commands before reading any language JSON file:

```bash
dotnet run --project LanguageTool -- check
dotnet run --project LanguageTool -- rules
dotnet run --project LanguageTool -- overview . --depth 1
dotnet run --project LanguageTool -- overview Help --culture all --depth 2 --limit 80
dotnet run --project LanguageTool -- children ContentWidgets
dotnet run --project LanguageTool -- search Restart --culture en-US --prefix ContentWidgets --limit 20
dotnet run --project LanguageTool -- table Help --fields Title,Name --limit 12
```

## Workflow

1. Inspect the key rules and nearby key structure before adding new ones:

```bash
dotnet run --project LanguageTool -- rules
dotnet run --project LanguageTool -- overview . --depth 1
dotnet run --project LanguageTool -- overview ContentWidgets --depth 2 --limit 120
dotnet run --project LanguageTool -- overview Help --culture all --depth 2 --limit 80
dotnet run --project LanguageTool -- children ContentWidgets.ModManagementScreen
dotnet run --project LanguageTool -- show ContentWidgets.ModManagementScreen --culture zh-CN --depth 1 --limit 80
dotnet run --project LanguageTool -- list --culture zh-CN --prefix ContentWidgets.ModManagementScreen
dotnet run --project LanguageTool -- get ContentWidgets.ModManagementScreen.Refresh --culture en-US
```

2. Search without opening large JSON files:

```bash
dotnet run --project LanguageTool -- search server --culture all --prefix ContentWidgets --limit 50
dotnet run --project LanguageTool -- search WorldServerSettings --culture en-US --in path
```

3. Audit structured sections across cultures without opening large JSON files:

```bash
dotnet run --project LanguageTool -- overview Help --culture all --depth 2 --limit 80
dotnet run --project LanguageTool -- table Help --fields Title,Name --limit 20
dotnet run --project LanguageTool -- table ContentWidgets.ModManagementScreen --fields value --cultures zh-CN,en-US
```

Use `overview` first when you need a level/depth map of a language subtree. It reports node kind, scalar fields, child keys, previews and structural issues. Use `.` or `/` for the root path.
Use `table` when a section contains numbered child objects or metadata fields and you need to compare mapping/order across languages.

4. Synchronize invariant metadata fields from the canonical language:

```bash
dotnet run --project LanguageTool -- sync-field Help Name --from zh-CN --remove-extra --dry-run
dotnet run --project LanguageTool -- sync-field Help Name --from zh-CN --remove-extra
```

Use `sync-field` for runtime IDs or non-translatable metadata such as `Help.*.Name`. Do not use it for localized display text such as `Title` or `value`.

5. Add or update one string key in all supported languages:

```bash
dotnet run --project LanguageTool -- set ContentWidgets.SomeScreen.SomeKey \
  --zh-CN 中文 \
  --en-US English \
  --pt-PT Português \
  --ru-RU Русский
```

6. Rename a key in all language files:

```bash
dotnet run --project LanguageTool -- rename OldScreen.OldKey NewScreen.NewKey
```

7. Remove a key from all language files:

```bash
dotnet run --project LanguageTool -- remove SomeScreen.ObsoleteKey
```

8. Always validate after a localization change:

```bash
dotnet run --project LanguageTool -- check
```

## Rules

- Do not directly edit language JSON for normal localization key changes.
- Prefer complete four-language updates. `set` requires all four languages unless `--allow-partial` is intentionally used.
- Use `overview` for level/depth navigation before opening large language files.
- Use `table` before reading large language files when diagnosing cross-language section mapping.
- Treat fields like `Help.*.Name` as invariant runtime identifiers; use `sync-field` to keep them identical across cultures.
- Use JSON path dots only as path separators, for example `ContentWidgets.PlayScreen.12` or `ContentWidgets.WorldServerSettingsScreen.PortDescription`.
- `LanguageManager.Get("PlayScreen", 12)` usually corresponds to `ContentWidgets.PlayScreen.12` in the JSON files.
- `LanguageManager.Get(section, key)` corresponds to `{section}.{key}`.
- `LanguageManager.GetContentWidgets(name, key)` corresponds to `ContentWidgets.{name}.{key}`.
- XML text like `[ScreenName:Key]` is resolved through `ContentWidgets.ScreenName.Key`.
- Numeric keys such as `12` are object keys, not array indexes. Array indexes use `[0]`.
- Do not create real JSON key names containing `.`. Use `_` for compound flat keys, for example `Strings.GameMode_Creative_Description`.
- Existing dotted key names are still addressable with escaped dots, but treat that as compatibility only.
- For `Strings` keys, prefer `StringsManager.GetString("GameMode", gameMode, "Description")` in C# instead of manually composing `GameMode_Creative_Description`.
- Keep UI code references aligned with language keys before finishing.
