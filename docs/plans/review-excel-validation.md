# P2 — Excel in-cell checkbox risk spike

**Date:** 2026-11-19. **Result:** BCL-only encoding is feasible and structurally matched to a pinned upstream workbook. **Interactive Excel acceptance is NOT RUN; checkpoint A remains open.** No App/Core/existing tests, product dependencies, Office settings, existing workbooks or deployment were changed.

**2026-11-20 follow-up:** conditional authorization for an isolated hidden Excel COM validation was received. A read-only activation preflight found startup add-ins whose initialization/modal behavior is unreviewed. Validation therefore **stopped before COM activation** under the user's safety condition. This is an environmental safety blocker, not an observed workbook/encoding failure. Details and reproducible evidence below.

## Recommendation and gate

Prefer a small `ZipArchive`/`XmlWriter` writer for this narrowly scoped, write-only report, subject to user approval and the manual Excel checks below. Reusable research implementation: `tools/review-excel-spike/CheckboxWorkbook.cs`, API `CheckboxWorkbook.Write(path, IReadOnlyList<SpikeRow>)`. It creates Metadata and Clashes sheets, a real Excel table, and unchecked checkbox-formatted Boolean cells. It does **not** require Excel installed to generate a workbook. No NuGet, Python or other third-party package was installed or referenced; the harness targets the installed .NET 9 framework, and the writer uses BCL APIs available in .NET 8.

This is **not yet a production writer approval**. Do not substitute static Unicode glyphs, naked Booleans, legacy floating controls, or auto-convert a failing sample in Excel and claim the original worked. First open the generated sample unchanged and execute the checklist on the specified Excel target. If it fails, preserve the repair log and compare an Excel-created/saved sample before changing the encoding.

XlsxWriter is a reference, not a proposed product dependency (Python; BSD-2-Clause). No additional package size/license approval is needed for the standalone BCL candidate. If implementation copies upstream implementation rather than independently writing the documented format, retain its applicable license/copyright notices. Full production export service work remains separate: immutable report projection, atomic temp/replace, cancellation/error cleanup, empty reports, text length/control-character/OOXML escape handling, and test coverage. This spike buffers sheet XML in memory; its timing is not a guarantee for full report export.

## Exact encoding (not guessed `x14`)

Microsoft describes the modern checkbox as **TRUE/FALSE plus checkbox formatting**, not a separate linked cell. XlsxWriter's `insert_checkbox` sets the checkbox format, then writes a Boolean. The independently generated workbook uses that same mechanism:

1. A data cell such as `Clashes!D2`: `<c r="D2" s="1" t="b"><v>0</v></c>`; `s=1` selects the second `cellXfs/xf`. `0` is unchecked, `1` checked.
2. That `xf` contains `extLst/ext` with URI `{C7286773-470A-42A8-94C5-96B5CB345126}` and `<xfpb:xfComplement i="0"/>`, where `xfpb` is `http://schemas.microsoft.com/office/spreadsheetml/2022/featurepropertybag`.
3. Workbook relationship type: `http://schemas.microsoft.com/office/2022/11/relationships/FeaturePropertyBag`, target `featurePropertyBag/featurePropertyBag.xml` relative to `xl/workbook.xml`.
4. Part `/xl/featurePropertyBag/featurePropertyBag.xml`, content type `application/vnd.ms-excel.featurepropertybag+xml`, with the following ordered bags (references are zero-based):

   ```xml
   <FeaturePropertyBags xmlns="http://schemas.microsoft.com/office/spreadsheetml/2022/featurepropertybag">
     <bag type="Checkbox"/>
     <bag type="XFControls"><bagId k="CellControl">0</bagId></bag>
     <bag type="XFComplement"><bagId k="XFControls">1</bagId></bag>
     <bag type="XFComplements" extRef="XFComplementsMapperExtRef">
       <a k="MappedFeaturePropertyBags"><bagId>2</bagId></a>
     </bag>
   </FeaturePropertyBags>
   ```

