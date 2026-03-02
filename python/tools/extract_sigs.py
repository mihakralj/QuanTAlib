"""Extract C# export signatures for category module generation."""
import re

cs = open('python/src/Exports.Generated.cs', encoding='utf-8').read()

# Extract each function: entry point name + full C# parameter list
pattern = r'\[UnmanagedCallersOnly\(EntryPoint\s*=\s*"qtl_(\w+)"\)\]\s+public static int \w+\(([^)]+)\)'
matches = re.findall(pattern, cs)

for name, params in matches:
    # Parse param types + names
    parts = []
    for p in params.split(','):
        p = p.strip()
        tokens = p.split()
        if len(tokens) >= 2:
            parts.append(f"{tokens[0]} {tokens[1]}")
    print(f"qtl_{name}|{'|'.join(parts)}")
