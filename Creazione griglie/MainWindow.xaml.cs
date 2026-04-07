using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml;

namespace Creazione_griglie
{
    // Struttura dati per le miniature
    public class ViewportItem
    {
        public string Titolo { get; set; }
        public string Categoria { get; set; }
        public Model3DGroup Modello { get; set; }
        public MeshData FileRoot { get; set; }
    }

    public class GalleriaGroup
    {
        public string NomeGruppo { get; set; }
        public ObservableCollection<ViewportItem> Elementi { get; set; } = new ObservableCollection<ViewportItem>();
    }

    public class MementoState
    {
        public MeshData Target { get; private set; }
        public Color MeshColor { get; private set; }
        public double Alpha { get; private set; }
        public Color Specular { get; private set; }
        public Color Emissive { get; private set; }
        public double Power { get; private set; }
        public bool HasColor { get; private set; }
        public bool RemoveTexture { get; private set; }
        public string NewTexturePath { get; private set; }
        public double TextureScale { get; private set; }
        public double TextureRotation { get; private set; }

        public MementoState(MeshData target)
        {
            Target = target;
            MeshColor = target.MeshColor;
            Alpha = target.Alpha;
            Specular = target.Specular;
            Emissive = target.Emissive;
            Power = target.Power;
            HasColor = target.HasColor;
            RemoveTexture = target.RemoveTexture;
            NewTexturePath = target.NewTexturePath;
            TextureScale = target.TextureScale;
            TextureRotation = target.TextureRotation;
        }

        public void Restore(string cartellaAttuale, string cartellaPadre)
        {
            Target.MeshColor = MeshColor;
            Target.Alpha = Alpha;
            Target.Specular = Specular;
            Target.Emissive = Emissive;
            Target.Power = Power;
            Target.HasColor = HasColor;
            Target.RemoveTexture = RemoveTexture;
            Target.NewTexturePath = NewTexturePath;
            Target.TextureScale = TextureScale;
            Target.TextureRotation = TextureRotation;

            Target.OriginalMaterial = MeshHelper.CreaMaterialeWPF(Target, cartellaAttuale, cartellaPadre);
            if (Target.Model3D != null)
            {
                Target.Model3D.Material = Target.OriginalMaterial;
                Target.Model3D.BackMaterial = Target.OriginalMaterial;
            }
        }
    }

    public class UndoManager
    {
        private LinkedList<List<MementoState>> history = new LinkedList<List<MementoState>>();
        private int maxHistory = 50;

        public void PushState(List<MementoState> state)
        {
            history.AddLast(state);
            if (history.Count > maxHistory)
            {
                history.RemoveFirst();
            }
        }

        public List<MementoState> PopState()
        {
            if (history.Count == 0) return null;
            var state = history.Last.Value;
            history.RemoveLast();
            return state;
        }

        public void Clear() => history.Clear();
        public bool CanUndo => history.Count > 0;
    }

    public partial class MainWindow : Window
    {
        private string BaseStylesPath;
        private string InternalStylesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaseStyles");

        private string cartellaAttuale = "";
        private string cartellaPadre = "";

        private ObservableCollection<MeshData> alberoGerarchico = new ObservableCollection<MeshData>();
        public ObservableCollection<GalleriaGroup> GruppiGalleria { get; set; } = new ObservableCollection<GalleriaGroup>();

        private MeshData pezzoSelezionato;
        private MeshData fileAttivoVisibile = null;
        private bool isUpdatingZoom = false;

        private UndoManager _undoManager = new UndoManager();

        public MainWindow()
        {
            InitializeComponent();

            treeComponenti.ItemsSource = alberoGerarchico;
            listaGruppiGalleria.ItemsSource = GruppiGalleria;

            btnSalva.IsEnabled = false;
            BaseStylesPath = StyleEnvironment.TrovaPercorsoStili();

            if (string.IsNullOrEmpty(BaseStylesPath))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgCartellaNonTrovata") as string ?? "Cartella non trovata",
                                Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(hwnd, 2);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);

                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RegistraStatoUndo(List<MeshData> targets)
        {
            if (targets == null || targets.Count == 0) return;

            var stati = targets.Select(t => new MementoState(t)).ToList();
            _undoManager.PushState(stati);
            btnUndo.IsEnabled = true;
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var statiToRestore = _undoManager.PopState();
            if (statiToRestore != null)
            {
                foreach (var s in statiToRestore)
                {
                    s.Restore(cartellaAttuale, cartellaPadre);
                }

                ApplicaTrasparenzaAlModello3D();
                AggiornaPannelloProprieta();
            }
            btnUndo.IsEnabled = _undoManager.CanUndo;
        }

