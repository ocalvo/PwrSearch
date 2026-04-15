# PwrSearch

Fast directory search and navigation for PowerShell, powered by a native `Search-Directory` cmdlet written in C#.

Built for very large source trees (Razzle-sized enlistments, Chromium, monorepos) where `Get-ChildItem -Recurse` is too slow. Search a quarter-million-file tree and jump to the match in a single command.

## Features

- **`Search-Directory`** — a native cmdlet that streams matches as it walks the tree, with pluggable exclude-dir pruning, case-insensitive ordinal matching, wildcards, and multi-part path patterns.
- **`_sd`** — thin PowerShell wrapper around `Search-Directory` that auto-detects a Razzle root (`$env:_XROOT`) and excludes build-output dirs (`obj`, `objd`, `objr`, `objc`, plus `Tools`, `public`, `SetupAuthoring` when rooted).
- **`go`** — friendly `cd`-with-search: jumps to a named location from `$go_locations` or falls back to `_sd` and pushes the first match.
- **Cross-edition** — targets `netstandard2.0`, works on Windows PowerShell 5.1 and PowerShell 7 (Linux/macOS included, though some excludes are Windows-flavored).
- **No binary in git** — `SearchDir.dll` is built from source by `build.ps1`; CI builds and publishes to the PowerShell Gallery.

## Install

From the PowerShell Gallery:

```powershell
Install-Module PwrSearch
```

From source (contributors):

```powershell
git clone https://github.com/ocalvo/PwrSearch.git
cd PwrSearch
.\build.ps1                          # dotnet build + copy SearchDir.dll next to the psm1
Import-Module .\PwrSearch.psd1
```

## Usage

### `Search-Directory`

```powershell
Search-Directory `
    -SearchDirectories C:\src `
    -ExcludeDirectories 'obj','bin','.git' `
    -Pattern 'MyProject' `
    [-SubstringMatch $true] `
    [-All]
```

- **`-SearchDirectories`** — one or more roots to search.
- **`-ExcludeDirectories`** — bare names (matched by `Name`, case-insensitive) *and/or* rooted absolute paths (matched by `FullName`). Checks are `HashSet<string>` lookups, so excludes are O(1) per directory.
- **`-Pattern`** — match string. Backslash splits it into **ordered path parts**: `'src\tools'` matches any `src/*/tools` where both names match their respective directory. Wildcards (`*`) allowed inside each part.
- **`-SubstringMatch $true`** — match anywhere in the directory name (default is starts-with).
- **`-All`** — return every match. Without it, streams the first match and stops.

Results are emitted as `DirectoryInfo` objects, so you can pipe into any filesystem cmdlet:

```powershell
Search-Directory -SearchDirectories C:\src -ExcludeDirectories obj,bin -Pattern 'tests' -All |
    ForEach-Object { $_ | Get-ChildItem -Filter '*.cs' }
```

### `_sd` (the fast path)

Searches from `$env:_XROOT` if set, otherwise from the current location. Automatically excludes common build-output dirs.

```powershell
_sd foo.cpp           # first match
_sd foo.cpp -All      # all matches
_sd 'src\tools'       # multi-part path pattern
```

### `go` — named navigation

```powershell
go home      # ~
go src       # C:\src
go bin       # C:\bin
go scripts   # $profile's directory
go <name>    # any key in $go_locations, or a _sd fallback search
```

Extend with your own:

```powershell
$go_locations['myrepo'] = 'C:\repos\myrepo'
```

## How the search works

`Search-Directory` walks the tree breadth-first but **prioritizes already-matched path parts** — once a pattern part matches, its child states move to a higher depth bucket, which drains first. Within a bucket, exact-name matches (strength 2) outrank substring matches (strength 1), so you typically hit the intended directory in the first few results even on huge trees.

Hot-path optimizations worth knowing about:

- Exclude-dir filtering is O(1) per directory (`HashSet<string>` with `OrdinalIgnoreCase`), not O(n) over a predicate list.
- No LINQ / no allocated enumerators in the child-iteration loop.
- Wildcard patterns compile to `RegexOptions.Compiled | CultureInvariant`.
- Non-wildcard matchers use `string.StartsWith`/`string.IndexOf` with `StringComparison.Ordinal*` — no culture tables, no per-call allocation.
- Inaccessible directories are silently skipped (`UnauthorizedAccessException`, `DirectoryNotFoundException`).

## Build

`build.ps1` runs `dotnet build` on `src/SearchDir.csproj` and copies the resulting DLL next to `PwrSearch.psm1`:

```powershell
.\build.ps1                          # Release (default)
.\build.ps1 -Configuration Debug
```

Project layout:

```
PwrSearch/
├── PwrSearch.psd1            # module manifest (ModuleVersion is x-release-please-version)
├── PwrSearch.psm1            # PowerShell glue: _sd, _gosd, go / Goto-KnownLocation
├── SearchDir.dll             # built by build.ps1 (gitignored)
├── build.ps1                 # dotnet build + copy
├── src/
│   ├── SearchDir.csproj      # SDK-style, netstandard2.0, PowerShellStandard.Library
│   └── SearchDirectory.cs    # the Search-Directory cmdlet
└── .github/workflows/
    ├── release-please.yml    # opens/maintains a release PR from conventional commits
    └── publish.yml           # on release: setup-dotnet → build → verify manifest → Publish-Module
```

## Release automation

Versioning and publishing are driven by [release-please](https://github.com/googleapis/release-please):

1. Commit with [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `feat!:` …) on `main`.
2. `release-please.yml` opens (or updates) a release PR that bumps `PwrSearch.psd1`'s `ModuleVersion` via the `x-release-please-version` marker and regenerates `CHANGELOG.md`.
3. Merge the release PR → GitHub creates a tagged release.
4. `publish.yml` fires on `release: published`: sets up .NET, runs `build.ps1`, verifies the manifest version matches the tag, and runs `Publish-Module` with the `PSGALLERY_API_KEY` secret.

## License

MIT — see [LICENSE](LICENSE).
