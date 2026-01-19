# Workflow: Full .NET Build Cycle

> **Trigger phrases**: "build", "restore build test", "clean build", "dotnet cycle", "build everything"

## Description
Performs a complete .NET build cycle: restore → clean → build → test → report/fix warnings and errors.

## Steps

### 1. Restore Dependencies
```powershell
dotnet restore QuanTAlib.sln --verbosity minimal
```
**Expected**: All packages restored successfully.

### 2. Clean Solution
```powershell
dotnet clean QuanTAlib.sln --configuration Debug --verbosity minimal
```
**Expected**: Clean completed without errors.

### 3. Build Solution
```powershell
dotnet build QuanTAlib.sln --configuration Debug --no-restore
```
**Expected**: Build succeeded with 0 errors. Note any warnings for fixing.

### 4. Build Solution
```powershell
dotnet build QuanTAlib.sln --configuration Debug --no-restore
```
**Expected**: Build succeeded with 0 errors. Release has stricter warnings-as-errors.

### 5. Run Library Tests
```powershell
dotnet test lib/QuanTAlib.Tests.csproj --configuration Debug --no-build --verbosity normal
```
**Expected**: All tests pass.

### 6. Run Quantower Adapter Tests
```powershell
dotnet test quantower/Quantower.Tests.csproj --configuration Debug --no-build --verbosity normal
```
**Expected**: All tests pass.

### 7. Report & Fix Issues
After each step, if warnings or errors occur:
1. **Parse the output** to identify:
   - Error codes (CS####, IDE####, MA####, etc.)
   - File paths and line numbers
   - Warning/error messages
2. **Categorize issues**:
   - Build errors → Must fix before proceeding
   - Test failures → Investigate and fix
   - Warnings → Investigate and Fix if clear, suggest if complex
3. **Apply fixes** using `replace_in_file` for targeted changes
4. **Re-run the failed step** to verify the fix

## Test Failure Investigation
1. Read the test file to understand the assertion
2. Read the implementation being tested
3. Determine if issue is in test or implementation
4. Fix the root cause, not symptoms
