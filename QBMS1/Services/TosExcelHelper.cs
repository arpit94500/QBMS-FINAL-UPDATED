using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace QBMS1.Services
{
    // Minimal XLSX reader/writer built on framework ZIP/XML APIs so no new NuGet package is required.
    public static class TosExcelHelper
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static byte[] CreateWorkbook(IList<string[]> rows)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    WriteText(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                    WriteText(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    WriteText(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"TOS Question Template\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                    WriteText(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                    var sb = new StringBuilder();
                    sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
                    for (int r = 0; r < rows.Count; r++)
                    {
                        sb.Append("<row r=\"").Append(r + 1).Append("\">");
                        for (int c = 0; c < rows[r].Length; c++)
                        {
                            sb.Append("<c r=\"").Append(ColumnName(c + 1)).Append(r + 1).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                              .Append(System.Security.SecurityElement.Escape(rows[r][c] ?? string.Empty))
                              .Append("</t></is></c>");
                        }
                        sb.Append("</row>");
                    }
                    sb.Append("</sheetData></worksheet>");
                    WriteText(zip, "xl/worksheets/sheet1.xml", sb.ToString());
                }
                return output.ToArray();
            }
        }

        public static List<List<string>> ReadFirstWorksheet(Stream stream)
        {
            using (var source = new MemoryStream())
            {
                stream.CopyTo(source);
                source.Position = 0;
                using (var zip = new ZipArchive(source, ZipArchiveMode.Read, true))
                {
                    var workbookEntry = zip.GetEntry("xl/workbook.xml");
                    var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
                    if (workbookEntry == null || relsEntry == null) throw new InvalidDataException("This is not a valid Excel .xlsx workbook.");
                    XDocument workbook; XDocument rels;
                    using (var s = workbookEntry.Open()) workbook = XDocument.Load(s);
                    using (var s = relsEntry.Open()) rels = XDocument.Load(s);
                    var firstSheet = workbook.Descendants(MainNs + "sheet").FirstOrDefault();
                    if (firstSheet == null) throw new InvalidDataException("The Excel workbook has no worksheet.");
                    var relId = (string)firstSheet.Attribute(RelNs + "id");
                    XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
                    var target = (string)rels.Descendants(packageRel + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == relId)?.Attribute("Target");
                    if (string.IsNullOrEmpty(target)) throw new InvalidDataException("Unable to locate the first worksheet.");
                    var sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.Replace("../", "");
                    var sheetEntry = zip.GetEntry(sheetPath);
                    if (sheetEntry == null) sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheetEntry == null) throw new InvalidDataException("The first worksheet is missing.");
                    var shared = new List<string>();
                    var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
                    if (sharedEntry != null)
                    {
                        using (var s = sharedEntry.Open())
                        {
                            var sharedDoc = XDocument.Load(s);
                            shared = sharedDoc.Descendants(MainNs + "si").Select(si => string.Concat(si.Descendants(MainNs + "t").Select(t => t.Value))).ToList();
                        }
                    }
                    XDocument sheet;
                    using (var s = sheetEntry.Open()) sheet = XDocument.Load(s);
                    var result = new List<List<string>>();
                    foreach (var row in sheet.Descendants(MainNs + "sheetData").Elements(MainNs + "row"))
                    {
                        var cells = new Dictionary<int, string>();
                        int max = 0;
                        foreach (var cell in row.Elements(MainNs + "c"))
                        {
                            var reference = (string)cell.Attribute("r") ?? "A1";
                            int col = ColumnNumber(Regex.Match(reference, "^[A-Z]+").Value);
                            max = Math.Max(max, col);
                            var type = (string)cell.Attribute("t");
                            string value = string.Empty;
                            if (type == "inlineStr") value = string.Concat(cell.Descendants(MainNs + "t").Select(t => t.Value));
                            else
                            {
                                var v = cell.Element(MainNs + "v");
                                value = v == null ? string.Empty : v.Value;
                                if (type == "s" && int.TryParse(value, out int ix) && ix >= 0 && ix < shared.Count) value = shared[ix];
                                if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                            }
                            cells[col] = value;
                        }
                        var values = new List<string>();
                        for (int i = 1; i <= Math.Max(max, 1); i++) values.Add(cells.ContainsKey(i) ? cells[i] : string.Empty);
                        result.Add(values);
                    }
                    return result;
                }
            }
        }

        private static void WriteText(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content);
        }
        private static string ColumnName(int number)
        {
            var result = string.Empty;
            while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
            return result;
        }
        private static int ColumnNumber(string letters)
        {
            int result = 0;
            foreach (char c in letters) result = result * 26 + (c - 'A' + 1);
            return result;
        }
    }
}
