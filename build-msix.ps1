# Builds the Microsoft Store package: dist\DisplayControl_<version>.msixbundle (x64 + arm64).
# Needs the .NET 10 SDK and the Windows 10/11 SDK (for makeappx.exe and makepri.exe).
#
# Local test build (default identity):
#   powershell -ExecutionPolicy Bypass -File .\build-msix.ps1
# Store build - use the values from Partner Center > Product management > Product identity:
#   powershell -ExecutionPolicy Bypass -File .\build-msix.ps1 -IdentityName "12345Name.DisplayControl" `
#       -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Your publisher name" `
#       -DisplayName "The app name you reserved"
# The Store signs the package itself, so Store uploads don't need a certificate.

param(
    [string]$Version = "1.0.1.0",
    [string]$IdentityName = "DisplayControl.Dev",
    [string]$Publisher = "CN=DisplayControlDev",
    [string]$PublisherDisplayName = "Riyaj Kafardeen",
    [string]$DisplayName = "Display Control",
    [string[]]$Architectures = @("x64", "arm64")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "src\DisplayControl\DisplayControl.csproj"
$work = Join-Path $root "obj\msix"
$dist = Join-Path $root "dist"

# Newest Windows SDK tools.
$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $sdkBin) { throw "makeappx.exe not found. Install the Windows SDK: winget install Microsoft.WindowsSDK.10.0.26100" }
$makeappx = $sdkBin.FullName
$makepri = Join-Path $sdkBin.DirectoryName "makepri.exe"

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work, $dist | Out-Null
$bundleIn = Join-Path $work "bundle"
New-Item -ItemType Directory -Force $bundleIn | Out-Null

foreach ($arch in $Architectures) {
    Write-Host "== $arch"
    $layout = Join-Path $work "$arch\layout"

    # Self-contained: Store users don't need the .NET runtime installed.
    dotnet publish $proj -c Release -r "win-$arch" --self-contained true -o $layout --nologo -p:Version=$($Version.Substring(0, $Version.LastIndexOf('.')))
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $arch" }
    Remove-Item (Join-Path $layout "*.pdb") -ErrorAction SilentlyContinue

    Copy-Item (Join-Path $root "packaging\Assets") (Join-Path $layout "Assets") -Recurse -Force

    $manifest = Get-Content (Join-Path $root "packaging\Package.appxmanifest") -Raw
    $manifest = $manifest.Replace('$IDENTITY_NAME$', $IdentityName).Replace('$PUBLISHER$', $Publisher).
        Replace('$PUBLISHER_DISPLAY_NAME$', $PublisherDisplayName).Replace('$DISPLAY_NAME$', $DisplayName).
        Replace('$VERSION$', $Version).Replace('$ARCH$', $arch)
    Set-Content (Join-Path $layout "AppxManifest.xml") $manifest -Encoding UTF8

    # resources.pri lets Windows pick the right icon size (scale-200, targetsize-24, unplated...).
    $priConfig = Join-Path $work "$arch\priconfig.xml"
    & $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
    & $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri failed for $arch" }

    $msix = Join-Path $bundleIn "DisplayControl_${Version}_$arch.msix"
    & $makeappx pack /d $layout /p $msix /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $arch" }
    Write-Host "Packed $msix"
}

$bundle = Join-Path $dist "DisplayControl_$Version.msixbundle"
& $makeappx bundle /d $bundleIn /p $bundle /bv $Version /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed" }
Copy-Item (Join-Path $bundleIn "*.msix") $dist -Force
Write-Host "Built $bundle"
