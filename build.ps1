# Builds bin\BrightnessControl.exe with the .NET 10 SDK.
# Usage:  powershell -ExecutionPolicy Bypass -File .\build.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\BrightnessControl\BrightnessControl.csproj"
$out  = Join-Path $root "bin"

# Stop a running copy so the exe can be overwritten.
Get-Process BrightnessControl -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet publish $proj -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $(Join-Path $out 'BrightnessControl.exe')"
