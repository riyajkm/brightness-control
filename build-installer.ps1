# Builds the installer: dist\DisplayControl-<version>-setup.exe (one setup for x64 and ARM64 PCs).
# Needs the .NET 10 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
# Usage:  powershell -ExecutionPolicy Bypass -File .\build-installer.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\DisplayControl\DisplayControl.csproj"
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$stage = Join-Path $root "obj\installer"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it: winget install JRSoftware.InnoSetup" }

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

# Self-contained single-file exe like the portable zips, but left uncompressed: the installer's solid LZMA
# compression then shares the identical managed code between the x64 and ARM64 builds (~70 MB instead of ~97 MB).
foreach ($arch in "x64", "arm64") {
    $out = Join-Path $root "obj\installer-publish\$arch"
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish $proj -c Release -r "win-$arch" --self-contained true -o $out --nologo `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=none
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $arch" }
    Copy-Item (Join-Path $out "DisplayControl.exe") (Join-Path $stage "DisplayControl-$arch.exe")
}

& $iscc /Q "/DAppVersion=$version" "/DStage=$stage" (Join-Path $root "installer\DisplayControl.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
Write-Host "Built $(Join-Path $root "dist\DisplayControl-$version-setup.exe")"
