---
name: pwrsearch-navigation
description: "Read when searching for files or directories or navigating the filesystem with sd / go / Search-Directory / Get-RepoRoot, or when PwrSearch fails to import. Covers the sd pattern syntax and built-in excludes, go named locations and their runtime extension, Get-RepoRoot marker walking including .git-as-a-file worktrees, that SearchDir.dll is gitignored and build.ps1 must be run once after cloning, and the retired _XROOT / _sd / Goto-KnownLocation names."
---

# PwrSearch - search and navigation

Fast directory and file search. The heavy lifting is a native C# module,
`SearchDir.dll`, built from `src/` and loaded as a nested module.

## `sd <pattern> [-All]`

Search from the repo root (or cwd when there is no repo) and emit matching
`DirectoryInfo` objects. Built-in excludes: `obj`, `objd`, `objr`, `objc`, `bin`,
`.git`, `node_modules`.

```powershell
sd foo.cpp           # first match
sd foo.cpp -All      # all matches
sd 'src\tools'       # multi-part path pattern
sd src/tools         # forward slashes are normalised
```

## `go` / `Switch-Location`

Named navigation with an `sd` fallback.

```powershell
go home      # ~
go src       # C:\src
go bin       # C:\bin
go scripts   # the profile's directory
go <name>    # any key in $go_locations, else an sd fallback search
```

Extend at runtime:

```powershell
$go_locations['myrepo'] = 'C:\repos\myrepo'
```

## `Search-Directory` (C#)

Breadth-first search with exclude-dir pruning, case-insensitive ordinal
matching, wildcards, and multi-part path patterns.

```powershell
Search-Directory -SearchDirectories $root `
                 -ExcludeDirectories 'bin','obj','.git' `
                 -Pattern 'Foo' `
                 [-SubstringMatch $true] [-All]
```

## `Get-RepoRoot` (C#)

Walks parent directories from `-Path` (default: current location) looking for
`-Marker` (default: `.git`). Returns the first directory containing it, or
nothing. Handles `.git` as a **directory and as a file** - the latter is how
submodules and worktrees look, and is the case naive implementations miss.

```powershell
Get-RepoRoot
Get-RepoRoot -Path .\some\subdir
Get-RepoRoot -Marker 'pnpm-workspace.yaml'
```

Implemented in C# for speed, using `System.IO.Path` / `Directory.Exists` /
`File.Exists` directly and bypassing the PowerShell provider layer.

## Build - required after cloning

`SearchDir.dll` is **gitignored**, so a fresh clone cannot import the module
until it is built:

```powershell
.\build.ps1                          # Release; copies the DLL next to the psm1
.\build.ps1 -Configuration Debug
```

If `Import-Module PwrSearch` fails on a new machine, this is almost always why.
Note the manifest runs `build.ps1` through `ScriptsToProcess`, which executes in
the **caller's** global scope - and that script sets
`$ErrorActionPreference = 'Stop'`. In a long-lived session that leaks and changes
error behaviour for everything afterwards; see the `pwsh-session-mcp` skill.

## Retired names

Earlier versions used `$env:_XROOT` (a build-environment variable), a `_sd`
helper, and
`Goto-KnownLocation`. All three are gone:

- `_XROOT` → `Get-RepoRoot` (portable, git-based, C# fast path)
- `_sd` → `sd`
- `Goto-KnownLocation` → `Switch-Location` (an approved verb, which silences the
  unapproved-verb warning at import time)

The `go` alias is unchanged. References to the old three in downstream code need
updating.

## Contributing

release-please with Conventional Commits; never commit to `main`. See the
`release-please-prs` skill for the title grammar.
