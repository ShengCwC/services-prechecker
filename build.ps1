[CmdletBinding()]
param(
    [string]$OutputDirectory = "dist"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$frameworkDirectory = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $frameworkDirectory "csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# compiler was not found at $compiler"
}

$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$resolvedProject = [System.IO.Path]::GetFullPath($projectRoot)
if (-not $resolvedOutput.StartsWith($resolvedProject, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside the project directory."
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

$assemblyInfoPath = Join-Path $projectRoot "src\ServicesPrechecker\AssemblyInfo.cs"
$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
$versionMatch = [regex]::Match(
    $assemblyInfo,
    'AssemblyInformationalVersion\("(?<version>[^"]+)"\)')
if (-not $versionMatch.Success) {
    throw "AssemblyInformationalVersion was not found in $assemblyInfoPath"
}

$productVersion = $versionMatch.Groups["version"].Value
if ($productVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "AssemblyInformationalVersion is not a release-safe version: $productVersion"
}

$executableName = "ServicesPrechecker-v$productVersion.exe"
$exePath = Join-Path $resolvedOutput $executableName
$checksumPath = "$exePath.sha256"
$pdbPath = Join-Path $resolvedOutput "ServicesPrechecker-v$productVersion.pdb"

foreach ($artifact in @($exePath, $checksumPath, $pdbPath)) {
    if (Test-Path -LiteralPath $artifact) {
        Remove-Item -LiteralPath $artifact -Force
    }
}

$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot "src\ServicesPrechecker") -Filter *.cs |
    Sort-Object Name |
    ForEach-Object FullName

$references = @(
    (Join-Path $frameworkDirectory "System.dll"),
    (Join-Path $frameworkDirectory "System.Core.dll"),
    (Join-Path $frameworkDirectory "System.ServiceProcess.dll"),
    (Join-Path $frameworkDirectory "WPF\WindowsBase.dll"),
    (Join-Path $frameworkDirectory "WPF\PresentationCore.dll"),
    (Join-Path $frameworkDirectory "WPF\PresentationFramework.dll"),
    (Join-Path $frameworkDirectory "System.Xaml.dll")
)

$arguments = @(
    "/nologo",
    "/utf8output",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/debug:pdbonly",
    "/out:$exePath",
    "/pdb:$pdbPath",
    "/win32icon:$(Join-Path $projectRoot 'assets\app.ico')",
    "/win32manifest:$(Join-Path $projectRoot 'app.manifest')",
    "/resource:$(Join-Path $projectRoot 'assets\banner.png'),UndefinedSS.ServicesPrechecker.Assets.banner.png",
    "/resource:$(Join-Path $projectRoot 'assets\logo.png'),UndefinedSS.ServicesPrechecker.Assets.logo.png",
    "/resource:$(Join-Path $projectRoot 'assets\hero-texture.png'),UndefinedSS.ServicesPrechecker.Assets.hero-texture.png",
    "/resource:$(Join-Path $projectRoot 'assets\app-logo.png'),UndefinedSS.ServicesPrechecker.Assets.app-logo.png",
    "/resource:$(Join-Path $projectRoot 'assets\app.ico'),UndefinedSS.ServicesPrechecker.Assets.app.ico"
)

foreach ($reference in $references) {
    $arguments += "/reference:$reference"
}

$arguments += $sourceFiles

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

$hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $executableName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Built: $exePath"
Write-Host "SHA-256: $hash"
