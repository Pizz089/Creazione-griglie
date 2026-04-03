using System;
using System.Collections.Generic;
using System.IO;

namespace DirectXEditor
{
    // Gestisco le regole di sistema, i percorsi e la numerazione degli stili
    public static class StyleEnvironment
    {
        // Cerco il percorso dell'applicazione
        public static string TrovaPercorsoStili()
        {
            string fallbackPath = @"C:\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles";

            try
            {
                var desktopPaths = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                           Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };

                foreach (var desktop in desktopPaths)
                {
                    if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) continue;
                    string[] files = Directory.GetFiles(desktop, "Focus.lnk", SearchOption.TopDirectoryOnly);

                    if (files.Length > 0)
                    {
                        string targetPath = @"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles";
                        if (Directory.Exists(targetPath)) return targetPath;
                    }
                }
            }
            catch { }

            return Directory.Exists(fallbackPath) ? fallbackPath : null;
        }

        // Blocco la sovrascrittura degli stili originali di fabbrica
        public static bool IsStyleProtected(string folderName)
        {
            string name = folderName.ToLower();
            if (name == "style#l1" || name == "style#l2" || name == "style#l3") return true;

            if (name.StartsWith("style#"))
            {
                string numStr = name.Substring(6);
                if (int.TryParse(numStr, out int num))
                {
                    if (num >= 0 && num <= 12) return true;
                }
            }
            return false;
        }

        // Trovo il prossimo slot disponibile dal 13 in poi, riempiendo i buchi
        public static string GetNextAvailableStyleName(string baseStylesPath)
        {
            int maxSystemNum = 12;
            List<int> numeriEsistenti = new List<int>();

            if (!string.IsNullOrEmpty(baseStylesPath) && Directory.Exists(baseStylesPath))
            {
                string[] dirs = Directory.GetDirectories(baseStylesPath, "style#*");
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d).ToLower();
                    if (name.StartsWith("style#"))
                    {
                        string numStr = name.Substring(6);
                        if (int.TryParse(numStr, out int num))
                        {
                            if (num > maxSystemNum) numeriEsistenti.Add(num);
                        }
                    }
                }
            }

            numeriEsistenti.Sort();
            int prossimoNumero = maxSystemNum + 1;

            foreach (int num in numeriEsistenti)
            {
                if (num == prossimoNumero) prossimoNumero++;
                else if (num > prossimoNumero) break;
            }

            return "style#" + prossimoNumero;
        }
    }
}