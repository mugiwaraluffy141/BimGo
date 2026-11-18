# Live sessions and Revit

← [Docs home](README.md)

**Go** extracts the model using the selected Options and opens a live review session. Scan → **R** selects an element in Revit. When the model changes, **F5** refreshes the snapshot; rerun the clash test to update results. The ribbon's **Live** button shows the session and can end it.

**Export .bimgo** creates a standalone snapshot. Changes to the RVT do not automatically update that file. Open it in BimGo without Revit to run tests, add issues and save review data.

## Comment markers

The ribbon's **Comment markers** button synchronizes comments from the model or a `.bimgo` file. Options → Player controls live markers and whether closed markers are removed. The bundled family is Revit 2025 format; Revit 2023 and 2024 require a compatible marker family selected in Options until an older family is supplied and verified.
