#!/usr/bin/env python3
"""U+FFFD (REPLACEMENT CHARACTER) check for source text files.

Forbids the Unicode replacement character (U+FFFD, UTF-8 bytes EF BF BD)
in source text files. U+FFFD in a committed file is virtually always the
result of a broken encoding roundtrip (file read with the wrong encoding
and re-saved), which silently destroyed the original characters — there is
no legitimate reason to commit it.

Opt-out: a file containing the marker "ufffd-allow" is skipped entirely
(for files that legitimately document the character, e.g. bug reports or
this check's own documentation).

Findings are always a hard failure (exit 1) — both in pre-commit (staged
files) and in pre-push (--all, whole repository).

Run as a pre-commit hook (default: only staged files) or with --all to scan
the entire repository. --strict is accepted for symmetry with the other
checks; findings already fail unconditionally.
"""
import argparse
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.stderr.reconfigure(encoding='utf-8', errors='replace')

TEXT_EXTENSIONS = {
    '.cs', '.razor', '.cshtml', '.html', '.htm', '.css', '.js', '.ts',
    '.resx', '.csproj', '.props', '.targets', '.sln', '.slnx',
    '.md', '.txt', '.csv', '.json', '.xml', '.yml', '.yaml', '.svg',
    '.sh', '.py', '.ps1', '.cmd', '.bat', '.sql',
}

REPLACEMENT_CHAR = '\ufffd'  # escape, so this script itself contains no literal U+FFFD
ALLOW_MARKER = 'ufffd-allow'


def run(*args):
    return subprocess.run(args, capture_output=True, text=True)


def repo_root():
    res = run('git', 'rev-parse', '--show-toplevel')
    if res.returncode != 0:
        print('ERROR: not inside a git repository', file=sys.stderr)
        return None
    return Path(res.stdout.strip())


def staged_files(root):
    res = run('git', '-C', str(root), 'diff', '--cached', '--name-only', '--diff-filter=ACM')
    if res.returncode != 0:
        return []
    return [p for p in res.stdout.splitlines() if p]


def is_candidate(relpath):
    p = Path(relpath)
    if p.suffix.lower() in TEXT_EXTENSIONS:
        return True
    # extensionless hook scripts under .githooks
    return p.suffix == '' and p.parts and p.parts[0] == '.githooks'


def all_text_files(root):
    # tracked files only — untracked working files cannot be pushed anyway;
    # -C + --full-name keep the paths root-relative even when invoked from a subdir
    res = run('git', '-C', str(root), 'ls-files', '--full-name')
    if res.returncode != 0:
        return []
    return [p for p in res.stdout.splitlines() if p and is_candidate(p)]


def find_issues(path):
    """Returns list of issue lines for one file, or [] if clean/skipped."""
    try:
        raw = path.read_bytes()
    except OSError:
        return []
    if ALLOW_MARKER.encode() in raw:
        return []
    try:
        text = raw.decode('utf-8')
    except UnicodeDecodeError:
        return []  # not UTF-8 text — out of scope
    issues = []
    for ln, line in enumerate(text.splitlines(), start=1):
        count = line.count(REPLACEMENT_CHAR)
        if count:
            issues.append(f'  Zeile {ln}: {count}x U+FFFD — {line.strip()[:120]}')
    return issues


def parse_args():
    parser = argparse.ArgumentParser(description='U+FFFD replacement character check')
    parser.add_argument('--all', action='store_true', help='scan the whole repository, not only staged files')
    parser.add_argument('--strict', action='store_true', help='accepted for symmetry; findings always fail')
    return parser.parse_args()


def main():
    args = parse_args()
    root = repo_root()
    if root is None:
        return 1

    if args.all:
        files = all_text_files(root)
        scan_mode = 'all'
    else:
        files = [f for f in staged_files(root) if is_candidate(f)]
        scan_mode = 'staged'

    checked = 0
    any_found = False
    for rel in files:
        path = root / rel
        if not path.exists():
            continue
        checked += 1
        issues = find_issues(path)
        if issues:
            any_found = True
            print(f'ERROR: U+FFFD (Ersatzzeichen) in {rel}:')
            print('\n'.join(issues))
            print('  -> U+FFFD entsteht durch einen fehlerhaften Encoding-Roundtrip und')
            print('     darf nicht committed werden. Das ursprüngliche Zeichen wiederherstellen;')
            print('     für legitime Fälle den Marker "ufffd-allow" in die Datei aufnehmen.')
            print()

    if any_found:
        return 1

    print(f'OK: {checked} {scan_mode} text file(s) checked, no U+FFFD found.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
