using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using ReviewExcelSpike;

var output = Path.GetFullPath(args.Length > 0 ? args[0] : "tools/review-excel-spike/artifacts");
Directory.CreateDirectory(output);
string[] notes = ["=1+1", "+SUM(A1:A2)", "-1+2", "@SUM(A1:A2)", "Tiếng Việt <&> \"text\"", "https://example.invalid/", "  preserved  "];
foreach (var count in new[] { 1, 100, 10_000 })
{
    var rows = Enumerable.Range(1, count).Select(i => new SpikeRow($"CL-{i:D5}", i % 2 == 0 ? "Resolved" : "New", notes[(i - 1) % notes.Length])).ToArray();
    var path = Path.Combine(output, $"checkbox-{count}.xlsx");
    var watch = Stopwatch.StartNew();
    CheckboxWorkbook.Write(path, rows);
    watch.Stop();
    Validate(path, rows);
    // Writer must never overwrite an existing sample/user file.
    var original = File.ReadAllBytes(path);
    try { CheckboxWorkbook.Write(path, rows); throw new Exception("Overwrite accepted"); }
    catch (IOException) { }
    Check(original.SequenceEqual(File.ReadAllBytes(path)), "Existing file changed");
    Console.WriteLine($"PASS structural + overwrite tests: {path}; {new FileInfo(path).Length:N0} bytes; write {watch.ElapsedMilliseconds} ms");
}

// Optional independently sourced upstream fixture: no download or package install by this program.
if (args.Length > 1)
{
    using var fixture = ZipFile.OpenRead(Path.GetFullPath(args[1]));
    using var generated = ZipFile.OpenRead(Path.Combine(output, "checkbox-1.xlsx"));
    Check(XNode.DeepEquals(Read(fixture, CheckboxWorkbook.FeaturePart).Root, Read(generated, CheckboxWorkbook.FeaturePart).Root), "Feature bag differs from upstream fixture");
    XNamespace b = CheckboxWorkbook.Bag;
    var upstreamXf = Read(fixture, "xl/styles.xml").Descendants(b + "xfComplement").Single();
    var ourXf = Read(generated, "xl/styles.xml").Descendants(b + "xfComplement").Single();
    Check(XNode.DeepEquals(upstreamXf, ourXf), "xfComplement differs");
    Check((string?)upstreamXf.Parent?.Attribute("uri") == CheckboxWorkbook.CheckboxExtension, "Extension URI differs");
    Console.WriteLine("PASS upstream fixture feature-bag and style-extension comparison");
}
Console.WriteLine("Excel NOT launched. Interactive click/save/reopen/sort/filter acceptance NOT RUN.");

static void Validate(string path, IReadOnlyList<SpikeRow> rows)
{
    using var zip = ZipFile.OpenRead(path);
    XNamespace s = CheckboxWorkbook.Main;
    XNamespace b = CheckboxWorkbook.Bag;
    XNamespace p = "http://schemas.openxmlformats.org/package/2006/relationships";
    XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    foreach (var entry in zip.Entries) { using var stream = entry.Open(); _ = XDocument.Load(stream); }
    Check(zip.Entries.Count == 10, "Unexpected package parts");
    var rel = Read(zip, "xl/_rels/workbook.xml.rels").Descendants(p + "Relationship").Single(e => (string?)e.Attribute("Type") == CheckboxWorkbook.FeatureRelationship);
    Check((string?)rel.Attribute("Target") == "featurePropertyBag/featurePropertyBag.xml", "Feature bag target");
    Check(Read(zip, "[Content_Types].xml").Descendants(ct + "Override").Any(e => (string?)e.Attribute("PartName") == "/" + CheckboxWorkbook.FeaturePart && (string?)e.Attribute("ContentType") == "application/vnd.ms-excel.featurepropertybag+xml"), "Feature content type");
    var bags = Read(zip, CheckboxWorkbook.FeaturePart).Root!.Elements(b + "bag").ToArray();
    Check(bags.Select(e => (string?)e.Attribute("type")).SequenceEqual(new[] { "Checkbox", "XFControls", "XFComplement", "XFComplements" }), "Feature bag order");
    Check(bags[1].Element(b + "bagId")!.Value == "0" && bags[2].Element(b + "bagId")!.Value == "1" && bags[3].Descendants(b + "bagId").Single().Value == "2", "Feature bag references");
    var styles = Read(zip, "xl/styles.xml").Root!.Element(s + "cellXfs")!.Elements(s + "xf").ToArray();
    Check(styles.Length == 2 && (string?)styles[1].Descendants(b + "xfComplement").Single().Attribute("i") == "0", "Checkbox style reference");
    var sheet = Read(zip, "xl/worksheets/sheet2.xml");
    Check(!sheet.Descendants(s + "f").Any() && !sheet.Descendants(s + "hyperlink").Any(), "Untrusted text became formula/link");
    var data = sheet.Descendants(s + "row").Skip(1).ToArray();
    Check(data.Length == rows.Count, "Data row count");
    for (var i = 0; i < data.Length; i++)
    {
        var cells = data[i].Elements(s + "c").ToArray();
        Check(cells[0].Descendants(s + "t").Single().Value == rows[i].ClashId, "Clash ID");
        Check(cells[2].Descendants(s + "t").Single().Value == rows[i].Notes, "Text roundtrip");
        Check((string?)cells[3].Attribute("s") == "1" && (string?)cells[3].Attribute("t") == "b" && cells[3].Element(s + "v")!.Value == "0", "Initial unchecked checkbox");
    }
    var table = Read(zip, "xl/tables/table1.xml");
    Check((string?)table.Root!.Attribute("ref") == $"A1:D{rows.Count + 1}" && (string?)table.Root.Element(s + "autoFilter")!.Attribute("ref") == $"A1:D{rows.Count + 1}", "Full row sort/filter table range");
}

static XDocument Read(ZipArchive zip, string name)
{
    using var stream = (zip.GetEntry(name) ?? throw new Exception("Missing " + name)).Open();
    return XDocument.Load(stream);
}
static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
