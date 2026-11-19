# BimGo — Clash Review

BimGo lets teams review Revit models in a standalone Windows viewer. Use **Go** for a live session, or **Export .bimgo** to share a snapshot that opens without Revit.

Create hard and clearance clash tests, inspect results using Scan, Measure, Find Room and section boxes, and record issues with comments, pictures and markup. Reports are available as HTML and XML; issues can also be exchanged as BCF. In a live session, select elements in Revit and refresh after the model changes. Comment markers can be synchronized into Revit.

The planned release targets Revit 2023–2027 with a separate add-in for each year and one viewer. **Host acceptance is still pending**; the included marker family is from Revit 2025 and cannot be loaded by Revit 2023 or 2024 without a compatible replacement. See the [implementation plan](docs/plans/clash-review.md).

Start with the [user guide](docs/README.md), [clash detection](docs/clash-detection.md), and [comments and BCF](docs/comments-and-bcf.md). For maintainers, see [releasing](docs/releasing.md) and [DEVELOPING.md](DEVELOPING.md).

Build the viewer with `dotnet build src/BimGo.App/BimGo.App.csproj -c Release` and run independent tests with `dotnet test tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release`. Revit builds require the matching Revit API and solution configuration, such as `Release R25`. `build/publish.ps1` builds the viewer, selected add-in bundles, and installer.

Builds only create artifacts by default. To run the viewer directly from its build output, use `dotnet run --project src/BimGo.App/BimGo.App.csproj -c Debug` or `-c Release`. To opt into a local deployment, close BimGo and Revit and pass `-p:DeployBimGoLocally=true`; the viewer is published self-contained in either configuration, with Debug symbols retained. For example, `dotnet build src/BimGo.sln -c "Debug R26" -m:1 -p:DeployBimGoLocally=true` updates the viewer under `%LocalAppData%\Programs\BimGo` and deploys the R26 add-in to `%AppData%\Autodesk\ApplicationPlugins\BimGo.bundle`. Use `-p:BimGoInstallDir="D:\path\to\BimGoInstall"` to direct the viewer deployment to another directory. Revit loads the add-in at its next start.

Publisher **Aussie BIM Guru** · licence **MIT No Attribution (MIT-0)**. Autodesk and Revit are registered trademarks of Autodesk, Inc.; BimGo is independent of Autodesk.

## Limitations and credits

Hard clash depth is an estimate; a solid fully contained inside another is not reported. Tests assume closed solids and have a 10,000-clash limit per test. Linked models are read-only and extraction covers top-level instances. A live refresh re-extracts the model.

BimGo uses Silk.NET OpenGL bindings. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for third-party licences and notices. The project is licensed under [MIT No Attribution (MIT-0)](LICENSE), without warranty.
