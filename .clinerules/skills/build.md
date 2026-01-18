# Skill: Full Build

**Triggers**: `build`, `full build`, `dotnet build`, `restore build test`, `build and test`, `run tests`

## Quick Reference

Execute these commands in sequence from repo root:

```powershell
# 1. Restore
dotnet restore QuanTAlib.sln --verbosity minimal

# 2. Clean  
dotnet clean QuanTAlib.sln --configuration Debug --verbosity minimal

# 3. Build
dotnet build QuanTAlib.sln --configuration Debug --no-restore

# 4. Test Library
dotnet test lib/QuanTAlib.Tests.csproj --configuration Debug --no-build --verbosity normal

# 5. Test Quantower
dotnet test quantower/Quantower.Tests.csproj --configuration Debug --no-build --verbosity normal
```

## On Errors/Warnings

1. **Build errors**: Read the error message, identify file:line, fix using `replace_in_file`
2. **Test failures**: Read test output, examine the failing test, fix implementation or test
3. **Warnings**: Fix straightforward ones (missing `nameof()`, unused imports, etc.)

## Alternative: PowerShell Script

```powershell
.\temp\scripts\full-build.ps1
.\temp\scripts\full-build.ps1 -Configuration Release
.\temp\scripts\full-build.ps1 -SkipTests