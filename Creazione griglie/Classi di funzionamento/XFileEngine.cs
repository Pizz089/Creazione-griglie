using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Media.Media3D;

using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Point = System.Windows.Point;

namespace DirectXEditor
{
    public class MaterialDef
    {
        public string OriginalContent = "";
        public string TextureName = "";
        public Color DiffuseColor = Colors.LightGray;
        public double Alpha = 1.0;
        public double Power = 0.0;
        public Color Specular = Colors.Black;
        public Color Emissive = Colors.Black;
        public bool HasColor = false;
    }

    public static class XFileEngine
    {
        private static readonly Regex regexFrameMesh = new Regex(@"\b(Frame|Mesh)\b\s*([a-zA-Z0-9_]+\s*)?\{", RegexOptions.Compiled);
        private static readonly Regex regexMatrix = new Regex(@"\bFrameTransformMatrix\b\s*\{", RegexOptions.Compiled);

        public static List<MeshData> Parse(string testo)
        {
            string testoPulito = Regex.Replace(testo, @"//.*|#.*", "");
            Dictionary<string, MaterialDef> dizionarioMateriali = EstraiMaterialiGlobali(testoPulito);

            MeshData root = new MeshData { Name = "Modello 3D", IsGroup = true };
            EstraiNodi(testoPulito, Matrix3D.Identity, root, dizionarioMateriali, "Sconosciuto");

            return new List<MeshData>(root.Children);
        }

        private static Dictionary<string, MaterialDef> EstraiMaterialiGlobali(string testo)
        {
            var dict = new Dictionary<string, MaterialDef>();
            int idx = 0;

            while ((idx = testo.IndexOf("Material ", idx)) != -1)
            {
                int startName = idx + 9;
                int startBlock = testo.IndexOf('{', startName);
                if (startBlock == -1) break;

                string name = testo.Substring(startName, startBlock - startName).Trim();
                int endBlock = TrovaFineBlocco(testo, startBlock);
                if (endBlock == -1) break;

                string matContent = testo.Substring(startBlock + 1, endBlock - startBlock - 1);
                dict[name] = ParseSingleMaterialContent(matContent);
                idx = endBlock + 1;
            }
            return dict;
        }

