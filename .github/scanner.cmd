@echo off
setlocal EnableDelayedExpansion

REM Get tokens from Windows User environment
for /f "tokens=*" %%a in ('powershell -NoProfile -Command "[System.Environment]::GetEnvironmentVariable('SONAR_TOKEN', 'User')"') do set "SONAR_TOKEN=%%a"
for /f "tokens=*" %%a in ('powershell -NoProfile -Command "[System.Environment]::GetEnvironmentVariable('CODACY_PROJECT_TOKEN', 'User')"') do set "CODACY_PROJECT_TOKEN=%%a"
for /f "tokens=*" %%a in ('powershell -NoProfile -Command "[System.Environment]::GetEnvironmentVariable('QODANA_TOKEN', 'User')"') do set "QODANA_TOKEN=%%a"

REM Clean corrupted obj directories using PowerShell (more robust for NTFS issues)
echo Cleaning build artifacts with PowerShell...
powershell -NoProfile -Command "Get-ChildItem -Path '%~dp0..' -Directory -Filter 'obj' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"
powershell -NoProfile -Command "Get-ChildItem -Path '%~dp0..' -Directory -Filter 'bin' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"
REM Also clean TestResults and .sonarqube
powershell -NoProfile -Command "Remove-Item -Path '%~dp0..\TestResults' -Recurse -Force -ErrorAction SilentlyContinue"
powershell -NoProfile -Command "Remove-Item -Path '%~dp0..\.sonarqube' -Recurse -Force -ErrorAction SilentlyContinue"

REM Convert script path to WSL format using PowerShell for reliable drive letter conversion
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$p='%~dp0scanner.sh'; $d=$p.Substring(0,1).ToLower(); '/mnt/' + $d + $p.Substring(2).Replace('\','/')"` ) do set "SCRIPT_PATH=%%P"

REM Run scanner with tokens passed via environment (as root for tool access)
wsl -d Debian -u root -- env SONAR_TOKEN="%SONAR_TOKEN%" CODACY_PROJECT_TOKEN="%CODACY_PROJECT_TOKEN%" QODANA_TOKEN="%QODANA_TOKEN%" bash "%SCRIPT_PATH%" %*
