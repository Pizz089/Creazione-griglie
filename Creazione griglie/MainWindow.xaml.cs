using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace DirectXEditor
{
    // Struttura dati per le miniature
    public class ViewportItem
    {
        public string Titolo { get; set; }
        public string Categoria { get; set; }
        public Model3DGroup Modello { get; set; }
        public MeshData FileRoot { get; set; }
    }

    // Gestisco le famiglie separate visivamente in galleria
    public class GalleriaGroup
    {
        public string NomeGruppo { get; set; }
        public ObservableCollection<ViewportItem> Elementi { get; set; } = new ObservableCollection<ViewportItem>();
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

        public MainWindow()
        {
            InitializeComponent();

            treeComponenti.ItemsSource = alberoGerarchico;
            listaGruppiGalleria.ItemsSource = GruppiGalleria;

            btnSalva.IsEnabled = false;

            // Innesco la ricerca dinamica del percorso all'avvio
            BaseStylesPath = TrovaPercorsoStili();

            if (string.IsNullOrEmpty(BaseStylesPath))
            {
                MessageBox.Show("Attenzione: Impossibile trovare la cartella degli stili.\n\nHo cercato il collegamento 'Focus' sul Desktop e il percorso standard (C:\\Program Files (x86)\\Steltronic\\Vision\\MediaNova\\MeshBase\\Styles), ma senza successo.\n\nAssicurati che il software Steltronic sia installato correttamente.", "Cartella Non Trovata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Cerco prima l'app Focus sul desktop per dedurre la cartella, altrimenti uso il percorso di fallback
        private string TrovaPercorsoStili()
        {
            string fallbackPath = @"C:\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles";

            try
            {
                // Percorsi dei desktop (utente e pubblico)
                var desktopPaths = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

                foreach (var desktop in desktopPaths)
                {
                    if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) continue;

                    // Cerco il file Focus.lnk
                    string[] files = Directory.GetFiles(desktop, "Focus.lnk", SearchOption.TopDirectoryOnly);

                    if (files.Length > 0)
                    {
                        // Estraggo il percorso completo del file a cui punta il collegamento (es. C:\...\Vision\Focus.exe)
                        string targetPath = OttieniDestinazioneCollegamento(files[0]);

                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            // Isolo la cartella contenitrice (es. C:\...\Vision)
                            string cartellaOrigineFocus = Path.GetDirectoryName(targetPath);

                            // Ricostruisco il percorso verso la cartella Styles
                            string pathStiliDinamico = Path.Combine(cartellaOrigineFocus, "MediaNova", "MeshBase", "Styles");

                            if (Directory.Exists(pathStiliDinamico))
                            {
                                return pathStiliDinamico;
                            }
                        }
                    }
                }
            }
    catch { }

            // Se arrivo qui, significa che il collegamento non c'è o non è valido, quindi tento la via fissa
            if (Directory.Exists(fallbackPath))
            {
                return fallbackPath;
            }

            return null; // Fallimento totale
        }

        // Leggo di nascosto dove punta un file .lnk di Windows
        private string OttieniDestinazioneCollegamento(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                return shortcut.TargetPath;
            }
            catch
            {
                return null;
            }
        }

        // Verifico se lo stile che tento di salvare è tra quelli di sistema
        private bool IsStyleProtectedForSave(string folderName)
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

        // Calcolo il numero progressivo per il nuovo stile
        private string GetNextAvailableStyleName()
        {
            int maxNum = 12;
            if (!string.IsNullOrEmpty(BaseStylesPath) && Directory.Exists(BaseStylesPath))
            {
                string[] dirs = Directory.GetDirectories(BaseStylesPath, "style#*");
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d).ToLower();
                    if (name.StartsWith("style#"))
                    {
                        string numStr = name.Substring(6);
                        if (int.TryParse(numStr, out int num))
                        {
                            if (num > maxNum) maxNum = num;
                        }
                    }
                }
            }
            return "style#" + (maxNum + 1);
        }

        private void BtnCarica_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(BaseStylesPath) || !Directory.Exists(BaseStylesPath))
            {
                MessageBox.Show("La cartella base degli stili non esiste. Impossibile caricare.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Leggo i file fisici e preparo l'albero gerarchico
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
                SvuotaSelezione();
                ImpostaVistaU();

                btnSalva.IsEnabled = !IsStyleProtectedForSave(Path.GetFileName(cartellaAttuale));

                if (fileCaricatiConSuccesso > 0)
                    txtStatus.Text = $"Stile caricato con {fileCaricatiConSuccesso} componenti chiave.";
                else
                    txtStatus.Text = $"Attenzione: nessuno dei componenti chiave trovato.";

                masterRoot.IsSelected = true;
            }
            catch (Exception ex) { MessageBox.Show("Errore caricamento: " + ex.Message); }
        }

        // Smisto i modelli nelle categorie per la galleria
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

        // Isolo il singolo modello per la modifica
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

        // Ripristino la telecamera frontale sul modello
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

        // Uso un raggio per capire quale mesh 3D ho cliccato
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
                txtTexturePath.Text = pezzoSelezionato.RemoveTexture ? "Rimossa" : Path.GetFileName(pezzoSelezionato.TextureName);

                slZoomSkin.Value = pezzoSelezionato.TextureScale;
                txtZoomSkin.Text = pezzoSelezionato.TextureScale.ToString("0.00");
            }
            else
            {
                pannelloProprieta.IsEnabled = false;
                txtNomeSelezionato.Text = "Nessuna selezione";
            }
            isUpdatingZoom = false;
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
            double maxU = 0;
            foreach (var uv in pezzoSelezionato.Geometry.TextureCoordinates) if (uv.X > maxU) maxU = uv.X;
            slZoomSkin.Value = maxU > 0 ? maxU : 1.0;
        }

        // Mostro il color picker all'utente
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
                        var coloreEsistente = ModernColorPicker.ListaRecenti.FirstOrDefault(b => b.Color == startDiffuse);
                        if (coloreEsistente != null) ModernColorPicker.ListaRecenti.Remove(coloreEsistente);

                        ModernColorPicker.ListaRecenti.Insert(0, new SolidColorBrush(startDiffuse));
                        if (ModernColorPicker.ListaRecenti.Count > 10) ModernColorPicker.ListaRecenti.RemoveAt(ModernColorPicker.ListaRecenti.Count - 1);
                    }
                }

                ModernColorPicker cp = new ModernColorPicker(startDiffuse, startAlpha, startSpecular, startEmissive, startPower);
                cp.Owner = this;

                if (cp.ShowDialog() == true)
                {
                    List<MeshData> targetMeshes = TrovaElementiCoinvolti();
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
                    txtStatus.Text = $"Materiale applicato a {targetMeshes.Count} elementi.";
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
                txtStatus.Text = $"Skin caricata su {targetMeshes.Count} elementi.";
            }
        }

        private void BtnRemoveSkin_Click(object sender, RoutedEventArgs e)
        {
            if (pezzoSelezionato == null) return;
            foreach (var m in TrovaElementiCoinvolti())
            {
                m.RemoveTexture = true;
                m.TextureRotation = 0;
                m.OriginalMaterial = MeshHelper.CreaMaterialeWPF(m, cartellaAttuale, cartellaPadre);
                if (m.Model3D != null)
                {
                    m.Model3D.Material = m.OriginalMaterial;
                    m.Model3D.BackMaterial = m.OriginalMaterial;
                }
            }
            AggiornaPannelloProprieta();
        }

        // Sfoco gli elementi non attivi per far risaltare la selezione
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
                    highlightGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, coloreTema.R, coloreTema.G, coloreTema.B))));

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
                                if (d.Brush.IsFrozen) d.Brush = d.Brush.Clone();
                                d.Brush.Opacity = 0.2;
                            }
                        }
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
            if (IsStyleProtectedForSave(nomeCartellaAttuale))
            {
                MessageBox.Show($"Impossibile sovrascrivere lo stile di sistema '{nomeCartellaAttuale}'.\n\nGli stili da 0 a 12 e gli stili L1, L2, L3 sono protetti. Utilizza il pulsante 'Salva con nome' per creare un nuovo stile modificabile.", "Salvataggio Bloccato", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Vuoi davvero sovrascrivere lo stile '{nomeCartellaAttuale}'?",
                "Conferma Salvataggio", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && EseguiSalvataggio(cartellaAttuale))
            {
                txtStatus.Text = "Stile sovrascritto con successo!";
            }
        }

        private void BtnSalvaConNome_Click(object sender, RoutedEventArgs e)
        {
            if (alberoGerarchico.Count == 0 || string.IsNullOrEmpty(cartellaAttuale)) return;
            if (string.IsNullOrEmpty(BaseStylesPath))
            {
                MessageBox.Show("Cartella degli stili non trovata. Impossibile salvare.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string nuovoNome = GetNextAvailableStyleName();
            MessageBoxResult result = MessageBox.Show(
                $"Il nuovo stile verrà salvato automaticamente come:\n\n{nuovoNome}\n\nVuoi procedere?",
                "Salvataggio Nuovo Stile", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string cartellaDest = Path.Combine(BaseStylesPath, nuovoNome);
                try { Directory.CreateDirectory(cartellaDest); }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Errore permessi. Avvia come Amministratore.", "Accesso Negato", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (EseguiSalvataggio(cartellaDest))
                {
                    try
                    {
                        string[] xmlOriginari = Directory.GetFiles(cartellaAttuale, "*.xml", SearchOption.AllDirectories);
                        foreach (string xml in xmlOriginari)
                        {
                            string rel = xml.Replace(cartellaAttuale, "").TrimStart('\\', '/');
                            string destXml = Path.Combine(cartellaDest, rel);
                            Directory.CreateDirectory(Path.GetDirectoryName(destXml));
                            File.Copy(xml, destXml, true);
                        }
                    }
                    catch { }

                    cartellaAttuale = cartellaDest;
                    alberoGerarchico[0].Name = nuovoNome.ToUpper();
                    btnSalva.IsEnabled = true;
                    txtStatus.Text = $"Nuovo stile '{nuovoNome}' salvato con successo!";
                }
            }
        }

        // Motore integrato per la generazione off-screen dell'anteprima icon.jpg
        private void GeneraIconaStile(string cartellaDestinazione, MeshData masterRoot)
        {
            try
            {
                // Pesco specificatamente il file player.x della cartella 10 Pin
                List<MeshData> tuttiIFileX = new List<MeshData>();
                MeshHelper.EstraiTuttiIFileX(masterRoot, tuttiIFileX);

                MeshData playerNode = tuttiIFileX.FirstOrDefault(f => f.OriginalFileName.ToLower().Contains("p10") && f.OriginalFileName.ToLower().EndsWith("player.x"));
                if (playerNode == null) return;

                List<MeshData> partiPlayer = new List<MeshData>();
                MeshHelper.AppiattisciGerarchia(new List<MeshData> { playerNode }, partiPlayer);

                Model3DGroup iconGroup = new Model3DGroup();

                // Rigenero i modelli tridimensionali mascherando i punteggi extra richiesti
                foreach (var md in partiPlayer)
                {
                    if (md.IsGroup || md.Geometry.Positions.Count == 0) continue;

                    string nomeUpper = md.Name.ToUpper();
                    if (nomeUpper.Contains("_SP") || nomeUpper.Contains("_C") || nomeUpper.Contains("_T"))
                        continue;

                    Material materiale = MeshHelper.CreaMaterialeWPF(md, cartellaAttuale, cartellaPadre);

                    GeometryModel3D modello = new GeometryModel3D(md.Geometry, materiale)
                    {
                        BackMaterial = materiale
                    };

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

                Rect3D bounds = iconGroup.Bounds;

                // Calcolo il fov limitato ai primi 4 frame (circa il 42% della larghezza totale dell'elemento)
                double fovWidth = bounds.SizeX * 0.42;
                double altezzaModello = bounds.SizeY;

                // Imposto una risoluzione dinamica per rispettare il corretto aspect ratio senza deformare nulla
                int altezzaRender = 200;
                int larghezzaRender = (int)(altezzaRender * (fovWidth / altezzaModello));

                OrthographicCamera camera = new OrthographicCamera(
                    new Point3D(bounds.X + (fovWidth / 2), bounds.Y + (altezzaModello / 2), bounds.Z + bounds.SizeZ + 100),
                    new Vector3D(0, 0, -1),
                    new Vector3D(0, 1, 0),
                    fovWidth
                );

                ModelVisual3D modelVisual = new ModelVisual3D { Content = iconGroup };

                Model3DGroup luciGroup = new Model3DGroup();
                luciGroup.Children.Add(new AmbientLight(Colors.White)); // Luce totale per massima leggibilità
                ModelVisual3D luciVisual = new ModelVisual3D { Content = luciGroup };

                // Creo una viewport fantasma per generare l'immagine invisibile all'utente
                Viewport3D viewport = new Viewport3D { Width = larghezzaRender, Height = altezzaRender, Camera = camera };
                viewport.Children.Add(luciVisual);
                viewport.Children.Add(modelVisual);

                // La infilo in un contenitore nero e forzo il ricalcolo degli spazi in WPF
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

        // Eseguo il salvataggio fisico del file .x sul disco
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

                    // Innesco lo scatto e la stampa dell'icona
                    GeneraIconaStile(cartellaDestinazione, alberoGerarchico[0]);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}