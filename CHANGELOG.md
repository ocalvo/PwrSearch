# Changelog

All notable changes to PwrSearch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Releases are automated by [release-please](https://github.com/googleapis/release-please)
from [Conventional Commits](https://www.conventionalcommits.org/).

## [1.1.1](https://github.com/ocalvo/PwrSearch/compare/v1.1.0...v1.1.1) (2026-04-15)


### Bug Fixes

* **ci:** move publish job into release-please workflow ([524c521](https://github.com/ocalvo/PwrSearch/commit/524c521a229a747fc53401a89d6b5bda44074633))
* **ci:** use correct branch name in release-please workflow ([69b59e9](https://github.com/ocalvo/PwrSearch/commit/69b59e98ce461e4144f4aa13161124d3b9ff2c2c))

## [1.1.0](https://github.com/ocalvo/PwrSearch/compare/v1.0.0...v1.1.0) (2026-04-15)


### Features

* add Get-RepoRoot cmdlet, rename _sd to sd, drop Razzle _XROOT ([e25297f](https://github.com/ocalvo/PwrSearch/commit/e25297f89be22b5e9cee5a4b619121d4eeeba1ec))
* add Get-RepoRoot cmdlet, rename _sd to sd, drop Razzle _XROOT ([ec4ce9e](https://github.com/ocalvo/PwrSearch/commit/ec4ce9e06841b272be06908d283e63c2b15ce320))

## [Unreleased]

### Added
- `PwrSearch.psd1` module manifest (ModuleVersion `1.0.0`, tracked by release-please via `x-release-please-version` marker).
- `build.ps1` — builds `src/SearchDir.csproj` via `dotnet build` and copies `SearchDir.dll` next to `PwrSearch.psm1`.
- `.github/workflows/release-please.yml` — opens and maintains release PRs from conventional commits on `main`.
- `.github/workflows/publish.yml` — on GitHub release, runs `setup-dotnet`, builds, verifies the manifest version matches the tag, and runs `Publish-Module` with `PSGALLERY_API_KEY`.
- Comprehensive `README.md` covering features, install, usage, search algorithm, build layout, and release automation.

### Changed
- `src/SearchDir.csproj` rewritten as an SDK-style project targeting `netstandard2.0`, using `PowerShellStandard.Library` so the cmdlet works on both Windows PowerShell 5.1 and PowerShell 7.
- `SearchDirectory.cs` hot-path optimizations:
  - Exclude-directory filtering now uses two `HashSet<string>` with `StringComparer.OrdinalIgnoreCase` (name + rooted full-path), giving O(1) rejection per directory instead of O(n) scans over a predicate list.
  - `AdvanceSearch` inlined into `ProcessRecord`, eliminating the per-child `IEnumerable` state machine and its enumerator allocation.
  - `PartQuery` construction is now iterative right-to-left — one pass over the split pattern parts instead of the old recursive `LINQ` chain with `Skip(...).Count()`.
  - Wildcard regexes compile with `RegexOptions.Compiled | RegexOptions.CultureInvariant`.
  - Non-wildcard matchers use `string.StartsWith` / `string.IndexOf` with `StringComparison.Ordinal*`.
  - Bucket sorting uses a cached static `IComparer<SearchState>` instead of a lambda allocated per drain.
  - Also catches `DirectoryNotFoundException` alongside `UnauthorizedAccessException`, so a directory vanishing mid-walk no longer kills the search.

### Removed
- Checked-in `SearchDir.dll` — now built from source by `build.ps1` and CI; gitignored at the module root.
- Legacy `src/SearchDir.sln` and `src/Properties/AssemblyInfo.cs` — not needed with SDK-style projects.
