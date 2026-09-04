#!/usr/bin/env bash
set -euo pipefail

repository_root=$(git rev-parse --show-toplevel)
cd "$repository_root"

mapfile -t changed_files < <(
    {
        git diff --name-only --diff-filter=ACMR -- '*.cs'
        git diff --cached --name-only --diff-filter=ACMR -- '*.cs'
        git ls-files --others --exclude-standard -- '*.cs'
    } | sort -u
)

existing_files=()
for path in "${changed_files[@]}"; do
    if [[ -f "$path" ]]; then
        existing_files+=("$path")
    fi
done

if (( ${#existing_files[@]} == 0 )); then
    echo "No changed C# files to validate."
    exit 0
fi

echo "Validating ${#existing_files[@]} changed C# file(s)."
dotnet format SCNET.slnx whitespace --verify-no-changes --no-restore --include "${existing_files[@]}"
dotnet format SCNET.slnx style --verify-no-changes --no-restore --include "${existing_files[@]}"
