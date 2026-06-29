using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        public double SkinOffsetU { get; private set; }
        public double SkinOffsetV { get; private set; }
        public double SkinOffsetX { get; private set; }
        public double SkinOffsetY { get; private set; }
        public double SkinOffsetZ { get; private set; }
        public System.Windows.Media.PointCollection TextureCoordinates { get; private set; }
        public System.Windows.Media.PointCollection OriginalTextureCoordinates { get; private set; }

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
            SkinOffsetU = target.SkinOffsetU;
            SkinOffsetV = target.SkinOffsetV;
            SkinOffsetX = target.SkinOffsetX;
            SkinOffsetY = target.SkinOffsetY;
            SkinOffsetZ = target.SkinOffsetZ;
            // Snapshot delle UV correnti e del backup per gestire correttamente l'undo della proiezione planare
            if (target.Geometry?.TextureCoordinates != null)
                TextureCoordinates = new System.Windows.Media.PointCollection(target.Geometry.TextureCoordinates);
            if (target.OriginalTextureCoordinates != null)
                OriginalTextureCoordinates = new System.Windows.Media.PointCollection(target.OriginalTextureCoordinates);
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
            Target.SkinOffsetU = SkinOffsetU;
            Target.SkinOffsetV = SkinOffsetV;
            Target.SkinOffsetX = SkinOffsetX;
            Target.SkinOffsetY = SkinOffsetY;
            Target.SkinOffsetZ = SkinOffsetZ;
            if (TextureCoordinates != null && Target.Geometry != null)
                Target.Geometry.TextureCoordinates = new System.Windows.Media.PointCollection(TextureCoordinates);
            Target.OriginalTextureCoordinates = OriginalTextureCoordinates != null
                ? new System.Windows.Media.PointCollection(OriginalTextureCoordinates)
                : null;

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
        private string InternalStylesPath = EmbeddedResourceManager.GetTempStylesPath();

        private string cartellaAttuale = "";
        private string cartellaPadre = "";

        private ObservableCollection<MeshData> alberoGerarchico = new ObservableCollection<MeshData>();
        public ObservableCollection<GalleriaGroup> GruppiGalleria { get; set; } = new ObservableCollection<GalleriaGroup>();

        private MeshData pezzoSelezionato;
        private MeshData fileAttivoVisibile = null;
        private bool isUpdatingZoom = false;
        // Quando true, ogni operazione (skin/colore/zoom/offset) si applica a tutte le mesh della scena
        // invece che alla sola selezione. Attivato da BtnSelezionaScena_Click, disattivato quando l'utente
        // clicca un singolo elemento (TreeView o viewport) per tornare a selezione puntuale.
        private bool modalitaScena = false;
        // Multi-selezione: attivata da Shift+click nel viewport. Tutte le mesh in meshSelezionate diventano
        // bersaglio di TrovaElementiCoinvolti. pezzoSelezionato resta uno qualunque della multi-selezione
        // (l'ultimo cliccato) cosi' il pannello proprieta' resta abilitato. Uso un HashSet indipendente da
        // IsSelected perche' il TreeView e' single-select e azzera l'IsSelected delle mesh non focus.
        private bool multiSelezione = false;
        private readonly HashSet<MeshData> meshSelezionate = new HashSet<MeshData>();
        private bool isUpdatingLuci = false;
        private LightSettings impostazioniLuci = new LightSettings();

        // TeamStyle: una TeamStyleData per ogni tipo gioco presente nello stile corrente
        private readonly Dictionary<string, TeamStyleData> teamStyles = new Dictionary<string, TeamStyleData>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] teamGameTypes = { "p10", "p5", "duck", "candle" };
        private static readonly Dictionary<string, string> teamGameNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "p10", "10 Pin" }, { "p5", "5 Pin" }, { "duck", "Duck Pin" }, { "candle", "Candle Pin" }
        };
        private bool isUpdatingTeam = false;

        private UndoManager _undoManager = new UndoManager();

        public MainWindow()
        {
            InitializeComponent();

            treeComponenti.ItemsSource = alberoGerarchico;
            listaGruppiGalleria.ItemsSource = GruppiGalleria;

            // Allineo il toggle lingua alla preferenza già applicata da App.OnStartup
            if (App.LinguaCorrente == "EN") rbLinguaEN.IsChecked = true;
            else rbLinguaIT.IsChecked = true;

            btnSalva.IsEnabled = false;

            // Cerco gli stili esterni
            BaseStylesPath = StyleEnvironment.TrovaPercorsoStili();

            // Verifico se l'estrazione è andata a buon fine
            // Nota: EmbeddedResourceManager.EstraiRisorseSeNecessario() ha già mostrato un errore
            // dettagliato se 0 risorse sono state trovate; qui registriamo solo l'esito finale.
            bool internalStylesOk = Directory.Exists(InternalStylesPath) &&
                                    Directory.EnumerateFileSystemEntries(InternalStylesPath).Any();
            if (!internalStylesOk)
            {
                MessageBox.Show(
                    $"Errore: La cartella degli stili di base è vuota o non esiste.\n\nPercorso atteso: {InternalStylesPath}\n\n" +
                    "Controlla che i file in BaseStyles\\ abbiano 'Build Action = Embedded Resource' nel progetto.",
                    "Stili di Base Mancanti", MessageBoxButton.OK, MessageBoxImage.Error);
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

            bool hasUserStyles = Directory.GetDirectories(BaseStylesPath, "style#*")
                .Select(d => Path.GetFileName(d))
                .Any(name => !StyleEnvironment.IsStyleProtected(name));

            if (!hasUserStyles)
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgNessunStilePersonalizzato") as string ?? "Nessuno stile personalizzato trovato.",
                                Application.Current.TryFindResource("MsgAttenzione") as string ?? "Attenzione",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StyleSelectorWindow selWindow = new StyleSelectorWindow(BaseStylesPath, StyleFilter.OnlyUser);
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
                impostazioniLuci = XmlHelper.LeggiImpostazioniLuci(fileXml);
                PopolaTextboxLuci();
                ApplicaSfondoDaXML();
                CaricaTeamStyles();
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
            catch (Exception ex) { MessageBox.Show((Application.Current.TryFindResource("MsgErroreCaricamento") as string ?? "Errore caricamento: ") + ex.Message); }
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

            if (modalitaScena)
            {
                // Modalita' scena: bersaglio = mesh base (no testi, no t_*) della scena visibile
                var sorgenti = fileAttivoVisibile != null
                    ? new List<MeshData> { fileAttivoVisibile }
                    : new List<MeshData>(alberoGerarchico);
                var raccolte = new List<MeshData>();
                foreach (var s in sorgenti) RaccoglaiMeshNonTesto(s, raccolte);
                targets.AddRange(raccolte.Where(m => !MeshFaParteDiFileT(m)));
                return targets;
            }

            if (multiSelezione)
            {
                // Multi-selezione via Shift+click: ritorno tutte le mesh nel set indipendente
                // (non posso usare IsSelected perche' il TreeView e' single-select e lo azzera).
                targets.AddRange(meshSelezionate.Where(m => !m.IsGroup && m.Geometry?.Positions != null && m.Geometry.Positions.Count > 0));
                return targets;
            }

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

            // Selezione puntuale: esco dalla modalita' scena e dalla multi-selezione
            modalitaScena = false;
            multiSelezione = false;
            meshSelezionate.Clear();

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
            bool shiftPremuto = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (result is RayMeshGeometry3DHitTestResult ray && ray.ModelHit is GeometryModel3D hit)
            {
                List<MeshData> tutti = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(alberoGerarchico, tutti);

                foreach (var m in tutti)
                {
                    if (m.Model3D == hit)
                    {
                        // Shift+click: toggle multi-selezione senza azzerare le altre
                        if (shiftPremuto)
                        {
                            modalitaScena = false;
                            // Se non ero in multi-selezione, ci entro includendo la precedente pezzoSelezionato
                            if (!multiSelezione && pezzoSelezionato != null)
                            {
                                meshSelezionate.Add(pezzoSelezionato);
                            }
                            multiSelezione = true;

                            if (meshSelezionate.Contains(m))
                            {
                                meshSelezionate.Remove(m);
                                if (pezzoSelezionato == m)
                                {
                                    pezzoSelezionato = meshSelezionate.FirstOrDefault();
                                    if (pezzoSelezionato == null) multiSelezione = false;
                                }
                            }
                            else
                            {
                                meshSelezionate.Add(m);
                                pezzoSelezionato = m;
                            }
                            AggiornaPannelloProprieta();
                            ApplicaTrasparenzaAlModello3D();
                            MeshHelper.EspandiGenitori(alberoGerarchico, m);
                            return;
                        }

                        // Click normale: selezione puntuale, esco da modalita' scena e multi-selezione
                        modalitaScena = false;
                        multiSelezione = false;
                        meshSelezionate.Clear();
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
            // Click su area vuota del viewport: esco anche dalle modalita' scena/multi-selezione
            modalitaScena = false;
            multiSelezione = false;
            meshSelezionate.Clear();
            SvuotaSelezione();
        }

        private void AggiornaPannelloProprieta()
        {
            isUpdatingZoom = true;
            if (pezzoSelezionato != null)
            {
                pannelloSelezionePart1.IsEnabled = true;
                pannelloSelezionePart2.IsEnabled = true;
                if (modalitaScena)
                {
                    int n = TrovaElementiCoinvolti().Count;
                    txtNomeSelezionato.Text = $"Scena intera ({n} elementi)";
                }
                else if (multiSelezione)
                {
                    int n = TrovaElementiCoinvolti().Count;
                    txtNomeSelezionato.Text = $"Multi-selezione ({n} elementi)";
                }
                else
                {
                    txtNomeSelezionato.Text = pezzoSelezionato.Name;
                }
                btnInfoOggetto.Visibility = Visibility.Visible;
                rectColore.Background = new SolidColorBrush(pezzoSelezionato.MeshColor);

                string labelNessuna = Application.Current.TryFindResource("StrNessuna") as string ?? "Nessuna";
                txtTexturePath.Text = pezzoSelezionato.RemoveTexture ? labelNessuna : Path.GetFileName(pezzoSelezionato.TextureName);

                // TextureScale è il moltiplicatore rispetto al bounding box UV: 1.0 = combacia esattamente
                slZoomSkin.Value = pezzoSelezionato.TextureScale;
                txtZoomSkin.Text = pezzoSelezionato.TextureScale.ToString("0.00");

                txtOffsetU.Text = pezzoSelezionato.SkinOffsetU.ToString("0.00");
                txtOffsetV.Text = pezzoSelezionato.SkinOffsetV.ToString("0.00");
                txtOffsetX.Text = pezzoSelezionato.SkinOffsetX.ToString("0.00");
                txtOffsetY.Text = pezzoSelezionato.SkinOffsetY.ToString("0.00");
                txtOffsetZ.Text = pezzoSelezionato.SkinOffsetZ.ToString("0.00");
            }
            else
            {
                pannelloSelezionePart1.IsEnabled = false;
                pannelloSelezionePart2.IsEnabled = false;
                btnInfoOggetto.Visibility = Visibility.Collapsed;
                txtNomeSelezionato.Text = Application.Current.TryFindResource("StrNessunaSelezione") as string ?? "Nessuna selezione";
            }
            isUpdatingZoom = false;
        }

        private void BtnInfoOggetto_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato?.Geometry == null) return;

            var bounds = pezzoSelezionato.Geometry.Bounds;
            double w = Math.Round(bounds.SizeX, 1);
            double h = Math.Round(bounds.SizeY, 1);
            double d = Math.Round(bounds.SizeZ, 1);

            string lblW = Application.Current.TryFindResource("MsgDimLarghezza") as string ?? "Larghezza";
            string lblH = Application.Current.TryFindResource("MsgDimAltezza") as string ?? "Altezza";
            string lblD = Application.Current.TryFindResource("MsgDimProfondita") as string ?? "Profondità";
            string titolo = Application.Current.TryFindResource("MsgDimensioniTitolo") as string ?? "Dimensioni Oggetto";

            string msg = $"{pezzoSelezionato.Name}\n\n" +
                         $"{lblW}:   {w} px\n" +
                         $"{lblH}:     {h} px\n" +
                         $"{lblD}:  {d} px";

            MessageBox.Show(msg, titolo, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PopolaTextboxLuci()
        {
            isUpdatingLuci = true;
            txtLuce1X.Text = impostazioniLuci.Dir1X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            txtLuce1Y.Text = impostazioniLuci.Dir1Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            txtLuce1Z.Text = impostazioniLuci.Dir1Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            txtLuce2X.Text = impostazioniLuci.Dir2X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            txtLuce2Y.Text = impostazioniLuci.Dir2Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            txtLuce2Z.Text = impostazioniLuci.Dir2Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            rectLuce1Colore.Background = new SolidColorBrush(impostazioniLuci.Diffuse1);
            rectLuce2Colore.Background = new SolidColorBrush(impostazioniLuci.Diffuse2);
            isUpdatingLuci = false;
        }

        // ===================== TeamStyle =====================

        // Carica i TeamStyle.xml dei tipi gioco presenti nello stile corrente e prepara il tab.
        private void CaricaTeamStyles()
        {
            teamStyles.Clear();
            foreach (var g in teamGameTypes)
            {
                string p = Path.Combine(cartellaAttuale, g, "TeamStyle.xml");
                if (File.Exists(p)) teamStyles[g] = TeamStyleHelper.Load(p);
            }

            isUpdatingTeam = true;
            cmbTeamGioco.Items.Clear();
            foreach (var g in teamGameTypes)
                if (teamStyles.ContainsKey(g))
                    cmbTeamGioco.Items.Add(new ComboBoxItem { Content = teamGameNames[g], Tag = g });
            if (cmbTeamGioco.Items.Count > 0) cmbTeamGioco.SelectedIndex = 0;
            isUpdatingTeam = false;

            bool any = teamStyles.Count > 0;
            txtTeamVuoto.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            pannelloTeamCampi.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            cmbTeamGioco.IsEnabled = rbTeamPerGame.IsChecked == true;

            if (any) PopolaCampiTeam(CurrentTeamData());
        }

        private string SelectedTeamGame()
        {
            if (cmbTeamGioco.SelectedItem is ComboBoxItem it && it.Tag is string s) return s;
            return teamStyles.Keys.FirstOrDefault();
        }

        private TeamStyleData CurrentTeamData()
        {
            string g = SelectedTeamGame();
            if (g != null && teamStyles.TryGetValue(g, out var d)) return d;
            return teamStyles.Values.FirstOrDefault();
        }

        // I bersagli della modifica: tutti i giochi oppure solo quello selezionato.
        private IEnumerable<TeamStyleData> TeamTargets()
        {
            if (rbTeamAll.IsChecked == true) return teamStyles.Values.ToList();
            string g = SelectedTeamGame();
            if (g != null && teamStyles.TryGetValue(g, out var d)) return new[] { d };
            return Enumerable.Empty<TeamStyleData>();
        }

        // Riempie tutti i campi (numerici + campioni colore) del tab TeamStyle dai dati indicati.
        private void PopolaCampiTeam(TeamStyleData d)
        {
            if (d == null) return;
            isUpdatingTeam = true;
            foreach (var obj in LogicalDescendants(pannelloTeamCampi))
            {
                if (obj is TextBox tb && tb.Tag is string k && d.Scalars.ContainsKey(k))
                    tb.Text = d.Scalars[k].ToString("0.###", CultureInfo.InvariantCulture);
                else if (obj is Border b && b.Tag is string ck && d.Colors.ContainsKey(ck))
                    b.Background = new SolidColorBrush(d.Colors[ck]);
            }
            isUpdatingTeam = false;
        }

        private void TeamMode_Changed(object sender, RoutedEventArgs e)
        {
            if (cmbTeamGioco == null) return; // può scattare durante l'inizializzazione XAML
            cmbTeamGioco.IsEnabled = rbTeamPerGame.IsChecked == true;
        }

        private void TeamGioco_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingTeam) return;
            PopolaCampiTeam(CurrentTeamData());
        }

        private void TeamNum_Changed(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingTeam) return;
            if (!(sender is TextBox tb) || !(tb.Tag is string k)) return;
            if (!double.TryParse(tb.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) return;
            foreach (var d in TeamTargets()) d.Scalars[k] = v;
            if (!StyleEnvironment.IsStyleProtected(Path.GetFileName(cartellaAttuale))) btnSalva.IsEnabled = true;
        }

        private void TeamColore_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border bd) || !(bd.Tag is string k) || teamStyles.Count == 0) return;

            Color start = Colors.White;
            var cur = CurrentTeamData();
            if (cur != null && cur.Colors.TryGetValue(k, out var c0)) start = c0;

            var cp = new FlatColorPicker(start) { Owner = this };
            if (cp.ShowDialog() == true)
            {
                foreach (var d in TeamTargets()) d.Colors[k] = cp.FinalColor;
                bd.Background = new SolidColorBrush(cp.FinalColor);
                if (!StyleEnvironment.IsStyleProtected(Path.GetFileName(cartellaAttuale))) btnSalva.IsEnabled = true;
            }
        }

        private void SalvaTeamStyles(string cartellaDestinazione)
        {
            foreach (var kv in teamStyles)
                TeamStyleHelper.Save(Path.Combine(cartellaDestinazione, kv.Key, "TeamStyle.xml"), kv.Value);
        }

        private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject d)
                {
                    yield return d;
                    foreach (var sub in LogicalDescendants(d)) yield return sub;
                }
            }
        }

        private void RectLuceColore_Click(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(cartellaAttuale)) return;

            bool isLuce1 = sender == rectLuce1Colore;
            Color coloreBase = isLuce1 ? impostazioniLuci.Diffuse1 : impostazioniLuci.Diffuse2;

            FlatColorPicker cp = new FlatColorPicker(coloreBase);
            cp.Owner = this;

            if (cp.ShowDialog() == true)
            {
                if (isLuce1)
                {
                    impostazioniLuci.Diffuse1 = cp.FinalColor;
                    rectLuce1Colore.Background = new SolidColorBrush(cp.FinalColor);
                }
                else
                {
                    impostazioniLuci.Diffuse2 = cp.FinalColor;
                    rectLuce2Colore.Background = new SolidColorBrush(cp.FinalColor);
                }
                XmlHelper.RicostruisciLuci(impostazioniLuci, luciGroup);
                btnSalva.IsEnabled = true;
            }
        }

        private void TxtLuci_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingLuci || string.IsNullOrEmpty(cartellaAttuale)) return;

            bool changed = false;
            if (double.TryParse(txtLuce1X.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x1)) { impostazioniLuci.Dir1X = x1; changed = true; }
            if (double.TryParse(txtLuce1Y.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y1)) { impostazioniLuci.Dir1Y = y1; changed = true; }
            if (double.TryParse(txtLuce1Z.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double z1)) { impostazioniLuci.Dir1Z = z1; changed = true; }
            if (double.TryParse(txtLuce2X.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x2)) { impostazioniLuci.Dir2X = x2; changed = true; }
            if (double.TryParse(txtLuce2Y.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y2)) { impostazioniLuci.Dir2Y = y2; changed = true; }
            if (double.TryParse(txtLuce2Z.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double z2)) { impostazioniLuci.Dir2Z = z2; changed = true; }

            if (changed)
            {
                XmlHelper.RicostruisciLuci(impostazioniLuci, luciGroup);
                btnSalva.IsEnabled = true;
            }
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
                // TextureScale è il moltiplicatore: 1.0 = immagine combacia col bounding box UV, >1 zoom in, <1 zoom out (tile)
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

            // Per le skin caricate dall'utente il viewport segue il bbox UV → 1.0 = fit perfetto.
            // Per le texture originali del .x si usa il mapping legacy → fit = maxU.
            double target = 1.0;
            if (string.IsNullOrEmpty(pezzoSelezionato.NewTexturePath))
            {
                double maxU = 0;
                foreach (var uv in pezzoSelezionato.Geometry.TextureCoordinates) if (uv.X > maxU) maxU = uv.X;
                target = maxU > 0 ? maxU : 1.0;
            }
            slZoomSkin.Value = target;
        }

        // Offset locale UV: sposta la skin sulla superficie senza riproiettare. Solo aggiorna il viewport del brush.
        private void ApplicaOffsetLocale(double offsetU, double offsetV)
        {
            foreach (var m in TrovaElementiCoinvolti())
            {
                m.SkinOffsetU = offsetU;
                m.SkinOffsetV = offsetV;
                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
        }

        // Calcola e applica l'offsetV per allineare la skin al bordo alto / centro / basso del bbox UV
        // delle mesh selezionate. Compensa anche il TextureScale (zoom) corrente cosi' funziona a qualunque zoom.
        private void AllineaSkinV(string modo)
        {
            var targets = TrovaElementiCoinvolti();
            if (targets.Count == 0) return;

            // Trovo il range UV V coperto dalle mesh selezionate
            double minV = double.MaxValue, maxV = double.MinValue;
            foreach (var m in targets)
            {
                if (m.Geometry?.TextureCoordinates == null) continue;
                foreach (var uv in m.Geometry.TextureCoordinates)
                {
                    if (uv.Y < minV) minV = uv.Y;
                    if (uv.Y > maxV) maxV = uv.Y;
                }
            }
            if (minV > maxV) return;

            double T = targets[0].TextureScale > 0 ? targets[0].TextureScale : 1.0;
            double off = (1 - T) / 2;

            double offsetV;
            switch (modo)
            {
                case "alto":
                    // Bordo alto della skin (image V=0) sul bordo alto del bbox UV (minV)
                    offsetV = minV - off;
                    break;
                case "basso":
                    // Bordo basso della skin (image V=1) sul bordo basso del bbox UV (maxV)
                    offsetV = maxV - off - T;
                    break;
                case "centro":
                default:
                    // Centro dell'immagine al centro del bbox UV
                    offsetV = (minV + maxV - 1.0) / 2;
                    break;
            }

            isUpdatingZoom = true;
            foreach (var m in targets)
            {
                m.SkinOffsetV = offsetV;
                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
            txtOffsetV.Text = offsetV.ToString("0.00");
            isUpdatingZoom = false;
        }

        private void BtnAllineaAlto_Click(object sender, RoutedEventArgs e) => AllineaSkinV("alto");
        private void BtnAllineaCentro_Click(object sender, RoutedEventArgs e) => AllineaSkinV("centro");
        private void BtnAllineaBasso_Click(object sender, RoutedEventArgs e) => AllineaSkinV("basso");

        // Restituisce true se la mesh appartiene a un file .x il cui nome inizia con "t_"
        // (animation frames Steltronic t_1.x, t_2.x, t_strike.x, ecc.). Risale all'albero per trovare il file root.
        private bool MeshFaParteDiFileT(MeshData mesh)
        {
            if (mesh == null || alberoGerarchico.Count == 0) return false;
            MeshData fileRoot = MeshHelper.TrovaFileRootDiAppartenenza(alberoGerarchico[0], mesh);
            if (fileRoot == null) return false;
            // Controllo sia il Name (mostrato in tree, di solito derivato dal nome del file)
            // sia l'OriginalFileName (percorso relativo del .x sul disco)
            string nome = fileRoot.Name ?? "";
            string fileName = System.IO.Path.GetFileName(fileRoot.OriginalFileName ?? "");
            return nome.StartsWith("t_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("t_", StringComparison.OrdinalIgnoreCase);
        }

        // Pattern dei nomi di Frame Steltronic che identificano elementi di TESTO (score, total, recap, ecc.).
        // Tutto cio' che sta dentro un frame con questi nomi va escluso dalla selezione "base scena".
        // Esempi: FR1_T1, FR8_C2, FR3_TOT, FR5_SP1 (player.X) - NUMBERS (frameruler) - p1_name, p2_hdp, TeamName, Data (recap).
        private static readonly System.Text.RegularExpressions.Regex regexFrameTesto =
            new System.Text.RegularExpressions.Regex(@"^(FR\d+_|p\d+_|NUMBERS$|TeamName$|Data$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool NomeFrameDiTesto(string nome) =>
            !string.IsNullOrEmpty(nome) && regexFrameTesto.IsMatch(nome);

        // Walk gerarchico che raccoglie SOLO le mesh foglia di base, saltando ogni sub-tree il cui Frame
        // ancestor ha nome di tipo testuale (FR\d+_*, NUMBERS, p\d+_*, TeamName, Data).
        private void RaccoglaiMeshNonTesto(MeshData node, List<MeshData> result)
        {
            if (node == null) return;
            // Se questo nodo e' un Frame di testo, ferma la discesa (esclude lui e i discendenti)
            if (node.IsGroup && NomeFrameDiTesto(node.Name)) return;
            // Foglia con geometria reale: la prendo
            if (!node.IsGroup && node.Geometry?.Positions != null && node.Geometry.Positions.Count > 0)
            {
                result.Add(node);
            }
            // Continua sui figli
            foreach (var child in node.Children) RaccoglaiMeshNonTesto(child, result);
        }

        private void TxtOffsetU_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingZoom || pezzoSelezionato == null) return;
            if (txtOffsetU.IsFocused && double.TryParse(txtOffsetU.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double valU))
            {
                double valV = double.TryParse(txtOffsetV.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
                isUpdatingZoom = true;
                ApplicaOffsetLocale(valU, valV);
                isUpdatingZoom = false;
            }
        }

        private void TxtOffsetV_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingZoom || pezzoSelezionato == null) return;
            if (txtOffsetV.IsFocused && double.TryParse(txtOffsetV.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double valV))
            {
                double valU = double.TryParse(txtOffsetU.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double u) ? u : 0;
                isUpdatingZoom = true;
                ApplicaOffsetLocale(valU, valV);
                isUpdatingZoom = false;
            }
        }

        // Pulsanti incrementali per la posizione spaziale della skin (offset U/V locali).
        // Step grande = 0.10, step piccolo = 0.01. Range clamped a [-1, 1] (limiti UV).
        // Leggo i valori correnti direttamente dalle proprieta' della mesh (pezzoSelezionato.SkinOffsetU/V)
        // per evitare incoerenze col testo della textbox (focus/refresh/click ripetuti).
        private void ApplicaPassoOffsetU(double delta)
        {
            if (pezzoSelezionato == null) return;
            double cur = pezzoSelezionato.SkinOffsetU;
            double nuovoU = Math.Max(-1, Math.Min(1, cur + delta));
            double valV = pezzoSelezionato.SkinOffsetV;
            isUpdatingZoom = true;
            txtOffsetU.Text = nuovoU.ToString("0.00");
            ApplicaOffsetLocale(nuovoU, valV);
            isUpdatingZoom = false;
        }

        private void ApplicaPassoOffsetV(double delta)
        {
            if (pezzoSelezionato == null) return;
            double cur = pezzoSelezionato.SkinOffsetV;
            double nuovoV = Math.Max(-1, Math.Min(1, cur + delta));
            double valU = pezzoSelezionato.SkinOffsetU;
            isUpdatingZoom = true;
            txtOffsetV.Text = nuovoV.ToString("0.00");
            ApplicaOffsetLocale(valU, nuovoV);
            isUpdatingZoom = false;
        }

        private void BtnOffsetU_BigMinus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetU(-0.10);
        private void BtnOffsetU_Minus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetU(-0.01);
        private void BtnOffsetU_Plus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetU(+0.01);
        private void BtnOffsetU_BigPlus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetU(+0.10);

        private void BtnOffsetV_BigMinus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetV(-0.10);
        private void BtnOffsetV_Minus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetV(-0.01);
        private void BtnOffsetV_Plus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetV(+0.01);
        private void BtnOffsetV_BigPlus_Click(object sender, RoutedEventArgs e) => ApplicaPassoOffsetV(+0.10);

        // Offset globale XYZ: cambia l'origine della proiezione 3D, quindi serve riproiettare le UV.
        private void TxtOffsetXYZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingZoom || pezzoSelezionato == null) return;
            if (!double.TryParse(txtOffsetX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double oX)) return;
            if (!double.TryParse(txtOffsetY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double oY)) return;
            if (!double.TryParse(txtOffsetZ.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double oZ)) return;

            List<MeshData> targets = TrovaElementiCoinvolti();
            foreach (var m in targets)
            {
                m.SkinOffsetX = oX;
                m.SkinOffsetY = oY;
                m.SkinOffsetZ = oZ;
            }
            // Solo per skin utente: riproiettiamo le UV con la nuova origine 3D
            if (targets.Count > 0 && !string.IsNullOrEmpty(targets[0].NewTexturePath))
            {
                MeshHelper.ApplicaProiezionePlanare(targets);
                foreach (var m in targets)
                {
                    m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                    if (m.Model3D != null)
                    {
                        m.Model3D.Material = m.OriginalMaterial;
                        m.Model3D.BackMaterial = m.OriginalMaterial;
                    }
                }
            }
        }

        private void BtnResetOffsetSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato == null) return;
            List<MeshData> targets = TrovaElementiCoinvolti();
            RegistraStatoUndo(targets);

            foreach (var m in targets)
            {
                m.SkinOffsetU = 0; m.SkinOffsetV = 0;
                m.SkinOffsetX = 0; m.SkinOffsetY = 0; m.SkinOffsetZ = 0;
            }
            if (targets.Count > 0 && !string.IsNullOrEmpty(targets[0].NewTexturePath))
            {
                MeshHelper.ApplicaProiezionePlanare(targets);
            }
            foreach (var m in targets)
            {
                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
            AggiornaPannelloProprieta();
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
                    string pathTrovato = MeshHelper.TrovaPercorsoTextura(cartellaAttuale, cartellaPadre, texPath);
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
                    m.TextureRotation = 0;
                    m.TextureScale = 1.0;
                    // Reset offset cosi la nuova skin parte centrata
                    m.SkinOffsetU = 0; m.SkinOffsetV = 0;
                    m.SkinOffsetX = 0; m.SkinOffsetY = 0; m.SkinOffsetZ = 0;

                    // Texture di base standard quando si applica una skin: bianco diffuse + emissive, Power=250, specular nero.
                    // Garantisce che la skin appaia coi colori naturali senza tinte indotte dal materiale originale.
                    m.MeshColor = Color.FromRgb(255, 255, 255);
                    m.Alpha = 1.0;
                    m.Power = 250.0;
                    m.Specular = Color.FromRgb(0, 0, 0);
                    m.Emissive = Color.FromRgb(255, 255, 255);
                    m.HasColor = true;
                }

                // Rigenero le UV con proiezione planare condivisa fra tutte le mesh selezionate, cosi la
                // skin appare come una decalcomania unica anche su gruppi multi-mesh e su superfici curve.
                MeshHelper.ApplicaProiezionePlanare(targetMeshes);

                foreach (var m in targetMeshes)
                {
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

        // Seleziona TUTTI gli elementi visibili (foglia) come se fossero un'unica selezione: il pannello
        // proprieta' si abilita e ogni operazione (skin, colore, zoom, offset) si applica a tutti contemporaneamente.
        // Il flag modalitaScena viene letto da TrovaElementiCoinvolti per restituire l'intera scena invece
        // delle sole mesh che condividono la texture del pezzo selezionato.
        private void BtnSelezionaScena_Click(object sender, RoutedEventArgs e)
        {
            if (alberoGerarchico.Count == 0) return;

            // Sorgente della "scena": file attivo se massimizzato, altrimenti tutta la gerarchia (galleria)
            var sorgenti = fileAttivoVisibile != null
                ? new List<MeshData> { fileAttivoVisibile }
                : new List<MeshData>(alberoGerarchico);

            // Raccolgo le foglie attraverso un walk gerarchico che esclude i sub-tree con nomi tipici di testo
            // (FR1_T1, FR1_C1, NUMBERS, p1_name, ecc.) cosi la skin va sugli elementi base e non sulle scritte.
            var foglie = new List<MeshData>();
            foreach (var s in sorgenti) RaccoglaiMeshNonTesto(s, foglie);
            // Filtro aggiuntivo: escludo le mesh dei file t_*.x (animation frames Steltronic)
            foglie = foglie.Where(m => !MeshFaParteDiFileT(m)).ToList();

            if (foglie.Count == 0)
            {
                txtStatus.Text = "Nessun elemento da selezionare sulla scena.";
                return;
            }

            // Metto un rappresentante come pezzoSelezionato (il primo) cosi il pannello si abilita.
            // Attivo modalitaScena per far si che TrovaElementiCoinvolti restituisca TUTTE le foglie.
            SvuotaSelezione();
            foreach (var f in foglie) f.IsSelected = true;
            pezzoSelezionato = foglie[0];
            modalitaScena = true;

            AggiornaPannelloProprieta();
            ApplicaTrasparenzaAlModello3D();
            txtStatus.Text = $"Scena selezionata ({foglie.Count} elementi). Usa colore/skin/zoom per modificare tutto insieme.";
        }

        private void BtnRemoveSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato == null) return;

            List<MeshData> targetMeshes = TrovaElementiCoinvolti();
            RegistraStatoUndo(targetMeshes);

            // Ripristino le UV originali del file .x prima di rifare il materiale
            MeshHelper.RipristinaUVOriginali(targetMeshes);

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

                // In multi-selezione tutte le mesh nel set sono evidenziate; altrimenti solo pezzoSelezionato.
                bool isHighlighted = multiSelezione
                    ? meshSelezionate.Contains(m)
                    : (m == pezzoSelezionato);

                if (pezzoSelezionato == null && !multiSelezione)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
                else if (isHighlighted)
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
                    MessageBox.Show(Application.Current.TryFindResource("MsgErrorePermessi") as string ?? "Errore permessi. Avvia come Amministratore.",
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
            var riassuntoSalvataggio = new List<string>();
            try
            {
                if (alberoGerarchico[0].Children.Count > 0)
                {
                    List<MeshData> tuttiIFileX = new List<MeshData>();
                    MeshHelper.EstraiTuttiIFileX(alberoGerarchico[0], tuttiIFileX);

                    Directory.CreateDirectory(cartellaDestinazione);

                    // Nomi immagine da conservare: icon + tutte le skin attive
                    var immaginiConservate = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "icon.jpg" };

                    foreach (var fileRoot in tuttiIFileX)
                    {
                        if (string.IsNullOrEmpty(fileRoot.OriginalXFileContent)) continue;

                        // La texture va nella stessa sottocartella del file .x che la usa
                        string sottoCartella = Path.GetDirectoryName(fileRoot.OriginalFileName) ?? "";
                        string cartellaTexDest = string.IsNullOrEmpty(sottoCartella)
                            ? cartellaDestinazione
                            : Path.Combine(cartellaDestinazione, sottoCartella);

                        List<MeshData> tuttiNodiFile = new List<MeshData>();
                        MeshHelper.AppiattisciGerarchia(fileRoot.Children, tuttiNodiFile);

                        foreach (var m in tuttiNodiFile)
                        {
                            if (m.IsGroup || m.RemoveTexture) continue;

                            // Nome finale della texture nel file .x (dopo eventuale sostituzione utente)
                            string nomeTex = !string.IsNullOrEmpty(m.NewTexturePath)
                                ? Path.GetFileName(m.NewTexturePath)
                                : Path.GetFileName(m.TextureName ?? "");
                            if (string.IsNullOrEmpty(nomeTex)) continue;

                            immaginiConservate.Add(nomeTex);

                            // Sorgente: percorso assoluto della nuova texture oppure cerca nello stile origine
                            string fullSorgente = !string.IsNullOrEmpty(m.NewTexturePath) && File.Exists(m.NewTexturePath)
                                ? m.NewTexturePath
                                : MeshHelper.TrovaPercorsoTextura(cartellaAttuale, cartellaPadre, m.TextureName);

                            if (fullSorgente != null)
                            {
                                Directory.CreateDirectory(cartellaTexDest);
                                string pathFinale = Path.Combine(cartellaTexDest, nomeTex);
                                MeshHelper.SalvaTextureFisicamente(fullSorgente, pathFinale, 0);
                            }
                        }

                        string fullDestPath = Path.Combine(cartellaDestinazione, fileRoot.OriginalFileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath));
                        string nuovoTestoX = XFileEngine.ApplicaSalvataggio(fileRoot.OriginalXFileContent, tuttiNodiFile, out string diag);
                        File.WriteAllText(fullDestPath, nuovoTestoX);

                        // Diagnostica per scoprire se le sostituzioni vanno a segno
                        riassuntoSalvataggio.Add($"{fileRoot.OriginalFileName}: {diag}");
                    }

                    if (viewPortMassimizzato.Background is SolidColorBrush bgBrush)
                        SalvaSfondoInXML(bgBrush.Color, cartellaDestinazione);

                    XmlHelper.SalvaLuciInXML(impostazioniLuci, Path.Combine(cartellaDestinazione, "Lights.xml"));

                    SalvaTeamStyles(cartellaDestinazione);

                    ThumbnailGenerator.GeneraIcona(cartellaDestinazione, alberoGerarchico[0], cartellaAttuale, cartellaPadre);

                    // Elimina tutte le immagini nella cartella destinazione non presenti nella lista attiva
                    string[] estensioni = { ".png", ".jpg", ".jpeg", ".bmp" };
                    foreach (string file in Directory.GetFiles(cartellaDestinazione, "*", SearchOption.AllDirectories))
                    {
                        if (!estensioni.Contains(Path.GetExtension(file).ToLower())) continue;
                        if (!immaginiConservate.Contains(Path.GetFileName(file)))
                            File.Delete(file);
                    }

                    // Conferma a video che il salvataggio e' stato completato
                    if (riassuntoSalvataggio.Count > 0)
                        MessageBox.Show("Salvataggio completato con successo.", "Salvataggio", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (string.IsNullOrEmpty(BaseStylesPath) || !Directory.Exists(BaseStylesPath))
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgCartellaNonTrovata") as string ?? "Cartella non trovata",
                                Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StyleSelectorWindow selWindow = new StyleSelectorWindow(BaseStylesPath, StyleFilter.OnlyBase);
            selWindow.Owner = this;

            if (selWindow.ShowDialog() == true && !string.IsNullOrEmpty(selWindow.SelectedStyleName))
            {
                string percorsoScelto = Path.Combine(BaseStylesPath, selWindow.SelectedStyleName);
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

        private void LinguaIT_Checked(object sender, RoutedEventArgs e) => App.ImpostaLingua("IT");
        private void LinguaEN_Checked(object sender, RoutedEventArgs e) => App.ImpostaLingua("EN");

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0;

            StartupDialog dialog = new StartupDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            // Allineo il toggle lingua della title bar alla scelta fatta nel dialog d'avvio
            if (App.LinguaCorrente == "EN") rbLinguaEN.IsChecked = true;
            else rbLinguaIT.IsChecked = true;

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