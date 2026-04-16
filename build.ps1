#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [switch]$Force,
    [string]$DotnetVersion = '10'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$proj = Join-Path $root 'src\SearchDir.csproj'
$target = Join-Path $root 'SearchDir.dll'

if (-not $Force -and (Test-Path $target)) {
    Write-Verbose "SearchDir.dll already present at $target; skipping build. Pass -Force to rebuild."
    return
}

$sdks = dotnet --list-sdks 2>&1
if (-not $sdks -or ($sdks -is [System.Management.Automation.ErrorRecord])) {
    if (Get-Command apk -ErrorAction SilentlyContinue) {
        Write-Host "No .NET SDK found; installing dotnet${DotnetVersion}-sdk via apk..."
        & apk add --no-progress "dotnet${DotnetVersion}-sdk"
    } elseif (Get-Command apt-get -ErrorAction SilentlyContinue) {
        Write-Host "No .NET SDK found; installing dotnet-sdk-${DotnetVersion}.0 via apt-get..."
        & apt-get install -y "dotnet-sdk-${DotnetVersion}.0"
    }
}

Write-Host "Building SearchDir.dll..."
dotnet build $proj -c $Configuration | Write-Verbose
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$dll = Join-Path $root "src\bin\$Configuration\netstandard2.0\SearchDir.dll"
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

Copy-Item -Path $dll -Destination (Join-Path $root 'SearchDir.dll') -Force
Write-Verbose "Copied SearchDir.dll to module root."
