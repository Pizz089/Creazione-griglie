using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Creazione_griglie
{
    // Motore integrato per la generazione off-screen dell'anteprima icon.jpg
    public static class ThumbnailGenerator
    {
        public static void GeneraIcona(string cartellaDestinazione, MeshData masterRoot, string cartellaAttuale, string cartellaPadre)
        {
            try
            {
                List<MeshData> tuttiIFileX = new List<MeshData>();
                MeshHelper.EstraiTuttiIFileX(masterRoot, tuttiIFileX);

                MeshData playerNode = tuttiIFileX.FirstOrDefault(f => f.OriginalFileName.ToLower().Contains("p10") && f.OriginalFileName.ToLower().EndsWith("player.x"));
                if (playerNode == null) return;

                List<MeshData> partiPlayer = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(new List<MeshData> { playerNode }, partiPlayer);

                Model3DGroup iconGroup = new Model3DGroup();

                // Monto i pezzi 3D necessari alla foto
                foreach (var md in partiPlayer)
                {
                    if (md.IsGroup || md.Geometry.Positions.Count == 0) continue;

                    string nomeUpper = md.Name.ToUpper();
                    if (nomeUpper.Contains("_SP") || nomeUpper.Contains("_C") || nomeUpper.Contains("_T")) continue;

                    Material materiale = MeshHelper.CreaMaterialeWPF(md, cartellaAttuale, cartellaPadre);
                    GeometryModel3D modello = new GeometryModel3D(md.Geometry, materiale) { BackMaterial = materiale };

                    Transform3DGroup tGroup = new Transform3DGroup();
                    tGroup.Children.Add(new ScaleTransform3D(md.ScaleXYZ, md.ScaleXYZ, md.ScaleXYZ));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), md.RotX)));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), md.RotY)));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), md.RotZ)));
                    tGroup.Children.Add(new TranslateTransform3D(md.PosX, md.PosY, md.PosZ));

                    modello.Transform = tGroup;
                    iconGroup.Children.Add(modello);
                }

                if (iconGroup.Children.Count == 0) return;

                // Calcolo l'inquadratura perfetta
                Rect3D bounds = iconGroup.Bounds;
                double fovWidth = bounds.SizeX * 0.42;
                double fovHeight = bounds.SizeY;

                int altezzaRender = 150;
                int larghezzaRender = (int)(altezzaRender * (fovWidth / fovHeight));

                OrthographicCamera camera = new OrthographicCamera(
                    new Point3D(bounds.X + (fovWidth / 2), bounds.Y + (fovHeight / 2), bounds.Z + bounds.SizeZ + 100),
                    new Vector3D(0, 0, -1),
                    new Vector3D(0, 1, 0),
                    fovWidth
                );

                ModelVisual3D modelVisual = new ModelVisual3D { Content = iconGroup };
                Model3DGroup luciGroup = new Model3DGroup();
                luciGroup.Children.Add(new AmbientLight(Colors.White));
                ModelVisual3D luciVisual = new ModelVisual3D { Content = luciGroup };

                // Creo una scena invisibile all'utente e scatto la foto
                Viewport3D viewport = new Viewport3D { Width = larghezzaRender, Height = altezzaRender, Camera = camera };
                viewport.Children.Add(luciVisual);
                viewport.Children.Add(modelVisual);

                Grid contenitoreRender = new Grid { Width = larghezzaRender, Height = altezzaRender, Background = Brushes.Black };
                contenitoreRender.Children.Add(viewport);
                contenitoreRender.Measure(new Size(larghezzaRender, altezzaRender));
                contenitoreRender.Arrange(new Rect(0, 0, larghezzaRender, altezzaRender));
                contenitoreRender.UpdateLayout();

                RenderTargetBitmap rtb = new RenderTargetBitmap(larghezzaRender, altezzaRender, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(contenitoreRender);

                JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outPath = Path.Combine(cartellaDestinazione, "icon.jpg");
                using (FileStream fs = new FileStream(outPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }
            }
            catch { }
        }
    }
}