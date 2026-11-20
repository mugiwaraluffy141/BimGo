# Clash detection

← [Docs home](README.md)

Clash detection stays off until you use it: **MORE → CLASHES**, or **Y** for clash mode.

## Sets and tests

**SETS** are search sets: rules on category, family, type, level, phase, model or any extracted parameter (is, is one
of, contains, number ranges, and "not" rules that take elements out), in groups. **TESTS** run set A against set B (or
a set against itself) as **hard** clashes (crossing deeper than the tolerance) or **clearance** clashes (closer than a
distance). Tests and sets can be exported and imported to share between models.

Right-click a Test/Set for its setup actions; **F2** renames and **Ctrl+C/V** copies/pastes its configuration
within the same model/session. Paste creates a new ID and does not duplicate results or review history.

![The clashes panel](images/clash-panel.png)

## Reviewing

**Y** docks the clash list. Use WASD to move, right-drag to orbit, middle-drag to pan, and the wheel to zoom outside the
dock; the wheel scrolls the list over it. **.** and **,** step through clashes: each gets a section box and a view across
the pair. **1–5** set the status, **G** changes how the rest of the
model shows, **B** the section box, **R** shows the pair in Revit (live). Statuses carry over when you run a test again.

Double-click a clash result row to show that single clash in Revit through the same live-session action.
Group headers, markup icons, text inputs and Ctrl/Shift selection clicks do not trigger this navigation.
Live-host acceptance remains tracked separately in the [validation checklist](plans/review-workflow-usability-validation.md).

![Clash mode](images/clash-mode.png)

Clashes can be grouped (by element, level, category, grid, box or by hand), turned into comments (and so BCF), and
reported as HTML, Navisworks-style XML, or an Excel workbook (.xlsx).

Use **REPORT… → EXPORT REPORT (HTML / XML / EXCEL)… → FORMAT: EXCEL**. No environment variable is needed.
Excel exports shown clashes, the selection, or the current test, with one row per clash and an unchecked Done checkbox.
The checklist is for tracking work in Excel only; it does not update clash statuses in BimGo. Excel for Microsoft 365
on Windows 11 is the target. Checkbox interaction, save/reopen and sort/filter acceptance in Excel remain pending;
the report card shows a short notice when Excel is selected.

To send results back into BimGo, use **REPORT… → EXPORT BIMGO REPORT (JSON)…** and have the recipient use
**IMPORT BIMGO REPORT (JSON)…** with the corresponding model. JSON exchanges clash data, not model geometry;
send a `.bimgo` snapshot when the recipient also needs the model.

![A clash report](images/clash-report.png)
