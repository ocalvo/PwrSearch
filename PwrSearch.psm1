########################################################
# Directory search from the repo root (or cwd if there is no repo).
function sd
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string] $Pattern,

        [switch] $All
    )

    $Pattern = $Pattern.Replace('/', '\')

    $root = Get-RepoRoot
    if (-not $root) { $root = (Get-Location).Path }

    [string[]] $exclude = @('obj', 'objd', 'objr', 'objc', 'bin', '.git', 'node_modules')

    if ($All.IsPresent)
    {
        Search-Directory -SearchDirectories $root -ExcludeDirectories $exclude -Pattern $Pattern -All
    }
    else
    {
        Search-Directory -SearchDirectories $root -ExcludeDirectories $exclude -Pattern $Pattern
    }
}

function _gosd
{
    param([string] $pattern)

    if (Test-Path $pattern)
    {
        Push-Location (Get-Item $pattern).FullName
        return $true
    }

    $dir = sd $pattern
    if ($dir)
    {
        Push-Location $dir.FullName
        return $true
    }

    return $false
}

########################################################
# `go` — named navigation with `sd` fallback.

if ($null -eq $global:go_locations)
{
    $global:go_locations = @{}
}

function Switch-Location
{
    [CmdletBinding()]
    param([string] $Name)

    if ($go_locations.ContainsKey($Name))
    {
        Set-Location $go_locations[$Name]
    }
    else
    {
        if (-not (_gosd $Name))
        {
            Write-Output 'The following locations are defined:'
            Write-Output $go_locations
        }
    }
}

$go_locations['home']    = '~'
$go_locations['src']     = 'C:\src'
$go_locations['bin']     = 'C:\bin'
$go_locations['scripts'] = (Get-Item $profile).Directory.FullName

Set-Alias go Switch-Location

Export-ModuleMember -Function Switch-Location, sd -Cmdlet * -Alias go
