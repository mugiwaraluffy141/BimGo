using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace BimGo.Reports
{
    /// <summary>BCL-only, write-only XLSX preview. No Office automation or live document references.</summary>
    internal static class ClashExcelWriter
    {
        private const string MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string OFFICE_REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PACKAGE_REL = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string BAG = "http://schemas.microsoft.com/office/spreadsheetml/2022/featurepropertybag";
        private const string FEATURE_REL = "http://schemas.microsoft.com/office/2022/11/relationships/FeaturePropertyBag";
        private const string CHECKBOX_EXTENSION = "{C7286773-470A-42A8-94C5-96B5CB345126}";
        private static readonly XNamespace S = MAIN;
        private static readonly string[] HEADERS = { "ClashId", "Test", "Group", "Status", "Assigned To", "Notes",
            "Element A Id", "Model A", "Category A", "Family Type A", "Level A", "Element B Id", "Model B", "Category B",
            "Family Type B", "Level B", "Grid", "X (m)", "Y (m)", "Z (m)", "Distance (m)", "Crossing", "Done" };

        /// <summary>
        /// Writes a unique sibling temporary file, then atomically replaces the destination. Rejects empty reports,
        /// nonfinite measurements and text over 32,767 UTF-16 units rather than silently truncating notes.
        /// Cancellation before publication leaves the destination unchanged; cleanup only touches our own temp file.
        /// </summary>
        public static void Write(string path, ClashExcelSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Rows == null || snapshot.Rows.Count is < 1 or > 1_048_575)
            {
                throw new ArgumentException("Excel reports require 1 to 1,048,575 clashes. Narrow the scope or select a nonempty scope.", nameof(snapshot));
            }
            cancellationToken.ThrowIfCancellationRequested();
            string destination = Path.GetFullPath(path);
            string temp = Path.Combine(Path.GetDirectoryName(destination), ".bimgo-excel-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool ownsTemp = false;
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsTemp = true;
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        WritePackage(zip, snapshot, cancellationToken);
                    }
                    file.Flush(flushToDisk: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                // No delete-then-move fallback: a locked/unauthorized destination must survive failure.
                if (File.Exists(destination)) { File.Replace(temp, destination, null); }
                else { File.Move(temp, destination); }
                ownsTemp = false;
            }
            finally
            {
                if (ownsTemp)
                {
                    try { File.Delete(temp); }
                    catch (IOException) { /* Do not mask the export failure. */ }
                    catch (UnauthorizedAccessException) { /* Do not touch any other file. */ }
                }
            }
        }

        private static void WritePackage(ZipArchive zip, ClashExcelSnapshot snapshot, CancellationToken token)
        {
            Add(zip, "[Content_Types].xml", XDocument.Parse("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/tables/table1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml"/>
                  <Override PartName="/xl/featurePropertyBag/featurePropertyBag.xml" ContentType="application/vnd.ms-excel.featurepropertybag+xml"/>
                </Types>
                """));
            Add(zip, "_rels/.rels", Relationships(("rId1", OFFICE_REL + "/officeDocument", "xl/workbook.xml")));
            Add(zip, "xl/_rels/workbook.xml.rels", Relationships(
                ("rId1", OFFICE_REL + "/worksheet", "worksheets/sheet1.xml"),
                ("rId2", OFFICE_REL + "/worksheet", "worksheets/sheet2.xml"),
                ("rId3", OFFICE_REL + "/styles", "styles.xml"),
                ("rId4", FEATURE_REL, "featurePropertyBag/featurePropertyBag.xml")));
            Add(zip, "xl/workbook.xml", XDocument.Parse($"""
                <workbook xmlns="{MAIN}" xmlns:r="{OFFICE_REL}">
                  <bookViews><workbookView activeTab="1"/></bookViews>
                  <sheets><sheet name="Metadata" sheetId="1" r:id="rId1"/><sheet name="Clashes" sheetId="2" r:id="rId2"/></sheets>
                </workbook>
                """));
            // Documented in review-excel-validation.md: Boolean cells + XF complement + ordered feature bags.
            Add(zip, "xl/styles.xml", XDocument.Parse($"""
                <styleSheet xmlns="{MAIN}">
                  <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
                  <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="2">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"><extLst>
                      <ext uri="{CHECKBOX_EXTENSION}" xmlns:xfpb="{BAG}"><xfpb:xfComplement i="0"/></ext>
                    </extLst></xf>
                  </cellXfs>
                  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
                  <dxfs count="0"/><tableStyles count="0" defaultTableStyle="TableStyleMedium9" defaultPivotStyle="PivotStyleLight16"/>
                </styleSheet>
                """));
            Add(zip, "xl/featurePropertyBag/featurePropertyBag.xml", XDocument.Parse($"""
                <FeaturePropertyBags xmlns="{BAG}">
                  <bag type="Checkbox"/>
                  <bag type="XFControls"><bagId k="CellControl">0</bagId></bag>
                  <bag type="XFComplement"><bagId k="XFControls">1</bagId></bag>
                  <bag type="XFComplements" extRef="XFComplementsMapperExtRef"><a k="MappedFeaturePropertyBags"><bagId>2</bagId></a></bag>
                </FeaturePropertyBags>
                """));
            string[][] metadata = {
                new[] { "Model", snapshot.ModelTitle }, new[] { "Scope", snapshot.Scope },
                new[] { "Created UTC", snapshot.CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) },
                new[] { "Clash count", snapshot.Rows.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "Length units", ClashExcelSnapshot.UNITS },
                new[] { "Done", "Independent unchecked review checkbox; not Clash Status. Edits do not update BimGo." },
                new[] { "Compatibility", "Developer preview: modern Microsoft 365 in-cell checkboxes; interactive Excel acceptance pending." }
            };
            Add(zip, "xl/worksheets/sheet1.xml", new XDocument(new XElement(S + "worksheet",
                new XElement(S + "sheetData", metadata.Select((pair, i) => new XElement(S + "row", new XAttribute("r", i + 1),
                    TextCell("A" + (i + 1), pair[0]), TextCell("B" + (i + 1), pair[1])))))));
            WriteClashes(zip, snapshot.Rows, token);
            Add(zip, "xl/worksheets/_rels/sheet2.xml.rels", Relationships(("rId1", OFFICE_REL + "/table", "../tables/table1.xml")));
            string range = "A1:W" + (snapshot.Rows.Count + 1).ToString(CultureInfo.InvariantCulture);
            Add(zip, "xl/tables/table1.xml", new XDocument(new XElement(S + "table",
                new XAttribute("id", "1"), new XAttribute("name", "ClashesTable"), new XAttribute("displayName", "ClashesTable"),
                new XAttribute("ref", range), new XAttribute("totalsRowShown", "0"),
                new XElement(S + "autoFilter", new XAttribute("ref", range)),
                new XElement(S + "tableColumns", new XAttribute("count", HEADERS.Length), HEADERS.Select((h, i) =>
                    new XElement(S + "tableColumn", new XAttribute("id", i + 1), new XAttribute("name", h)))),
                new XElement(S + "tableStyleInfo", new XAttribute("name", "TableStyleMedium9"), new XAttribute("showFirstColumn", "0"),
                    new XAttribute("showLastColumn", "0"), new XAttribute("showRowStripes", "1"), new XAttribute("showColumnStripes", "0")))));
        }

        private static void WriteClashes(ZipArchive zip, IReadOnlyList<ClashExcelRow> rows, CancellationToken token)
        {
            using var stream = zip.CreateEntry("xl/worksheets/sheet2.xml", CompressionLevel.Optimal).Open();
            using var writer = XmlWriter.Create(stream, Settings());
            writer.WriteStartElement("worksheet", MAIN);
            new XElement(S + "dimension", new XAttribute("ref", "A1:W" + (rows.Count + 1).ToString(CultureInfo.InvariantCulture))).WriteTo(writer);
            new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", "0"), new XAttribute("tabSelected", "1"),
                new XElement(S + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")),
                new XElement(S + "selection", new XAttribute("pane", "bottomLeft"), new XAttribute("activeCell", "A2"), new XAttribute("sqref", "A2")))).WriteTo(writer);
            new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", "1"), new XAttribute("max", "23"),
                new XAttribute("width", "22"), new XAttribute("customWidth", "1"))).WriteTo(writer);
            writer.WriteStartElement("sheetData", MAIN);
            new XElement(S + "row", new XAttribute("r", "1"), HEADERS.Select((h, i) => TextCell(((char)('A' + i)) + "1", h))).WriteTo(writer);
            for (int i = 0; i < rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                ClashExcelRow r = rows[i] ?? throw new ArgumentException("A report contains a missing clash row.");
                string n = (i + 2).ToString(CultureInfo.InvariantCulture);
                string[] text = { r.ClashId, r.Test, r.Group, r.Status, r.AssignedTo, r.Notes,
                    r.ElementAId == 0 ? string.Empty : r.ElementAId.ToString(CultureInfo.InvariantCulture), r.ModelA, r.CategoryA, r.FamilyTypeA, r.LevelA,
                    r.ElementBId == 0 ? string.Empty : r.ElementBId.ToString(CultureInfo.InvariantCulture), r.ModelB, r.CategoryB, r.FamilyTypeB, r.LevelB, r.Grid };
                // IDs are text: Excel numbers lose integer precision beyond 15 digits.
                new XElement(S + "row", new XAttribute("r", i + 2), text.Select((v, c) => TextCell(((char)('A' + c)) + n, v)),
                    NumberCell("R" + n, r.X), NumberCell("S" + n, r.Y), NumberCell("T" + n, r.Z), NumberCell("U" + n, r.Distance),
                    BooleanCell("V" + n, r.Crossing), BooleanCell("W" + n, false, checkbox: true)).WriteTo(writer);
            }
            writer.WriteEndElement();
            new XElement(S + "tableParts", new XAttribute("count", "1"),
                new XElement(S + "tablePart", new XAttribute(XName.Get("id", OFFICE_REL), "rId1"))).WriteTo(writer);
            writer.WriteEndElement();
        }

        private static XElement TextCell(string address, string value) => new(S + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
            new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), EncodeText(value, address))));

        private static XElement NumberCell(string address, double value)
        {
            if (!double.IsFinite(value)) { throw new ArgumentException($"Excel cell {address} contains a nonfinite measurement."); }
            return new XElement(S + "c", new XAttribute("r", address), new XElement(S + "v", value.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static XElement BooleanCell(string address, bool value, bool checkbox = false) => new(S + "c", new XAttribute("r", address),
            new XAttribute("t", "b"), checkbox ? new XAttribute("s", "1") : null, new XElement(S + "v", value ? "1" : "0"));

        private static string EncodeText(string value, string address)
        {
            value ??= string.Empty;
            if (value.Length > 32_767) { throw new ArgumentException($"Excel cell {address} exceeds 32,767 characters. Shorten the text (including Notes) or use another report format; nothing was saved."); }
            var encoded = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                // Protect literal SpreadsheetML escape sequences before encoding invalid XML code units.
                if (c == '_' && i + 6 < value.Length && value[i + 1] == 'x' && value[i + 6] == '_' &&
                    IsHex(value[i + 2]) && IsHex(value[i + 3]) && IsHex(value[i + 4]) && IsHex(value[i + 5]))
                {
                    encoded.Append("_x005F_");
                }
                else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    encoded.Append(c).Append(value[++i]);
                }
                else if (!XmlConvert.IsXmlChar(c) || char.IsSurrogate(c) || c == '\r')
                {
                    encoded.Append("_x").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
                }
                else { encoded.Append(c); }
            }
            return encoded.ToString();
        }

        private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

        private static XDocument Relationships(params (string Id, string Type, string Target)[] entries)
        {
            XNamespace ns = PACKAGE_REL;
            return new XDocument(new XElement(ns + "Relationships", entries.Select(e => new XElement(ns + "Relationship",
                new XAttribute("Id", e.Id), new XAttribute("Type", e.Type), new XAttribute("Target", e.Target)))));
        }

        private static XmlWriterSettings Settings() => new() { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.None };

        private static void Add(ZipArchive zip, string path, XDocument document)
        {
            using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
            using var writer = XmlWriter.Create(stream, Settings());
            document.Save(writer);
        }
    }
}
