import re, os

# Collect all function names from new category files
new_fns = set()
for f in sorted(os.listdir('python/quantalib')):
    if f.endswith('.py') and not f.startswith('_') and f != 'indicators.py':
        content = open(f'python/quantalib/{f}', encoding='utf-8').read()
        new_fns.update(re.findall(r'^def (\w+)\(', content, re.MULTILINE))

# Collect from old indicators.py
old_content = open('python/quantalib/indicators.py', encoding='utf-8').read()
old_fns = set(re.findall(r'^def (\w+)\(', old_content, re.MULTILINE))
old_fns = {f for f in old_fns if not f.startswith('_')}

missing = sorted(old_fns - new_fns)
extra = sorted(new_fns - old_fns)

with open('python/tools/diff_report.txt', 'w') as out:
    out.write(f'Old indicators.py: {len(old_fns)} public functions\n')
    out.write(f'New category files: {len(new_fns)} functions\n\n')
    out.write(f'Missing from new ({len(missing)}):\n')
    for m in missing:
        out.write(f'  {m}\n')
    out.write(f'\nNew indicators not in old ({len(extra)}):\n')
    for e in extra:
        out.write(f'  {e}\n')

print('Done - see python/tools/diff_report.txt')
