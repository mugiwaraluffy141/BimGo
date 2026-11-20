using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ReviewExcelSpike;

public sealed record SpikeRow(string ClashId, string Status, string Notes, bool Done = false);

/// <summary>
/// Standalone research writer, not a production export service. Encoding follows
/// XlsxWriter fbd21d2a3c534b82e4ea2dd1b2572c45e9ea1ef8; see validation report.
/// No Excel automation, macros, drawings, formulas or third-party dependencies.
/// </summary>
public static class CheckboxWorkbook
{
    internal const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal const string Bag = "http://schemas.microsoft.com/office/spreadsheetml/2022/featurepropertybag";
    internal const string FeatureRelationship = "http://schemas.microsoft.com/office/2022/11/relationships/FeaturePropertyBag";
    internal const string CheckboxExtension = "{C7286773-470A-42A8-94C5-96B5CB345126}";
    internal const string FeaturePart = "xl/featurePropertyBag/featurePropertyBag.xml";
    private const string OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace S = Main;

    // CreateNew deliberately refuses to overwrite any existing file. Atomic replacement,
    // cancellation, limits/invalid text policy and report projection belong to integration.
    public static void Write(string path, IReadOnlyList<SpikeRow> rows)
    {
        if (rows.Count is < 1 or > 1_048_575)
            throw new ArgumentOutOfRangeException(nameof(rows));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        var last = (rows.Count + 1).ToString(CultureInfo.InvariantCulture);
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
        Add(zip, "_rels/.rels", Relationships(("rId1", OfficeRel + "/officeDocument", "xl/workbook.xml")));
        Add(zip, "xl/_rels/workbook.xml.rels", Relationships(
            ("rId1", OfficeRel + "/worksheet", "worksheets/sheet1.xml"),
            ("rId2", OfficeRel + "/worksheet", "worksheets/sheet2.xml"),
            ("rId3", OfficeRel + "/styles", "styles.xml"),
            ("rId4", FeatureRelationship, "featurePropertyBag/featurePropertyBag.xml")));
        Add(zip, "xl/workbook.xml", XDocument.Parse($"""
            <workbook xmlns="{Main}" xmlns:r="{OfficeRel}">
              <bookViews><workbookView activeTab="1"/></bookViews>
              <sheets><sheet name="Metadata" sheetId="1" r:id="rId1"/><sheet name="Clashes" sheetId="2" r:id="rId2"/></sheets>
            </workbook>
            """));
        Add(zip, "xl/styles.xml", XDocument.Parse($"""
            <styleSheet xmlns="{Main}">
              <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
              <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="2">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"><extLst>
                  <ext uri="{CheckboxExtension}" xmlns:xfpb="{Bag}"><xfpb:xfComplement i="0"/></ext>
                </extLst></xf>
              </cellXfs>
              <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
              <dxfs count="0"/>
              <tableStyles count="0" defaultTableStyle="TableStyleMedium9" defaultPivotStyle="PivotStyleLight16"/>
            </styleSheet>
            """));
        Add(zip, FeaturePart, XDocument.Parse($"""
            <FeaturePropertyBags xmlns="{Bag}">
              <bag type="Checkbox"/>
              <bag type="XFControls"><bagId k="CellControl">0</bagId></bag>
              <bag type="XFComplement"><bagId k="XFControls">1</bagId></bag>
              <bag type="XFComplements" extRef="XFComplementsMapperExtRef"><a k="MappedFeaturePropertyBags"><bagId>2</bagId></a></bag>
            </FeaturePropertyBags>
            """));
        Add(zip, "xl/worksheets/sheet1.xml", new XDocument(new XElement(S + "worksheet",
            new XElement(S + "sheetData",
                Row(1, Text("A1", "Purpose"), Text("B1", "P2 research sample; interactive acceptance NOT RUN")),
                Row(2, Text("A2", "Row count"), Text("B2", rows.Count.ToString(CultureInfo.InvariantCulture))),
                Row(3, Text("A3", "Done"), Text("B3", "Independent Boolean cell with checkbox style; not Clash Status"))))));
        var data = new XElement(S + "sheetData", Row(1, Text("A1", "ClashId"), Text("B1", "Status"), Text("C1", "Notes"), Text("D1", "Done")));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = (i + 2).ToString(CultureInfo.InvariantCulture);
            var row = rows[i];
            data.Add(Row(i + 2, Text("A" + r, row.ClashId), Text("B" + r, row.Status), Text("C" + r, row.Notes),
                new XElement(S + "c", new XAttribute("r", "D" + r), new XAttribute("s", "1"), new XAttribute("t", "b"),
                    new XElement(S + "v", row.Done ? "1" : "0"))));
        }
        Add(zip, "xl/worksheets/sheet2.xml", new XDocument(new XElement(S + "worksheet",
            new XAttribute(XNamespace.Xmlns + "r", OfficeRel),
            new XElement(S + "dimension", new XAttribute("ref", "A1:D" + last)),
            new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", "0"), new XAttribute("tabSelected", "1"))),
            new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", "1"), new XAttribute("max", "4"), new XAttribute("width", "28"), new XAttribute("customWidth", "1"))),
            data,
            new XElement(S + "tableParts", new XAttribute("count", "1"), new XElement(S + "tablePart", new XAttribute(XName.Get("id", OfficeRel), "rId1"))))));
        Add(zip, "xl/worksheets/_rels/sheet2.xml.rels", Relationships(("rId1", OfficeRel + "/table", "../tables/table1.xml")));
        Add(zip, "xl/tables/table1.xml", XDocument.Parse($"""
            <table xmlns="{Main}" id="1" name="ClashesTable" displayName="ClashesTable" ref="A1:D{last}" totalsRowShown="0">
              <autoFilter ref="A1:D{last}"/>
              <tableColumns count="4"><tableColumn id="1" name="ClashId"/><tableColumn id="2" name="Status"/><tableColumn id="3" name="Notes"/><tableColumn id="4" name="Done"/></tableColumns>
              <tableStyleInfo name="TableStyleMedium9" showFirstColumn="0" showLastColumn="0" showRowStripes="1" showColumnStripes="0"/>
            </table>
            """));
    }

    private static XElement Row(int index, params XElement[] cells) => new(S + "row", new XAttribute("r", index), cells);

    private static XElement Text(string address, string value)
    {
        if (value.Length > 32_767) throw new ArgumentException("Excel cell text limit exceeded.");
        return new XElement(S + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
            new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
    }

    private static XDocument Relationships(params (string Id, string Type, string Target)[] entries)
    {
        XNamespace ns = PackageRel;
        return new XDocument(new XElement(ns + "Relationships", entries.Select(e => new XElement(ns + "Relationship",
            new XAttribute("Id", e.Id), new XAttribute("Type", e.Type), new XAttribute("Target", e.Target)))));
    }

    private static void Add(ZipArchive zip, string path, XDocument document)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        document.Save(writer);
    }
}
