using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace QBMS1.Services
{
    /// <summary>
    /// Creates and reads .docx files using the Open XML package format.
    /// It deliberately uses framework assemblies only so it works in the
    /// project's offline .NET Core 3.1 environment.
    /// </summary>
    public static class TosWordTemplateHelper
    {
        public static readonly string[] Headers = new[]
        {
            "S.NO", "Examination", "Paper Name", "Section_Name",
            "SUBSECTION_Name", "Topic_name", "difficulty_level",
            "Question_type", "question_text", "Option-1", "Option-2",
            "Option-3", "Option-4", "Question Mark", "Correct_Answer",
            "QB_Maker(Put Personal Number)"
        };

        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        public static byte[] CreateDocument(IList<string[]> rows, string title)
        {
            return CreateDocument(rows, title, Headers, true);
        }

        // Used for the duplicate-check result file: same table, extra columns, no template instructions.
        public static byte[] CreateDocument(IList<string[]> rows, string title, string[] headers, bool includeInstructions)
        {
            using (var output = new MemoryStream())
            {
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    AddTextEntry(archive, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                        "</Types>");
                    AddTextEntry(archive, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                        "</Relationships>");

                    var body = new XElement(W + "body");
                    body.Add(Paragraph(title, true, 28));
                    if (includeInstructions) body.Add(Paragraph("INSTRUCTIONS FOR POPULATION OF QUESTION BANK TEMPLATE:\n 1. Enter data only in the columns Question Text, Option 1, Option 2, Option 3, Option 4, Correct Answer and Question Bank Maker.\n 2.The columns Paper, Section, Subsection and Topics shall not be modified or altered in any manner.\n 3.In the Correct Answer column, enter only A, B, C or D, corresponding to the correct option.\n 4.In the Question Bank Maker column, enter the nominated setter's personal number without hyphens. Example: 70777A.\n 4.No columns shall be added, deleted, renamed or rearranged.\n 5.The question bank and its contents shall be treated as confidential and shall not be shared through any unauthorised medium.\n 6.The nominated officer shall populate the template personally. Delegation of the task is not permitted.\nOn forwarding the completed template and confirmation of receipt, all other copies of the document shall be permanently deleted or destroyed from all storage media and in all formats, including soft and hard copies. No copies shall be retained or shared further.\n ", false, 18));
                    body.Add(new XElement(W + "p"));

                    var table = new XElement(W + "tbl",
                        new XElement(W + "tblPr",
                            new XElement(W + "tblBorders",
                                Border("top"), Border("left"), Border("bottom"), Border("right"), Border("insideH"), Border("insideV")),
                            new XElement(W + "tblLayout", new XAttribute(W + "type", "autofit"))),
                        new XElement(W + "tblGrid", headers.Select(h => new XElement(W + "gridCol", new XAttribute(W + "w", "900")))));

                    table.Add(CreateTableRow(headers, true));
                    foreach (var row in rows)
                        table.Add(CreateTableRow(row, false));
                    body.Add(table);
                    body.Add(new XElement(W + "sectPr",
                        new XElement(W + "pgSz", new XAttribute(W + "w", "16838"), new XAttribute(W + "h", "11906"), new XAttribute(W + "orient", "landscape")),
                        new XElement(W + "pgMar", new XAttribute(W + "top", "500"), new XAttribute(W + "right", "400"), new XAttribute(W + "bottom", "500"), new XAttribute(W + "left", "400"), new XAttribute(W + "header", "250"), new XAttribute(W + "footer", "250"), new XAttribute(W + "gutter", "0"))));

                    var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
                        new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body));
                    AddTextEntry(archive, "word/document.xml", document.ToString(SaveOptions.DisableFormatting));
                }
                return output.ToArray();
            }
        }

        public static List<string[]> ReadFirstTable(Stream stream)
        {
            if (stream == null || !stream.CanRead) throw new InvalidDataException("The selected file cannot be read.");
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) throw new InvalidDataException("This is not a valid Word .docx file. Please use the template downloaded from QBMS.");
                if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("The Word document content is too large to process safely.");
                XDocument document;
                using (var input = entry.Open()) document = XDocument.Load(input, LoadOptions.None);
                var table = document.Descendants(W + "tbl").FirstOrDefault();
                if (table == null) throw new InvalidDataException("No question table was found in the Word document.");

                var result = new List<string[]>();
                foreach (var tr in table.Elements(W + "tr"))
                {
                    var cells = tr.Elements(W + "tc").Select(ReadCellText).ToArray();
                    result.Add(cells);
                }
                return result;
            }
        }

        private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

        // Same table and row order as ReadFirstTable, but every cell as HTML the QBMS pages can show:
        //  - text is HTML-encoded, paragraphs/line breaks become <br>
        //  - Word equations (OMML) become LaTeX: \( ... \) inline, \[ ... \] on their own line (MathJax)
        //  - pictures (png/jpg/gif up to 1 MB) become <img src="data:..."> at their position in the text
        public static List<string[]> ReadFirstTableHtml(Stream stream)
        {
            var result = new List<string[]>();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) return result;
                XDocument document;
                using (var input = entry.Open()) document = XDocument.Load(input, LoadOptions.None);

                var targets = new Dictionary<string, string>();
                var relsEntry = archive.GetEntry("word/_rels/document.xml.rels");
                if (relsEntry != null)
                {
                    using (var input = relsEntry.Open())
                    {
                        foreach (var rel in XDocument.Load(input).Descendants().Where(x => x.Name.LocalName == "Relationship"))
                        {
                            var id = (string)rel.Attribute("Id");
                            var target = (string)rel.Attribute("Target");
                            if (id != null && target != null) targets[id] = target;
                        }
                    }
                }

                var table = document.Descendants(W + "tbl").FirstOrDefault();
                if (table == null) return result;

                foreach (var tr in table.Elements(W + "tr"))
                {
                    result.Add(tr.Elements(W + "tc").Select(tc => CellToHtml(tc, archive, targets)).ToArray());
                }
            }
            return result;
        }

        private static string CellToHtml(XElement cell, ZipArchive archive, Dictionary<string, string> targets)
        {
            var paragraphs = new List<string>();
            foreach (var p in cell.Elements(W + "p"))
            {
                var sb = new StringBuilder();
                WalkParagraph(p, sb, archive, targets);
                paragraphs.Add(sb.ToString());
            }

            var html = string.Join("<br>", paragraphs);
            while (html.EndsWith("<br>")) html = html.Substring(0, html.Length - 4);
            return html.Trim();
        }

        private static void WalkParagraph(XElement node, StringBuilder sb, ZipArchive archive, Dictionary<string, string> targets)
        {
            foreach (var child in node.Elements())
            {
                var ns = child.Name.Namespace;
                var name = child.Name.LocalName;

                if (ns == OmmlToLatex.M && name == "oMathPara")
                {
                    foreach (var math in child.Elements(OmmlToLatex.M + "oMath"))
                    {
                        var latex = OmmlToLatex.Convert(math);
                        if (latex.Length > 0) sb.Append("\\[").Append(System.Net.WebUtility.HtmlEncode(latex)).Append("\\]");
                    }
                }
                else if (ns == OmmlToLatex.M && name == "oMath")
                {
                    var latex = OmmlToLatex.Convert(child);
                    if (latex.Length > 0) sb.Append("\\(").Append(System.Net.WebUtility.HtmlEncode(latex)).Append("\\)");
                }
                else if (ns == W && name == "t")
                {
                    sb.Append(System.Net.WebUtility.HtmlEncode(child.Value));
                }
                else if (ns == W && name == "tab")
                {
                    sb.Append(" ");
                }
                else if (ns == W && (name == "br" || name == "cr"))
                {
                    sb.Append("<br>");
                }
                else if (ns == A && name == "blip")
                {
                    sb.Append(ImageHtml(child, archive, targets));
                }
                else if (name == "AlternateContent")
                {
                    // Word stores many pictures twice (Choice + Fallback); use one.
                    var chosen = child.Elements().FirstOrDefault(x => x.Name.LocalName == "Choice")
                                 ?? child.Elements().FirstOrDefault();
                    if (chosen != null) WalkParagraph(chosen, sb, archive, targets);
                }
                else
                {
                    WalkParagraph(child, sb, archive, targets);
                }
            }
        }

        private static string ImageHtml(XElement blip, ZipArchive archive, Dictionary<string, string> targets)
        {
            var relId = (string)blip.Attribute(R + "embed");
            string target;
            if (relId == null || !targets.TryGetValue(relId, out target)) return "";

            var path = target.StartsWith("/") ? target.TrimStart('/') : "word/" + target.Replace("../", "");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            string mime = ext == ".png" ? "image/png" : (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : ext == ".gif" ? "image/gif" : null;
            if (mime == null) return "";

            var media = archive.GetEntry(path);
            if (media == null || media.Length > 1024 * 1024) return "";

            string base64;
            using (var ms = new MemoryStream())
            {
                using (var s = media.Open()) s.CopyTo(ms);
                base64 = Convert.ToBase64String(ms.ToArray());
            }

            // Keep the size the picture has in the Word document (EMU -> px), within sensible limits.
            int width = 0;
            var holder = blip.Ancestors().FirstOrDefault(x => x.Name.LocalName == "inline" || x.Name.LocalName == "anchor");
            var extent = holder == null ? null : holder.Elements().FirstOrDefault(x => x.Name.LocalName == "extent");
            long cx;
            if (extent != null && long.TryParse((string)extent.Attribute("cx"), out cx)) width = (int)Math.Max(20, Math.Min(600, cx / 9525));

            return "<img src=\"data:" + mime + ";base64," + base64 + "\" style=\"" + (width > 0 ? "width:" + width + "px;" : "") + "max-width:100%;height:auto;\">";
        }

        // Result file for a Word upload: the ORIGINAL document (so pictures, equations and layout stay exactly
        // as the Setter made them) with extra columns added to the question table. extraRows[k] belongs to the
        // k-th non-empty data row - the same rows ReadFirstTable returns after the header.
        public static byte[] AppendColumns(byte[] original, string[] extraHeaders, IList<string[]> extraRows)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(original, 0, original.Length);
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry == null) throw new InvalidDataException("The stored document is not a valid Word file.");
                    XDocument document;
                    using (var input = entry.Open()) document = XDocument.Load(input, LoadOptions.None);

                    var table = document.Descendants(W + "tbl").FirstOrDefault();
                    if (table == null) throw new InvalidDataException("No question table was found in the stored document.");

                    var grid = table.Element(W + "tblGrid");
                    if (grid != null)
                        foreach (var h in extraHeaders) grid.Add(new XElement(W + "gridCol", new XAttribute(W + "w", "3000")));

                    var rows = table.Elements(W + "tr").ToList();
                    if (rows.Count > 0)
                        foreach (var h in extraHeaders) rows[0].Add(ResultCell(h, true));

                    int k = 0;
                    for (int r = 1; r < rows.Count; r++)
                    {
                        var texts = rows[r].Elements(W + "tc").Select(ReadCellText).ToList();
                        if (!texts.Any(c => !string.IsNullOrWhiteSpace(c))) continue;

                        for (int c = 0; c < extraHeaders.Length; c++)
                        {
                            var value = k < extraRows.Count && c < extraRows[k].Length ? extraRows[k][c] : "";
                            rows[r].Add(ResultCell(value, false));
                        }
                        k++;
                    }

                    entry.Delete();
                    var replaced = archive.CreateEntry("word/document.xml", CompressionLevel.Optimal);
                    using (var output = replaced.Open()) document.Save(output, SaveOptions.DisableFormatting);
                }
                return ms.ToArray();
            }
        }

        private static XElement ResultCell(string text, bool header)
        {
            var tc = new XElement(W + "tc",
                new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", "3000"), new XAttribute(W + "type", "dxa"))));
            foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var run = new XElement(W + "r");
                if (header) run.Add(new XElement(W + "rPr", new XElement(W + "b")));
                run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), line));
                tc.Add(new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", "0"))), run));
            }
            return tc;
        }

        private static string ReadCellText(XElement cell)
        {
            var paragraphs = cell.Descendants(W + "p").Select(p =>
                string.Concat(p.Descendants().Where(e => e.Name == W + "t" || e.Name == M + "t").Select(t => (string)t)));
            return string.Join("\n", paragraphs).Trim();
        }

        private static XElement CreateTableRow(IEnumerable<string> values, bool header)
        {
            var tr = new XElement(W + "tr");
            foreach (var value in values)
            {
                var runProperties = header ? new XElement(W + "rPr", new XElement(W + "b")) : null;
                var run = new XElement(W + "r");
                if (runProperties != null) run.Add(runProperties);
                run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? string.Empty));
                var paragraph = new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", "0"))), run);
                tr.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", "900"), new XAttribute(W + "type", "dxa"))), paragraph));
            }
            return tr;
        }

        private static XElement Paragraph(string text, bool bold, int fontSize)
        {
            var run = new XElement(W + "r",
                bold ? new XElement(W + "rPr", new XElement(W + "b"), new XElement(W + "sz", new XAttribute(W + "val", fontSize))) : new XElement(W + "rPr", new XElement(W + "sz", new XAttribute(W + "val", fontSize))),
                new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text ?? string.Empty));
            return new XElement(W + "p", run);
        }

        private static XElement Border(string edge)
        {
            return new XElement(W + edge, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", "4"), new XAttribute(W + "space", "0"), new XAttribute(W + "color", "808080"));
        }

        private static void AddTextEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content);
        }
    }
}
