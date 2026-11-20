using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BimGo.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BimGo.Tests
{
    [TestClass]
    public sealed class ClashExcelWriterTests
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private string _directory;

        [TestInitialize]
        public void Initialize() => _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "BimGoExcelTests-" + Guid.NewGuid().ToString("N"))).FullName;

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_directory, recursive: true); // Only the unique directory owned by this test.

        private string Destination => Path.Combine(_directory, "report.xlsx");
        private static ClashExcelRow Row(string id = "CL-001") => new(id, "Ống gió", "Group 1", "Resolved", "Reviewer", "Notes",
            long.MaxValue, "Host.rvt", "Ducts", "Duct type", "Level 1", 0, "Link.rvt", null, null, null, "C-4", 1.25, -2.5, 3, 0.025, true);
        private static ClashExcelSnapshot Snapshot(params ClashExcelRow[] rows) => new("Model & <title>", "Selection", new DateTime(2026, 11, 20, 1, 2, 3, DateTimeKind.Utc), Array.AsReadOnly(rows));
        private static XDocument Part(ZipArchive zip, string name)
        {
            using var stream = zip.GetEntry(name).Open();
            return XDocument.Load(stream);
        }
        private static XElement Cell(XDocument sheet, string address) => sheet.Descendants(S + "c").Single(c => (string)c.Attribute("r") == address);
        private static string Text(XDocument sheet, string address) => Cell(sheet, address).Descendants(S + "t").Single().Value;

        [TestMethod]
        public void PackageIncludesFullRowsMetadataTableFreezeAndOnlyInternalRelationships()
        {
            ClashExcelWriter.Write(Destination, Snapshot(Row("second"), Row("first") with { Status = "New", Notes = "Different", Crossing = false }));
            using var zip = ZipFile.OpenRead(Destination);
            Assert.AreEqual(10, zip.Entries.Count);
            foreach (var entry in zip.Entries)
            {
                using var stream = entry.Open();
                var xml = XDocument.Load(stream);
                Assert.IsFalse(xml.Descendants().Any(e => e.Name.LocalName is "f" or "hyperlink"));
                Assert.IsFalse(xml.Descendants().Attributes("TargetMode").Any());
            }
            var workbook = Part(zip, "xl/workbook.xml");
            CollectionAssert.AreEqual(new[] { "Metadata", "Clashes" }, workbook.Descendants(S + "sheet").Select(s => (string)s.Attribute("name")).ToArray());
            var sheet = Part(zip, "xl/worksheets/sheet2.xml");
            Assert.AreEqual(3, sheet.Descendants(S + "row").Count());
            Assert.IsTrue(sheet.Descendants(S + "row").All(r => r.Elements(S + "c").Count() == 23));
            Assert.AreEqual("second", Text(sheet, "A2"));
            Assert.AreEqual("first", Text(sheet, "A3"));
            Assert.AreEqual("Ống gió", Text(sheet, "B2"));
            Assert.AreEqual("Group 1", Text(sheet, "C2"));
            Assert.AreEqual("Resolved", Text(sheet, "D2"));
            Assert.AreEqual("Reviewer", Text(sheet, "E2"));
            Assert.AreEqual("Notes", Text(sheet, "F2"));
            Assert.AreEqual(long.MaxValue.ToString(CultureInfo.InvariantCulture), Text(sheet, "G2"));
            Assert.AreEqual("Host.rvt", Text(sheet, "H2"));
            Assert.AreEqual("Ducts", Text(sheet, "I2"));
            Assert.AreEqual("Duct type", Text(sheet, "J2"));
            Assert.AreEqual("Level 1", Text(sheet, "K2"));
            Assert.AreEqual("", Text(sheet, "L2"));
            Assert.AreEqual("Link.rvt", Text(sheet, "M2"));
            foreach (string column in new[] { "N", "O", "P" }) { Assert.AreEqual("", Text(sheet, column + "2")); }
            Assert.AreEqual("C-4", Text(sheet, "Q2"));
            Assert.AreEqual("1", Cell(sheet, "V2").Element(S + "v").Value);
            Assert.AreEqual("0", Cell(sheet, "V3").Element(S + "v").Value);
            Assert.AreEqual("frozen", (string)sheet.Descendants(S + "pane").Single().Attribute("state"));
            Assert.AreEqual("A2", (string)sheet.Descendants(S + "pane").Single().Attribute("topLeftCell"));
            var table = Part(zip, "xl/tables/table1.xml");
            Assert.AreEqual("A1:W3", (string)table.Root.Attribute("ref"));
            Assert.AreEqual("A1:W3", (string)table.Root.Element(S + "autoFilter").Attribute("ref"));
            CollectionAssert.AreEqual(sheet.Descendants(S + "row").First().Descendants(S + "t").Select(t => t.Value).ToArray(),
                table.Descendants(S + "tableColumn").Select(c => (string)c.Attribute("name")).ToArray());
            var metadata = Part(zip, "xl/worksheets/sheet1.xml");
            Assert.AreEqual("Model & <title>", Text(metadata, "B1"));
            Assert.AreEqual("Selection", Text(metadata, "B2"));
            Assert.AreEqual("2026-11-20T01:02:03.0000000Z", Text(metadata, "B3"));
            Assert.AreEqual("2", Text(metadata, "B4"));
            Assert.AreEqual("m", Text(metadata, "B5"));
        }

        [TestMethod]
        public void DoneIsUncheckedBooleanWithExactFeatureBagMappingNotJustBooleanOrGlyph()
        {
            ClashExcelWriter.Write(Destination, Snapshot(Row(), Row("2")));
            using var zip = ZipFile.OpenRead(Destination);
            var sheet = Part(zip, "xl/worksheets/sheet2.xml");
            foreach (string address in new[] { "W2", "W3" })
            {
                var c = Cell(sheet, address);
                Assert.AreEqual("b", (string)c.Attribute("t"));
                Assert.AreEqual("1", (string)c.Attribute("s"));
                Assert.AreEqual("0", c.Element(S + "v").Value);
            }
            XNamespace bag = "http://schemas.microsoft.com/office/spreadsheetml/2022/featurepropertybag";
            var xf = Part(zip, "xl/styles.xml").Root.Element(S + "cellXfs").Elements(S + "xf").ElementAt(1);
            Assert.AreEqual("{C7286773-470A-42A8-94C5-96B5CB345126}", (string)xf.Descendants(S + "ext").Single().Attribute("uri"));
            Assert.AreEqual("0", (string)xf.Descendants(bag + "xfComplement").Single().Attribute("i"));
            var expected = XElement.Parse($"""
                <FeaturePropertyBags xmlns="{bag}"><bag type="Checkbox"/><bag type="XFControls"><bagId k="CellControl">0</bagId></bag><bag type="XFComplement"><bagId k="XFControls">1</bagId></bag><bag type="XFComplements" extRef="XFComplementsMapperExtRef"><a k="MappedFeaturePropertyBags"><bagId>2</bagId></a></bag></FeaturePropertyBags>
                """);
            Assert.IsTrue(XNode.DeepEquals(expected, Part(zip, "xl/featurePropertyBag/featurePropertyBag.xml").Root));
            Assert.IsTrue(Part(zip, "xl/_rels/workbook.xml.rels").Root.Elements().Any(e =>
                (string)e.Attribute("Type") == "http://schemas.microsoft.com/office/2022/11/relationships/FeaturePropertyBag" &&
                (string)e.Attribute("Target") == "featurePropertyBag/featurePropertyBag.xml"));
            Assert.IsTrue(Part(zip, "[Content_Types].xml").Root.Elements().Any(e =>
                (string)e.Attribute("PartName") == "/xl/featurePropertyBag/featurePropertyBag.xml" &&
                (string)e.Attribute("ContentType") == "application/vnd.ms-excel.featurepropertybag+xml"));
        }

        [TestMethod]
        public void TextIsLiteralAndControlsEscapesUnicodeAndWhitespaceRoundTrip()
        {
            string[] samples = { "=SUM(1,2)", "+1", "-2", "@name", "https://example.invalid", "  Tiếng Việt <&> \" 😀\t\r\n ",
                "\0\u0001\u000b\u001f\ufffe\uffff\ud800", "_x0000_ _xABcd_ _x005F_x0041_ _x0041_ _xzzzz_" };
            ClashExcelWriter.Write(Destination, Snapshot(samples.Select((s, i) => Row(i.ToString(CultureInfo.InvariantCulture)) with { Notes = s }).ToArray()));
            using var zip = ZipFile.OpenRead(Destination);
            var sheet = Part(zip, "xl/worksheets/sheet2.xml");
            for (int i = 0; i < samples.Length; i++)
            {
                var cell = Cell(sheet, "F" + (i + 2));
                Assert.AreEqual("inlineStr", (string)cell.Attribute("t"));
                var text = cell.Descendants(S + "t").Single();
                Assert.AreEqual("preserve", (string)text.Attribute(XNamespace.Xml + "space"));
                // OOXML decoding is one pass: protected literal _xNNNN_ must not be decoded twice.
                string decoded = Regex.Replace(text.Value, "_x([0-9A-Fa-f]{4})_", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
                Assert.AreEqual(samples[i], decoded);
            }
        }

        [TestMethod]
        public void MeasurementsAreFiniteInvariantNumericCellsEvenUnderCommaDecimalCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
                ClashExcelWriter.Write(Destination, Snapshot(Row()));
                using var zip = ZipFile.OpenRead(Destination);
                var sheet = Part(zip, "xl/worksheets/sheet2.xml");
                foreach (var pair in new[] { ("R2", "1.25"), ("S2", "-2.5"), ("T2", "3"), ("U2", "0.025") })
                {
                    Assert.IsNull(Cell(sheet, pair.Item1).Attribute("t"));
                    Assert.AreEqual(pair.Item2, Cell(sheet, pair.Item1).Element(S + "v").Value);
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestMethod]
        public void LongNotesAndNonfiniteNumbersFailWithoutOverwritingOrLeavingOurTemp()
        {
            byte[] original = { 1, 3, 5, 7 };
            File.WriteAllBytes(Destination, original);
            foreach (var row in new[] { Row() with { Notes = new string('n', 32_768) }, Row() with { X = double.NaN },
                Row() with { Y = double.PositiveInfinity }, Row() with { Z = double.NegativeInfinity }, Row() with { Distance = double.NaN } })
            {
                Assert.ThrowsExactly<ArgumentException>(() => ClashExcelWriter.Write(Destination, Snapshot(row)));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(Destination));
                Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
            }
        }

        [TestMethod]
        public void MaximumLengthTextIsNotTruncatedAndExistingDestinationIsReplaced()
        {
            File.WriteAllText(Destination, "previous report");
            string notes = new string('n', 32_765) + "😀";
            ClashExcelWriter.Write(Destination, Snapshot(Row() with { Notes = notes }));
            using var zip = ZipFile.OpenRead(Destination);
            Assert.AreEqual(notes, Text(Part(zip, "xl/worksheets/sheet2.xml"), "F2"));
            Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
        }

        [TestMethod]
        public void EmptyReportAndPreCancelledWriteDoNotCreateFiles()
        {
            Assert.ThrowsExactly<ArgumentException>(() => ClashExcelWriter.Write(Destination, Snapshot()));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => ClashExcelWriter.Write(Destination, Snapshot(Row()), cancellation.Token));
            Assert.AreEqual(0, Directory.GetFiles(_directory).Length);
        }

        [TestMethod]
        public void CancellationDuringRowGenerationPreservesDestinationAndCleansTemp()
        {
            File.WriteAllText(Destination, "original");
            using var cancellation = new CancellationTokenSource();
            var snapshot = Snapshot() with { Rows = new CancellingRows(cancellation) };
            Assert.ThrowsExactly<OperationCanceledException>(() => ClashExcelWriter.Write(Destination, snapshot, cancellation.Token));
            Assert.AreEqual("original", File.ReadAllText(Destination));
            Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
        }

        private sealed class CancellingRows(CancellationTokenSource cancellation) : IReadOnlyList<ClashExcelRow>
        {
            public int Count => 2;
            public ClashExcelRow this[int index]
            {
                get { cancellation.Cancel(); return Row(); }
            }
            public IEnumerator<ClashExcelRow> GetEnumerator() => Enumerable.Repeat(Row(), Count).GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [TestMethod]
        public void LockedDestinationSurvivesAndOnlyOurTemporaryFileIsCleaned()
        {
            byte[] original = { 9, 8, 7 };
            File.WriteAllBytes(Destination, original);
            string unrelated = Path.Combine(_directory, "report.xlsx.tmp");
            File.WriteAllText(unrelated, "not ours");
            using (var locked = new FileStream(Destination, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsExactly<IOException>(() => ClashExcelWriter.Write(Destination, Snapshot(Row())));
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Destination));
            Assert.AreEqual("not ours", File.ReadAllText(unrelated));
            Assert.AreEqual(2, Directory.GetFiles(_directory).Length);
        }

        [TestMethod]
        public void ReadOnlyDestinationSurvivesUnauthorizedFailureAndTempIsCleaned()
        {
            File.WriteAllText(Destination, "original");
            File.SetAttributes(Destination, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() => ClashExcelWriter.Write(Destination, Snapshot(Row())));
                Assert.AreEqual("original", File.ReadAllText(Destination));
                Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
            }
            finally { File.SetAttributes(Destination, FileAttributes.Normal); }
        }

        [TestMethod]
        public void NullOptionalTextIsBlankAndOverlongMetadataIsRejectedWithoutPublication()
        {
            var row = Row() with { Notes = null, Test = null, Group = null, AssignedTo = null, Grid = null, ModelA = null };
            ClashExcelWriter.Write(Destination, Snapshot(row));
            using (var zip = ZipFile.OpenRead(Destination))
            {
                var sheet = Part(zip, "xl/worksheets/sheet2.xml");
                foreach (string column in new[] { "B", "C", "E", "F", "H", "Q" }) { Assert.AreEqual("", Text(sheet, column + "2")); }
            }
            byte[] original = File.ReadAllBytes(Destination);
            Assert.ThrowsExactly<ArgumentException>(() => ClashExcelWriter.Write(Destination, Snapshot(row) with { Scope = new string('s', 32_768) }));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Destination));
            Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
        }

        [TestMethod]
        public void MissingDirectoryDoesNotCreateDestinationOrFallbackFiles()
        {
            string path = Path.Combine(_directory, "missing", "report.xlsx");
            Assert.ThrowsExactly<DirectoryNotFoundException>(() => ClashExcelWriter.Write(path, Snapshot(Row())));
            Assert.AreEqual(0, Directory.GetFileSystemEntries(_directory).Length);
        }

        [TestMethod]
        public void TenThousandRowsKeepIdsAndDoneInsideTheFullTable()
        {
            var rows = Enumerable.Range(1, 10_000).Select(i => Row("CL-" + i.ToString("D5", CultureInfo.InvariantCulture))).ToArray();
            ClashExcelWriter.Write(Destination, Snapshot(rows));
            using var zip = ZipFile.OpenRead(Destination);
            var sheet = Part(zip, "xl/worksheets/sheet2.xml");
            Assert.AreEqual(10_001, sheet.Descendants(S + "row").Count());
            Assert.AreEqual("A1:W10001", (string)Part(zip, "xl/tables/table1.xml").Root.Attribute("ref"));
            CollectionAssert.AreEqual(rows.Select(r => r.ClashId).ToArray(), sheet.Descendants(S + "row").Skip(1)
                .Select(r => r.Elements(S + "c").First().Descendants(S + "t").Single().Value).ToArray());
            Assert.IsTrue(sheet.Descendants(S + "row").Skip(1).All(r =>
            {
                var done = r.Elements(S + "c").Last();
                return (string)done.Attribute("s") == "1" && (string)done.Attribute("t") == "b" && done.Element(S + "v").Value == "0";
            }));
        }
    }
}
