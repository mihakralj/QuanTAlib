# Workspace Configuration

## Temporary Files and Scripts

**Location:** `c:/github/quantalib/temp/`

Use this directory for:
- Temporary files generated during operations
- Generated scripts (.ps1, .sh, .bat, .cmd)
- Intermediate build artifacts
- Test data files
- Benchmark results
- Any ephemeral content that doesn't need version control

**Guidelines:**
- Always use the `temp/` directory for any temporary files
- Use descriptive filenames with timestamps when appropriate (e.g., `benchmark_20260110_185530.txt`)
- Clean up temporary files when no longer needed
- Do not commit the temp directory (it's in .gitignore)
- Use subdirectories within temp/ to organize different types of temporary files

**Example paths:**
- Scripts: `temp/scripts/`
- Benchmarks: `temp/benchmarks/`
- Test data: `temp/testdata/`
- Logs: `temp/logs/`

## Directory Structure

```
quantalib/
├── temp/                  # Temporary workspace (gitignored)
│   ├── scripts/          # Generated scripts
│   ├── benchmarks/       # Benchmark results
│   ├── testdata/         # Test data files
│   └── logs/             # Temporary logs
├── lib/                   # Library source code
├── quantower/            # Quantower adapters
├── perf/                 # Performance benchmarks
├── ndepend/              # NDepend analysis
└── .clinerules/          # Agent instructions
