Write-Host "Cleaning Core..."
dotnet clean Core.slnf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Restoring Core..."
dotnet restore Core.slnf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building Core..."
dotnet build Core.slnf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Testing Core..."
dotnet test Core.slnf
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Core Build & Test Complete!"
