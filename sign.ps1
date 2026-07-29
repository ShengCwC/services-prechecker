[CmdletBinding(DefaultParameterSetName = "Pfx")]
param(
    [string]$ExecutablePath,

    [Parameter(Mandatory = $true, ParameterSetName = "Pfx")]
    [string]$PfxPath,

    [Parameter(Mandatory = $true, ParameterSetName = "Pfx")]
    [string]$PfxPassword,

    [Parameter(Mandatory = $true, ParameterSetName = "Store")]
    [string]$CertificateThumbprint,

    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $candidate = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "dist") `
        -Filter "ServicesPrechecker-v*.exe" |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) {
        throw "No versioned ServicesPrechecker executable was found in dist."
    }

    $ExecutablePath = $candidate.FullName
}

$resolvedExecutable = if ([System.IO.Path]::IsPathRooted($ExecutablePath)) {
    [System.IO.Path]::GetFullPath($ExecutablePath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $ExecutablePath))
}
if (-not (Test-Path -LiteralPath $resolvedExecutable)) {
    throw "Executable not found: $resolvedExecutable"
}

if ($PSCmdlet.ParameterSetName -eq "Pfx") {
    $resolvedPfx = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $PfxPath))
    if (-not (Test-Path -LiteralPath $resolvedPfx)) {
        throw "PFX file not found: $resolvedPfx"
    }

    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $certificate.Import(
        $resolvedPfx,
        $PfxPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)
}
else {
    $certificate = Get-ChildItem "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
}

if (-not $certificate.HasPrivateKey) {
    throw "The selected certificate does not contain a private key."
}

$signature = Set-AuthenticodeSignature `
    -FilePath $resolvedExecutable `
    -Certificate $certificate `
    -HashAlgorithm SHA256 `
    -TimestampServer $TimestampServer

if ($signature.Status -notin @("Valid", "UnknownError")) {
    throw "Signing failed: $($signature.Status) - $($signature.StatusMessage)"
}

$hash = (Get-FileHash -LiteralPath $resolvedExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$executableName = [System.IO.Path]::GetFileName($resolvedExecutable)
"$hash  $executableName" |
    Set-Content -LiteralPath "$resolvedExecutable.sha256" -Encoding ascii

Write-Host "Signed: $resolvedExecutable"
Write-Host "Signer: $($signature.SignerCertificate.Subject)"
Write-Host "Status: $($signature.Status)"
