using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Creazione_griglie
{
    public partial class ModernColorPicker : Window
    {
        public Color FinalDiffuse { get; private set; }
        public Color FinalSpecular { get; private set; }
        public Color FinalEmissive { get; private set; }
        public double FinalPower { get; private set; }
        public double FinalAlpha { get; private set; }
        public Color SelectedColor { get; private set; }

        private bool isUpdating = false;

        private GeometryModel3D gmFront;
        private GeometryModel3D gmIso;
        private GeometryModel3D gmIsoUp;
        private GeometryModel3D gm45;

        public List<SolidColorBrush> ListaPredefiniti { get; set; }
        public static ObservableCollection<SolidColorBrush> ListaRecenti { get; set; } = new ObservableCollection<SolidColorBrush>();

        public ModernColorPicker(Color diffuse, double alpha, Color specular, Color emissive, double power)
        {
            InitializeComponent();

            ListaPredefiniti = new List<SolidColorBrush>
            {
                new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                new SolidColorBrush(Color.FromRgb(233, 30, 99)),
                new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                new SolidColorBrush(Color.FromRgb(103, 58, 183)),
                new SolidColorBrush(Color.FromRgb(63, 81, 181)),
                new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                new SolidColorBrush(Color.FromRgb(3, 169, 244)),
                new SolidColorBrush(Color.FromRgb(0, 188, 212)),
                new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                new SolidColorBrush(Color.FromRgb(139, 195, 74)),
                new SolidColorBrush(Color.FromRgb(205, 220, 57)),
                new SolidColorBrush(Color.FromRgb(255, 235, 59)),
                new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                new SolidColorBrush(Color.FromRgb(255, 87, 34)),
                new SolidColorBrush(Color.FromRgb(121, 85, 72)),
                new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };

            FinalDiffuse = diffuse;
            FinalAlpha = alpha;
            FinalSpecular = specular;
            FinalEmissive = emissive;
            FinalPower = power;

            InizializzaMotore3D();
            DataContext = this;
            Canale_Changed(radDiffuse, null);
        }

        private void InizializzaMotore3D()
        {
            MeshGeometry3D sphereMesh = GeneraSfera(30, 30);

            gmFront = new GeometryModel3D(sphereMesh, null);
            gmIso = new GeometryModel3D(sphereMesh, null);
            gmIsoUp = new GeometryModel3D(sphereMesh, null);
            gm45 = new GeometryModel3D(sphereMesh, null);

            ConfiguraViewport(mvFront, gmFront);
            ConfiguraViewport(mvIso, gmIso);
            ConfiguraViewport(mvIsoUp, gmIsoUp);
            ConfiguraViewport(mv45, gm45);

            AggiornaMateriale3D();
        }

        private void ConfiguraViewport(ModelVisual3D mv, GeometryModel3D model)
        {
            Model3DGroup group = new Model3DGroup();
            group.Children.Add(new AmbientLight(Color.FromRgb(100, 100, 100)));
            group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
            group.Children.Add(new DirectionalLight(Color.FromRgb(50, 50, 50), new Vector3D(1, 1, 1)));

            group.Children.Add(model);
            mv.Content = group;
        }

        private MeshGeometry3D GeneraSfera(int slices, int stacks)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI / 2 - stack * Math.PI / stacks;
                double y = Math.Sin(phi);
                double scale = Math.Cos(phi);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = slice * 2 * Math.PI / slices;
                    double x = scale * Math.Sin(theta);
                    double z = scale * Math.Cos(theta);
                    Vector3D normal = new Vector3D(x, y, z);
                    mesh.Positions.Add(new Point3D(x, y, z));
                    mesh.Normals.Add(normal);
                }
            }
            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int n = slice + stack * (slices + 1);
                    mesh.TriangleIndices.Add(n);
                    mesh.TriangleIndices.Add(n + slices + 1);
                    mesh.TriangleIndices.Add(n + 1);

                    mesh.TriangleIndices.Add(n + 1);
                    mesh.TriangleIndices.Add(n + slices + 1);
                    mesh.TriangleIndices.Add(n + slices + 2);
                }
            }
            return mesh;
        }

        private void Canale_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdating || slA == null || slR == null || slG == null || slB == null || lblAlpha == null) return;

            isUpdating = true;

            Color activeColor = Colors.Black;

            if (radDiffuse?.IsChecked == true)
            {
                activeColor = FinalDiffuse;
                slA.Value = FinalAlpha * 255;
                slA.IsEnabled = true;
                txtA.IsEnabled = true;
                lblAlpha.Opacity = 1.0;
            }
            else if (radSpecular?.IsChecked == true)
            {
                activeColor = FinalSpecular;
                slA.IsEnabled = false;
                txtA.IsEnabled = false;
                lblAlpha.Opacity = 0.3;
            }
            else if (radEmissive?.IsChecked == true)
            {
                activeColor = FinalEmissive;
                slA.IsEnabled = false;
                txtA.IsEnabled = false;
                lblAlpha.Opacity = 0.3;
            }

            slR.Value = activeColor.R;
            slG.Value = activeColor.G;
            slB.Value = activeColor.B;
            slPower.Value = FinalPower;

            AggiornaTestiColori();
            isUpdating = false;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdating || slR == null || slG == null || slB == null || slA == null || slPower == null) return;

            AggiornaValoriGlobali();
            AggiornaTestiColori();
            AggiornaMateriale3D();
        }

        private void Txt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdating) return;

            if (sender is TextBox txt && txt.IsFocused)
            {
                if (double.TryParse(txt.Text, out double val))
                {
                    isUpdating = true;

                    if (txt == txtR) slR.Value = val;
                    else if (txt == txtG) slG.Value = val;
                    else if (txt == txtB) slB.Value = val;
                    else if (txt == txtA) slA.Value = val;
                    else if (txt == txtPower) slPower.Value = val;

                    AggiornaValoriGlobali();
                    AggiornaTestiColori();
                    AggiornaMateriale3D();

                    isUpdating = false;
                }
            }
        }

        private void AggiornaValoriGlobali()
        {
            Color newC = Color.FromRgb((byte)slR.Value, (byte)slG.Value, (byte)slB.Value);
            FinalPower = slPower.Value;

            if (radDiffuse?.IsChecked == true)
            {
                FinalDiffuse = newC;
                FinalAlpha = slA.Value / 255.0;
            }
            else if (radSpecular?.IsChecked == true)
            {
                FinalSpecular = newC;
            }
            else if (radEmissive?.IsChecked == true)
            {
                FinalEmissive = newC;
            }
        }

        private void AggiornaTestiColori()
        {
            if (txtR != null && !txtR.IsFocused) txtR.Text = ((int)slR.Value).ToString();
            if (txtG != null && !txtG.IsFocused) txtG.Text = ((int)slG.Value).ToString();
            if (txtB != null && !txtB.IsFocused) txtB.Text = ((int)slB.Value).ToString();
            if (txtA != null && !txtA.IsFocused) txtA.Text = ((int)slA.Value).ToString();
            if (txtPower != null && !txtPower.IsFocused) txtPower.Text = FinalPower.ToString("0.0");

            Color preview = Color.FromRgb((byte)slR.Value, (byte)slG.Value, (byte)slB.Value);
            if (radDiffuse?.IsChecked == true) preview.A = (byte)slA.Value;

            if (ColorPreview != null) ColorPreview.Background = new SolidColorBrush(preview);
        }

        private void AggiornaMateriale3D()
        {
            if (gmFront == null) return;

            MaterialGroup mg = new MaterialGroup();

            Color diff = FinalDiffuse;
            diff.A = (byte)(FinalAlpha * 255);
            mg.Children.Add(new DiffuseMaterial(new SolidColorBrush(diff)));

            if (FinalEmissive.R > 0 || FinalEmissive.G > 0 || FinalEmissive.B > 0)
            {
                mg.Children.Add(new EmissiveMaterial(new SolidColorBrush(FinalEmissive)));
            }

            if (FinalPower > 0 && (FinalSpecular.R > 0 || FinalSpecular.G > 0 || FinalSpecular.B > 0))
            {
                mg.Children.Add(new SpecularMaterial(new SolidColorBrush(FinalSpecular), FinalPower));
            }

            gmFront.Material = mg;
            gmIso.Material = mg;
            gmIsoUp.Material = mg;
            gm45.Material = mg;
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedColor = brush.Color;

                slR.Value = SelectedColor.R;
                slG.Value = SelectedColor.G;
                slB.Value = SelectedColor.B;

                if (radDiffuse?.IsChecked == true)
                {
                    slA.Value = 255;
                }
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Salvo il colore correntemente a schermo in memoria (risolvendo il problema del mancato salvataggio se si muovono solo gli slider)
            var existing = ListaRecenti.FirstOrDefault(b => b.Color == FinalDiffuse);

            if (existing != null)
            {
                ListaRecenti.Remove(existing);
            }

            ListaRecenti.Insert(0, new SolidColorBrush(FinalDiffuse));

            if (ListaRecenti.Count > 10)
            {
                ListaRecenti.RemoveAt(ListaRecenti.Count - 1);
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}