param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "SensitivityRandomizer.sln"

& (Join-Path $root "tests\UiArchitectureTests.ps1")

$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $install = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($install) {
            $candidate = Join-Path $install "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) { $msbuild = Get-Item $candidate }
        }
    }
}

if (-not $msbuild) {
    throw "MSBuild not found. Install Visual Studio 2022 Build Tools with .NET desktop build tools."
}

$msbuildPath = if ($msbuild.Path) { $msbuild.Path } elseif ($msbuild.FullName) { $msbuild.FullName } else { $msbuild.Source }
& $msbuildPath $solution /m /t:Rebuild /p:Configuration=$Configuration /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

$release = Join-Path $root "release\win-x64"
New-Item -ItemType Directory -Force -Path $release | Out-Null
Copy-Item (Join-Path $root "src\SensitivityRandomizer\bin\x64\$Configuration\SensitivityRandomizer.exe") $release -Force
Copy-Item (Join-Path $root "src\SensitivityRandomizer\bin\x64\$Configuration\SensitivityRandomizer.exe.config") $release -Force
Copy-Item (Join-Path $root "src\ResetGuard\bin\x64\$Configuration\SensitivityResetGuard.exe") $release -Force
Copy-Item (Join-Path $root "src\ResetGuard\bin\x64\$Configuration\SensitivityResetGuard.exe.config") $release -Force
Copy-Item (Join-Path $root "lib\wrapper.dll") $release -Force
Copy-Item (Join-Path $root "lib\Newtonsoft.Json.dll") $release -Force
Copy-Item (Join-Path $root "README.md") $release -Force
Copy-Item (Join-Path $root "README.ru.md") $release -Force
Copy-Item (Join-Path $root "README.en.md") $release -Force
Copy-Item (Join-Path $root "CHANGELOG.md") $release -Force
Copy-Item (Join-Path $root "LICENSE.txt") $release -Force
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $release -Force
Copy-Item (Join-Path $root "SIGNING.md") $release -Force
Copy-Item (Join-Path $root "lib\LICENSE.RawAccel.txt") $release -Force
Copy-Item (Join-Path $root "docs\TROUBLESHOOTING.md") $release -Force
Copy-Item (Join-Path $root "START_HERE.txt") $release -Force
New-Item -ItemType Directory -Force -Path (Join-Path $release "assets") | Out-Null
Copy-Item (Join-Path $root "assets\SensitivityRandomizer-logo.png") (Join-Path $release "assets") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $release "docs") | Out-Null
Copy-Item (Join-Path $root "docs\*.md") (Join-Path $release "docs") -Force

$hashNames = @(
    "SensitivityRandomizer.exe",
    "SensitivityResetGuard.exe",
    "SensitivityResetGuard.exe.config",
    "SensitivityRandomizer.exe.config",
    "wrapper.dll",
    "Newtonsoft.Json.dll"
)
$hashLines = foreach ($name in $hashNames) {
    $hash = (Get-FileHash (Join-Path $release $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[System.IO.File]::WriteAllLines((Join-Path $release "SHA256SUMS.txt"), $hashLines)

Write-Host "Built: $release"
