# PwrSearch

Fast directory and file search backed by a native `SearchDir.dll`, plus a `go` navigation command.

## Key Commands

### `_sd <pattern> [-All]`

Searches directory trees for a file/directory matching `pattern` (substring match).

- Roots at `$env:_XROOT` when inside a Razzle environment; otherwise uses the current location.
- Automatically excludes common build output dirs (`objd`, `obj`, `objr`, `objc`, etc.).
- `-All`: return all matches instead of just the first.

```powershell
_sd foo.cpp          # find first file matching "foo.cpp"
_sd foo.cpp -All     # find all matches
```

### `go <location>`

Navigate to a known or searched location.

```powershell
go home      # ~
go src       # C:\src
go bin       # C:\bin
go scripts   # PowerShell profile directory
go <name>    # any key in $go_locations, or falls back to _sd search
```

Extend with custom locations:

```powershell
$go_locations["myrepo"] = "C:\repos\myrepo"
```

### `_gosd <pattern>`

Combines `_sd` + `Push-Location`: searches for `pattern` and navigates there.

## Module Members

- `Goto-KnownLocation` (alias: `go`)
- `_sd`, `_gosd` — internal helpers (not exported, but available in session)
- `$global:go_locations` — hashtable of named locations
