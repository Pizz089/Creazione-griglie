using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Creazione_griglie
{
    public static class EmbeddedResourceManager
    {
        private const string BaseStylesMarker = "BaseStyles.";

        // Ottengo il percorso della cartella Temp
        public static string GetTempStylesPath()
        {
            return Path.Combine(Path.GetTempPath(), "CreazioneGriglie_Resources", "BaseStyles");
        }

        public static void EstraiRisorseSeNecessario()
        {
            try
            {
                string targetFolder = GetTempStylesPath();

                // Se esistono già cartelle con il nome corretto (style#N), l'estrazione è già stata fatta
                if (Directory.Exists(targetFolder) &&
                    Directory.GetDirectories(targetFolder, "style#*").Length > 0)
                    return;

                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();
                int estratti = 0;

                // NOTA: MSBuild converte '#' in '_' nei nomi delle risorse embedded.
                // La cartella "style#1" sul disco diventa "style_1" nel nome risorsa.
                // Estraiamo con il nome "style_1" e poi rinominiamo in fondo.
                //
                // Le risorse sono nominate: Namespace.BaseStyles.style_1.candle.Base.png
                foreach (string resourceName in resourceNames)
                {
                    int markerIdx = resourceName.IndexOf(BaseStylesMarker, StringComparison.Ordinal);
                    if (markerIdx < 0) continue;

                    // Il marcatore deve essere preceduto da un punto o trovarsi all'inizio
                    if (markerIdx > 0 && resourceName[markerIdx - 1] != '.') continue;

                    // Parte dopo "BaseStyles." → es. "style_1.candle.Base.png"
                    string afterMarker = resourceName.Substring(markerIdx + BaseStylesMarker.Length);
                    if (string.IsNullOrEmpty(afterMarker)) continue;

                    int lastDot = afterMarker.LastIndexOf('.');
                    if (lastDot < 0) continue;

                    string extension = afterMarker.Substring(lastDot);     // ".png"
                    string pathPart  = afterMarker.Substring(0, lastDot);  // "style_1.candle.Base"

                    // Ricostruisco il percorso fisico (con underscore, come nel nome risorsa)
                    string relPath = pathPart.Replace('.', Path.DirectorySeparatorChar) + extension;
                    // = "style_1\candle\Base.png"

                    string finalFilePath = Path.Combine(targetFolder, relPath);
                    string directoryPath = Path.GetDirectoryName(finalFilePath);

                    if (!Directory.Exists(directoryPath))
                        Directory.CreateDirectory(directoryPath);

                    using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (resourceStream == null) continue;
                        using (FileStream fileStream = new FileStream(finalFilePath, FileMode.Create, FileAccess.Write))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }

                    estratti++;
                }

                if (estratti == 0)
                {
                    MessageBox.Show(
                        "Errore: nessuna risorsa BaseStyles trovata nell'eseguibile.\n\n" +
                        "Controlla che i file in BaseStyles\\ abbiano 'Build Action = Embedded Resource'.",
                        "Risorse Mancanti", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // MSBuild ha convertito '#' in '_': rinomino style_N → style#N
                foreach (string dir in Directory.GetDirectories(targetFolder, "style_*"))
                {
                    string baseName = Path.GetFileName(dir);
                    string suffix = baseName.Substring("style_".Length);
                    if (suffix.Length > 0 && suffix.All(char.IsDigit))
                    {
                        string correctedPath = Path.Combine(targetFolder, "style#" + suffix);
                        if (!Directory.Exists(correctedPath))
                            Directory.Move(dir, correctedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore estrazione risorse: " + ex.Message, "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}