[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $frameworkDirectory "csc.exe"
$testExecutable = Join-Path $env:TEMP "ServicesPrechecker-ServicePolicyTests.exe"

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
    "/reference:$(Join-Path $frameworkDirectory 'System.ServiceProcess.dll')",
    (Join-Path $projectRoot "src\ServicesPrechecker\ServiceModels.cs"),
    (Join-Path $projectRoot "src\ServicesPrechecker\ServiceControlOperations.cs"),
    (Join-Path $projectRoot "src\ServicesPrechecker\ServiceManager.cs"),
    (Join-Path $PSScriptRoot "ServiceStatePolicyTests.cs")
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Service policy test compilation failed with exit code $LASTEXITCODE."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "Service policy tests failed with exit code $LASTEXITCODE."
}
