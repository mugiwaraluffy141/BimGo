# Clash review validation notes

Date: 2026-11-16. These are build and source-inspection results, not host acceptance.

- The viewer builds with `dotnet build src/BimGo.App/BimGo.App.csproj --no-restore -m:1 -v:q` (0 errors, 175 warnings). No GUI interaction was performed in this check.
- Source inspection found the main input and pause menu entry points for model editing, push, tours, media, kiosk, weather and lighting studies removed. The control and help lists use the review-action filter. The kiosk command-line switch opens an ordinary file session. Clash, comment markup and report code remains reachable; their actual workflows have not been exercised here.
- On startup, an existing file's journal is loaded and replayed; saving passes that journal to the writer. This is a source check, not an old-file round-trip test. Existing entourage data is still loaded and may render saved billboards and swap rules, although its editing entry point and new Revit slot extraction are removed; assess the desired appearance using a historical file.
- A Revit 2023-compatible marker family has not been created or tested. The bundled RFA is Revit 2025 format; 2023/2024 require a compatible replacement as described in [marker family prerequisite](../marker-family-2023.md). Build success does not verify family placement or parameter GUIDs.
- Revit 2023–2027 host acceptance remains outstanding, including Go/export, live refresh, marker synchronization and repeated sync. GUI review of clash, markup, HTML/XML reports and BCF, plus historical file preservation, also remains outstanding. See the [plan](clash-review.md) and [specification](../specs/clash-review.md) for the acceptance steps.

## Revit release compilation

- Sequential commands `dotnet build src/BimGo.Revit/BimGo.Revit.csproj -c 'Release R23' -m:1 -v:q` through `Release R26` passed with zero errors. The R23 build reported 19 warnings on its final incremental run; R24, R25 and R26 each reported 65 warnings, largely existing XML documentation warnings. Single-node builds avoid an MSBuild project-reference failure observed with the default parallel setting.
- `Release R27` also passed with zero errors and 65 warnings using `-m:1 -v:q -p:RevitApiDir=<temporary-reference-directory>`. That directory contained `RevitAPI.dll` and `RevitAPIUI.dll` from the cached Nice3point Revit API 2027.3.0 reference packages. This is compile-only: the reference assemblies are not an installed Revit 2027 runtime.
- Release builds write the installer layout under `src/BimGo.Revit/bin/Release/<year>/` and do not run the Debug-only deployment steps. The 2023 and 2024 add-in folders include the .NET Framework dependencies copied from NuGet, including System.Text.Json and its supporting assemblies. Revit API assemblies are not packaged.
