# PwrSearch

PowerShell module for fast directory/file search (`Search-Directory` / `_sd`) and a `go` navigation command. Backed by a native `SearchDir.dll` built from `src/SearchDir.csproj` (netstandard2.0, works on Windows PowerShell 5.1 and PowerShell 7).

## Build

```powershell
.\build.ps1              # Release build, copies SearchDir.dll next to PwrSearch.psm1
.\build.ps1 -Configuration Debug
```

The built `SearchDir.dll` is gitignored; run `build.ps1` once after cloning before importing the module.
