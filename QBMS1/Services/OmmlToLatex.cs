using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace QBMS1.Services
{
    /// <summary>
    /// Converts equations made with the Word equation editor (Office Math, OMML) to LaTeX,
    /// which the QBMS question pages render with MathJax (\( ... \) and \[ ... \]).
    /// Covers fractions, powers, indices, roots, sums/integrals, brackets, functions, limits,
    /// accents, bars, matrices and equation arrays. Unknown structures keep their text.
    /// </summary>
    public static class OmmlToLatex
    {
        public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        private static readonly Dictionary<string, string> NaryMap = new Dictionary<string, string>
        {
            { "\u2211", "\\sum" }, { "\u220F", "\\prod" }, { "\u2210", "\\coprod" },
            { "\u222B", "\\int" }, { "\u222C", "\\iint" }, { "\u222D", "\\iiint" }, { "\u222E", "\\oint" },
            { "\u22C3", "\\bigcup" }, { "\u22C2", "\\bigcap" }, { "\u22C1", "\\bigvee" }, { "\u22C0", "\\bigwedge" }
        };

        private static readonly Dictionary<string, string> AccentMap = new Dictionary<string, string>
        {
            { "\u0302", "\\hat" }, { "\u02C6", "\\hat" }, { "\u030C", "\\check" }, { "\u02C7", "\\check" },
            { "\u0303", "\\tilde" }, { "\u02DC", "\\tilde" }, { "\u0304", "\\bar" }, { "\u00AF", "\\bar" },
            { "\u20D7", "\\vec" }, { "\u2192", "\\vec" }, { "\u0307", "\\dot" }, { "\u02D9", "\\dot" },
            { "\u0308", "\\ddot" }, { "\u00A8", "\\ddot" }
        };

        private static readonly HashSet<string> KnownFunctions = new HashSet<string>
        {
            "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh",
            "log", "ln", "lg", "exp", "lim", "max", "min", "det", "gcd", "sup", "inf", "arg", "deg", "dim", "ker", "Pr"
        };

        // Converts one <m:oMath> element to a LaTeX formula (without delimiters).
        public static string Convert(XElement oMath)
        {
            return Children(oMath).Trim();
        }

        private static string Children(XElement element)
        {
            var sb = new StringBuilder();
            foreach (var child in element.Elements()) sb.Append(Node(child));
            return sb.ToString();
        }

        private static string Arg(XElement parent, string name)
        {
            var e = parent.Element(M + name);
            return e == null ? "" : Children(e).Trim();
        }

        private static string Group(string latex)
        {
            return "{" + latex + "}";
        }

        private static string Node(XElement e)
        {
            switch (e.Name.LocalName)
            {
                case "r": return Run(e);
                case "f": return Fraction(e);
                case "sSup": return Group(Arg(e, "e")) + "^" + Group(Arg(e, "sup"));
                case "sSub": return Group(Arg(e, "e")) + "_" + Group(Arg(e, "sub"));
                case "sSubSup": return Group(Arg(e, "e")) + "_" + Group(Arg(e, "sub")) + "^" + Group(Arg(e, "sup"));
                case "sPre": return "{}_" + Group(Arg(e, "sub")) + "^" + Group(Arg(e, "sup")) + Group(Arg(e, "e"));
                case "rad": return Radical(e);
                case "nary": return Nary(e);
                case "d": return Delimiter(e);
                case "func": return Function(e);
                case "limLow": return LimLow(e);
                case "limUpp": return "\\overset" + Group(Arg(e, "lim")) + Group(Arg(e, "e"));
                case "acc": return Accent(e);
                case "bar": return Bar(e);
                case "groupChr": return Group(Arg(e, "e"));
                case "box": case "borderBox": case "phant": return Group(Arg(e, "e"));
                case "eqArr": return EqArray(e);
                case "m": return Matrix(e);
                case "e": case "num": case "den": case "sub": case "sup": case "lim": case "deg": case "fName":
                    return Children(e);
                case "oMath": return Children(e);
                default: return Children(e); // properties (…Pr) have no text children worth keeping
            }
        }

        private static string Run(XElement r)
        {
            var sb = new StringBuilder();
            foreach (var t in r.Elements(M + "t")) sb.Append(EscapeText(t.Value));
            if (sb.Length == 0) return "";

            var rPr = r.Element(M + "rPr");
            var sty = rPr == null ? null : (string)rPr.Element(M + "sty")?.Attribute(M + "val");
            var literal = rPr != null && rPr.Element(M + "lit") != null;
            var text = sb.ToString();

            if (literal || sty == "p") return "\\mathrm{" + text + "}";
            if (sty == "b") return "\\mathbf{" + text + "}";
            if (sty == "bi") return "\\boldsymbol{" + text + "}";
            return text;
        }

        private static string EscapeText(string text)
        {
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '%': sb.Append("\\%"); break;
                    case '#': sb.Append("\\#"); break;
                    case '&': sb.Append("\\&"); break;
                    case '$': sb.Append("\\$"); break;
                    case '_': sb.Append("\\_"); break;
                    case '{': sb.Append("\\{"); break;
                    case '}': sb.Append("\\}"); break;
                    case '\\': sb.Append("\\backslash "); break;
                    case '\u00D7': sb.Append("\\times "); break;
                    case '\u00F7': sb.Append("\\div "); break;
                    case '\u00B1': sb.Append("\\pm "); break;
                    case '\u2264': sb.Append("\\le "); break;
                    case '\u2265': sb.Append("\\ge "); break;
                    case '\u2260': sb.Append("\\ne "); break;
                    case '\u221E': sb.Append("\\infty "); break;
                    case '\u2192': sb.Append("\\to "); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        private static string Fraction(XElement f)
        {
            var type = (string)f.Element(M + "fPr")?.Element(M + "type")?.Attribute(M + "val");
            var num = Arg(f, "num");
            var den = Arg(f, "den");
            if (type == "noBar") return "\\binom" + Group(num) + Group(den);
            if (type == "lin") return Group(num) + "/" + Group(den);
            return "\\frac" + Group(num) + Group(den);
        }

        private static string Radical(XElement rad)
        {
            var hideDeg = (string)rad.Element(M + "radPr")?.Element(M + "degHide")?.Attribute(M + "val");
            var deg = Arg(rad, "deg");
            var body = Arg(rad, "e");
            if (deg.Length == 0 || hideDeg == "1" || hideDeg == "on" || hideDeg == "true")
                return "\\sqrt" + Group(body);
            return "\\sqrt[" + deg + "]" + Group(body);
        }

        private static string Nary(XElement n)
        {
            var pr = n.Element(M + "naryPr");
            var chr = (string)pr?.Element(M + "chr")?.Attribute(M + "val");
            var subHide = IsOn((string)pr?.Element(M + "subHide")?.Attribute(M + "val"));
            var supHide = IsOn((string)pr?.Element(M + "supHide")?.Attribute(M + "val"));

            string op;
            if (string.IsNullOrEmpty(chr)) op = "\\int";
            else if (!NaryMap.TryGetValue(chr, out op)) op = chr;

            var sb = new StringBuilder(op);
            var sub = Arg(n, "sub");
            var sup = Arg(n, "sup");
            if (!subHide && sub.Length > 0) sb.Append("_").Append(Group(sub));
            if (!supHide && sup.Length > 0) sb.Append("^").Append(Group(sup));
            sb.Append(" ").Append(Group(Arg(n, "e")));
            return sb.ToString();
        }

        private static bool IsOn(string value)
        {
            return value == "1" || value == "on" || value == "true";
        }

        private static string Bracket(string ch, bool left)
        {
            if (ch == null) return left ? "(" : ")";
            switch (ch)
            {
                case "": return ".";
                case "{": return "\\{";
                case "}": return "\\}";
                case "\u27E8": case "\u2329": return "\\langle ";
                case "\u27E9": case "\u232A": return "\\rangle ";
                case "\u2016": return "\\|";
                case "\u230A": return "\\lfloor ";
                case "\u230B": return "\\rfloor ";
                case "\u2308": return "\\lceil ";
                case "\u2309": return "\\rceil ";
                default: return ch;
            }
        }

        private static string Delimiter(XElement d)
        {
            var pr = d.Element(M + "dPr");
            var beg = (string)pr?.Element(M + "begChr")?.Attribute(M + "val");
            var end = (string)pr?.Element(M + "endChr")?.Attribute(M + "val");
            var sep = (string)pr?.Element(M + "sepChr")?.Attribute(M + "val") ?? "|";
            var parts = d.Elements(M + "e").Select(x => Children(x).Trim()).ToList();
            return "\\left" + Bracket(beg, true) + " " + string.Join(" " + sep + " ", parts) + " \\right" + Bracket(end, false);
        }

        private static string Function(XElement f)
        {
            var name = Arg(f, "fName");
            var body = Arg(f, "e");
            var plain = name.Replace("\\mathrm{", "").Replace("}", "").Trim();
            if (KnownFunctions.Contains(plain)) return "\\" + plain + " " + body;
            return name + " " + body;
        }

        private static string LimLow(XElement e)
        {
            var body = Arg(e, "e");
            var lim = Arg(e, "lim");
            var plain = body.Replace("\\mathrm{", "").Replace("}", "").Trim();
            if (plain == "lim" || plain == "max" || plain == "min" || plain == "sup" || plain == "inf")
                return "\\" + plain + "_" + Group(lim);
            return "\\underset" + Group(lim) + Group(body);
        }

        private static string Accent(XElement a)
        {
            var chr = (string)a.Element(M + "accPr")?.Element(M + "chr")?.Attribute(M + "val");
            string command;
            if (string.IsNullOrEmpty(chr) || !AccentMap.TryGetValue(chr, out command)) command = "\\hat";
            return command + Group(Arg(a, "e"));
        }

        private static string Bar(XElement b)
        {
            var pos = (string)b.Element(M + "barPr")?.Element(M + "pos")?.Attribute(M + "val");
            return (pos == "bot" ? "\\underline" : "\\overline") + Group(Arg(b, "e"));
        }

        private static string EqArray(XElement a)
        {
            var rows = a.Elements(M + "e").Select(x => Children(x).Trim());
            return "\\begin{aligned} " + string.Join(" \\\\ ", rows) + " \\end{aligned}";
        }

        private static string Matrix(XElement m)
        {
            var rows = m.Elements(M + "mr").Select(r =>
                string.Join(" & ", r.Elements(M + "e").Select(c => Children(c).Trim())));
            return "\\begin{matrix} " + string.Join(" \\\\ ", rows) + " \\end{matrix}";
        }
    }
}
