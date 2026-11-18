# Cross-runtime snapshot smoke

On Windows, from the repository root, build the harness and exchange a fixture in both directions:

```powershell
dotnet build tests/BimGo.Compatibility.Smoke/BimGo.Compatibility.Smoke.csproj -c Release
$fixture = Join-Path (Resolve-Path 'tests/BimGo.Compatibility.Smoke/bin') 'smoke.bimgo'
$framework = '.\tests\BimGo.Compatibility.Smoke\bin\Release\net48\BimGo.Compatibility.Smoke.exe'
$net8 = '.\tests\BimGo.Compatibility.Smoke\bin\Release\net8.0\BimGo.Compatibility.Smoke.dll'
& $framework write $fixture
dotnet $net8 read $fixture
dotnet $net8 write $fixture
& $framework read $fixture
Remove-Item -LiteralPath $fixture
```

Each command must exit successfully. This checks snapshot serialization and high-bit IFC GUID conversion, not clash computation or data extraction in a Revit host.