`xfComplement i=0` indexes the mapped complement, **not** bag zero directly. This candidate needs no `x14`, VML, drawings, ActiveX, macro, custom number-format glyph or independent linked-cell formula. It does not use DXF conditional checkbox formatting, so no `DXFComplements` bag is required. Structural tests establish it is not a Boolean-only/glyph file; they cannot establish that this particular Excel installation renders/toggles it.

### Sort/filter and identity

The Done value and rendering belong to the same cell; there is no external control-to-row link to maintain. `ClashesTable` includes **all four columns** (`ClashId`, `Status`, `Notes`, `Done`) with table autofilter over `A1:D{lastRow}`. Sorting the complete table should move the ClashId and its Done value/style together; filtering hides complete rows. This is the architectural reason to use cell controls rather than floating controls, **not an executed sort/filter result**. Sorting just a manually selected column outside the whole-row/table operation can still corrupt row identity; it is not a supported workflow.

## Authoritative and pinned source evidence

Read on 2026-11-19:

- [Microsoft Support — Using check boxes in Excel](https://support.microsoft.com/en-us/office/using-checkboxes-in-excel-da85546d-c110-49b8-b633-9cebadcaf8d4): describes TRUE/FALSE with checkbox formatting, mouse/Space toggle, and Clear Formats retaining the Boolean. Applies to Excel for Microsoft 365.
- [Microsoft Excel team — Introducing Checkboxes in Excel](https://techcommunity.microsoft.com/blog/excelblog/introducing-checkboxes-in-excel/4173561): January 29, 2026 update says fully rolled out on desktop Current Channel (also Monthly Enterprise and Semi-Annual); this is availability evidence, not proof of the active license/features on this machine.
- [XlsxWriter worksheet API](https://xlsxwriter.readthedocs.io/worksheet.html#worksheet-insert-checkbox): checkbox API and underlying Boolean semantics.
- Upstream pinned revision: [`fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8`](https://github.com/jmcnamara/XlsxWriter/tree/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8).
  - [`worksheet.py`, `insert_checkbox`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/worksheet.py): format plus `_write_boolean`.
  - [`styles.py`, `_write_xf_format_extensions`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/styles.py): extension URI/namespace/index.
  - [`feature_property_bag.py`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/feature_property_bag.py): ordered bags and mapping.
  - [`relationships.py`, `_add_feature_bag_relationship`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/relationships.py): exact relationship type and target.
  - [`contenttypes.py`, `_add_feature_bag_property`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/contenttypes.py): content type.
  - [`packager.py`](https://github.com/jmcnamara/XlsxWriter/blob/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/packager.py): workbook relationship and feature part packaging.
  - [Independent reference workbook `checkbox01.xlsx`](https://raw.githubusercontent.com/jmcnamara/XlsxWriter/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/test/comparison/xlsx_files/checkbox01.xlsx), downloaded for structural comparison only. No claim that we ran upstream tests or established this fixture's provenance by running Excel.

## Environment (read-only inspection, Excel not launched)

- `HKLM\SOFTWARE\Microsoft\Office\ClickToRun\Configuration`: `VersionToReport` and `ClientVersionToReport` = **16.0.20625.20108**, `Platform=x64`.
- `UpdateChannel=http://officecdn.microsoft.com/pr/492350f6-3a01-4f97-b9c0-c7c6ddf67d60` (Current Channel identifier).
- `ProductReleaseIds=ProPlus2019Retail,O365ProPlusRetail`: mixed product IDs mean registry alone does **not** prove the active Excel edition/subscription/license. File > Account must confirm Microsoft 365 and feature availability.
- `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe` points to `C:\Program Files\Microsoft Office\Root\Office16\EXCEL.EXE`; file and product version both **16.0.20625.20108**.
- `[Microsoft.Win32.Registry]::ClassesRoot.OpenSubKey('Excel.Application\CLSID')` = `{00024500-0000-0000-C000-000000000046}`. The first PowerShell `HKCR:` provider probe was unavailable; direct ClassesRoot read confirmed registration. No COM activation was performed.
- No Excel process was returned by `Get-Process EXCEL` during inspection. No UI, hidden Excel automation, existing file access or Office registry writes were initiated.
- .NET SDK used: `10.0.401`; installed SDKs include `9.0.301`. No product build was needed for this isolated spike.

## Executed verification and artifacts

Run from repository root, using a **new output directory** on each run (writer intentionally uses `FileMode.CreateNew`):

```powershell
dotnet run --project tools/review-excel-spike/ReviewExcelSpike.csproj -- tools/review-excel-spike/artifacts/run-1 tools/review-excel-spike/artifacts/upstream-checkbox01.xlsx
```

The optional second argument is the downloaded pinned upstream fixture above; omit it for offline generation/tests without reference comparison. To download explicitly:

```powershell
New-Item -ItemType Directory -Force tools/review-excel-spike/artifacts
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/jmcnamara/XlsxWriter/fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8/xlsxwriter/test/comparison/xlsx_files/checkbox01.xlsx' -OutFile tools/review-excel-spike/artifacts/upstream-checkbox01.xlsx
```

**PASS:** all package entries parse as XML; feature bag relationship/content type; ordered bag mapping; checkbox style index; cell Boolean type/style/initial unchecked state; full row table/autofilter range; IDs and literal text round-trip (`=`, `+`, `-`, `@`, Vietnamese, XML-sensitive characters, URL and surrounding whitespace); no formulas/hyperlinks; attempting to overwrite preserves original bytes. **PASS:** generated feature bag is XML-tree-equal to the independently downloaded fixture; `xfComplement` and enclosing extension URI match. These are structural checks, not a complete OOXML schema validator.

`dotnet build tools/review-excel-spike/ReviewExcelSpike.csproj --no-restore -c Release`: **PASS**, 0 warnings/errors. Inline review found no product coupling or new dependencies; prototype limitations are explicitly gated above.

Generated files are local, ignored reproducible artifacts, not tracked product fixtures:

| Path relative to repository root | Size | Write time (single cold/warm mixed run, not a benchmark guarantee) |
|---|---:|---:|
| `tools/review-excel-spike/artifacts/run-1/checkbox-1.xlsx` | 4,063 bytes | 27 ms |
| `tools/review-excel-spike/artifacts/run-1/checkbox-100.xlsx` | 6,162 bytes | 2 ms |
| `tools/review-excel-spike/artifacts/run-1/checkbox-10000.xlsx` | 184,225 bytes | 85 ms |

Absolute sample directory: `D:\Home\My Projects\BimGo\tools\review-excel-spike\artifacts\run-1\`.

SHA-256 of files produced in this execution (ZIP timestamps may change hashes on regeneration):

```text
checkbox-1.xlsx      25F661CC473787B0AD332828CBDA0C6DA28E540B1A55B77A4AD0008C61DB79A1
checkbox-100.xlsx    F7235DB42F9434D3E99FD79F89A193616AFB194CB70A2BCD639B50C59C79E3B1
checkbox-10000.xlsx  1C82D5750AC20305DF4A4D099476EB11E1DC66476B0C773D5E7EBF5908AF338D
upstream fixture    F0EEBD8D0E4B184AA3DDB8983F22B80C95E62FF80943BD3F726AC3A7D1DAD6B1
```

## Required manual acceptance — ALL NOT RUN

Use only copies of these generated samples; never overwrite an existing user workbook. Do not change Office update channel/license settings as part of this test.

- [ ] Record Windows version, File > Account edition, Microsoft 365 subscription, channel, Excel version/build and 64-bit. Verify Insert > Checkbox is actually available. Registry mixed product IDs are not sufficient.
- [ ] Open original `checkbox-100.xlsx` unchanged: no repair/removed-records warning. Both sheets/table present; Done shows native unchecked boxes, not FALSE or glyphs. Preserve any repair log on failure.
- [ ] Click Done for **CL-00002**, **CL-00007**, **CL-00099**. Select another Done cell and use Space twice. Formula bar shows real TRUE/FALSE transitions; Status and Notes remain unchanged. This is required interactive evidence, not established by package inspection.
- [ ] Save As a new `checkbox-100-accepted.xlsx`, close, reopen; exactly those three IDs remain checked.
- [ ] Sort **the table** by ClashId descending and then Status; the same three IDs remain checked. Filter Done=TRUE: exactly CL-00002/00007/00099 remain visible, regardless of their different statuses. Filter Status=New and toggle CL-00007 off; clear filters; only CL-00002/CL-00099 remain checked. Save/reopen and recheck by ID.
- [ ] In a disposable copy, Clear Formats on a Done cell: checkbox disappears while TRUE/FALSE remains. Undo restores formatting. Demonstrates cell-control formatting rather than static text.
- [ ] Open 1-row and 10,000-row samples; toggle first/middle/last IDs, sort/filter/save/reopen and verify ID association. Record open/save time and usability. Writer timings above do not measure Excel performance.
- [ ] Confirm Notes prefixed `=`, `+`, `-`, `@` remain literal strings; URL is text; Vietnamese/XML-sensitive characters/whitespace survive save/reopen.
- [ ] Retain the original, accepted copy, screenshot/build evidence, tester/date, and any failures here. Re-run equivalent acceptance on **BimGo's actual output** after approved integration; a spike sample alone does not satisfy P14/checkpoint E.
- [ ] If editing Vietnamese Notes in Excel is part of acceptance, manually verify IME composition/commit/cancel there. The spike's Unicode serialization checks are not IME interaction evidence; BimGo editor IME acceptance belongs to the separate UI workstream.

## 2026-11-20 — isolated COM validation safety gate

### What was authorized and what actually ran

The follow-up permits creating a **new** invisible `Excel.Application` instance, opening only new sample copies, macros disabled, normal-load/read-only originals, SaveAs new paths, then save/reopen/sort/filter and native control inspection. It prohibits attaching a user instance, opening personal workbooks, changing Office settings/license/channel, broad process killing, or forcing activation if isolation/modal safety cannot be guaranteed.

Before activation, inspected the installed x64 Excel registry view, known XLSTART locations and Excel process IDs. The reproducible, read-only script is `tools/review-excel-spike/Test-ExcelIsolation.ps1`. It deliberately contains **no COM activation or workbook automation**; it reports a gate result, not an Excel acceptance result. It never edits registry values, disconnects add-ins, opens startup workbooks, or kills processes. A no-blocker scan would still require controlled-host review rather than automatically launching Office.

Executed:

```powershell
powershell.exe -NoProfile -NonInteractive -File tools/review-excel-spike/Test-ExcelIsolation.ps1
```

Actual evidence: `tools/review-excel-spike/artifacts/validation-preflight-20261120-060734-f44d97e2cd974b48a6eb757e6be88431/preflight.json` (ignored local artifact). Timestamp recorded by host: `2026-11-20T06:07:34.9967596+07:00`. Result: **`BlockedBeforeComActivation`** (intentional non-success exit).

| Inspection | Observed result |
|---|---|
| Excel file version | `16.0.20625.20108` |
| Existing Excel processes | None returned before or after the preflight |
| `HKCU\Software\Microsoft\Office\Excel\Addins\KMSO2PdfPlugins.Component` | `LoadBehavior=3` (startup-load registration) |
| `HKCU\Software\Microsoft\Office\Excel\Addins\MsoSidebar.Component` | `LoadBehavior=3` (startup-load registration) |
| `HKCU\Software\Microsoft\Office\Excel\Addins\MicrosoftDataStreamerforExcel` | `LoadBehavior` absent; conservatively unreviewed, **not** proof it will load |
| User and installed Office `XLSTART` directories | No entries found; filenames/content not opened |
| Current-user Excel `OPEN*` / `AltStartup` options | No nonempty values found |
| COM instances created / workbooks opened | **0 / 0** |
| Settings changed / processes killed | **None / none** |

### Why activation stopped

Excel add-ins can initialize as part of application startup, before the caller receives the COM object and can set `AutomationSecurity`, `EnableEvents`, `DisplayAlerts`, or `Interactive`. Those application properties are not a demonstrated sandbox for startup add-ins or license dialogs. With these unreviewed registrations, a prompt-free hidden launch cannot be established. There is **no claim that any named add-in is malicious, actually loaded, or known to show a prompt**; the blocker is inability to meet the requested isolation/modal guarantee without modifying the user's setup or first doing attended host preparation.

No safe-mode/command-line launch workaround was attempted: proving its behavior, obtaining only the intended instance, and avoiding UI would be a separate safety investigation. No `GetActiveObject`/ROT attachment, registry disablement, add-in disconnection or process termination workaround was attempted. Since activation never occurred, there are no workbook/instance RCWs requiring Close/Quit/release; the preflight disposes all registry handles in `finally` blocks.

Sources for this gate:

- [Microsoft — GetObject and CreateObject behavior](https://learn.microsoft.com/en-us/troubleshoot/microsoft-365-apps/office-suite-issues/getobject-createobject-behavior): distinguishes attaching vs creating an automation server; recommends a new application instance and Quit/releasing references. Its historical instancing table alone does not establish isolation of this installed build.
- [Microsoft — Registry entries for VSTO Add-ins / LoadBehavior values](https://learn.microsoft.com/en-us/visualstudio/vsto/registry-entries-for-vsto-add-ins?view=visualstudio): documents startup-load value 3 and registration locations. Used as initialization-risk evidence, **not** proof these registrations are VSTO add-ins or ran on this host.
- [Microsoft — Workbooks.Open](https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open): programmatic macros are enabled by default; requires `AutomationSecurity`; `CorruptLoad=xlNormalLoad` does not attempt object-model recovery. These would be mandatory settings for a subsequently approved runner, but **were not executed here**.

### Acceptance status and next safe route

| Requested runtime evidence | Status |
|---|---|
| Native `CellControl.Type` / feature API availability | **NOT PROBED** (do not infer supported/unsupported) |
| Range Boolean state writes/readback | **NOT RUN** |
| Normal-load/no-repair opening | **NOT RUN**; absence of a repair log without an open proves nothing |
| SaveAs/reopen checkbox formatting retention | **NOT RUN** |
| Table sort/filter checked-ID association | **NOT RUN** |
| Actual mouse/Space toggle, focus/IME | **NOT RUN**; Value2 writes would not prove these anyway |

Recommendation is unchanged: **select the BCL writer as the provisional candidate, not as Excel-accepted output**. Existing fixture/structural evidence is enough to continue technical design, not to close P2/checkpoint A or claim interactive acceptance. Next use a user-prepared clean test profile/VM with the required active Microsoft 365 edition, startup/add-in behavior reviewed, and activation dialogs resolved in an attended session. Do not disable current-profile add-ins/settings automatically. A future COM runner must independently verify its PID against a pre-launch snapshot, operate exclusively on new generated copies, use macro-disabled normal-load read-only opening and new SaveAs paths, and close only its own workbook/Quit its own instance/release all RCWs in `finally`. Preserve failing samples and evidence; never claim Boolean writes tested clicks.

### Remaining blockers / unknowns

1. **Runtime target validation** is blocked by the startup/add-in safety gate above. The follow-up authorized isolated COM validation conditionally, but no safe activation was established. No claim of clickability, repair-free opening, save persistence or sort/filter acceptance is made.
2. **Actual active Microsoft 365 license and exposed feature** require File > Account/Insert inspection (mixed product registry IDs).
3. **Production writer approval/integration** remains at checkpoint A. The BCL sample is a narrowly scoped encoding prototype, not a complete report service. Older Excel/LibreOffice behavior is untested and outside the requested target.
4. **Full row sort/filter identity** is reasoned from the in-cell/table model, not host-tested. No workaround to floating controls should be adopted without renewed approval.
