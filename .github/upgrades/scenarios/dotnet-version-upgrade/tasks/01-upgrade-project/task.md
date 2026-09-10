# 01-upgrade-project: upgrade-project

Upgrade `UmiaPP.csproj` to target `net10.0-windows` and update NuGet package dependencies to compatible versions.

## Research Findings
- Project file: `UmiaPP.csproj`
- Target Framework change: `net8.0-windows` -> `net10.0-windows`
- Package updates:
  - `Microsoft.EntityFrameworkCore.Sqlite`: 8.0.13 -> 10.0.12
  - `CommunityToolkit.Mvvm`: 8.4.0 -> 8.4.2
  - `ClosedXML`: 0.104.2 -> 0.105.1

**Done when**: `UmiaPP.csproj` targets `net10.0-windows`, packages are updated, and the project builds successfully with 0 errors and 0 warnings.
