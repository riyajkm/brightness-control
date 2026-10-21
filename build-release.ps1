# Builds the portable downloads for a GitHub release:
#   dist\DisplayControl-<version>-win-x64.zip and -win-arm64.zip
# Each holds one self-contained DisplayControl.exe (no .NET install needed) plus the README and licence.
# Usage:  powershell -ExecutionPolicy Bypass -File .\build-release.ps1

param([string[]]$Architectures = @("x64", "arm64"))

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\DisplayControl\DisplayControl.csproj"
$dist = Join-Path $root "dist"
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($arch in $Architectures) {
    $out = Join-Path $root "obj\release\$arch"
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish $proj -c Release -r "win-$arch" --self-contained true -o $out --nologo `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $arch" }

    $stage = Join-Path $root "obj\release\stage-$arch"
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item (Join-Path $out "DisplayControl.exe") $stage
    Copy-Item (Join-Path $root "README.md"), (Join-Path $root "LICENSE") $stage

    $zip = Join-Path $dist "DisplayControl-$version-win-$arch.zip"
    Remove-Item $zip -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Write-Host "Built $zip"
}
