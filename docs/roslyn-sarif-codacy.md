# Roslyn SARIF Generation and Codacy Integration

## Overview

QuanTAlib now automatically generates Roslyn SARIF (Static Analysis Results Interchange Format) files during every build and uploads them to Codacy for continuous code quality monitoring.

## Configuration

### Build Configuration

The `Directory.Build.props` file has been configured to generate SARIF files for all projects:

```xml
<PropertyGroup>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>
  <ErrorLog>$(MSBuildProjectDirectory)/roslyn.sarif</ErrorLog>
  <ErrorLogFormat>SARIF2.1</ErrorLogFormat>
</PropertyGroup>
```

### Git Configuration

SARIF files are excluded from version control via `.gitignore`:

```
# Roslyn SARIF files (generated during build and uploaded to Codacy)
**/roslyn.sarif
roslyn.sarif
```

## CI/CD Pipeline

### Build Phase

The GitHub Actions workflow (`Publish.yml`) includes SARIF generation in the build step:

1. **Build Projects**: All projects are built in Debug configuration
2. **Collect SARIF Files**: All `roslyn.sarif` files are collected from project directories
3. **Upload Artifacts**: SARIF files are uploaded as artifacts for downstream jobs

### Codacy Upload Phase

A dedicated job (`Codacy_SARIF_Upload`) handles SARIF file uploads:

1. **Download SARIF Reports**: Retrieves SARIF artifacts from the build job
2. **Install Codacy CLI**: Downloads the latest Codacy Analysis CLI
3. **Upload to Codacy**: Uploads each SARIF file using the Codacy CLI with project metadata

## Local Development

### Generate SARIF Files

SARIF files are automatically generated during any build:

```bash
dotnet build --configuration Debug
```

After building, SARIF files will be located in each project directory:
- `lib/roslyn.sarif` - Main library analysis
- `quantower/roslyn.sarif` - Quantower adapter analysis

### View SARIF Files

SARIF files are JSON-formatted and can be viewed with:
- Visual Studio Code with SARIF Viewer extension
- Any text editor (JSON format)
- Codacy web interface (after upload)

## Analyzers Included

The following Roslyn analyzers contribute to the SARIF reports:

1. **Roslynator.Analyzers** (v4.12.9)
   - Code style and quality rules
   - Performance optimizations
   - Modern C# patterns

2. **Meziantou.Analyzer** (v2.0.183)
   - Security and correctness rules
   - API usage guidelines
   - Best practices enforcement

3. **SonarAnalyzer.CSharp** (v10.x)
   - Code smells and bugs
   - Security vulnerabilities
   - Maintainability issues

4. **.NET SDK Analyzers**
   - Framework-specific rules
   - API compatibility
   - Performance guidelines

## Suppressed Rules

Certain rules are suppressed globally in `Directory.Build.props`:

```xml
<NoWarn>$(NoWarn);S1144;S1944;S2053;S2245;S2259;S2583;S2589;S3329;S3655;S3776;S3949;S3966;S4158;S4347;S5773;S6781;MA0048;MA0051</NoWarn>
```

These suppressions are intentional design decisions aligned with QuanTAlib's high-performance requirements.

## Codacy Integration

### Required Secrets

The GitHub Actions workflow requires the following secret:

- `CODACY_PROJECT_TOKEN`: API token for uploading results to Codacy

### Upload Process

1. SARIF files are collected after build
2. Each SARIF file is uploaded individually
3. Results are associated with the specific commit SHA
4. Tool identifier: `roslyn`
5. Upload continues even if individual files fail

### View Results

Analysis results are available at:
https://app.codacy.com/gh/mihakralj/QuanTAlib

## Troubleshooting

### SARIF Not Generated

If SARIF files are not being generated:

1. Verify `ErrorLog` property is set in `Directory.Build.props`
2. Ensure analyzers are installed (check NuGet packages)
3. Build in Debug or Release configuration (not Clean)
4. Check MSBuild output for analyzer warnings

### Upload Failures

If Codacy uploads fail:

1. Verify `CODACY_PROJECT_TOKEN` secret is set
2. Check GitHub Actions logs for specific errors
3. Ensure SARIF files contain valid JSON
4. Verify network connectivity to Codacy API

### Large SARIF Files

If SARIF files become too large:

1. Increase `upload-batch-size` in the workflow
2. Consider splitting uploads by project
3. Review suppressed warnings (might need adjustment)
4. Use `--upload-batch-size 100000` for very large files

## Performance Impact

- **Build Time**: +5-10% due to analyzer execution
- **SARIF Generation**: <1s per project
- **File Size**: 100KB-500KB per project
- **Upload Time**: 2-5s per SARIF file

## Future Enhancements

Potential improvements for consideration:

1. **Differential Analysis**: Upload only changed files
2. **Parallel Uploads**: Upload multiple SARIF files concurrently
3. **Local Validation**: Pre-commit hooks to validate SARIF
4. **Custom Rules**: Project-specific analyzer configurations
5. **Trend Analysis**: Track metrics over time