        private static MaterialDef ParseSingleMaterialContent(string content)
        {
            MaterialDef def = new MaterialDef();
            def.OriginalContent = content;

            var texMatch = Regex.Match(content, @"TextureFilename\s*\{\s*""([^""]+)""");
            if (texMatch.Success) def.TextureName = texMatch.Groups[1].Value;

            string[] tokens = content.Split(new char[] { ';', ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            List<double> num = new List<double>();

            foreach (var t in tokens)
            {
                if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) num.Add(val);
            }

            if (num.Count >= 4)
            {
                def.Alpha = num[3];
                def.DiffuseColor = Color.FromArgb((byte)(num[3] * 255), (byte)(num[0] * 255), (byte)(num[1] * 255), (byte)(num[2] * 255));
                def.HasColor = true;
            }
            if (num.Count >= 5) def.Power = num[4];
            if (num.Count >= 8) def.Specular = Color.FromRgb((byte)(num[5] * 255), (byte)(num[6] * 255), (byte)(num[7] * 255));
            if (num.Count >= 11) def.Emissive = Color.FromRgb((byte)(num[8] * 255), (byte)(num[9] * 255), (byte)(num[10] * 255));

            return def;
        }

        private static void EstraiNodi(string bloccoTesto, Matrix3D matricePadre, MeshData nodoPadre, Dictionary<string, MaterialDef> materiali, string nomeEreditato)
        {
            Matrix3D matriceCorrente = matricePadre;

            Match mMatrix = regexMatrix.Match(bloccoTesto);
            Match mPrimoFiglio = regexFrameMesh.Match(bloccoTesto);

            if (mMatrix.Success)
            {
                if (!mPrimoFiglio.Success || mMatrix.Index < mPrimoFiglio.Index)
                {
                    int start = mMatrix.Index + mMatrix.Length - 1;
                    int end = TrovaFineBlocco(bloccoTesto, start);
                    if (end != -1)
                    {
                        string matTesto = bloccoTesto.Substring(start + 1, end - start - 1);
                        matriceCorrente = Matrix3D.Multiply(AnalizzaMatrice(matTesto), matricePadre);
                    }
                }
            }

            int cursore = 0;
            while (cursore < bloccoTesto.Length)
            {
                Match m = regexFrameMesh.Match(bloccoTesto, cursore);
                if (!m.Success) break;

                string tipo = m.Groups[1].Value;
                string nomeNodo = m.Groups[2].Value.Trim();
                if (string.IsNullOrEmpty(nomeNodo)) nomeNodo = nomeEreditato;

                int startBlocco = m.Index + m.Length - 1;
                int endBlocco = TrovaFineBlocco(bloccoTesto, startBlocco);
                if (endBlocco == -1) break;

                string contenuto = bloccoTesto.Substring(startBlocco + 1, endBlocco - startBlocco - 1);

                if (tipo == "Mesh")
                {
                    List<MeshData> subMeshes = ParsaMeshMultiMateriale(contenuto, matriceCorrente, materiali, nomeNodo);
                    foreach (var sm in subMeshes) nodoPadre.Children.Add(sm);
                }
                else if (tipo == "Frame")
                {
                    MeshData frameNode = new MeshData { Name = nomeNodo, IsGroup = true };
                    EstraiNodi(contenuto, matriceCorrente, frameNode, materiali, nomeNodo);
                    if (frameNode.Children.Count > 0) nodoPadre.Children.Add(frameNode);
                }

                cursore = endBlocco + 1;
            }
        }

        private static Matrix3D AnalizzaMatrice(string testoMatrice)
        {
            string[] tokens = testoMatrice.Split(new char[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 16)
            {
                try
                {
                    return new Matrix3D(
                        double.Parse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[3], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[4], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[5], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[6], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[7], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[8], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[9], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[10], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[11], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[12], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[13], NumberStyles.Any, CultureInfo.InvariantCulture),
                        double.Parse(tokens[14], NumberStyles.Any, CultureInfo.InvariantCulture), double.Parse(tokens[15], NumberStyles.Any, CultureInfo.InvariantCulture)
                    );
                }
                catch { }
            }
            return Matrix3D.Identity;
        }

        private static List<MeshData> ParsaMeshMultiMateriale(string bloccoMesh, Matrix3D matriceCorrente, Dictionary<string, MaterialDef> globalMats, string baseName)
        {
            string[] keywords = { "MeshNormals", "MeshTextureCoords", "MeshMaterialList", "VertexColors", "SkinWeights", "XSkinMeshHeader", "DeclData", "Animation" };
            int minIndex = bloccoMesh.Length;
            foreach (string kw in keywords)
            {
                int idx = bloccoMesh.IndexOf(kw);
                if (idx != -1 && idx < minIndex) minIndex = idx;
            }

            string datiBase = bloccoMesh.Substring(0, minIndex);
            string[] tokens = datiBase.Split(new char[] { ';', ',', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            List<Point3D> posizioni = new List<Point3D>();
            List<List<int>> facce = new List<List<int>>();

            int index = 0;
            if (tokens.Length > 0 && int.TryParse(tokens[index++], out int numVertici))
            {
                for (int i = 0; i < numVertici && index < tokens.Length; i++)
                {
                    double x = double.Parse(tokens[index++], NumberStyles.Any, CultureInfo.InvariantCulture);
                    double y = double.Parse(tokens[index++], NumberStyles.Any, CultureInfo.InvariantCulture);
                    double z = double.Parse(tokens[index++], NumberStyles.Any, CultureInfo.InvariantCulture);

                    // CONVERSIONE 1: Inversione dell'asse Z per il passaggio da Left-Handed a Right-Handed
                    Point3D p = matriceCorrente.Transform(new Point3D(x, y, z));
                    p.Z = -p.Z;
                    posizioni.Add(p);
                }

                if (index < tokens.Length && int.TryParse(tokens[index++], out int numFacce))
                {
                    for (int i = 0; i < numFacce; i++)
                    {
                        if (index >= tokens.Length) break;
                        int numVerticiFaccia = int.Parse(tokens[index++]);
                        List<int> facciaCorrente = new List<int>();

                        if (numVerticiFaccia >= 3)
                        {
                            int v0 = int.Parse(tokens[index++]);
                            int vPrecedente = int.Parse(tokens[index++]);
                            for (int j = 2; j < numVerticiFaccia; j++)
                            {
                                int vCorrente = int.Parse(tokens[index++]);

                                // CONVERSIONE 2: Poiché abbiamo invertito Z, i triangoli sono "svuotati". 
                                // Dobbiamo invertirne l'ordine di rendering (da 0,1,2 a 0,2,1) per mostrare la faccia giusta!
                                facciaCorrente.Add(v0);
                                facciaCorrente.Add(vPrecedente);
                                facciaCorrente.Add(vCorrente);

                                vPrecedente = vCorrente;
                            }
                        }
                        else index += numVerticiFaccia;

                        facce.Add(facciaCorrente);
                    }
                }
            }

            List<Point> uvs = new List<Point>();
            int inizioUV = bloccoMesh.IndexOf("MeshTextureCoords");
            if (inizioUV != -1)
            {
                int inizioBloccoUV = bloccoMesh.IndexOf("{", inizioUV);
                if (inizioBloccoUV != -1)
                {
                    int fineUVBlock = TrovaFineBlocco(bloccoMesh, inizioBloccoUV);
                    if (fineUVBlock != -1)
                    {
                        string bloccoUV = bloccoMesh.Substring(inizioBloccoUV + 1, fineUVBlock - inizioBloccoUV - 1);
                        string[] uvTokens = bloccoUV.Split(new char[] { ';', ',', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                        int uvIndex = 0;
                        if (uvTokens.Length > 0 && int.TryParse(uvTokens[uvIndex++], out int numUV))
                        {
                            for (int i = 0; i < numUV && uvIndex + 1 < uvTokens.Length; i++)
                            {
                                double u = double.Parse(uvTokens[uvIndex++], NumberStyles.Any, CultureInfo.InvariantCulture);
                                double v = double.Parse(uvTokens[uvIndex++], NumberStyles.Any, CultureInfo.InvariantCulture);

                                // CONVERSIONE 3: Conversione dell'asse V (Verticale)
                                // DirectX legge dall'alto (0) verso il basso (1)
                                // WPF legge dal basso (0) verso l'alto (1)
                                uvs.Add(new Point(u, v));
                            }
                        }
                    }
                }
            }

            while (uvs.Count > 0 && uvs.Count < posizioni.Count) uvs.Add(new Point(0, 0));

            List<MaterialDef> meshMaterials = new List<MaterialDef>();
            List<int> faceMatIndices = new List<int>();

            int matListIdx = bloccoMesh.IndexOf("MeshMaterialList");
            if (matListIdx != -1)
            {
                int startMatBlock = bloccoMesh.IndexOf("{", matListIdx);
                int endMatBlock = TrovaFineBlocco(bloccoMesh, startMatBlock);
                if (startMatBlock != -1 && endMatBlock != -1)
                {
                    string matContent = bloccoMesh.Substring(startMatBlock + 1, endMatBlock - startMatBlock - 1);
                    int firstBrace = matContent.IndexOf("{");
                    string indexContent = firstBrace != -1 ? matContent.Substring(0, firstBrace) : matContent;
                    string[] matTokens = indexContent.Split(new char[] { ';', ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (matTokens.Length >= 2)
                    {
                        int nFaceIndexes = int.Parse(matTokens[1]);
                        for (int i = 0; i < nFaceIndexes && i + 2 < matTokens.Length; i++) faceMatIndices.Add(int.Parse(matTokens[i + 2]));
                    }

                    int searchIdx = 0;
                    while (searchIdx < matContent.Length)
                    {
                        int nextOpen = matContent.IndexOf('{', searchIdx);
                        if (nextOpen == -1) break;
                        int endBrace = TrovaFineBlocco(matContent, nextOpen);
                        if (endBrace == -1) break;

                        string beforeBrace = matContent.Substring(searchIdx, nextOpen - searchIdx);
                        bool isInline = beforeBrace.LastIndexOf("Material") != -1;

                        if (isInline)
                        {
                            meshMaterials.Add(ParseSingleMaterialContent(matContent.Substring(nextOpen + 1, endBrace - nextOpen - 1)));
                        }
                        else
                        {
                            string refName = matContent.Substring(nextOpen + 1, endBrace - nextOpen - 1).Trim();
                            meshMaterials.Add(globalMats.ContainsKey(refName) ? globalMats[refName] : new MaterialDef());
                        }
                        searchIdx = endBrace + 1;
                    }
                }
            }

            if (meshMaterials.Count == 0)
            {
                MaterialDef defMat = new MaterialDef();
                int directMatIdx = bloccoMesh.IndexOf("Material ");
                if (directMatIdx != -1 && (matListIdx == -1 || directMatIdx < matListIdx))
                {
                    int sBlock = bloccoMesh.IndexOf('{', directMatIdx);
                    if (sBlock != -1)
                    {
                        int eBlock = TrovaFineBlocco(bloccoMesh, sBlock);
                        if (eBlock != -1) defMat = ParseSingleMaterialContent(bloccoMesh.Substring(sBlock + 1, eBlock - sBlock - 1));
                    }
                }
                meshMaterials.Add(defMat);
            }

            if (faceMatIndices.Count == 0) for (int i = 0; i < facce.Count; i++) faceMatIndices.Add(0);

            List<MeshData> ris = new List<MeshData>();
            for (int i = 0; i < meshMaterials.Count; i++)
            {
                MeshData md = new MeshData();
                md.OriginalMaterialContent = meshMaterials[i].OriginalContent;
                md.TextureName = meshMaterials[i].TextureName;
                md.MeshColor = meshMaterials[i].DiffuseColor;
                md.Alpha = meshMaterials[i].Alpha;
                md.Power = meshMaterials[i].Power;
                md.Specular = meshMaterials[i].Specular;
                md.Emissive = meshMaterials[i].Emissive;
                md.HasColor = meshMaterials[i].HasColor;
                md.Name = meshMaterials.Count > 1 ? $"{baseName} (Strato {i + 1})" : baseName;

                foreach (var p in posizioni) md.Geometry.Positions.Add(p);
                foreach (var uv in uvs) md.Geometry.TextureCoordinates.Add(uv);
                ris.Add(md);
            }

            for (int i = 0; i < facce.Count; i++)
            {
                int mIdx = i < faceMatIndices.Count ? faceMatIndices[i] : 0;
                if (mIdx >= 0 && mIdx < ris.Count)
                {
                    foreach (int tIdx in facce[i]) ris[mIdx].Geometry.TriangleIndices.Add(tIdx);
                }
            }

            ris.RemoveAll(m => m.Geometry.TriangleIndices.Count == 0);
            return ris;
        }

        private static int TrovaFineBlocco(string testo, int inizio)
        {
            int contatore = 0;
            for (int i = inizio; i < testo.Length; i++)
            {
                if (testo[i] == '{') contatore++;
                else if (testo[i] == '}')
                {
                    contatore--;
                    if (contatore == 0) return i;
                }
            }
            return -1;
        }

        public static string ApplicaSalvataggio(string testoOriginale, List<MeshData> allMeshesFlat)
        {
            string testoModificato = testoOriginale;
            HashSet<string> elaborati = new HashSet<string>();

            foreach (var mesh in allMeshesFlat)
            {
                if (string.IsNullOrEmpty(mesh.OriginalMaterialContent) || elaborati.Contains(mesh.OriginalMaterialContent))
                    continue;

                string texBlock = "";

                // Formattazione corretta del blocco TextureFilename come si aspetta DirectX
                if (!mesh.RemoveTexture && !string.IsNullOrEmpty(mesh.NewTexturePath))
                {
                    texBlock = $"  TextureFilename {{\r\n   \"{Path.GetFileName(mesh.NewTexturePath)}\";\r\n  }}\r\n";
                }
                else if (!mesh.RemoveTexture && !string.IsNullOrEmpty(mesh.TextureName))
                {
                    texBlock = $"  TextureFilename {{\r\n   \"{Path.GetFileName(mesh.TextureName)}\";\r\n  }}\r\n";
                }

                // Inserisco uno \r\n prima di chiudere la stringa per spingere la parentesi finale } del Material sulla riga successiva
                string newContent = $"\r\n  {(mesh.MeshColor.R / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.MeshColor.G / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.MeshColor.B / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{mesh.Alpha.ToString("0.000000", CultureInfo.InvariantCulture)};;\r\n" +
                                    $"  {mesh.Power.ToString("0.000000", CultureInfo.InvariantCulture)};\r\n" +
                                    $"  {(mesh.Specular.R / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.Specular.G / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.Specular.B / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};;\r\n" +
                                    $"  {(mesh.Emissive.R / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.Emissive.G / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};{(mesh.Emissive.B / 255.0).ToString("0.000000", CultureInfo.InvariantCulture)};;\r\n" +
                                    $"{texBlock}";

                testoModificato = testoModificato.Replace(mesh.OriginalMaterialContent, newContent);
                elaborati.Add(mesh.OriginalMaterialContent);
            }

            return testoModificato;
        }
    }
}