        private void BtnCarica_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(BaseStylesPath) || !Directory.Exists(BaseStylesPath))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgCartellaNonTrovata") as string ?? "Cartella non trovata",
                                Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StyleSelectorWindow selWindow = new StyleSelectorWindow(BaseStylesPath);
            selWindow.Owner = this;

            if (selWindow.ShowDialog() == true && !string.IsNullOrEmpty(selWindow.SelectedStyleName))
            {
                string percorsoScelto = Path.Combine(BaseStylesPath, selWindow.SelectedStyleName);
                EseguiCaricamentoFisico(percorsoScelto);
            }
        }

        private void EseguiCaricamentoFisico(string percorsoScelto)
        {
            try
            {
                cartellaAttuale = Path.GetFullPath(percorsoScelto).TrimEnd(Path.DirectorySeparatorChar);
                cartellaPadre = Directory.GetParent(cartellaAttuale)?.FullName ?? cartellaAttuale;

                alberoGerarchico.Clear();

                MeshData masterRoot = new MeshData
                {
                    Name = Path.GetFileName(cartellaAttuale).ToUpper(),
                    IsGroup = true,
                    IsExpanded = true
                };

                string[] fileXml = Directory.GetFiles(cartellaAttuale, "*.xml", SearchOption.AllDirectories);
                string[] fileX = Directory.GetFiles(cartellaAttuale, "*.x", SearchOption.AllDirectories);

                int fileCaricatiConSuccesso = 0;

                foreach (string file in fileX)
                {
                    try
                    {
                        string nomeFile = Path.GetFileName(file);
                        string percorsoRelativo = file.Replace(cartellaAttuale, "").TrimStart('\\', '/');

                        string dirLower = Path.GetDirectoryName(percorsoRelativo).ToLower();
                        string nomeFileLower = nomeFile.ToLower();

                        bool includi = false;
                        string nomeCartellaCustom = "";

                        if (dirLower == "" && nomeFileLower == "recap.x")
                        {
                            includi = true;
                        }
                        else if (!nomeFileLower.StartsWith("t_"))
                        {
                            string cartellaBase = dirLower.Split(new char[] { '\\', '/' }).FirstOrDefault() ?? "";

                            if (cartellaBase == "p10") { includi = true; nomeCartellaCustom = "10 Pin"; }
                            else if (cartellaBase == "p5") { includi = true; nomeCartellaCustom = "5 Pin"; }
                            else if (cartellaBase == "duck") { includi = true; nomeCartellaCustom = "Duck Pin"; }
                            else if (cartellaBase == "candle") { includi = true; nomeCartellaCustom = "Candle Pin"; }
                        }

                        if (!includi) continue;

                        string percorsoVisivo = string.IsNullOrEmpty(nomeCartellaCustom) ? nomeFile : Path.Combine(nomeCartellaCustom, nomeFile);

                        string testoX = File.ReadAllText(file);
                        List<MeshData> datiEstratti = XFileEngine.Parse(testoX);

                        double pX = 0, pY = 0, pZ = 0, rX = 0, rY = 0, rZ = 0, scala = 1;

                        XmlHelper.LeggiCoordinateDaXML(fileXml, nomeFile, ref pX, ref pY, ref pZ, ref rX, ref rY, ref rZ, ref scala);

                        MeshData cartellaDestinazione = MeshHelper.CostruisciAlberoCartelle(masterRoot, percorsoVisivo);
                        string nomePulito = Path.GetFileNameWithoutExtension(nomeFile);

                        MeshData fileRoot = new MeshData
                        {
                            Name = nomePulito,
                            IsGroup = true,
                            IsExpanded = false,
                            OriginalXFileContent = testoX,
                            OriginalFileName = percorsoRelativo
                        };

                        foreach (var nodo in datiEstratti)
                        {
                            MeshHelper.ApplicaCoordinateRicorsivo(nodo, pX, pY, pZ, rX, rY, rZ, scala);
                            fileRoot.Children.Add(nodo);
                        }

                        cartellaDestinazione.Children.Add(fileRoot);
                        fileCaricatiConSuccesso++;
                    }
                    catch (Exception) { }
                }

                alberoGerarchico.Add(masterRoot);

                XmlHelper.ConfiguraLuciDaXML(fileXml, luciGroup);
                ApplicaSfondoDaXML();
                SvuotaSelezione();
                ImpostaVistaU();

                btnSalva.IsEnabled = !StyleEnvironment.IsStyleProtected(Path.GetFileName(cartellaAttuale));

                _undoManager.Clear();
                btnUndo.IsEnabled = false;

                if (fileCaricatiConSuccesso > 0)
                    txtStatus.Text = $"Stile caricato con {fileCaricatiConSuccesso} componenti. / Style loaded with {fileCaricatiConSuccesso} components.";
                else
                    txtStatus.Text = $"Attenzione: nessun componente trovato. / Warning: no components found.";

                masterRoot.IsSelected = true;
            }
            catch (Exception ex) { MessageBox.Show("Errore caricamento: " + ex.Message); }
        }

        private void MostraGalleria(MeshData cartella)
        {
            GruppiGalleria.Clear();
            pannelloGalleria.Visibility = Visibility.Visible;
            pannelloMassimizzato.Visibility = Visibility.Collapsed;
            fileAttivoVisibile = null;

            List<MeshData> tuttiIFile = new List<MeshData>();
            MeshHelper.EstraiTuttiIFileX(cartella, tuttiIFile);

            List<ViewportItem> elementiTemporanei = new List<ViewportItem>();

            foreach (var fileRoot in tuttiIFile)
            {
                string nomeCategoria = "Generale";
                MeshData padre = TrovaPadre(alberoGerarchico.Count > 0 ? alberoGerarchico[0] : cartella, fileRoot);
                if (padre != null && padre.IsGroup && padre != alberoGerarchico[0])
                {
                    nomeCategoria = padre.Name;
                }

                Model3DGroup mg = new Model3DGroup();
                List<MeshData> tuttiPezzi = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(new List<MeshData> { fileRoot }, tuttiPezzi);

                foreach (var md in tuttiPezzi)
                {
                    if (md.IsGroup || md.Geometry.Positions.Count == 0) continue;

                    if (md.OriginalMaterial == null)
                        md.OriginalMaterial = MeshHelper.CreaMaterialeWPF(md, cartellaAttuale, cartellaPadre);

                    GeometryModel3D modelloGalleria = new GeometryModel3D(md.Geometry, md.OriginalMaterial)
                    {
                        BackMaterial = md.OriginalMaterial
                    };

                    Transform3DGroup tGroup = new Transform3DGroup();
                    tGroup.Children.Add(new ScaleTransform3D(md.ScaleXYZ, md.ScaleXYZ, md.ScaleXYZ));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), md.RotX)));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), md.RotY)));
                    tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), md.RotZ)));
                    tGroup.Children.Add(new TranslateTransform3D(md.PosX, md.PosY, md.PosZ));

                    modelloGalleria.Transform = tGroup;
                    mg.Children.Add(modelloGalleria);
                }

                elementiTemporanei.Add(new ViewportItem { Titolo = fileRoot.Name, Categoria = nomeCategoria, Modello = mg, FileRoot = fileRoot });
            }

            var raggruppamento = elementiTemporanei
                .GroupBy(x => x.Categoria)
                .OrderBy(g => g.Key.ToLower() == "10 pin" ? 0 : 1)
                .ThenBy(g => g.Key);

            foreach (var gruppo in raggruppamento)
            {
                var nuovoGruppo = new GalleriaGroup { NomeGruppo = gruppo.Key };
                foreach (var elemento in gruppo)
                {
                    nuovoGruppo.Elementi.Add(elemento);
                }
                GruppiGalleria.Add(nuovoGruppo);
            }
        }

        private void MostraSingoloMassimizzato(MeshData fileRoot)
        {
            pannelloGalleria.Visibility = Visibility.Collapsed;
            pannelloMassimizzato.Visibility = Visibility.Visible;

            modelGroupMassimizzato.Children.Clear();
            fileAttivoVisibile = fileRoot;
            if (fileAttivoVisibile == null) return;

            List<MeshData> tutti = new List<MeshData>();
            MeshHelper.AppiattisciGerarchia(new List<MeshData> { fileAttivoVisibile }, tutti);

            foreach (var md in tutti)
            {
                if (md.IsGroup || md.Geometry.Positions.Count == 0) continue;

                if (md.OriginalMaterial == null)
                    md.OriginalMaterial = MeshHelper.CreaMaterialeWPF(md, cartellaAttuale, cartellaPadre);

                GeometryModel3D modello = new GeometryModel3D(md.Geometry, md.OriginalMaterial);
                modello.BackMaterial = md.OriginalMaterial;

                Transform3DGroup tGroup = new Transform3DGroup();
                tGroup.Children.Add(new ScaleTransform3D(md.ScaleXYZ, md.ScaleXYZ, md.ScaleXYZ));
                tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), md.RotX)));
                tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), md.RotY)));
                tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), md.RotZ)));
                tGroup.Children.Add(new TranslateTransform3D(md.PosX, md.PosY, md.PosZ));

                modello.Transform = tGroup;
                md.Model3D = modello;

                modelGroupMassimizzato.Children.Add(modello);
            }

            ImpostaVistaU();
        }

        private void ImpostaVistaU()
        {
            Point3D cameraPos = new Point3D(0, 0, 100);
            Vector3D lookDir = new Vector3D(0, 0, -100);
            Vector3D upDir = new Vector3D(0, 1, 0);

            viewPortMassimizzato.SetView(cameraPos, lookDir, upDir, 500);
            viewPortMassimizzato.ZoomExtents();
        }

        private void BtnIngrandisci_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ViewportItem item)
            {
                MostraSingoloMassimizzato(item.FileRoot);

                item.FileRoot.IsSelected = true;
                MeshHelper.EspandiGenitori(alberoGerarchico, item.FileRoot);

                pezzoSelezionato = null;
                AggiornaPannelloProprieta();
                ApplicaTrasparenzaAlModello3D();
            }
        }

        private void BtnTornaGalleria_Click(object sender, RoutedEventArgs e)
        {
            if (fileAttivoVisibile != null && alberoGerarchico.Count > 0)
            {
                MeshData cartellaPadre = TrovaPadre(alberoGerarchico[0], fileAttivoVisibile);
                if (cartellaPadre != null)
                {
                    cartellaPadre.IsSelected = true;
                    MostraGalleria(cartellaPadre);

                    pezzoSelezionato = null;
                    AggiornaPannelloProprieta();
                }
            }
        }

        private MeshData TrovaPadre(MeshData root, MeshData figlioCercato)
        {
            if (root == null || root.Children == null) return null;
            if (root.Children.Contains(figlioCercato)) return root;

            foreach (var child in root.Children)
            {
                var ris = TrovaPadre(child, figlioCercato);
                if (ris != null) return ris;
            }
            return null;
        }

        private List<MeshData> TrovaElementiCoinvolti()
        {
            List<MeshData> targets = new List<MeshData>();

            if (chkApplicaATutti.IsChecked == true && pezzoSelezionato != null)
            {
                List<MeshData> tutti = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(alberoGerarchico, tutti);

                string texRif = pezzoSelezionato.RemoveTexture ? "" :
                    (string.IsNullOrEmpty(pezzoSelezionato.NewTexturePath) ? pezzoSelezionato.TextureName : pezzoSelezionato.NewTexturePath);

                foreach (var m in tutti)
                {
                    if (m.IsGroup) continue;

                    string mTex = m.RemoveTexture ? "" :
                        (string.IsNullOrEmpty(m.NewTexturePath) ? m.TextureName : m.NewTexturePath);

                    bool haStessaTexture = !string.IsNullOrEmpty(texRif) && mTex == texRif;
                    bool haStessoColoreSenzaTexture = string.IsNullOrEmpty(texRif) && string.IsNullOrEmpty(mTex) && m.MeshColor == pezzoSelezionato.MeshColor;

                    if (haStessaTexture || haStessoColoreSenzaTexture)
                    {
                        targets.Add(m);
                    }
                }
            }
            else if (pezzoSelezionato != null)
            {
                targets.Add(pezzoSelezionato);
            }

            return targets;
        }

        private void TreeComponenti_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            MeshData nuovoSelezionato = treeComponenti.SelectedItem as MeshData;
            if (nuovoSelezionato == null) return;

            if (nuovoSelezionato.IsGroup && string.IsNullOrEmpty(nuovoSelezionato.OriginalXFileContent))
            {
                MostraGalleria(nuovoSelezionato);
                pezzoSelezionato = null;
            }
            else
            {
                MeshData nuovoFileAttivo = MeshHelper.TrovaFileRootDiAppartenenza(alberoGerarchico.Count > 0 ? alberoGerarchico[0] : null, nuovoSelezionato);
                if (nuovoFileAttivo != null && nuovoFileAttivo != fileAttivoVisibile)
                {
                    MostraSingoloMassimizzato(nuovoFileAttivo);
                }

                pezzoSelezionato = nuovoSelezionato.IsGroup ? null : nuovoSelezionato;
            }

            AggiornaPannelloProprieta();
            ApplicaTrasparenzaAlModello3D();
        }

        private void ViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(viewPortMassimizzato);
            HitTestResult result = VisualTreeHelper.HitTest(viewPortMassimizzato, p);

            if (result is RayMeshGeometry3DHitTestResult ray && ray.ModelHit is GeometryModel3D hit)
            {
                List<MeshData> tutti = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(alberoGerarchico, tutti);

                foreach (var m in tutti)
                {
                    if (m.Model3D == hit)
                    {
                        SvuotaSelezione();
                        m.IsSelected = true;

                        pezzoSelezionato = m;
                        AggiornaPannelloProprieta();
                        ApplicaTrasparenzaAlModello3D();
                        MeshHelper.EspandiGenitori(alberoGerarchico, m);

                        return;
                    }
                }
            }
            SvuotaSelezione();
        }

        private void AggiornaPannelloProprieta()
        {
            isUpdatingZoom = true;
            if (pezzoSelezionato != null)
            {
                pannelloProprieta.IsEnabled = true;
                txtNomeSelezionato.Text = pezzoSelezionato.Name;
                rectColore.Background = new SolidColorBrush(pezzoSelezionato.MeshColor);

                string labelNessuna = Application.Current.TryFindResource("StrNessuna") as string ?? "Nessuna";
                txtTexturePath.Text = pezzoSelezionato.RemoveTexture ? labelNessuna : Path.GetFileName(pezzoSelezionato.TextureName);

                slZoomSkin.Value = pezzoSelezionato.TextureScale;
                txtZoomSkin.Text = pezzoSelezionato.TextureScale.ToString("0.00");
            }
            else
            {
                pannelloProprieta.IsEnabled = false;
                txtNomeSelezionato.Text = Application.Current.TryFindResource("StrNessunaSelezione") as string ?? "Nessuna selezione";
            }
            isUpdatingZoom = false;
        }

        private void Zoom_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (pezzoSelezionato != null && !isUpdatingZoom)
            {
                RegistraStatoUndo(TrovaElementiCoinvolti());
            }
        }

        private void SlZoomSkin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingZoom || pezzoSelezionato == null) return;

            isUpdatingZoom = true;
            if (!txtZoomSkin.IsFocused) txtZoomSkin.Text = slZoomSkin.Value.ToString("0.00");
            ApplicaZoomAiModelli(slZoomSkin.Value);
            isUpdatingZoom = false;
        }

        private void TxtZoomSkin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingZoom || pezzoSelezionato == null) return;

            if (txtZoomSkin.IsFocused && double.TryParse(txtZoomSkin.Text, out double val))
            {
                isUpdatingZoom = true;
                slZoomSkin.Value = val;
                ApplicaZoomAiModelli(val);
                isUpdatingZoom = false;
            }
        }

        private void ApplicaZoomAiModelli(double valoreZoom)
        {
            foreach (var m in TrovaElementiCoinvolti())
            {
                m.TextureScale = valoreZoom;
                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);

                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
        }

        private void BtnAutoFitSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato?.Geometry == null || pezzoSelezionato.Geometry.TextureCoordinates.Count == 0) return;

            RegistraStatoUndo(TrovaElementiCoinvolti());

            double maxU = 0;
            foreach (var uv in pezzoSelezionato.Geometry.TextureCoordinates) if (uv.X > maxU) maxU = uv.X;
            slZoomSkin.Value = maxU > 0 ? maxU : 1.0;
        }

        private void RectColore_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato != null)
            {
                Color startDiffuse = pezzoSelezionato.HasColor ? pezzoSelezionato.MeshColor : Colors.LightGray;
                double startAlpha = pezzoSelezionato.Alpha;
                Color startSpecular = pezzoSelezionato.Specular;
                Color startEmissive = pezzoSelezionato.Emissive;
                double startPower = pezzoSelezionato.Power;

                string texPath = pezzoSelezionato.RemoveTexture ? "" : (string.IsNullOrEmpty(pezzoSelezionato.NewTexturePath) ? pezzoSelezionato.TextureName : pezzoSelezionato.NewTexturePath);
                if (!string.IsNullOrEmpty(texPath))
                {
                    string pathTrovato = File.Exists(Path.Combine(cartellaAttuale, texPath)) ? Path.Combine(cartellaAttuale, texPath) :
                        (File.Exists(Path.Combine(cartellaPadre, texPath)) ? Path.Combine(cartellaPadre, texPath) : null);
                    if (pathTrovato != null)
                    {
                        startDiffuse = MeshHelper.EstraiColoreDaImmagine(pathTrovato);
                        startSpecular = Color.FromRgb(60, 60, 60);
                        startPower = 40.0;
                        startEmissive = Colors.Black;
                    }
                }

                ModernColorPicker cp = new ModernColorPicker(startDiffuse, startAlpha, startSpecular, startEmissive, startPower);
                cp.Owner = this;

                if (cp.ShowDialog() == true)
                {
                    List<MeshData> targetMeshes = TrovaElementiCoinvolti();
                    RegistraStatoUndo(targetMeshes);

                    foreach (var m in targetMeshes)
                    {
                        m.MeshColor = cp.FinalDiffuse;
                        m.Alpha = cp.FinalAlpha;
                        m.Specular = cp.FinalSpecular;
                        m.Emissive = cp.FinalEmissive;
                        m.Power = cp.FinalPower;
                        m.HasColor = true;
                        m.RemoveTexture = true;
                        m.NewTexturePath = "";

                        m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                        if (m.Model3D != null)
                        {
                            m.Model3D.Material = m.OriginalMaterial;
                            m.Model3D.BackMaterial = m.OriginalMaterial;
                        }
                    }

                    ApplicaTrasparenzaAlModello3D();
                    AggiornaPannelloProprieta();
                    txtStatus.Text = $"Materiale applicato a {targetMeshes.Count} elementi. / Material applied.";
                }
            }
        }

        private void BtnSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato == null) return;
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Immagini|*.png;*.jpg;*.bmp" };

            if (ofd.ShowDialog() == true)
            {
                List<MeshData> targetMeshes = TrovaElementiCoinvolti();
                RegistraStatoUndo(targetMeshes);

                foreach (var m in targetMeshes)
                {
                    m.NewTexturePath = ofd.FileName;
                    m.RemoveTexture = false;
                    m.TextureRotation = 180;

                    m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                    if (m.Model3D != null)
                    {
                        m.Model3D.Material = m.OriginalMaterial;
                        m.Model3D.BackMaterial = m.OriginalMaterial;
                    }
                }
                AggiornaPannelloProprieta();
                txtStatus.Text = $"Skin caricata su {targetMeshes.Count} elementi. / Skin loaded.";
            }
        }

        private void BtnRemoveSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato == null) return;

            List<MeshData> targetMeshes = TrovaElementiCoinvolti();
            RegistraStatoUndo(targetMeshes);

            foreach (var m in targetMeshes)
            {
                m.RemoveTexture = true;
                m.TextureRotation = 0;
                m.Specular = Color.FromRgb(60, 60, 60);
                m.Power = 40.0;
                m.Emissive = Colors.Black;

                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
            AggiornaPannelloProprieta();
        }

        private void ApplicaTrasparenzaAlModello3D()
        {
            if (fileAttivoVisibile == null) return;

            List<MeshData> visibili = new List<MeshData>();
            MeshHelper.AppiattisciGerarchia(new List<MeshData> { fileAttivoVisibile }, visibili);

            foreach (var m in visibili)
            {
                if (m.Model3D == null || m.OriginalMaterial == null) continue;

                if (pezzoSelezionato == null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
                else if (m == pezzoSelezionato)
                {
                    MaterialGroup highlightGroup = new MaterialGroup();
                    highlightGroup.Children.Add(m.OriginalMaterial);

                    Color coloreTema = (Color)ColorConverter.ConvertFromString("#FF1E88E5");
                    Color highlightColor = Color.FromRgb((byte)(coloreTema.R * 0.3), (byte)(coloreTema.G * 0.3), (byte)(coloreTema.B * 0.3));
                    highlightGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(highlightColor)));

                    m.Model3D.Material = highlightGroup;
                    m.Model3D.BackMaterial = highlightGroup;
                }
                else
                {
                    Material copia = m.OriginalMaterial.Clone();
                    if (copia is MaterialGroup g)
                    {
                        foreach (var c in g.Children)
                        {
                            if (c is DiffuseMaterial d && d.Brush != null)
                            {
                                d.Brush = d.Brush.Clone();
                                d.Brush.Opacity = 0.2;
                            }
                        }
                    }
                    else if (copia is DiffuseMaterial d && d.Brush != null)
                    {
                        d.Brush = d.Brush.Clone();
                        d.Brush.Opacity = 0.2;
                    }

                    m.Model3D.Material = copia;
                    m.Model3D.BackMaterial = copia;
                }
            }
        }

        private void SvuotaSelezione()
        {
            pezzoSelezionato = null;
            foreach (var m in alberoGerarchico) MeshHelper.DeselezionaRicorsivo(m);

            AggiornaPannelloProprieta();
            ApplicaTrasparenzaAlModello3D();
        }

        private void RectColore_Click(object sender, MouseButtonEventArgs e) => RectColore_Click(sender, (RoutedEventArgs)e);

        private void BtnSalva_Click(object sender, RoutedEventArgs e)
        {
            if (alberoGerarchico.Count == 0 || string.IsNullOrEmpty(cartellaAttuale)) return;

            string nomeCartellaAttuale = Path.GetFileName(cartellaAttuale);

            if (StyleEnvironment.IsStyleProtected(nomeCartellaAttuale))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgStileProtetto") as string ?? "Impossibile sovrascrivere lo stile di sistema.",
                                Application.Current.TryFindResource("MsgAttenzione") as string ?? "Attenzione",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show(Application.Current.TryFindResource("MsgSovrascrivi") as string ?? "Vuoi sovrascrivere?",
                                                      Application.Current.TryFindResource("MsgConferma") as string ?? "Conferma",
                                                      MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes && EseguiSalvataggio(cartellaAttuale))
            {
                txtStatus.Text = "Stile sovrascritto con successo! / Style successfully saved!";
            }
        }

        private void BtnSalvaConNome_Click(object sender, RoutedEventArgs e)
        {
            if (alberoGerarchico.Count == 0 || string.IsNullOrEmpty(cartellaAttuale)) return;
            if (string.IsNullOrEmpty(BaseStylesPath))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgCartellaNonTrovata") as string ?? "Cartella non trovata",
                                Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string nuovoNome = StyleEnvironment.GetNextAvailableStyleName(BaseStylesPath);
            MessageBoxResult result = MessageBox.Show(Application.Current.TryFindResource("MsgNuovoStile") as string ?? "Il nuovo stile verrà salvato, procedo?",
                                                      Application.Current.TryFindResource("MsgConferma") as string ?? "Conferma",
                                                      MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string cartellaDest = Path.Combine(BaseStylesPath, nuovoNome);
                try { Directory.CreateDirectory(cartellaDest); }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Errore permessi. Avvia come Amministratore. / Access Denied.",
                                    Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    foreach (string dir in Directory.GetDirectories(cartellaAttuale, "*", SearchOption.AllDirectories))
                        Directory.CreateDirectory(dir.Replace(cartellaAttuale, cartellaDest));
                    foreach (string file in Directory.GetFiles(cartellaAttuale, "*.*", SearchOption.AllDirectories))
                        File.Copy(file, file.Replace(cartellaAttuale, cartellaDest), true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{(Application.Current.TryFindResource("MsgErrore") as string)}: {ex.Message}",
                                    Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (EseguiSalvataggio(cartellaDest))
                {
                    cartellaAttuale = cartellaDest;
                    alberoGerarchico[0].Name = nuovoNome.ToUpper();
                    btnSalva.IsEnabled = true;
                    txtStatus.Text = $"Nuovo stile salvato / New style saved!";
                }
            }
        }

        private bool EseguiSalvataggio(string cartellaDestinazione)
        {
            try
            {
                if (alberoGerarchico[0].Children.Count > 0)
                {
                    List<MeshData> tuttiIFileX = new List<MeshData>();
                    MeshHelper.EstraiTuttiIFileX(alberoGerarchico[0], tuttiIFileX);

                    foreach (var fileRoot in tuttiIFileX)
                    {
                        if (string.IsNullOrEmpty(fileRoot.OriginalXFileContent)) continue;

                        List<MeshData> tuttiNodiFile = new List<MeshData>();
                        MeshHelper.AppiattisciGerarchia(fileRoot.Children, tuttiNodiFile);

                        foreach (var m in tuttiNodiFile)
                        {
                            if (m.IsGroup || m.RemoveTexture) continue;
                            string pathSorgente = !string.IsNullOrEmpty(m.NewTexturePath) ? m.NewTexturePath : m.TextureName;

                            if (!string.IsNullOrEmpty(pathSorgente))
                            {
                                string fullSorgente = Path.IsPathRooted(pathSorgente) ? pathSorgente :
                                    (File.Exists(Path.Combine(cartellaAttuale, pathSorgente)) ? Path.Combine(cartellaAttuale, pathSorgente) : Path.Combine(cartellaPadre, pathSorgente));

                                if (File.Exists(fullSorgente))
                                {
                                    string pathFinale = Path.Combine(cartellaDestinazione, Path.GetFileName(pathSorgente));
                                    Directory.CreateDirectory(Path.GetDirectoryName(pathFinale));
                                    MeshHelper.SalvaTextureFisicamente(fullSorgente, pathFinale, m.TextureRotation);
                                }
                            }
                        }

                        string fullDestPath = Path.Combine(cartellaDestinazione, fileRoot.OriginalFileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath));

                        string nuovoTestoX = XFileEngine.ApplicaSalvataggio(fileRoot.OriginalXFileContent, tuttiNodiFile);
                        File.WriteAllText(fullDestPath, nuovoTestoX);
                    }

                    if (viewPortMassimizzato.Background is SolidColorBrush bgBrush)
                    {
                        SalvaSfondoInXML(bgBrush.Color, cartellaDestinazione);
                    }

                    ThumbnailGenerator.GeneraIcona(cartellaDestinazione, alberoGerarchico[0], cartellaAttuale, cartellaPadre);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(Application.Current.TryFindResource("MsgErrore") as string)}: {ex.Message}",
                                Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => ImpostaVistaU();

        private void BtnGestioneStili_Click(object sender, RoutedEventArgs e)
        {
            StyleSelectorWindow selWindow = new StyleSelectorWindow(InternalStylesPath);
            selWindow.Owner = this;

            if (selWindow.ShowDialog() == true && !string.IsNullOrEmpty(selWindow.SelectedStyleName))
            {
                string percorsoScelto = Path.Combine(InternalStylesPath, selWindow.SelectedStyleName);
                EseguiCaricamentoFisico(percorsoScelto);
            }
        }

        private void BtnSfondoColore_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(cartellaAttuale))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgSelezionaPrima") as string ?? "Carica uno stile",
                                Application.Current.TryFindResource("MsgAttenzione") as string ?? "Attenzione",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Color coloreBase = Color.FromRgb(30, 34, 43);
            if (viewPortMassimizzato.Background is SolidColorBrush brushEsistente)
            {
                coloreBase = brushEsistente.Color;
            }

            FlatColorPicker cp = new FlatColorPicker(coloreBase);
            cp.Owner = this;

            if (cp.ShowDialog() == true)
            {
                viewPortMassimizzato.Background = new SolidColorBrush(cp.FinalColor);
                btnSalva.IsEnabled = true;
            }
        }

        private void SalvaSfondoInXML(Color coloreFinale, string cartellaDestinazione)
        {
            try
            {
                string pathXml = Path.Combine(cartellaDestinazione, "Lights.xml");
                if (!File.Exists(pathXml)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(pathXml);
                XmlNode root = doc.DocumentElement;

                XmlNodeList colorNodes = doc.GetElementsByTagName("bkgColor");
                XmlNode bgNode = colorNodes.Count > 0 ? colorNodes[0] : null;

                if (bgNode == null)
                {
                    bgNode = doc.CreateElement("bkgColor");
                    root.AppendChild(bgNode);
                }
                bgNode.RemoveAll();

                string[] canali = { "Alpha", "Red", "Green", "Blue" };
                double[] valori = { coloreFinale.A / 255.0, coloreFinale.R / 255.0, coloreFinale.G / 255.0, coloreFinale.B / 255.0 };

                for (int i = 0; i < canali.Length; i++)
                {
                    XmlElement child = doc.CreateElement(canali[i]);
                    child.InnerText = valori[i].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    bgNode.AppendChild(child);
                }

                doc.Save(pathXml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(Application.Current.TryFindResource("MsgErrore") as string)}: {ex.Message}");
            }
        }

        private void ApplicaSfondoDaXML()
        {
            try
            {
                string pathXml = Path.Combine(cartellaAttuale, "Lights.xml");
                if (!File.Exists(pathXml)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(pathXml);

                XmlNodeList colorNodes = doc.GetElementsByTagName("bkgColor");
                if (colorNodes.Count > 0)
                {
                    XmlNode colorNode = colorNodes[0];
                    double a = LeggiValoreColore(colorNode, "Alpha", 1.0);
                    double r = LeggiValoreColore(colorNode, "Red", 0.12);
                    double g = LeggiValoreColore(colorNode, "Green", 0.13);
                    double b = LeggiValoreColore(colorNode, "Blue", 0.17);

                    Color bgCol = Color.FromArgb((byte)(a * 255), (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
                    viewPortMassimizzato.Background = new SolidColorBrush(bgCol);
                    return;
                }

                viewPortMassimizzato.Background = (Brush)FindResource("BgFinestra");
            }
            catch { }
        }

        private double LeggiValoreColore(XmlNode parent, string nodeName, double defaultVal)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.Name == nodeName)
                {
                    if (double.TryParse(child.InnerText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        return val;
                    }
                }
            }
            return defaultVal;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0;

            StartupDialog dialog = new StartupDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            if (dialog.SceltaUtente == StartupAction.Nessuna)
            {
                this.Close();
                return;
            }

            this.Opacity = 1;

            if (dialog.SceltaUtente == StartupAction.CreaNuovo)
            {
                BtnGestioneStili_Click(null, null);
            }
            else if (dialog.SceltaUtente == StartupAction.Modifica)
            {
                BtnCarica_Click(null, null);
            }
        }
    }
}