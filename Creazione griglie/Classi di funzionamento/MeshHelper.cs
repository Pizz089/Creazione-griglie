using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Creazione_griglie
{
    public static class MeshHelper
    {
        // Creo la struttura ad albero delle cartelle ricavandola dal percorso fisico
        public static MeshData CostruisciAlberoCartelle(MeshData rootNode, string percorsoRelativoFile)
        {
            string[] parti = percorsoRelativoFile.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            MeshData nodoAttuale = rootNode;

            for (int i = 0; i < parti.Length - 1; i++)
            {
                string nomeCartella = parti[i];
                MeshData cartellaEsistente = null;

                foreach (var figlio in nodoAttuale.Children)
                {
                    if (figlio.IsGroup && figlio.Name == nomeCartella && string.IsNullOrEmpty(figlio.OriginalXFileContent))
                    {
                        cartellaEsistente = figlio;
                        break;
                    }
                }

                if (cartellaEsistente == null)
                {
                    cartellaEsistente = new MeshData
                    {
                        Name = nomeCartella,
                        IsGroup = true,
                        IsExpanded = false
                    };
                    nodoAttuale.Children.Add(cartellaEsistente);
                }

                nodoAttuale = cartellaEsistente;
            }

            return nodoAttuale;
        }

        // Pesco solo i nodi che rappresentano file veri ignorando le cartelle
        public static void EstraiTuttiIFileX(MeshData nodo, List<MeshData> listaFile)
        {
            if (!string.IsNullOrEmpty(nodo.OriginalXFileContent))
            {
                listaFile.Add(nodo);
                return;
            }
            foreach (var figlio in nodo.Children)
            {
                EstraiTuttiIFileX(figlio, listaFile);
            }
        }

        // Trasmetto posizione, rotazione e scala a tutti i sottomodelli
        public static void ApplicaCoordinateRicorsivo(MeshData nodo, double pX, double pY, double pZ, double rX, double rY, double rZ, double scala)
        {
            nodo.PosX = pX; nodo.PosY = pY; nodo.PosZ = pZ;
            nodo.RotX = rX; nodo.RotY = rY; nodo.RotZ = rZ;
            nodo.ScaleXYZ = scala;
            foreach (var figlio in nodo.Children)
            {
                ApplicaCoordinateRicorsivo(figlio, pX, pY, pZ, rX, rY, rZ, scala);
            }
        }

        // Cerca una texture: prima nelle sottocartelle (specifiche per gioco), poi nella root (fallback comune)
        public static string TrovaPercorsoTextura(string cartellaAttuale, string cartellaPadre, string texPath)
        {
            if (string.IsNullOrEmpty(texPath)) return null;

            // Se è già un percorso assoluto e il file esiste (skin scelta dall'utente fuori dallo stile), usalo direttamente
            if (Path.IsPathRooted(texPath) && File.Exists(texPath)) return texPath;

            string nomeFile = Path.GetFileName(texPath);

            string CercaInCartella(string cartella)
            {
                if (!Directory.Exists(cartella)) return null;
                // 1. sottocartelle prima
                string inSub = Directory.GetDirectories(cartella)
                    .SelectMany(d => Directory.GetFiles(d, nomeFile, SearchOption.AllDirectories))
                    .FirstOrDefault();
                if (inSub != null) return inSub;
                // 2. poi root
                string inRoot = Path.Combine(cartella, nomeFile);
                return File.Exists(inRoot) ? inRoot : null;
            }

            return CercaInCartella(cartellaAttuale) ?? CercaInCartella(cartellaPadre);
        }

        // Bounding box delle coordinate UV: (minU, minV, larghezza, altezza). Serve a far combaciare l'immagine
        // anche su oggetti non rettangolari con UV non quadrate o non ancorate a (0,0).
        public static Rect CalcolaBoundsUV(MeshData md)
        {
            if (md?.Geometry == null || md.Geometry.TextureCoordinates.Count == 0) return new Rect(0, 0, 1, 1);
            double minU = double.MaxValue, maxU = double.MinValue;
            double minV = double.MaxValue, maxV = double.MinValue;
            foreach (var uv in md.Geometry.TextureCoordinates)
            {
                if (uv.X < minU) minU = uv.X;
                if (uv.X > maxU) maxU = uv.X;
                if (uv.Y < minV) minV = uv.Y;
                if (uv.Y > maxV) maxV = uv.Y;
            }
            double w = maxU - minU;
            double h = maxV - minV;
            if (w <= 0) w = 1;
            if (h <= 0) h = 1;
            return new Rect(minU, minV, w, h);
        }

        // Rigenera le UV usando una proiezione planare condivisa fra tutte le mesh in input.
        // Ogni file .x ha un suo UV unwrap pensato per la texture originale, quindi applicare una skin
        // diversa "sulle UV originali" mostra l'immagine spaccata/distorta (il famoso effetto "macchia").
        // La proiezione planare ignora le UV del .x e genera nuove UV dalla posizione 3D dei vertici,
        // proiettandoli sul piano perpendicolare alla dimensione piu piccola del bbox combinato.
        // In questo modo la skin appare come una decalcomania uniforme anche su gruppi multi-mesh.
        public static void ApplicaProiezionePlanare(IList<MeshData> targets)
        {
            if (targets == null || targets.Count == 0) return;

            // Bbox 3D combinato in SPAZIO MONDO: includo la trasformazione per-mesh (scale/rot/translate)
            // cosi' una scena multi-file ha il bbox reale invece di sovrapporre le coordinate locali di file diversi.
            bool inizializzato = false;
            double minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0, maxZ = 0;
            foreach (var m in targets)
            {
                if (m.Geometry?.Positions == null || m.Geometry.Positions.Count == 0) continue;
                Matrix3D mw = MatriceMondoMesh(m);
                foreach (var p in m.Geometry.Positions)
                {
                    Point3D wp = mw.Transform(p);
                    if (!inizializzato)
                    {
                        minX = maxX = wp.X; minY = maxY = wp.Y; minZ = maxZ = wp.Z;
                        inizializzato = true;
                    }
                    else
                    {
                        if (wp.X < minX) minX = wp.X; if (wp.X > maxX) maxX = wp.X;
                        if (wp.Y < minY) minY = wp.Y; if (wp.Y > maxY) maxY = wp.Y;
                        if (wp.Z < minZ) minZ = wp.Z; if (wp.Z > maxZ) maxZ = wp.Z;
                    }
                }
            }
            if (!inizializzato) return;

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            if (dx <= 0) dx = 1;
            if (dy <= 0) dy = 1;
            if (dz <= 0) dz = 1;

            // Asse della proiezione = dimensione piu piccola del bbox.
            // Cosi la skin viene "stampata" sul lato di area maggiore (la faccia piu visibile dell'oggetto).
            int asseProiezione = 0; // 0=X, 1=Y, 2=Z
            if (dy <= dx && dy <= dz) asseProiezione = 1;
            if (dz <= dx && dz <= dy) asseProiezione = 2;

            // Calcolo l'aspect ratio fisico dei due assi del piano di proiezione (dU/dV in unita' mondo)
            double dU = 0, dV = 0;
            switch (asseProiezione)
            {
                case 0: dU = dz; dV = dy; break; // piano YZ: U=Z, V=Y
                case 1: dU = dx; dV = dz; break; // piano XZ: U=X, V=Z
                case 2: dU = dx; dV = dy; break; // piano XY: U=X, V=Y
            }
            double aspectFisico = dV > 0 ? dU / dV : 1.0;

            // Aspect ratio dell'immagine skin: leggo dal primo target con NewTexturePath valido.
            // Tutti i target di una stessa applicazione condividono la stessa skin, quindi una sola lettura.
            double aspectImg = LeggiAspectImg(targets);

            // Calcolo i fattori di scala per le UV in modo che l'immagine mantenga il suo aspect ratio.
            // Modalita' COVER: l'immagine si adatta al lato PIU' LUNGO della scena; l'altro lato croppa
            // la parte di immagine in eccesso (mostra la fascia centrale). Niente margini di colore base.
            double scaleU = 1.0, scaleV = 1.0;
            if (aspectImg > 0 && aspectFisico > 0)
            {
                if (aspectFisico > aspectImg)
                {
                    // Superficie piu larga dell'immagine: il lato lungo e' U.
                    // L'immagine riempie U; V vede solo una banda centrale (image piu alta del bbox -> crop).
                    scaleV = aspectImg / aspectFisico;
                }
                else
                {
                    // Superficie piu stretta dell'immagine: il lato lungo e' V.
                    // L'immagine riempie V; U vede solo una banda centrale (image piu larga del bbox -> crop).
                    scaleU = aspectFisico / aspectImg;
                }
            }

            foreach (var m in targets)
            {
                if (m.Geometry?.Positions == null || m.Geometry.Positions.Count == 0) continue;

                // Backup UV originali (solo se non gia salvate per una skin precedente)
                if (m.OriginalTextureCoordinates == null)
                {
                    m.OriginalTextureCoordinates = new PointCollection(m.Geometry.TextureCoordinates);
                }

                // Offset globale in spazio mondo: sposta l'origine della proiezione.
                // Un offset positivo X sposta la skin a destra in spazio mondo.
                double oX = m.SkinOffsetX, oY = m.SkinOffsetY, oZ = m.SkinOffsetZ;

                // Trasformo le posizioni in spazio mondo cosi le UV riflettono la posizione globale della mesh
                Matrix3D mw = MatriceMondoMesh(m);

                var nuoveUV = new PointCollection();
                foreach (var p in m.Geometry.Positions)
                {
                    Point3D wp = mw.Transform(p);
                    double u = 0, v = 0;
                    switch (asseProiezione)
                    {
                        case 0: // proiezione sul piano YZ (vista laterale lungo X)
                            u = (wp.Z - minZ - oZ) / dz;
                            v = (maxY - wp.Y + oY) / dy; // V invertita: immagine "in piedi" rispetto a Y up
                            break;
                        case 1: // proiezione sul piano XZ (vista dall'alto lungo Y)
                            u = (wp.X - minX - oX) / dx;
                            v = (maxZ - wp.Z + oZ) / dz; // V invertita
                            break;
                        case 2: // proiezione sul piano XY (vista frontale lungo Z)
                            u = (wp.X - minX - oX) / dx;
                            v = (maxY - wp.Y + oY) / dy; // V invertita
                            break;
                    }
                    // Mantengo aspect ratio: scalo le UV attorno al centro (0.5, 0.5).
                    // Punti fuori [0,1] portano il brush in TileMode.None a mostrare trasparente -> colore base.
                    u = (u - 0.5) * scaleU + 0.5;
                    v = (v - 0.5) * scaleV + 0.5;
                    nuoveUV.Add(new Point(u, v));
                }
                m.Geometry.TextureCoordinates = nuoveUV;
            }
        }

        // Calcola la matrice di trasformazione mondo della mesh usando esattamente lo stesso Transform3DGroup
        // che la pipeline di rendering applica (vedi MainWindow.MostraGalleria / MostraSingoloMassimizzato).
        // Usando .Value sul gruppo si ottiene la matrice composta identica al render -> niente discrepanze.
        private static Matrix3D MatriceMondoMesh(MeshData md)
        {
            var tGroup = new Transform3DGroup();
            tGroup.Children.Add(new ScaleTransform3D(md.ScaleXYZ, md.ScaleXYZ, md.ScaleXYZ));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), md.RotX)));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), md.RotY)));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), md.RotZ)));
            tGroup.Children.Add(new TranslateTransform3D(md.PosX, md.PosY, md.PosZ));
            return tGroup.Value;
        }

        // Legge l'aspect ratio (width/height) della skin dal primo target con NewTexturePath valido.
        // Ritorna 0 se nessuno ha skin (caso di proiezione senza immagine -> nessun aspect-preserving).
        private static double LeggiAspectImg(IList<MeshData> targets)
        {
            foreach (var m in targets)
            {
                if (string.IsNullOrEmpty(m?.NewTexturePath) || !File.Exists(m.NewTexturePath)) continue;
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(m.NewTexturePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    if (bmp.PixelHeight > 0) return bmp.PixelWidth / (double)bmp.PixelHeight;
                }
                catch { }
            }
            return 0;
        }

        // Ripristina le UV originali dal backup. Da chiamare quando si rimuove la skin utente.
        public static void RipristinaUVOriginali(IList<MeshData> targets)
        {
            if (targets == null) return;
            foreach (var m in targets)
            {
                if (m?.OriginalTextureCoordinates != null && m.Geometry != null)
                {
                    m.Geometry.TextureCoordinates = new PointCollection(m.OriginalTextureCoordinates);
                    m.OriginalTextureCoordinates = null;
                }
            }
        }

        // Assemblo il materiale WPF per il rendering 3D, gestendo i percorsi delle immagini
        public static Material CreaMaterialeWPF(MeshData md, string cartellaAttuale, string cartellaPadre)
        {
            MaterialGroup group = new MaterialGroup();
            Brush baseBrush = null;
            string texPath = !md.RemoveTexture ? (string.IsNullOrEmpty(md.NewTexturePath) ? md.TextureName : md.NewTexturePath) : "";

            if (!string.IsNullOrEmpty(texPath))
            {
                string fullPath = TrovaPercorsoTextura(cartellaAttuale, cartellaPadre, texPath);
                if (fullPath != null)
                {
                    try
                    {
                        // OnLoad + IgnoreImageCache evitano lock sul file e immagine stantia se l'utente cambia/ricarica la skin
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bmp.UriSource = new Uri(fullPath);
                        bmp.EndInit();
                        bmp.Freeze();

                        ImageBrush ib = new ImageBrush(bmp);
                        // HighQuality migliora il filtraggio sui pixel a cavallo delle curve (anti-aliasing del texture sampling)
                        RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.HighQuality);
                        ib.ViewportUnits = BrushMappingMode.Absolute;
                        if (!string.IsNullOrEmpty(md.NewTexturePath))
                        {
                            // Le UV sono state rigenerate da ApplicaProiezionePlanare e stanno in [0,1].
                            // moltiplicatore 1 = immagine combacia esattamente con la proiezione; >1 zoom in
                            // (centro dell'immagine), <1 zoom out con colore base ai lati.
                            // SkinOffsetU/V spostano la skin sulla superficie (offset locale in spazio UV).
                            // Niente RelativeTransform 180 perche' la proiezione ha gia la V invertita corretta.
                            double t = md.TextureScale > 0 ? md.TextureScale : 1.0;
                            double off = (1 - t) / 2; // centro-zoom: viewport centrato su (0.5, 0.5)
                            ib.Viewport = new Rect(off + md.SkinOffsetU, off + md.SkinOffsetV, t, t);
                            ib.TileMode = TileMode.None;
                        }
                        else
                        {
                            // Texture caricata da file .x al reload (skin salvata o texture nativa): uso il mapping naturale
                            // WPF, senza RelativeTransform. Cosi' la pipeline al reload combacia esattamente con la pipeline
                            // user-skin a edit time, e l'orientamento e' identico anche nel sw target (Steltronic Focus).
                            ib.TileMode = TileMode.Tile;
                            ib.Viewport = new Rect(0, 0, md.TextureScale, md.TextureScale);
                        }
                        ib.Opacity = md.Alpha;

                        baseBrush = ib;
                    }
                    catch { }
                }
            }

            if (baseBrush == null)
            {
                Color c = md.MeshColor;
                if (c.R == 0 && c.G == 0 && c.B == 0 && (md.Emissive.R > 0 || md.Emissive.G > 0)) c = md.Emissive;
                c.A = (byte)(md.Alpha * 255);
                baseBrush = new SolidColorBrush(c);
            }

            // Per le skin utente (TileMode.None) metto sotto un layer col colore base della mesh:
            // le aree UV fuori dal viewport della skin lasciano vedere questo colore invece di rimanere nere.
            if (!string.IsNullOrEmpty(md.NewTexturePath) && baseBrush is ImageBrush)
            {
                Color cBase = md.MeshColor;
                if (cBase.R == 0 && cBase.G == 0 && cBase.B == 0 && (md.Emissive.R > 0 || md.Emissive.G > 0)) cBase = md.Emissive;
                cBase.A = (byte)(md.Alpha * 255);
                group.Children.Add(new DiffuseMaterial(new SolidColorBrush(cBase)));
            }

            group.Children.Add(new DiffuseMaterial(baseBrush));
            if (md.Power > 0) group.Children.Add(new SpecularMaterial(new SolidColorBrush(md.Specular), md.Power));
            return group;
        }

        public static void AppiattisciGerarchia(IEnumerable<MeshData> nodi, List<MeshData> result)
        {
            foreach (var n in nodi)
            {
                result.Add(n);
                AppiattisciGerarchia(n.Children, result);
            }
        }

        // Trovo il file padre a partire da un pezzettino cliccato
        public static MeshData TrovaFileRootDiAppartenenza(MeshData rootNode, MeshData nodo)
        {
            if (nodo == null || rootNode == null) return null;

            List<MeshData> tuttiIFileX = new List<MeshData>();
            EstraiTuttiIFileX(rootNode, tuttiIFileX);

            foreach (var fileRoot in tuttiIFileX)
            {
                if (ContieneNodo(fileRoot, nodo)) return fileRoot;
            }
            return null;
        }

        private static bool ContieneNodo(MeshData root, MeshData target)
        {
            if (root == target) return true;
            foreach (var child in root.Children)
            {
                if (ContieneNodo(child, target)) return true;
            }
            return false;
        }

        public static bool EspandiGenitori(IEnumerable<MeshData> nodi, MeshData bersaglio)
        {
            foreach (var nodo in nodi)
            {
                if (nodo == bersaglio) return true;
                if (EspandiGenitori(nodo.Children, bersaglio))
                {
                    nodo.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        public static void DeselezionaRicorsivo(MeshData n)
        {
            n.IsSelected = false;
            foreach (var c in n.Children) DeselezionaRicorsivo(c);
        }

        public static Color EstraiColoreDaImmagine(string percorsoImmagine)
        {
            try
            {
                BitmapImage bmp = new BitmapImage(new Uri(percorsoImmagine, UriKind.Absolute));
                if (bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    CroppedBitmap cb = new CroppedBitmap(bmp, new Int32Rect(0, 0, 1, 1));
                    byte[] pixels = new byte[4];
                    cb.CopyPixels(pixels, 4, 0);
                    return Color.FromRgb(pixels[2], pixels[1], pixels[0]);
                }
            }
            catch { }
            return Colors.LightGray;
        }

        public static void SalvaTextureFisicamente(string sorgente, string destinazione, double rotazione)
        {
            // Per rotazione 0 (caso comune nel save) uso una semplice File.Copy: piu robusto del re-encoding
            // tramite BitmapEncoder, che falliva silenziosamente su immagini con format particolari.
            if (rotazione == 0)
            {
                try
                {
                    File.Copy(sorgente, destinazione, true);
                }
                catch { }
                return;
            }

            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(sorgente);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                TransformedBitmap rotated = new TransformedBitmap();
                rotated.BeginInit();
                rotated.Source = bmp;
                rotated.Transform = new RotateTransform(rotazione);
                rotated.EndInit();

                using (FileStream fs = new FileStream(destinazione, FileMode.Create))
                {
                    BitmapEncoder encoder = Path.GetExtension(destinazione).ToLower().Contains("jpg") ?
                        (BitmapEncoder)new JpegBitmapEncoder() : new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rotated));
                    encoder.Save(fs);
                }
            }
            catch { }
        }
    }
}