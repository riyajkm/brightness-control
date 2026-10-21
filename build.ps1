# Builds bin\BrightnessControl.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# Usage:  powershell -ExecutionPolicy Bypass -File .\build.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out  = Join-Path $root "bin\BrightnessControl.exe"

# Stop a running copy so the exe can be overwritten.
Get-Process BrightnessControl -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force (Join-Path $root "bin") | Out-Null

& $csc /nologo /target:winexe /optimize+ /out:$out `
    /r:System.Management.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    (Join-Path $root "src\BrightnessControl.cs")

if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $out"
