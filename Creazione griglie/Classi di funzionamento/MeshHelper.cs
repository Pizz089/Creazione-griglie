using System;
using System.Collections.Generic;
using System.IO;
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

        // Assemblo il materiale WPF per il rendering 3D, gestendo i percorsi delle immagini
        public static Material CreaMaterialeWPF(MeshData md, string cartellaAttuale, string cartellaPadre)
        {
            MaterialGroup group = new MaterialGroup();
            Brush baseBrush = null;
            string texPath = !md.RemoveTexture ? (string.IsNullOrEmpty(md.NewTexturePath) ? md.TextureName : md.NewTexturePath) : "";

            if (!string.IsNullOrEmpty(texPath))
            {
                string fullPath = File.Exists(Path.Combine(cartellaAttuale, texPath)) ? Path.Combine(cartellaAttuale, texPath) :
                                 (File.Exists(Path.Combine(cartellaPadre, texPath)) ? Path.Combine(cartellaPadre, texPath) : null);
                if (fullPath != null)
                {
                    try
                    {
                        ImageBrush ib = new ImageBrush(new BitmapImage(new Uri(fullPath)));
                        ib.TileMode = TileMode.Tile;
                        ib.ViewportUnits = BrushMappingMode.Absolute;
                        ib.Viewport = new Rect(0, 0, md.TextureScale, md.TextureScale);
                        ib.Opacity = md.Alpha;

                        if (md.TextureRotation != 0)
                        {
                            ib.RelativeTransform = new RotateTransform(md.TextureRotation, 0.5, 0.5);
                        }

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
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(sorgente);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                BitmapSource finale = bmp;
                if (rotazione != 0)
                {
                    TransformedBitmap rotated = new TransformedBitmap();
                    rotated.BeginInit();
                    rotated.Source = bmp;
                    rotated.Transform = new RotateTransform(rotazione);
                    rotated.EndInit();
                    finale = rotated;
                }

                using (FileStream fs = new FileStream(destinazione, FileMode.Create))
                {
                    BitmapEncoder encoder = Path.GetExtension(destinazione).ToLower().Contains("jpg") ?
                        (BitmapEncoder)new JpegBitmapEncoder() : new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(finale));
                    encoder.Save(fs);
                }
            }
            catch { }
        }
    }
}