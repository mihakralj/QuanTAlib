import re, os
total = 0
for f in sorted(os.listdir('python/quantalib')):
    if f.endswith('.py') and not f.startswith('_') and f != 'indicators.py':
        content = open(f'python/quantalib/{f}', encoding='utf-8').read()
        defs = re.findall(r'^def (\w+)\(', content, re.MULTILINE)
        total += len(defs)
        print(f'{f}: {len(defs)} functions - {", ".join(defs[:10])}{"..." if len(defs) > 10 else ""}')
print(f'\nTotal: {total} wrapper functions across {len([f for f in os.listdir("python/quantalib") if f.endswith(".py") and not f.startswith("_") and f != "indicators.py"])} files')
