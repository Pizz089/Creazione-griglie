using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Media.Media3D;

using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Point = System.Windows.Point;

namespace Creazione_griglie
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

            // Uso Math.Round per evitare la perdita di precisione del cast a byte (es. 0.498039 * 255 = 126.999 truncato a 126 invece di 127)
            if (num.Count >= 4)
            {
                def.Alpha = num[3];
                def.DiffuseColor = Color.FromArgb((byte)Math.Round(num[3] * 255), (byte)Math.Round(num[0] * 255), (byte)Math.Round(num[1] * 255), (byte)Math.Round(num[2] * 255));
                def.HasColor = true;
            }
            if (num.Count >= 5) def.Power = num[4];
            if (num.Count >= 8) def.Specular = Color.FromRgb((byte)Math.Round(num[5] * 255), (byte)Math.Round(num[6] * 255), (byte)Math.Round(num[7] * 255));
            if (num.Count >= 11) def.Emissive = Color.FromRgb((byte)Math.Round(num[8] * 255), (byte)Math.Round(num[9] * 255), (byte)Math.Round(num[10] * 255));

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

                                uvs.Add(new Point(u, v));
                            }
                        }
                    }
                }
            }

            // Se il file salvato ha UV in numero diverso dai vertici (es. da un save precedente buggato), scartare:
            // DirectX/WPF richiedono che TextureCoordinates.Count == Positions.Count, altrimenti crash al render.
            // Trattando come "nessuna UV" l'utente puo' ri-applicare la skin per generare UV pulite.
            if (uvs.Count != posizioni.Count) uvs.Clear();

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

        // Restituisce il testo modificato e, in `diagnostica`, info su quante sostituzioni sono andate a segno.
        public static string ApplicaSalvataggio(string testoOriginale, List<MeshData> allMeshesFlat, out string diagnostica)
        {
            string testoModificato = testoOriginale;
            int matSostituiti = 0, matFalliti = 0;

            // Ogni mesh va mappata alla SUA specifica occorrenza del blocco Material nel file.
            // String.Replace e' globale: con oggetti che partono da un materiale identico
            // (stesso testo originale) sovrascriveva TUTTE le copie con un unico valore,
            // ignorando di fatto il flag "Applica a tutti gli oggetti simili". Qui invece
            // consumo le occorrenze in ordine di documento, una per mesh, e sostituisco solo
            // quella posizione. Cosi' un materiale modificato tocca solo il proprio oggetto;
            // i simili non toccati vengono riscritti identici a se stessi (no-op).
            // tuttiNodiFile arriva da AppiattisciGerarchia, che visita l'albero in ordine:
            // lo stesso ordine in cui i Material compaiono nel file -> l'accoppiamento e' corretto.
            var sostituzioni = new List<(int start, int len, string nuovo)>();
            var prossimaRicerca = new Dictionary<string, int>();

            foreach (var mesh in allMeshesFlat)
            {
                if (string.IsNullOrEmpty(mesh.OriginalMaterialContent))
                    continue;

                string orig = mesh.OriginalMaterialContent;
                int from = prossimaRicerca.TryGetValue(orig, out int p) ? p : 0;
                int pos = testoOriginale.IndexOf(orig, from, StringComparison.Ordinal);
                if (pos == -1)
                {
                    // Non trovata dalla posizione corrente. Se il testo non esiste proprio nel
                    // file e' un vero fallimento; se invece esiste ma e' gia' stato consumato si
                    // tratta di un Material condiviso (un unico blocco referenziato da piu' mesh)
                    // -> normale, lo possiede la prima mesh, le altre lo condividono. Salto.
                    if (testoOriginale.IndexOf(orig, 0, StringComparison.Ordinal) == -1)
                        matFalliti++;
                    continue;
                }
                prossimaRicerca[orig] = pos + orig.Length;

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

                sostituzioni.Add((pos, orig.Length, newContent));
                matSostituiti++;
            }

            // Applico le sostituzioni dal fondo verso l'inizio: poiche' le posizioni sono
            // calcolate sul testo originale, partire dalla coda evita di invalidare gli offset
            // delle sostituzioni precedenti.
            foreach (var s in sostituzioni.OrderByDescending(s => s.start))
            {
                testoModificato = testoModificato.Substring(0, s.start) + s.nuovo + testoModificato.Substring(s.start + s.len);
            }

            // Riscrivo i blocchi MeshTextureCoords per persistere la proiezione planare e gli offset locali/zoom.
            // Wrap in try-catch perche' se la riscrittura UV ha un bug non vogliamo perdere il salvataggio del materiale.
            int uvScritti = 0;
            string testoConUV = testoModificato;
            try
            {
                testoConUV = AggiornaMeshTextureCoords(testoModificato, allMeshesFlat, out uvScritti);
                // Safety check fondamentale: ogni MeshTextureCoords nel testo finale DEVE avere conteggio UV
                // uguale al conteggio vertici della Mesh che lo contiene. Se anche solo uno e' sballato,
                // DirectX/WPF crashano al load -> ripiego sul testo originale del materiale.
                if (ValidaConteggiMTC(testoConUV))
                {
                    testoModificato = testoConUV;
                }
                else
                {
                    uvScritti = -1; // marcatore per diagnostica: validazione fallita, ripiegato
                }
            }
            catch (Exception ex)
            {
                uvScritti = -2;
                diagnostica = $"ERRORE riscrittura UV: {ex.Message}";
            }

            diagnostica = $"materiali sostituiti: {matSostituiti}, falliti: {matFalliti}, blocchi UV riscritti: {uvScritti} (-1=anomalia ripiegato, -2=eccezione)";
            return testoModificato;
        }

        private static int ContaOccorrenze(string testo, string pattern)
        {
            int count = 0;
            int pos = 0;
            while ((pos = testo.IndexOf(pattern, pos)) != -1)
            {
                count++;
                pos += pattern.Length;
            }
            return count;
        }

        // Compatibilita' con il chiamante senza diagnostica
        public static string ApplicaSalvataggio(string testoOriginale, List<MeshData> allMeshesFlat)
        {
            return ApplicaSalvataggio(testoOriginale, allMeshesFlat, out _);
        }

        // Calcola le UV "bakate": applica il moltiplicatore di zoom (TextureScale) e l'offset locale (SkinOffsetU/V)
        // direttamente nelle coordinate UV. Cosi' al reload il brush con Viewport=(0,0,1,1) riproduce lo stesso
        // rendering ottenuto a edit time, sia in WPF che nel sw target (Steltronic Focus).
        private static List<Point> CalcolaUVBakate(MeshData md)
        {
            var uvs = md.Geometry?.TextureCoordinates;
            var result = new List<Point>();
            if (uvs == null || uvs.Count == 0) return result;

            // Bake solo se c'e' una skin utente attiva: TextureScale + SkinOffset entrano nel viewport del brush e
            // devono essere portate dentro le UV per essere persistenti nel file .x.
            bool hasSkin = !string.IsNullOrEmpty(md.NewTexturePath) && !md.RemoveTexture;
            if (!hasSkin)
            {
                foreach (var uv in uvs) result.Add(uv);
                return result;
            }

            double T = md.TextureScale > 0 ? md.TextureScale : 1.0;
            double off = (1 - T) / 2;
            double offU = md.SkinOffsetU;
            double offV = md.SkinOffsetV;
            foreach (var uv in uvs)
            {
                // (uv - off - offset) / T = brush relative coord che il viewport user-skin produce a runtime.
                // Salvo questo valore direttamente: al reload Viewport=(0,0,1,1) usa la UV come brush rel = stessa immagine.
                double u_rel = (uv.X - off - offU) / T;
                double v_rel = (uv.Y - off - offV) / T;
                result.Add(new Point(u_rel, v_rel));
            }
            return result;
        }

        // Iterazione sui Mesh block (saltando le definizioni template): per ogni Mesh block reale,
        // se al suo interno c'e' gia' un MeshTextureCoords lo sostituisce, altrimenti lo INSERISCE prima del }.
        // L'inserimento e' necessario perche' molti .x originali (Steltronic player.X ecc.) non hanno MeshTextureCoords:
        // le UV erano implicite o non usate. Senza il blocco a load WPF non ha mapping UV e la skin non si vede.
        // Il matching delle MeshData ai Mesh block avviene per CONTEGGIO VERTICI per evitare mismatch quando l'utente
        // ha skinnato solo alcuni elementi (i sibling sono adiacenti con stesso Positions.Count).
        private static string AggiornaMeshTextureCoords(string testo, List<MeshData> meshes, out int blocchiScritti)
        {
            blocchiScritti = 0;
            if (meshes == null || meshes.Count == 0) return testo;

            // Lista delle MeshData con UV proiettate disponibili (solo quelle effettivamente skinnate)
            var disponibili = meshes
                .Where(m => m?.Geometry?.TextureCoordinates != null
                         && m.Geometry.TextureCoordinates.Count > 0
                         && m.Geometry.Positions != null
                         && m.Geometry.Positions.Count == m.Geometry.TextureCoordinates.Count)
                .ToList();
            if (disponibili.Count == 0) return testo;

            // Indice di consumo per ogni conteggio vertici: i sibling con stesso count condividono le UV,
            // quindi posso usare lo stesso MeshData per piu' Mesh block con quel vertex count consecutivi.
            var usati = new HashSet<MeshData>();

            var regexMesh = new Regex(@"\bMesh\b\s+\w*\s*\{", RegexOptions.Compiled);
            var risultato = new StringBuilder();
            int pos = 0;

            while (pos < testo.Length)
            {
                var match = regexMesh.Match(testo, pos);
                if (!match.Success)
                {
                    risultato.Append(testo, pos, testo.Length - pos);
                    break;
                }

                int meshKeywordStart = match.Index;
                int openBrace = testo.IndexOf('{', meshKeywordStart);
                if (openBrace == -1) { risultato.Append(testo, pos, testo.Length - pos); break; }
                int closeBrace = TrovaFineBlocco(testo, openBrace);
                if (closeBrace == -1) { risultato.Append(testo, pos, testo.Length - pos); break; }

                if (PrecedutoDaTemplate(testo, meshKeywordStart))
                {
                    // E' "template Mesh { ... }" in cima al file: lascio intatto e proseguo
                    risultato.Append(testo, pos, closeBrace + 1 - pos);
                    pos = closeBrace + 1;
                    continue;
                }

                // Leggo il conteggio vertici di questo Mesh block: e' il primo intero dopo la {
                int vertCount = LeggiPrimoIntero(testo, openBrace + 1, closeBrace);

                // Cerco una MeshData skinnata con conteggio combaciante. Quando ne trovo una, marco
                // come usati TUTTI i suoi sibling (stesso Mesh block sorgente, multi-material), riconoscibili
                // perche' condividono la stessa Positions[0]. Senza questo, Mesh block successivi finiscono
                // per matchare sibling di mesh gia' usate -> UV duplicate fra mesh diverse.
                MeshData md = disponibili.FirstOrDefault(m => !usati.Contains(m) && m.Geometry.Positions.Count == vertCount);
                if (md != null)
                {
                    Point3D firstPos = md.Geometry.Positions[0];
                    foreach (var sibling in disponibili)
                    {
                        if (sibling.Geometry.Positions.Count == vertCount && sibling.Geometry.Positions[0] == firstPos)
                            usati.Add(sibling);
                    }
                }

                // Cerco un MeshTextureCoords reale dentro questo Mesh block
                int uvKeyword = -1, uvOpen = -1, uvClose = -1;
                int scanFrom = openBrace + 1;
                while (scanFrom < closeBrace)
                {
                    int idx = testo.IndexOf("MeshTextureCoords", scanFrom);
                    if (idx == -1 || idx >= closeBrace) break;
                    if (!PrecedutoDaTemplate(testo, idx))
                    {
                        uvKeyword = idx;
                        uvOpen = testo.IndexOf('{', idx);
                        if (uvOpen != -1 && uvOpen < closeBrace) uvClose = TrovaFineBlocco(testo, uvOpen);
                        break;
                    }
                    scanFrom = idx + 1;
                }

                // Verifica se l'esistente MTC ha conteggio valido rispetto ai vertici della mesh
                bool mtcEsistente = uvKeyword >= 0 && uvOpen >= 0 && uvClose >= 0 && uvClose < closeBrace;
                bool mtcEsistenteInvalido = false;
                if (mtcEsistente)
                {
                    int existingUvCount = LeggiPrimoIntero(testo, uvOpen + 1, uvClose);
                    if (existingUvCount != vertCount) mtcEsistenteInvalido = true;
                }

                if (md != null && mtcEsistente)
                {
                    // Sostituisco il contenuto del MeshTextureCoords esistente con quello giusto
                    risultato.Append(testo, pos, uvOpen + 1 - pos);
                    risultato.Append(BuildContenutoUV(CalcolaUVBakate(md)));
                    risultato.Append('}');
                    risultato.Append(testo, uvClose + 1, closeBrace + 1 - uvClose - 1);
                    blocchiScritti++;
                }
                else if (md != null)
                {
                    // Inserisco un nuovo MeshTextureCoords subito prima della } della Mesh
                    risultato.Append(testo, pos, closeBrace - pos);
                    risultato.Append("\r\n MeshTextureCoords {");
                    risultato.Append(BuildContenutoUV(CalcolaUVBakate(md)));
                    risultato.Append("}\r\n");
                    risultato.Append('}');
                    blocchiScritti++;
                }
                else if (mtcEsistenteInvalido)
                {
                    // Nessuna MeshData utile MA c'e' un MTC esistente con count sbagliato (probabile residuo
                    // di un save precedente buggato). Lo rimuovo per evitare crash al load.
                    risultato.Append(testo, pos, uvKeyword - pos);
                    risultato.Append(testo, uvClose + 1, closeBrace + 1 - uvClose - 1);
                    blocchiScritti++;
                }
                else
                {
                    // Nessuna MeshData per questo Mesh block e MTC esistente valido o assente: lascio com'e'
                    risultato.Append(testo, pos, closeBrace + 1 - pos);
                }

                pos = closeBrace + 1;
            }

            return risultato.ToString();
        }

        // Verifica che tutti i blocchi MeshTextureCoords nel testo abbiano conteggio UV uguale al conteggio vertici
        // del Mesh block che li contiene. Usato come safety check dopo AggiornaMeshTextureCoords.
        private static bool ValidaConteggiMTC(string testo)
        {
            var regexMesh = new Regex(@"\bMesh\b\s+\w*\s*\{", RegexOptions.Compiled);
            int pos = 0;
            while (pos < testo.Length)
            {
                var m = regexMesh.Match(testo, pos);
                if (!m.Success) break;

                int meshKeywordStart = m.Index;
                int openBrace = testo.IndexOf('{', meshKeywordStart);
                if (openBrace == -1) break;
                int closeBrace = TrovaFineBlocco(testo, openBrace);
                if (closeBrace == -1) return false;

                if (PrecedutoDaTemplate(testo, meshKeywordStart))
                {
                    pos = closeBrace + 1;
                    continue;
                }

                int vertCount = LeggiPrimoIntero(testo, openBrace + 1, closeBrace);

                int scanFrom = openBrace + 1;
                while (scanFrom < closeBrace)
                {
                    int idx = testo.IndexOf("MeshTextureCoords", scanFrom);
                    if (idx == -1 || idx >= closeBrace) break;
                    if (PrecedutoDaTemplate(testo, idx)) { scanFrom = idx + 1; continue; }

                    int uvOpen = testo.IndexOf('{', idx);
                    if (uvOpen == -1 || uvOpen >= closeBrace) break;
                    int uvClose = TrovaFineBlocco(testo, uvOpen);
                    if (uvClose == -1 || uvClose >= closeBrace) return false;

                    int uvCount = LeggiPrimoIntero(testo, uvOpen + 1, uvClose);
                    if (uvCount != vertCount) return false;

                    scanFrom = uvClose + 1;
                }
                pos = closeBrace + 1;
            }
            return true;
        }

        // Verifica se la keyword a indice `idx` e' preceduta dalla parola "template" (con whitespace tra le due)
        private static bool PrecedutoDaTemplate(string testo, int idx)
        {
            int prevNonWs = idx - 1;
            while (prevNonWs > 0 && (testo[prevNonWs] == ' ' || testo[prevNonWs] == '\t' || testo[prevNonWs] == '\r' || testo[prevNonWs] == '\n')) prevNonWs--;
            return prevNonWs >= 7 && testo.Substring(prevNonWs - 7, 8) == "template";
        }

        // Legge il primo intero (saltando whitespace e punteggiatura) nell'intervallo [start, end).
        // Usato per estrarre il conteggio vertici dal Mesh block (e' il primo numero dopo la "{").
        private static int LeggiPrimoIntero(string testo, int start, int end)
        {
            while (start < end && !char.IsDigit(testo[start])) start++;
            if (start >= end) return -1;
            int e = start;
            while (e < end && char.IsDigit(testo[e])) e++;
            return int.TryParse(testo.Substring(start, e - start), out int v) ? v : -1;
        }

        private static string BuildContenutoUV(List<Point> uvs)
        {
            var sb = new StringBuilder();
            sb.Append("\r\n ");
            sb.Append(uvs.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append(";\r\n");
            for (int i = 0; i < uvs.Count; i++)
            {
                sb.Append(uvs[i].X.ToString("0.000000", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(uvs[i].Y.ToString("0.000000", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(i == uvs.Count - 1 ? ";" : ",");
                sb.Append("\r\n");
            }
            return sb.ToString();
        }
    }
}