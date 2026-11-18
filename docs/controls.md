# Controls for clash review

← [Docs home](README.md)

When a model opens, **Clash Review**, **Inspect** and **Issues** are available in the review column. Collapse a panel with its header button. Click model geometry to use the selected Inspect or Issues tool. Navigate with WASD, right-drag to orbit, middle-drag to pan, and the wheel to zoom; use **MORE → FIT VIEW** to frame the model. The viewer toolbar opens settings and commands; **MORE → CONTROLS** changes bindings. F1 shows shortcuts for the current context.

Keyboard defaults include **1** for Scan, **2** for Measure, **4** for Comment, **P** for the section box and **Y** for clash mode. In clash mode use **.** and **,** to step between results, **1–5** to set status, **G** to change the background display, **B** for the section box and **R** to show the pair in Revit during a live session.

See [Clash detection](clash-detection.md) for the review workflow.

<!-- controls:begin (generated from the action map's defaults by tests/BimGo.Core.Tests/ControlsTests.cs: edit InputCatalog, not this table) -->
**General**

| Action | Keys |
|---|---|
| Help on / off | `F1` |
| Fullscreen | `F11` |
| Screenshot | `F12` |
| Hide / show the UI | `U` |
| Minimap | `Tab` |
| Refresh from Revit (live) | `F5` |
| Save | `Ctrl+S` |
| Save as | `Ctrl+Shift+S` |

**Movement** Desktop review: WASD moves freely, RMB orbits, MMB pans and the wheel zooms; MORE → FIT VIEW frames the model.

| Action | Keys |
|---|---|
| Move forward | `W` · `↑` |
| Move back | `S` · `↓` |
| Move left | `A` · `←` |
| Move right | `D` · `→` |
| Run (hold) | `Shift` |
| Level up | `PgUp` |
| Level down | `PgDn` |
| Go home | `H` |
| Set home here | `Shift+H` |
| Find room | `Ctrl+F` |
| Look up |  |
| Look down |  |
| Look left |  |
| Look right |  |

**Review tools**

| Action | Keys |
|---|---|
| Tool 1: Scan | `1` |
| Tool 2: Measure (again: next mode) | `2` |
| Tool 4: Comment | `4` |
| Review tools idle / select tool | `0` |
| Primary tool action | `LMB` |
| Secondary tool action | `RMB` |
| Clear this tool's markers | `X` |

**Views and tools**

| Action | Keys |
|---|---|
| Bookmark this view | `B` |
| Go to bookmark 1 | `Shift+1` |
| Go to bookmark 2 | `Shift+2` |
| Go to bookmark 3 | `Shift+3` |
| Go to bookmark 4 | `Shift+4` |
| Go to bookmark 5 | `Shift+5` |
| Go to bookmark 6 | `Shift+6` |
| Go to bookmark 7 | `Shift+7` |
| Go to bookmark 8 | `Shift+8` |
| Go to bookmark 9 | `Shift+9` |
| Coordinate readout | `L` |
| Section box editor | `P` |
| Cut at the aimed surface | `Shift+P` |
| Clear all cuts | `Ctrl+P` |
| Clash mode on / off (docked clash list) | `Y` |
| Grids panel (also in clash mode) | `Shift+G` |
| Grids: off / level underfoot / all levels (also in clash mode) | `C` |
| Colour profile on / off (also in clash mode) | `Shift+C` |
| Room tags on / off (options: Find room → ROOM DATA) | `Shift+N` |

**Scan tool** While the tool is selected (movement keys remain active).

| Action | Keys |
|---|---|
| Select in Revit (live) | `R` |
| Hide the target | `I` |
| Isolate its category (again: show all) | `Shift+I` |

**Measure tool** While the tool is selected (movement keys remain active).

| Action | Keys |
|---|---|
| Normal projection (distance mode) | `N` |
| Show / hide the saved measurements | `I` |
| Export the measurements (CSV) | `E` |

**Comment tool** While the tool is selected (movement keys remain active).

| Action | Keys |
|---|---|
| Edit the hovered comment | `E` |

**Markup editor** After a comment's picture is taken. Text typing keeps Enter / Backspace / Esc.

| Action | Keys |
|---|---|
| Arrow | `A` |
| Box | `R` |
| Ellipse | `E` |
| Pen | `P` |
| Text | `T` |
| Colour 1 | `1` |
| Colour 2 | `2` |
| Colour 3 | `3` |
| Colour 4 | `4` |
| Colour 5 | `5` |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` · `Ctrl+Shift+Z` |
| Save the markup | `Enter` |

**Section box editor** The section box key or Esc closes it; clear all cuts still works.

| Action | Keys |
|---|---|
| Drag a handle without snapping (hold) | `Shift` |

**Clash mode** Clash detection's docked list (the clash mode key or Esc leaves): RMB orbits, MMB pans, the wheel zooms off the dock and scrolls the list over it.

| Action | Keys |
|---|---|
| Next clash | `.` |
| Previous clash | `,` |
| Go to the current clash again (re-frame) | `Enter` |
| Status: new | `1` |
| Status: active | `2` |
| Status: reviewed | `3` |
| Status: approved | `4` |
| Status: resolved | `5` |
| Show in Revit: the pair selected in a clash 3D view (live) | `R` |
| Fly up (hold) | `Space` · `E` |
| Fly down (hold) | `Ctrl` · `Q` |
| Model view: ghost / wireframe / real / hidden | `G` |
| Section box on / off (on: around the current clash) | `B` |
| Select all shown clashes (focus on them all) / clear | `Ctrl+A` |
| Row pictures on / off | `P` |
| Group the selected clashes | `Ctrl+G` |
| Ungroup the picked group / take the selected clashes out of their groups | `Ctrl+Shift+G` |
| Pick points: click dots or rows to pick, then group them (again or Esc leaves) | `K` |
| Status: next (new → active → reviewed → approved → resolved) |  |

**Settings panel** While the Settings panel is open.

| Action | Keys |
|---|---|
| Close the Settings panel | `Tab` |

**Fixed (not rebindable)** Text boxes also keep Enter, Tab, Backspace, ↑ / ↓, Ctrl+V and Esc.

| Action | Keys |
|---|---|
| Back · cancel · stop | `Esc` |
| Close the window | `Alt+F4` |
| Menus, panels, handles, markup | `LMB` |
| Look around (drag; photo, section box, study) | `RMB` |
| Finer slider and stepper changes (hold) | `Shift` |
| Open a file (start screen) | `Ctrl+O` |

<!-- controls:end -->
