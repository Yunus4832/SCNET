#!/usr/bin/env python3
"""Check repository Markdown structure and local links; no network requests or writes."""

import argparse
import re
import subprocess
import unicodedata
from pathlib import Path
from urllib.parse import unquote, urlsplit


REPOSITORY_PREFIXES = (
    'https://gitee.com/Yunus0712/scnet/blob/main/',
    'https://github.com/Yunus4832/SCNET/blob/main/',
)


def headings(text):
    result = set()
    counts = {}
    fenced = False
    for line in text.splitlines():
        if line.lstrip().startswith(('```', '~~~')):
            fenced = not fenced
        if fenced:
            continue
        match = re.match(r'^#{1,6}\s+(.+?)\s*#*$', line)
        if not match:
            continue
        title = re.sub(r'\[([^]]+)\]\([^)]+\)', r'\1', match[1]).replace('`', '')
        slug = ''.join(c for c in title.lower() if not unicodedata.category(c).startswith(('P', 'S')) or c in '-_')
        slug = re.sub(r'\s', '-', slug)
        count = counts.get(slug, 0)
        counts[slug] = count + 1
        result.add(slug if count == 0 else f'{slug}-{count}')
    return result


def check(root, files):
    errors = []
    for path in files:
        text = path.read_text(encoding='utf-8-sig')
        relative = path.relative_to(root)
        fence = None
        visible = []
        for number, line in enumerate(text.splitlines(), 1):
            marker = re.match(r'^\s*(`{3,}|~{3,})', line)
            if marker:
                if fence is None:
                    fence = marker[1]
                elif marker[1][0] == fence[0] and len(marker[1]) >= len(fence):
                    fence = None
                continue
            if fence is None:
                visible.append((number, line))
            if line.rstrip() != line:
                errors.append(f'{relative}:{number}: trailing whitespace')
        if fence:
            errors.append(f'{relative}: unclosed code fence')
        if not text.endswith('\n') or text.endswith('\n\n'):
            errors.append(f'{relative}: expected one final newline')
        for number, line in visible:
            if re.match(r'^#{1,6}\s', line):
                lines = text.splitlines()
                if number > 1 and lines[number - 2].strip():
                    errors.append(f'{relative}:{number}: blank line required before heading')
                if number < len(lines) and lines[number].strip():
                    errors.append(f'{relative}:{number}: blank line required after heading')
            links = re.findall(r'!?\[[^\]\n]*\]\(([^)\n]+)\)', line)
            reference = re.match(r'^\s*\[[^]]+\]:\s*(\S+)', line)
            if reference:
                links.append(reference[1])
            for link in links:
                url = link.split(' "', 1)[0].strip('<>')
                target = None
                for prefix in REPOSITORY_PREFIXES:
                    if url.startswith(prefix):
                        url = url[len(prefix):]
                        target = root
                        break
                parsed = urlsplit(url)
                if parsed.scheme or parsed.netloc:
                    continue
                destination = ((target or path.parent) / unquote(parsed.path)).resolve() if parsed.path else path
                if not destination.exists():
                    errors.append(f'{relative}:{number}: missing target {link}')
                elif parsed.fragment and destination.suffix == '.md':
                    if unquote(parsed.fragment) not in headings(destination.read_text(encoding='utf-8-sig')):
                        errors.append(f'{relative}:{number}: missing heading {link}')
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument('files', nargs='*', help='Markdown paths relative to root; default: tracked and untracked, non-ignored Markdown')
    args = parser.parse_args()
    root = args.root.resolve()
    names = args.files or subprocess.check_output(
        ['git', '-C', str(root), 'ls-files', '--cached', '--others', '--exclude-standard', '--', '*.md'],
        text=True).splitlines()
    files = sorted({(root / name).resolve() for name in names if (root / name).is_file()})
    errors = check(root, files)
    for error in errors:
        print(error)
    print(f'Checked {len(files)} Markdown files; {len(errors)} issue(s). External URLs and semantic accuracy require separate review.')
    return bool(errors)


if __name__ == '__main__':
    raise SystemExit(main())
