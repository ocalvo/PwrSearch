# PwrSearch

Fast directory/file search and navigation for PowerShell. The heavy lifting lives in a native C# module, `SearchDir.dll`, built from `src/` and loaded as a nested module of `PwrSearch.psd1`.

## Cmdlets (C#, from `SearchDir.dll`)

### `Search-Directory`

Breadth-first search with exclude-dir pruning, case-insensitive ordinal matching, wildcards (`*`), and multi-part path patterns (`a\b\c`).

```powershell
Search-Directory -SearchDirectories $root `
                 -ExcludeDirectories 'bin','obj','.git' `
                 -Pattern 'Foo' `
                 [-SubstringMatch $true] `
                 [-All]
```

### `Get-RepoRoot`

Walks parent directories from `-Path` (default: current location) looking for `-Marker` (default: `.git`). Returns the first directory containing the marker, or nothing if none is found. Handles both `.git` directories *and* `.git` files (submodule worktrees).

```powershell
Get-RepoRoot                                  # current location
Get-RepoRoot -Path .\some\subdir
Get-RepoRoot -Marker 'pnpm-workspace.yaml'    # custom marker
```

Implemented in C# (`src/GetRepoRoot.cs`) for speed — uses `System.IO.Path` / `Directory.Exists` / `File.Exists` directly, bypassing the PowerShell provider layer.

## Functions (PowerShell, from `PwrSearch.psm1`)

### `sd <pattern> [-All]`

Search from the repo root (or cwd if there is no repo) and emit matching `DirectoryInfo` objects. Built-in excludes: `obj`, `objd`, `objr`, `objc`, `bin`, `.git`, `node_modules`.

```powershell
sd foo.cpp           # first match
sd foo.cpp -All      # all matches
sd 'src\tools'       # multi-part path pattern
```

Forward slashes in the pattern are normalized to backslashes, so `sd src/tools` works too.

### `go` / `Switch-Location`

Named navigation with an `sd` fallback.

```powershell
go home      # ~
go src       # C:\src
go bin       # C:\bin
go scripts   # $profile's directory
go <name>    # any key in $go_locations, or an sd fallback search
```

Extend at runtime:

```powershell
$go_locations['myrepo'] = 'C:\repos\myrepo'
```

## Build

```powershell
.\build.ps1                          # Release, copies SearchDir.dll next to PwrSearch.psm1
.\build.ps1 -Configuration Debug
```

`SearchDir.dll` is gitignored; run `build.ps1` once after cloning before importing the module. CI (`publish.yml`) runs the same script.

## Release Automation — Conventional Commit PR Titles (IMPORTANT)

This repo uses [release-please](https://github.com/googleapis/release-please) to bump `PwrSearch.psd1`'s `ModuleVersion` and generate `CHANGELOG.md` from commit history. **Release-please parses Conventional Commit messages from merged PRs to decide how to bump the version** — if a PR title doesn't match the grammar, release-please silently ignores it and the next release ships with a missing or incorrect entry.

**Never commit directly to `main` in this repo.** Release-please only opens (or updates) a release PR when it sees new Conventional Commit entries land on `main` via a merged PR — direct pushes to `main` that aren't part of a release-please PR are an anti-pattern, because they bypass the PR-title grammar check and are harder to revert. The workflow is always:

1. `git checkout -b <short-slug>` — create a feature branch (e.g. `feat/sd-depth`, `fix/reporoot-worktree`).
2. Commit your changes on that branch.
3. `gh pr create --title "<conventional-commit-title>" --body "..."` — open the PR. Use `--base main`.
4. Merge via squash; GitHub uses the PR title as the squash-commit subject, which is what release-please parses.
5. On merge, release-please opens or updates a release PR bumping the version in `PwrSearch.psd1` and `CHANGELOG.md`. Merge that PR to publish.

**Always use a Conventional Commit-formatted title when creating or updating a PR in this repo.**

Grammar: `<type>[optional scope][!]: <description>`

| Type | Version bump | Use for |
|------|--------------|---------|
| `feat:` | minor (`1.2.3` → `1.3.0`) | new features, new cmdlets, new parameters |
| `fix:` | patch (`1.2.3` → `1.2.4`) | bug fixes |
| `feat!:` / `fix!:` / `BREAKING CHANGE:` footer | major (`1.2.3` → `2.0.0`) | breaking API or behavior changes |
| `perf:` | patch | performance improvements |
| `refactor:` | patch | internal refactor, no behavior change |
| `docs:` | no release | README / CLAUDE.md / comments only |
| `test:` | no release | test-only changes |
| `build:` | no release | build system / dependencies |
| `ci:` | no release | CI config |
| `chore:` | no release | housekeeping |

Examples:

```
feat(sd): add -Depth parameter to cap recursion
fix(Get-RepoRoot): handle worktrees where .git is a file
perf(SearchDirectory): replace predicate list with HashSet exclude filter
feat!: rename Goto-KnownLocation to Switch-Location

BREAKING CHANGE: Goto-KnownLocation has been renamed to Switch-Location.
The `go` alias still works.
docs: document release-please PR title requirement
```

When opening a PR with `gh pr create`, set `--title` to one of the above forms. When squash-merging, GitHub uses the PR title as the commit subject, which is what release-please reads.

## Module Layout

```
PwrSearch/
├── PwrSearch.psd1            # manifest; NestedModules = @('SearchDir.dll'); ModuleVersion tagged with x-release-please-version
├── PwrSearch.psm1            # sd, _gosd, Switch-Location, go alias
├── SearchDir.dll             # built by build.ps1 (gitignored)
├── build.ps1                 # dotnet build + copy DLL next to the psm1
├── CHANGELOG.md              # maintained by release-please
├── src/
│   ├── SearchDir.csproj      # SDK-style, netstandard2.0, PowerShellStandard.Library
│   ├── SearchDirectory.cs    # Search-Directory cmdlet
│   └── GetRepoRoot.cs        # Get-RepoRoot cmdlet
└── .github/workflows/
    ├── release-please.yml    # opens/maintains release PRs from Conventional Commits on main
    └── publish.yml           # on release: setup-dotnet → build → verify manifest → Publish-Module
```

## Historical Note

Earlier versions depended on `$env:_XROOT` (a Razzle build-environment variable) and a PowerShell-only `_sd` helper with a `Goto-KnownLocation` function. All three have been replaced:

- `_XROOT` → `Get-RepoRoot` (portable, git-based, C# fast path)
- `_sd` → `sd` (native function, no underscore)
- `Goto-KnownLocation` → `Switch-Location` (approved PS verb; silences the unapproved-verb warning at `Import-Module` time)

The `go` alias is unchanged. If you see references to `_XROOT`, `_sd`, or `Goto-KnownLocation` in downstream code, they need to be updated.
