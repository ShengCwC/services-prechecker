[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $frameworkDirectory "csc.exe"
$testExecutable = Join-Path $env:TEMP "ServicesPrechecker-HardwareIdProviderTests.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# compiler was not found at $compiler"
}

$arguments = @(
    "/nologo",
    "/utf8output",
    "/target:exe",
    "/out:$testExecutable",
    "/reference:$(Join-Path $frameworkDirectory 'System.dll')",
    "/reference:$(Join-Path $frameworkDirectory 'System.Core.dll')",
    "/reference:$(Join-Path $frameworkDirectory 'System.Management.dll')",
    (Join-Path $projectRoot "src\ServicesPrechecker\HardwareIdProvider.cs"),
    (Join-Path $PSScriptRoot "HardwareIdProviderTests.cs")
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "HWID test compilation failed with exit code $LASTEXITCODE."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "HWID tests failed with exit code $LASTEXITCODE."
}
