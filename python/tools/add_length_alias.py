"""Add length→period alias to all old-style Python wrapper functions.

Replaces:  period = int(period)
With:      period = int(kwargs.get("length", period))

Only targets exact standalone `period = int(period)` lines (4-space indent).
Does NOT touch compound names like fastPeriod, slowPeriod, etc.
"""
import re
import os
import glob

base = os.path.join(os.path.dirname(__file__), '..', 'quantalib')
base = os.path.normpath(base)
files = sorted(glob.glob(os.path.join(base, '*.py')))
total = 0

for fpath in files:
    fname = os.path.basename(fpath)
    if fname.startswith('_'):
        continue
    with open(fpath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Match exactly '    period = int(period)' (4-space indent, standalone)
    pattern = r'^(    )period = int\(period\)$'
    matches = re.findall(pattern, content, re.MULTILINE)
    count = len(matches)
    if count > 0:
        new_content = re.sub(
            pattern,
            r'\1period = int(kwargs.get("length", period))',
            content,
            flags=re.MULTILINE,
        )
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f'{fname}: {count} replacements')
        total += count
    else:
        print(f'{fname}: 0 (skipped)')

print(f'\nTotal: {total} replacements across {len(files)} files')
