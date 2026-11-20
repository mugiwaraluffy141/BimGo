# Read-only, fail-closed Excel activation preflight. Intentionally NEVER creates
# an Excel COM instance, opens a workbook, changes Office settings or kills a process.
# A clean result here is not a guarantee that Office activation cannot show UI.
# Run with 64-bit Windows PowerShell; evidence stays under this spike's artifacts.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitProcess) {
    throw 'Use 64-bit PowerShell for the installed x64 Excel registry view.'
}

$artifacts = Join-Path $PSScriptRoot 'artifacts'
$ancestors = @((Get-Item -LiteralPath $PSScriptRoot))
if (Test-Path -LiteralPath $artifacts) {
    $ancestors += Get-Item -LiteralPath $artifacts
}
foreach ($item in $ancestors) {
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Refusing a redirected spike/artifacts directory.'
    }
}
$validationDir = Join-Path $artifacts ('validation-preflight-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($validationDir)
$blockers = [Collections.Generic.List[string]]::new()
$addins = [Collections.Generic.List[object]]::new()
$startup = [Collections.Generic.List[object]]::new()
$startupOptions = [Collections.Generic.List[object]]::new()
$excelIds = @(Get-Process EXCEL -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)

foreach ($root in @([Microsoft.Win32.Registry]::CurrentUser, [Microsoft.Win32.Registry]::LocalMachine)) {
    foreach ($subpath in @('Software\Microsoft\Office\Excel\Addins', 'Software\Microsoft\Office\16.0\Excel\Addins')) {
        $key = $root.OpenSubKey($subpath, $false)
        if ($null -eq $key) { continue }
        try {
            foreach ($name in $key.GetSubKeyNames()) {
                $entry = $key.OpenSubKey($name, $false)
                try {
                    $behavior = $entry.GetValue('LoadBehavior', $null)
                    $addins.Add([pscustomobject]@{
                        RegistryKey = $root.Name + '\' + $subpath + '\' + $name
                        ProgId = $name
                        LoadBehavior = $behavior
                    })
                    # Fail conservatively for any startup/on-demand enabled or unknown
                    # registration. No attempt to disable, disconnect or activate it.
                    if ($null -eq $behavior -or [int]$behavior -notin @(0, 1, 2, 8)) {
                        $blockers.Add("Unreviewed Excel add-in initialization: $name (LoadBehavior=$behavior)")
                    }
                }
                finally { if ($null -ne $entry) { $entry.Dispose() } }
            }
        }
        finally { $key.Dispose() }
    }
}

$options = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Office\16.0\Excel\Options', $false)
if ($null -ne $options) {
    try {
        foreach ($name in $options.GetValueNames()) {
            if ($name -match '^(OPEN\d*|AltStartup)$') {
                # Record only the setting name, not personal workbook/path values.
                $value = [string]$options.GetValue($name)
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $startupOptions.Add($name)
                    $blockers.Add("Configured Excel startup option: $name")
                }
            }
        }
    }
    finally { $options.Dispose() }
}

$appPath = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\App Paths\excel.exe', $false)
$excelPath = $null
if ($null -ne $appPath) {
    try { $excelPath = [string]$appPath.GetValue('') }
    finally { $appPath.Dispose() }
}
$version = $null
if ([string]::IsNullOrWhiteSpace($excelPath) -or -not (Test-Path -LiteralPath $excelPath -PathType Leaf)) {
    $blockers.Add('Registered Excel executable not found.')
}
else {
    $version = (Get-Item -LiteralPath $excelPath).VersionInfo.FileVersion
    $dirs = @((Join-Path $env:APPDATA 'Microsoft\Excel\XLSTART'), (Join-Path (Split-Path $excelPath) 'XLSTART'))
    foreach ($dir in $dirs) {
        $count = 0
        if (Test-Path -LiteralPath $dir) {
            # Enumerate names only; never open startup workbooks.
            $count = @(Get-ChildItem -LiteralPath $dir -Force).Count
            if ($count -ne 0) { $blockers.Add('Nonempty Excel XLSTART directory; automatic workbook opening cannot be excluded.') }
        }
        $startup.Add([pscustomobject]@{ Directory = $dir; EntryCount = $count })
    }
}

$status = 'RequiresControlledHostReview'
if ($blockers.Count -ne 0) { $status = 'BlockedBeforeComActivation' }
$result = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    Status = $status
    ExcelFileVersion = $version
    ExistingExcelProcessIds = $excelIds
    AddinRegistrations = @($addins.ToArray())
    StartupDirectories = @($startup.ToArray())
    NonemptyStartupOptionNames = @($startupOptions.ToArray())
    Blockers = @($blockers.ToArray())
    ExcelComActivated = $false
    WorkbooksOpened = 0
    OfficeSettingsChanged = $false
    InteractiveAcceptance = 'NOT RUN'
    Note = 'No COM automation follows this preflight, even if no blockers are detected. A clean registry scan alone cannot prove prompt-free Office activation.'
}
$evidence = Join-Path $validationDir 'preflight.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidence -Encoding UTF8
Write-Output "Evidence: $evidence"
Write-Output "Status: $status"
foreach ($reason in $blockers) { Write-Output "BLOCKER: $reason" }
if ($blockers.Count -ne 0) { exit 2 }
exit 3
