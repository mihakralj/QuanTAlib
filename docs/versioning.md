# Versioning

QuanTAlib follows [Semantic Versioning 2.0.0](https://semver.org/) with a strict deprecation window: nothing is removed without first being marked `[Obsolete]` for at least one minor release.

## SemVer

Version format is `MAJOR.MINOR.PATCH` (currently `0.8.12`).

| Change | Bump | Example |
| :--- | :--- | :--- |
| Bug fix, no API change | PATCH | 0.8.12 → 0.8.13 |
| Additive API, or an `[Obsolete]` deprecation | MINOR | 0.8.13 → 0.9.0 |
| Breaking change (removal, signature change) | MAJOR | 0.9.0 → 1.0.0 |

Pre-1.0, breaking changes bump MINOR instead of MAJOR (SemVer §4). QuanTAlib is still `0.x`, so the "one minor-version deprecation window" is: deprecate in `0.x`, remove in `0.x+1`.

## Deprecation window

When an API has to go away:

1. **Deprecate** — tag it `[Obsolete("Use X instead. Will be removed in 0.(x+1).", false)]` and ship it in release `0.x.y`. No breaking change, MINOR bump.
2. **Remove** — delete it in the next minor release `0.x+1.0`.

Removal without the window is a build error, not a policy suggestion. `PublicAPI.Shipped.txt` is frozen by `Microsoft.CodeAnalysis.PublicApiAnalyzers`, so deleting a public member fails the build (`RS0017`) until the removal is recorded in `PublicAPI.Unshipped.txt` with `*REMOVED*`. That record doubles as the audit trail that the deprecation window was honored.

## Version source

The canonical version is `lib/VERSION` — one line, e.g. `0.8.12`. Three language-agnostic consumers read it:

| Consumer | Reads via | Result |
| :--- | :--- | :--- |
| .NET / NuGet | `Directory.Build.props` → `$(QtaVersionFile)` | `Version`, `PackageVersion`, `InformationalVersion` = the file; `AssemblyVersion` / `FileVersion` = `{version}.0` |
| Python | `python/pyproject.toml` → `../lib/VERSION` | pip package version |
| CI | `.github/workflows/Release.yml` | release tag `v{version}` and dev-wheel stamping `{version}.dev{run}` |

## Bumping the version

1. Edit `lib/VERSION` to the new value.
2. Commit: `chore: bump version to X.Y.Z`.
3. CI builds the NuGet package and pip wheel at that version.

## Migrating off lib/VERSION

`lib/VERSION` exists because MSBuild, Python, and shell CI all need the same value and none of them can parse the others' config. Replacing it means one of:

- **Relocate it** — move to a root-level `VERSION` (or `version.txt`). Update the path in `Directory.Build.props` (`QtaVersionFile`), `python/pyproject.toml`, and the four reads in `Release.yml`. Low risk, cosmetic.
- **Tag-derived versioning** — `MinVer` for .NET (version = nearest git tag), plus CI writes the tag to a `version.py`/file for Python. Removes the file for .NET but not Python; the release pipeline changes shape.
- **Tool-managed file** — [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) `version.json` replaces `VERSION`; its `nbgv` CLI stamps both .NET and Python. Structured, but adds a tool dependency.

Recommendation: keep a single plain-text version file — it is the correct pattern for three language-agnostic consumers. Move it out of `lib/` only if its location bothers you; the policy above does not depend on where the file lives.
