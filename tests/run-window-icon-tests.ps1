[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $frameworkDirectory "csc.exe"
$testExecutable = Join-Path $env:TEMP "ServicesPrechecker-WindowIconLoaderTests.exe"

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
    "/reference:$(Join-Path $frameworkDirectory 'WPF\WindowsBase.dll')",
    "/reference:$(Join-Path $frameworkDirectory 'WPF\PresentationCore.dll')",
    "/reference:$(Join-Path $frameworkDirectory 'System.Xaml.dll')",
    "/resource:$(Join-Path $projectRoot 'assets\app.ico'),UndefinedSS.ServicesPrechecker.Tests.Assets.app.ico",
    (Join-Path $projectRoot "src\ServicesPrechecker\WindowIconLoader.cs"),
    (Join-Path $PSScriptRoot "WindowIconLoaderTests.cs")
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Window icon loader test compilation failed with exit code $LASTEXITCODE."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "Window icon loader tests failed with exit code $LASTEXITCODE."
}
