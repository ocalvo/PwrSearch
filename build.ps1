#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [switch]$Force,
    [string]$DotnetVersion = '10'
)

# The body runs in a child scope on purpose.
#
# The manifest runs this file through ScriptsToProcess, which executes in the
# CALLER'S session state - the global scope of whoever imported PwrSearch. A
# bare `$ErrorActionPreference = 'Stop'` there is not a setting for this build,
# it is a setting for that entire session, and it stays after the import
# returns. In a long-lived session - a persistent shell, an agent runspace -
# every later non-terminating error then becomes terminating, which presents as
# commands that silently produce nothing.
#
# Inside `& { }` the assignment is local and disappears with the scope, so the
# build still fails fast without redefining anyone else's error handling.
& {
    $ErrorActionPreference = 'Stop'

    $root = $PSScriptRoot
    $proj = Join-Path $root 'src' 'SearchDir.csproj'
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

    $dll = Join-Path $root 'src' 'bin' $Configuration 'netstandard2.0' 'SearchDir.dll'
    if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

    Copy-Item -Path $dll -Destination (Join-Path $root 'SearchDir.dll') -Force
    Write-Verbose "Copied SearchDir.dll to module root."
}
