using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Creazione_griglie
{
    public class LightSettings
    {
        public bool Light1Enabled = true;
        public double Dir1X = 0.8, Dir1Y = -0.8, Dir1Z = 0.5;
        public Color Diffuse1 = Colors.White;

        public bool Light2Enabled = true;
        public double Dir2X = 0.5, Dir2Y = 1.0, Dir2Z = 1.0;
        public Color Diffuse2 = Colors.White;

        public Color Ambient = Color.FromRgb(40, 40, 40);
    }

    public static class XmlHelper
    {
        // Analizzo il file Lights.xml e aggiungo le luci direttamente al gruppo 3D
        public static void ConfiguraLuciDaXML(string[] fileXml, Model3DGroup luciGroup)
        {
            luciGroup.Children.Clear();
            bool luciTrovate = false;
            Color totalAmbient = Colors.Black;

            foreach (string file in fileXml)
            {
                if (Path.GetFileNameWithoutExtension(file).ToLower().Contains("light"))
                {
                    try
                    {
                        XDocument doc = XDocument.Load(file);

                        foreach (XElement el in doc.Root.Elements())
                        {
                            string nomeTag = el.Name.LocalName.ToLower();

                            if (nomeTag.Contains("light"))
                            {
                                bool isEnabled = el.Element("enabled")?.Value?.ToLower() != "false";
                                if (!isEnabled) continue;

                                XElement diffuseEl = el.Element("diffuseLight") ?? el.Element("diffuselight");
                                XElement dirEl = el.Element("direction");

                                if (diffuseEl != null && dirEl != null)
                                {
                                    Color c = EstraiColoreRGB(diffuseEl, Colors.White);
                                    double dX = 0, dY = -1, dZ = -1;
                                    EstraiVettore(dirEl, ref dX, ref dY, ref dZ);

                                    luciGroup.Children.Add(new DirectionalLight(c, new Vector3D(dX, dY, -dZ)));
                                    luciTrovate = true;
                                }

                                XElement ambientEl = el.Element("ambientLight") ?? el.Element("ambientlight");
                                if (ambientEl != null)
                                {
                                    Color c = EstraiColoreRGB(ambientEl, Colors.Black);
                                    totalAmbient = SommaColori(totalAmbient, c);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            if (luciTrovate)
            {
                if (totalAmbient.R < 20 && totalAmbient.G < 20 && totalAmbient.B < 20)
                {
                    totalAmbient = Color.FromRgb(40, 40, 40);
                }
                luciGroup.Children.Add(new AmbientLight(totalAmbient));
            }
            else
            {
                luciGroup.Children.Add(new AmbientLight(Color.FromRgb(100, 100, 100)));
                luciGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
                luciGroup.Children.Add(new DirectionalLight(Color.FromRgb(50, 50, 50), new Vector3D(1, 1, 1)));
            }
        }

        private static Color SommaColori(Color c1, Color c2)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, c1.R + c2.R),
                (byte)Math.Min(255, c1.G + c2.G),
                (byte)Math.Min(255, c1.B + c2.B));
        }

        private static Color EstraiColoreRGB(XElement nodo, Color fallback)
        {
            if (nodo == null) return fallback;

            double r = -1, g = -1, b = -1;

            var redEl = nodo.Element("Red") ?? nodo.Element("red") ?? nodo.Element("R") ?? nodo.Element("r");
            var greenEl = nodo.Element("Green") ?? nodo.Element("green") ?? nodo.Element("G") ?? nodo.Element("g");
            var blueEl = nodo.Element("Blue") ?? nodo.Element("blue") ?? nodo.Element("B") ?? nodo.Element("b");

            if (redEl != null) r = ParseDouble(redEl.Value, -1);
            if (greenEl != null) g = ParseDouble(greenEl.Value, -1);
            if (blueEl != null) b = ParseDouble(blueEl.Value, -1);

            if (r >= 0 && g >= 0 && b >= 0)
            {
                if (r <= 1.0 && g <= 1.0 && b <= 1.0)
                    return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
                else
                    return Color.FromRgb((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b));
            }

            double x = 0, y = 0, z = 0;
            if (EstraiVettore(nodo, ref x, ref y, ref z))
            {
                if (x <= 1.0 && y <= 1.0 && z <= 1.0)
                    return Color.FromRgb((byte)(x * 255), (byte)(y * 255), (byte)(z * 255));
                else
                    return Color.FromRgb((byte)Math.Min(255, x), (byte)Math.Min(255, y), (byte)Math.Min(255, z));
            }

            return fallback;
        }

        // Leggo le impostazioni di direzione e colore delle luci dal file Lights.xml
        public static LightSettings LeggiImpostazioniLuci(string[] fileXml)
        {
            var result = new LightSettings();
            int lightIndex = 0;

            foreach (string file in fileXml)
            {
                if (!Path.GetFileNameWithoutExtension(file).ToLower().Contains("light")) continue;
                try
                {
                    XDocument doc = XDocument.Load(file);
                    foreach (XElement el in doc.Root.Elements())
                    {
                        if (!el.Name.LocalName.ToLower().Contains("light")) continue;

                        bool isEnabled = el.Element("enabled")?.Value?.ToLower() != "false";
                        XElement diffuseEl = el.Element("diffuseLight") ?? el.Element("diffuselight");
                        XElement dirEl = el.Element("direction");

                        if (lightIndex == 0)
                        {
                            result.Light1Enabled = isEnabled;
                            if (dirEl != null)
                            {
                                double dX = result.Dir1X, dY = result.Dir1Y, dZ = result.Dir1Z;
                                EstraiVettore(dirEl, ref dX, ref dY, ref dZ);
                                result.Dir1X = dX; result.Dir1Y = dY; result.Dir1Z = dZ;
                            }
                            if (diffuseEl != null) result.Diffuse1 = EstraiColoreRGB(diffuseEl, Colors.White);
                        }
                        else if (lightIndex == 1)
                        {
                            result.Light2Enabled = isEnabled;
                            if (dirEl != null)
                            {
                                double dX = result.Dir2X, dY = result.Dir2Y, dZ = result.Dir2Z;
                                EstraiVettore(dirEl, ref dX, ref dY, ref dZ);
                                result.Dir2X = dX; result.Dir2Y = dY; result.Dir2Z = dZ;
                            }
                            if (diffuseEl != null) result.Diffuse2 = EstraiColoreRGB(diffuseEl, Colors.White);
                        }
                        lightIndex++;
                    }
                }
                catch { }
            }
            return result;
        }

        // Ricostruisce il gruppo luci WPF dalle impostazioni correnti
        public static void RicostruisciLuci(LightSettings settings, Model3DGroup luciGroup)
        {
            luciGroup.Children.Clear();
            bool luciAggiunte = false;

            if (settings.Light1Enabled)
            {
                luciGroup.Children.Add(new DirectionalLight(settings.Diffuse1,
                    new Vector3D(settings.Dir1X, settings.Dir1Y, -settings.Dir1Z)));
                luciAggiunte = true;
            }
            if (settings.Light2Enabled)
            {
                luciGroup.Children.Add(new DirectionalLight(settings.Diffuse2,
                    new Vector3D(settings.Dir2X, settings.Dir2Y, -settings.Dir2Z)));
                luciAggiunte = true;
            }

            Color ambient = settings.Ambient;
            if (ambient.R < 20 && ambient.G < 20 && ambient.B < 20)
                ambient = Color.FromRgb(40, 40, 40);
            luciGroup.Children.Add(new AmbientLight(ambient));

            if (!luciAggiunte)
                luciGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
        }

        // Salva le direzioni delle luci nel file Lights.xml senza toccare altri valori
        public static void SalvaLuciInXML(LightSettings settings, string pathXml)
        {
            if (!File.Exists(pathXml)) return;
            try
            {
                XDocument doc = XDocument.Load(pathXml);
                int lightIndex = 0;
                foreach (XElement el in doc.Root.Elements())
                {
                    if (!el.Name.LocalName.ToLower().Contains("light")) continue;
                    XElement dirEl = el.Element("direction");
                    if (dirEl == null) continue;

                    double dx, dy, dz;
                    Color dc;
                    if (lightIndex == 0) { dx = settings.Dir1X; dy = settings.Dir1Y; dz = settings.Dir1Z; dc = settings.Diffuse1; }
                    else if (lightIndex == 1) { dx = settings.Dir2X; dy = settings.Dir2Y; dz = settings.Dir2Z; dc = settings.Diffuse2; }
                    else break;

                    AggiornaNodoXml(dirEl, "X", dx.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    AggiornaNodoXml(dirEl, "Y", dy.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    AggiornaNodoXml(dirEl, "Z", dz.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

                    // Salvo anche il colore diffuse
                    XElement diffuseEl = el.Element("diffuseLight") ?? el.Element("diffuselight");
                    if (diffuseEl == null) { diffuseEl = new XElement("diffuseLight"); el.AddFirst(diffuseEl); }
                    string r = (dc.R / 255.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    string g = (dc.G / 255.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    string b = (dc.B / 255.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    AggiornaNodoXml(diffuseEl, "Red", r);
                    AggiornaNodoXml(diffuseEl, "Green", g);
                    AggiornaNodoXml(diffuseEl, "Blue", b);

                    lightIndex++;
                }
                doc.Save(pathXml);
            }
            catch { }
        }

        private static void AggiornaNodoXml(XElement parent, string nome, string valore)
        {
            var el = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(nome, StringComparison.OrdinalIgnoreCase));
            if (el != null) el.Value = valore;
            else parent.Add(new XElement(nome, valore));
        }

        // Setaccio gli XML in cerca delle coordinate del pezzo
        public static void LeggiCoordinateDaXML(string[] fileXml, string nomeFileX, ref double pX, ref double pY, ref double pZ, ref double rX, ref double rY, ref double rZ, ref double scala)
        {
            string nomeBase = Path.GetFileNameWithoutExtension(nomeFileX).ToLower();

            foreach (string file in fileXml)
            {
                try
                {
                    XDocument doc = XDocument.Load(file);
                    foreach (XElement el in doc.Descendants())
                    {
                        string nomeTag = el.Name.LocalName.ToLower();

                        if (nomeTag.Contains(nomeBase))
                        {
                            if (nomeTag.Contains("pos")) EstraiVettore(el, ref pX, ref pY, ref pZ);
                            else if (nomeTag.Contains("rot")) EstraiVettore(el, ref rX, ref rY, ref rZ);
                            else if (nomeTag.Contains("scal"))
                            {
                                double sX = scala, sY = scala, sZ = scala;
                                if (EstraiVettore(el, ref sX, ref sY, ref sZ)) scala = sX;
                                else scala = ParseDouble(el.Value, scala);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static bool EstraiVettore(XElement el, ref double x, ref double y, ref double z)
        {
            if (el.Attribute("X") != null || el.Attribute("x") != null)
            {
                x = ParseDouble(el.Attribute("X")?.Value ?? el.Attribute("x")?.Value, x);
                y = ParseDouble(el.Attribute("Y")?.Value ?? el.Attribute("y")?.Value, y);
                z = ParseDouble(el.Attribute("Z")?.Value ?? el.Attribute("z")?.Value, z);
                return true;
            }

            var elX = el.Element("X") ?? el.Element("x");
            if (elX != null)
            {
                x = ParseDouble(elX.Value, x);
                y = ParseDouble((el.Element("Y") ?? el.Element("y"))?.Value, y);
                z = ParseDouble((el.Element("Z") ?? el.Element("z"))?.Value, z);
                return true;
            }

            string[] parti = el.Value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parti.Length >= 3)
            {
                x = ParseDouble(parti[0], x);
                y = ParseDouble(parti[1], y);
                z = ParseDouble(parti[2], z);
                return true;
            }

            return false;
        }

        private static double ParseDouble(string val, double fallback)
        {
            if (string.IsNullOrWhiteSpace(val)) return fallback;
            if (double.TryParse(val.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;
            return fallback;
        }
    }
}