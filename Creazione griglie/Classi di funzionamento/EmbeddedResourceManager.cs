using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Creazione_griglie
{
    public static class EmbeddedResourceManager
    {
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
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();

                // Cerco la posizione della stringa "BaseStyles" nei nomi delle risorse
                // Le risorse sono nominate come: Namespace.Cartella.Sottocartella.File.Estensione
                foreach (string resourceName in resourceNames)
                {
                    if (resourceName.Contains(".BaseStyles."))
                    {
                        // Isolo la parte del percorso che parte da BaseStyles
                        int index = resourceName.IndexOf(".BaseStyles.") + 1;
                        string relativePathInAssembly = resourceName.Substring(index); // "BaseStyles.p10.recap.x"

                        // Estraggo l'estensione (ultimi caratteri dopo l'ultimo punto)
                        int lastDot = resourceName.LastIndexOf('.');
                        string extension = resourceName.Substring(lastDot);

                        // Estraggo il nome senza estensione e senza il prefisso "BaseStyles."
                        string pathWithoutExt = resourceName.Substring(index + "BaseStyles.".Length, lastDot - (index + "BaseStyles.".Length));

                        // Ricostruisco il percorso file sostituendo i punti con gli slash, tranne l'ultimo dell'estensione
                        string finalRelativePath = pathWithoutExt.Replace('.', Path.DirectorySeparatorChar) + extension;

                        string finalFilePath = Path.Combine(targetFolder, finalRelativePath);
                        string directoryPath = Path.GetDirectoryName(finalFilePath);

                        // Creo le sottocartelle fisiche in Temp
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        // Estraggo il file dall'EXE e lo scrivo su disco
                        using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream != null)
                            {
                                using (FileStream fileStream = new FileStream(finalFilePath, FileMode.Create, FileAccess.Write))
                                {
                                    resourceStream.CopyTo(fileStream);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore estrazione: " + ex.Message);
            }
        }
    }
}