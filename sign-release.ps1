[CmdletBinding(DefaultParameterSetName = "CertificateStore")]
param(
    [string]$ReleaseDirectory = "",

    [Parameter(Mandatory = $true, ParameterSetName = "CertificateStore")]
    [string]$CertificateThumbprint,

    [Parameter(ParameterSetName = "CertificateStore")]
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertificateStoreLocation = "CurrentUser",

    [Parameter(Mandatory = $true, ParameterSetName = "ArtifactSigning")]
    [string]$ArtifactSigningMetadata,

    [Parameter(Mandatory = $true, ParameterSetName = "ArtifactSigning")]
    [string]$ArtifactSigningDlib,

    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = Join-Path $root "release\win-x64"
}
$ReleaseDirectory = [System.IO.Path]::GetFullPath($ReleaseDirectory)

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        if ($command.Path) { return $command.Path }
        if ($command.Source) { return $command.Source }
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $candidate = Get-ChildItem -Path (Join-Path $kitsRoot "*\x64\signtool.exe") -File -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    throw "SignTool.exe was not found. Install current Windows SDK Build Tools."
}

function Assert-PublicCodeSigningCertificate([string]$thumbprint, [string]$storeLocation) {
    $normalized = $thumbprint -replace "[^0-9A-Fa-f]", ""
    if ($normalized.Length -ne 40) {
        throw "CertificateThumbprint must be a 40-character Windows certificate thumbprint."
    }

    $certificatePath = "Cert:\$storeLocation\My\$normalized"
    if (-not (Test-Path $certificatePath)) {
        throw "The signing certificate was not found at $certificatePath."
    }

    $certificate = Get-Item $certificatePath
    if (-not $certificate.HasPrivateKey) {
        throw "The selected certificate has no accessible private key."
    }
    if ($certificate.NotAfter -le (Get-Date)) {
        throw "The selected certificate has expired."
    }
    if ($certificate.Subject -eq $certificate.Issuer) {
        throw "A self-signed certificate is not suitable for a public release or SmartScreen reputation."
    }

    $codeSigningEku = $certificate.EnhancedKeyUsageList |
        Where-Object { $_.ObjectId.Value -eq "1.3.6.1.5.5.7.3.3" } |
        Select-Object -First 1
    if (-not $codeSigningEku) {
        throw "The selected certificate is not valid for code signing."
    }
    return $normalized
}

if (-not (Test-Path $ReleaseDirectory -PathType Container)) {
    throw "Release directory does not exist: $ReleaseDirectory"
}
if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
    throw "An RFC 3161 timestamp URL is required."
}

$signTool = Find-SignTool
$ownedFiles = @(
    (Join-Path $ReleaseDirectory "SensitivityRandomizer.exe"),
    (Join-Path $ReleaseDirectory "SensitivityResetGuard.exe")
)
foreach ($file in $ownedFiles) {
    if (-not (Test-Path $file -PathType Leaf)) { throw "Release file is missing: $file" }
}

if ($PSCmdlet.ParameterSetName -eq "CertificateStore") {
    $normalizedThumbprint = Assert-PublicCodeSigningCertificate $CertificateThumbprint $CertificateStoreLocation
} else {
    if (-not (Test-Path $ArtifactSigningMetadata -PathType Leaf)) {
        throw "Artifact Signing metadata file does not exist: $ArtifactSigningMetadata"
    }
    if (-not (Test-Path $ArtifactSigningDlib -PathType Leaf)) {
        throw "Artifact Signing dlib does not exist: $ArtifactSigningDlib"
    }
    $metadata = Get-Content $ArtifactSigningMetadata -Raw | ConvertFrom-Json
    foreach ($property in @("Endpoint", "CodeSigningAccountName", "CertificateProfileName")) {
        if ([string]::IsNullOrWhiteSpace($metadata.$property)) {
            throw "Artifact Signing metadata is missing $property."
        }
    }
}

foreach ($file in $ownedFiles) {
    if ($PSCmdlet.ParameterSetName -eq "CertificateStore") {
        $arguments = @(
            "sign", "/v", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256",
            "/sha1", $normalizedThumbprint, "/d", "Sensitivity Randomizer",
            "/du", "https://dinati.ru/"
        )
        if ($CertificateStoreLocation -eq "LocalMachine") { $arguments += "/sm" }
        $arguments += $file
    } else {
        $arguments = @(
            "sign", "/v", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256",
            "/d", "Sensitivity Randomizer", "/du", "https://dinati.ru/",
            "/dlib", ([System.IO.Path]::GetFullPath($ArtifactSigningDlib)),
            "/dmdf", ([System.IO.Path]::GetFullPath($ArtifactSigningMetadata)),
            $file
        )
    }

    & $signTool @arguments
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file with exit code $LASTEXITCODE." }

    & $signTool verify /pa /all /v $file
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file." }

    $signature = Get-AuthenticodeSignature -FilePath $file
    if ($signature.Status -ne "Valid") {
        throw "Windows Authenticode validation failed for $file: $($signature.StatusMessage)"
    }
    Write-Host "Signed and verified: $file"
}

$hashNames = @(
    "SensitivityRandomizer.exe",
    "SensitivityResetGuard.exe",
    "SensitivityResetGuard.exe.config",
    "SensitivityRandomizer.exe.config",
    "wrapper.dll",
    "Newtonsoft.Json.dll"
)
$hashLines = foreach ($name in $hashNames) {
    $path = Join-Path $ReleaseDirectory $name
    if (-not (Test-Path $path -PathType Leaf)) { throw "Hash target is missing: $path" }
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[System.IO.File]::WriteAllLines((Join-Path $ReleaseDirectory "SHA256SUMS.txt"), $hashLines)

Write-Host "Release signatures are valid and SHA256SUMS.txt has been regenerated."
