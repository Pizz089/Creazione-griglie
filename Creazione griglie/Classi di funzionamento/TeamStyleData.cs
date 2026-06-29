using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;

namespace Creazione_griglie
{
    // Rappresenta le variabili di un file TeamStyle.xml (layout griglia punteggi + materiali).
    // I valori numerici (singoli e componenti X/Y/Z) sono in Scalars, le 8 tinte materiale in Colors,
    // entrambi indicizzati con la stessa chiave usata come Tag dei controlli UI (es. "posNome.X").
    public class TeamStyleData
    {
        public Dictionary<string, double> Scalars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Color> Colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        public TeamStyleData Clone()
        {
            return new TeamStyleData
            {
                Scalars = new Dictionary<string, double>(Scalars, StringComparer.OrdinalIgnoreCase),
                Colors = new Dictionary<string, Color>(Colors, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    public static class TeamStyleHelper
    {
        // Valori a nodo singolo nella radice
        public static readonly string[] RootScalars =
            { "vertSpacing", "actStripYRatio", "rulFrmDist1_2", "rulFrm1Offs", "maxSizeNome" };

        // Nodi vettoriali nella radice (ognuno con X/Y/Z)
        public static readonly string[] Vectors =
            { "scalNome", "posNome", "scalHdp", "posHdp", "scalTeamTot", "posTeamTot", "scalCumTeamTot", "posCumTeamTot" };

        // Nodi materiale (ognuno con le 4 tinte + glossiness)
        public static readonly string[] Materials = { "nameMaterial", "teamTotsMaterial" };
        public static readonly string[] MatColors = { "ambientCol", "diffuseCol", "emissiveCol", "specularCol" };

        // Restituisce tutte le chiavi numeriche conosciute (scalari + componenti vettore + glossiness)
        public static IEnumerable<string> AllScalarKeys()
        {
            foreach (var s in RootScalars) yield return s;
            foreach (var v in Vectors) { yield return v + ".X"; yield return v + ".Y"; yield return v + ".Z"; }
            foreach (var m in Materials) yield return m + ".glossiness";
        }

        // Tutte le chiavi colore conosciute (es. "nameMaterial.diffuseCol")
        public static IEnumerable<string> AllColorKeys()
        {
            foreach (var m in Materials)
                foreach (var c in MatColors)
                    yield return m + "." + c;
        }

        public static TeamStyleData Default()
        {
            var d = new TeamStyleData();

            d.Scalars["vertSpacing"] = 10;
            d.Scalars["actStripYRatio"] = 1.0;
            d.Scalars["rulFrmDist1_2"] = 90;
            d.Scalars["rulFrm1Offs"] = -450;
            d.Scalars["maxSizeNome"] = 290;

            SetVec(d, "scalNome", 36, 22, 10); SetVec(d, "posNome", -275, 1, -3);
            SetVec(d, "scalHdp", 36, 22, 10); SetVec(d, "posHdp", 5, 1, -3);
            SetVec(d, "scalTeamTot", 36, 22, 10); SetVec(d, "posTeamTot", 190, 1, -3);
            SetVec(d, "scalCumTeamTot", 36, 22, 10); SetVec(d, "posCumTeamTot", 365, 1, -3);

            foreach (var m in Materials)
            {
                d.Colors[m + ".ambientCol"] = Color.FromArgb(255, 0, 0, 0);
                d.Colors[m + ".diffuseCol"] = Color.FromArgb(255, 0, 0, 0);
                d.Colors[m + ".emissiveCol"] = Color.FromArgb(255, 255, 255, 255);
                d.Colors[m + ".specularCol"] = Color.FromArgb(255, 0, 0, 0);
                d.Scalars[m + ".glossiness"] = 20;
            }
            return d;
        }

        private static void SetVec(TeamStyleData d, string name, double x, double y, double z)
        {
            d.Scalars[name + ".X"] = x; d.Scalars[name + ".Y"] = y; d.Scalars[name + ".Z"] = z;
        }

        public static TeamStyleData Load(string path)
        {
            var d = Default();
            try
            {
                XElement root = XDocument.Load(path).Root;
                if (root == null) return d;

                foreach (var s in RootScalars)
                    ReadInto(d.Scalars, s, root.Element(s));

                foreach (var v in Vectors)
                {
                    XElement el = root.Element(v);
                    if (el == null) continue;
                    ReadInto(d.Scalars, v + ".X", el.Element("X"));
                    ReadInto(d.Scalars, v + ".Y", el.Element("Y"));
                    ReadInto(d.Scalars, v + ".Z", el.Element("Z"));
                }

                foreach (var m in Materials)
                {
                    XElement me = root.Element(m);
                    if (me == null) continue;
                    foreach (var c in MatColors)
                    {
                        XElement ce = me.Element(c);
                        if (ce != null) d.Colors[m + "." + c] = LeggiColore01(ce, d.Colors[m + "." + c]);
                    }
                    ReadInto(d.Scalars, m + ".glossiness", me.Element("glossiness"));
                }
            }
            catch { }
            return d;
        }

        // Salvataggio non distruttivo: aggiorna solo i nodi conosciuti, preserva il resto del file.
        public static void Save(string path, TeamStyleData d)
        {
            if (!File.Exists(path)) return;
            try
            {
                XDocument doc = XDocument.Load(path);
                XElement root = doc.Root;
                if (root == null) return;

                foreach (var s in RootScalars)
                    UpsertText(root, s, Fmt(d.Scalars[s]));

                foreach (var v in Vectors)
                {
                    XElement el = Ensure(root, v);
                    UpsertText(el, "X", Fmt(d.Scalars[v + ".X"]));
                    UpsertText(el, "Y", Fmt(d.Scalars[v + ".Y"]));
                    UpsertText(el, "Z", Fmt(d.Scalars[v + ".Z"]));
                }

                foreach (var m in Materials)
                {
                    XElement me = Ensure(root, m);
                    foreach (var c in MatColors)
                        ScriviColore01(Ensure(me, c), d.Colors[m + "." + c]);
                    UpsertText(me, "glossiness", Fmt(d.Scalars[m + ".glossiness"]));
                }

                doc.Save(path);
            }
            catch { }
        }

        private static void ReadInto(Dictionary<string, double> dict, string key, XElement el)
        {
            if (el == null) return;
            if (TryParse(el.Value, out double v)) dict[key] = v;
        }

        private static Color LeggiColore01(XElement nodo, Color fallback)
        {
            double a = ReadChannel(nodo, "Alpha", fallback.A / 255.0);
            double r = ReadChannel(nodo, "Red", fallback.R / 255.0);
            double g = ReadChannel(nodo, "Green", fallback.G / 255.0);
            double b = ReadChannel(nodo, "Blue", fallback.B / 255.0);
            return Color.FromArgb(To255(a), To255(r), To255(g), To255(b));
        }

        private static double ReadChannel(XElement nodo, string nome, double fallback)
        {
            XElement el = nodo.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(nome, StringComparison.OrdinalIgnoreCase));
            if (el != null && TryParse(el.Value, out double v)) return v;
            return fallback;
        }

        private static void ScriviColore01(XElement nodo, Color c)
        {
            UpsertText(nodo, "Alpha", Fmt(c.A / 255.0));
            UpsertText(nodo, "Red", Fmt(c.R / 255.0));
            UpsertText(nodo, "Green", Fmt(c.G / 255.0));
            UpsertText(nodo, "Blue", Fmt(c.B / 255.0));
        }

        private static byte To255(double v)
        {
            // I canali sono normalmente 0..1; tollera anche 0..255 nel caso di file anomali.
            double scaled = v <= 1.0 ? v * 255.0 : v;
            return (byte)Math.Max(0, Math.Min(255, Math.Round(scaled)));
        }

        private static XElement Ensure(XElement parent, string name)
        {
            XElement el = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (el == null) { el = new XElement(name); parent.Add(el); }
            return el;
        }

        private static void UpsertText(XElement parent, string name, string value)
        {
            XElement el = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (el != null) el.Value = value;
            else parent.Add(new XElement(name, value));
        }

        private static bool TryParse(string s, out double v)
        {
            return double.TryParse((s ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out v);
        }

        private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
