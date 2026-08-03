[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $frameworkDirectory "csc.exe"
$testExecutable = Join-Path $env:TEMP "ServicesPrechecker-ForensicScrollBarStyleTests.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# compiler was not found at $compiler"
}

$references = @(
    (Join-Path $frameworkDirectory "System.dll"),
    (Join-Path $frameworkDirectory "System.Core.dll"),
    (Join-Path $frameworkDirectory "System.Management.dll"),
    (Join-Path $frameworkDirectory "System.ServiceProcess.dll"),
    (Join-Path $frameworkDirectory "System.Web.Extensions.dll"),
    (Join-Path $frameworkDirectory "WPF\WindowsBase.dll"),
    (Join-Path $frameworkDirectory "WPF\PresentationCore.dll"),
    (Join-Path $frameworkDirectory "WPF\PresentationFramework.dll"),
    (Join-Path $frameworkDirectory "System.Xaml.dll")
)

$arguments = @(
    "/nologo",
    "/utf8output",
    "/target:exe",
    "/platform:x64",
    "/main:ForensicScrollBarStyleTests",
    "/out:$testExecutable"
)

foreach ($reference in $references) {
    $arguments += "/reference:$reference"
}

$arguments += Get-ChildItem -LiteralPath (Join-Path $projectRoot "src\ServicesPrechecker") -Filter *.cs |
    Sort-Object Name |
    ForEach-Object FullName
$arguments += Join-Path $PSScriptRoot "ForensicScrollBarStyleTests.cs"

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Forensic scroll bar style test compilation failed with exit code $LASTEXITCODE."
}

& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "Forensic scroll bar style tests failed with exit code $LASTEXITCODE."
}
