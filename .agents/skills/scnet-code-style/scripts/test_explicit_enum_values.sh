#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/scnet-enum-style-test.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT

cat > "$work/Good.cs" <<'CS'
[System.Flags] enum Flags { None = 0, A = 1 << 0, B = 1 << 1, All = A | B }
enum Signed : long { Min = -9223372036854775808L, Zero = 0L, Alias = Zero }
enum Unsigned : ulong { Max = 18446744073709551615UL }
class Text { string Value = "enum Example { Implicit }"; }
CS
cat > "$work/Implicit.cs" <<'CS'
enum Example { First, Explicit = 7, Next }
CS
cat > "$work/Conditional.cs" <<'CS'
enum Conditional
{
    First = 0,
#if ANDROID && DEBUG
    HiddenImplicit,
#else
    Explicit = 2,
#endif
    Last = 3
}
CS

check_case() {
    local name="$1" expected_exit="$2" expected_count="$3" actual_exit=0
    dotnet run --file "$script_directory/check_explicit_enum_values.cs" -- "$work/$name.cs" > "$work/output" 2>&1 || actual_exit=$?
    if [[ "$actual_exit" != "$expected_exit" ]]; then
        cat "$work/output"
        echo "$name: expected exit $expected_exit, got $actual_exit" >&2
        exit 1
    fi
    local count
    count="$(rg -c 'requires an explicit value\.' "$work/output" || true)"
    if [[ "${count:-0}" != "$expected_count" ]]; then
        cat "$work/output"
        echo "$name: expected $expected_count violations" >&2
        exit 1
    fi
    echo "$name passed"
}

check_case Good 0 0
check_case Implicit 1 2
check_case Conditional 1 1
