#!/usr/bin/env bash
set -euo pipefail

repository_root="$(git rev-parse --show-toplevel)"
cd "$repository_root"
formatter="${SCNET_JB:-jb}"
if ! command -v "$formatter" >/dev/null 2>&1; then
    echo "JetBrains formatter not found: $formatter" >&2
    echo "Install JetBrains.ReSharper.GlobalTools (see Doc/BuildAndConfig.md), or set SCNET_JB to its jb executable." >&2
    exit 1
fi
if (( $# == 0 )); then
    echo "Usage: validate_jetbrains_format.sh <repository-relative C# files...>" >&2
    exit 2
fi

work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-format-check.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT

# Preserve nested EditorConfig scopes without formatting or temporarily replacing working-tree files.
while IFS= read -r config; do
    [[ -f "$config" ]] || continue
    mkdir -p "$work/$(dirname "$config")"
    cp "$config" "$work/$config"
done < <(git ls-files --cached --others --exclude-standard -- .editorconfig '**/.editorconfig' | sort -u)

copies=()
for file in "$@"; do
    [[ "$file" != /* && "$file" != ../* && "$file" != */../* && -f "$file" ]] || {
        echo "Invalid repository-relative source path: $file" >&2
        exit 2
    }
    mkdir -p "$work/$(dirname "$file")"
    cp "$file" "$work/$file"
    copies+=("$work/$file")
done

if ! "$formatter" cleanupcode --profile='Built-in: Reformat Code' --no-updates --verbosity=WARN "${copies[@]}" > "$work/formatter.log" 2>&1; then
    cat "$work/formatter.log" >&2
    exit 1
fi

failed=0
for file in "$@"; do
    if ! cmp -s "$file" "$work/$file"; then
        echo "$file: error FORMAT: JetBrains formatting differs from .editorconfig." >&2
        failed=1
    fi
done
if (( failed == 0 )); then
    echo "JetBrains formatting verified: $# file(s); working tree unchanged."
fi
exit "$failed"
