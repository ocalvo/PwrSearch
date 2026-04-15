#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$proj = Join-Path $root 'src\SearchDir.csproj'

dotnet build $proj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$dll = Join-Path $root "src\bin\$Configuration\netstandard2.0\SearchDir.dll"
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

Copy-Item -Path $dll -Destination (Join-Path $root 'SearchDir.dll') -Force
Write-Host "Copied SearchDir.dll to module root." -ForegroundColor Green
