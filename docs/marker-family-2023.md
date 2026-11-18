# Comment marker family for Revit 2023 and 2024

The bundled BimGo Comment Marker was authored in Revit 2025. Revit 2023 and 2024 cannot load it. Until a compatible family has been authored and tested, supply a replacement saved in Revit 2023 and select its `.rfa` file in **Options → Player → Comment marker family**. Revit 2024 can upgrade that family on load. A family already present in the project can be used without loading a file.

Create the replacement in Revit 2023 with the family name `BimGo Comment Marker`, a type named `Default`, and one adaptive placement point. Add all 18 parameters from [BimGo_SharedParameters.txt](../tools/revit/BimGo_SharedParameters.txt) as writable instance shared parameters, preserving their GUIDs and data types. Match the bundled family's status display and parameter associations. Do not recreate the parameters by name: BimGo looks them up by GUID.

Before distributing it, load it into a test project and sync comments through both the ribbon and a live session. Verify placement, the ID and other parameter values, status display, update and repeat sync without duplicates. Repeat in Revit 2024. A successful build does not verify the family or host behavior.
