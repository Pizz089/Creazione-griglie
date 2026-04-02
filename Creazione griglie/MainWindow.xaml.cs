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
using System.Windows.Media.Media3D;

namespace DirectXEditor
{
    public partial class MainWindow : Window
    {
        private readonly string BaseStylesPath = @"C:\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles";
        private string InternalStylesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaseStyles");

        private string cartellaAttuale = "";
        private string cartellaPadre = "";

        private ObservableCollection<MeshData> alberoGerarchico = new ObservableCollection<MeshData>();
        private MeshData pezzoSelezionato;

        private MeshData fileAttivoVisibile = null;
        private bool isUpdatingZoom = false;

        public MainWindow()
        {
            InitializeComponent();
            treeComponenti.ItemsSource = alberoGerarchico;

            // Disabilito il salvataggio all'avvio
            btnSalva.IsEnabled = false;

            // Verifico l'esistenza della directory base
            if (!Directory.Exists(BaseStylesPath))
            {
                MessageBox.Show($"Attenzione: La cartella base degli stili non esiste nel tuo sistema.\n\nPercorso cercato:\n{BaseStylesPath}", "Cartella Non Trovata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

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

        private string GetNextAvailableStyleName()
        {
            int maxNum = 12;

            if (Directory.Exists(BaseStylesPath))
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
            if (!Directory.Exists(BaseStylesPath))
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

        private void EseguiCaricamentoFisico(string percorsoScelto)
        {
            try
            {
                // Pulisco la gerarchia precedente
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

                // Scorro e analizzo tutti i file .x trovati
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

                List<MeshData> tuttiIFileX = new List<MeshData>();
                MeshHelper.EstraiTuttiIFileX(masterRoot, tuttiIFileX);
                if (tuttiIFileX.Count > 0)
                {
                    fileAttivoVisibile = tuttiIFileX[0];
                    MostraNelViewport();
                }

                XmlHelper.ConfiguraLuciDaXML(fileXml, luciGroup);

                SvuotaSelezione();
                ImpostaVistaU();

                string nomeCartellaAttuale = Path.GetFileName(cartellaAttuale);
                btnSalva.IsEnabled = !IsStyleProtectedForSave(nomeCartellaAttuale);

                txtStatus.Text = fileCaricatiConSuccesso > 0 ?
                    $"Stile caricato: visualizzazione isolata a {fileCaricatiConSuccesso} componenti chiave." :
                    "Attenzione: nessuno dei componenti chiave è stato trovato.";
            }
            catch (Exception ex) { MessageBox.Show("Errore caricamento: " + ex.Message); }
        }

        private void MostraNelViewport()
        {
            // Pulisco la scena 3D
            modelGroup.Children.Clear();
            if (fileAttivoVisibile == null) return;

            List<MeshData> tutti = new List<MeshData>();
            MeshHelper.AppiattisciGerarchia(new List<MeshData> { fileAttivoVisibile }, tutti);

            foreach (var md in tutti)
            {
                if (md.IsGroup || md.Geometry.Positions.Count == 0) continue;

                if (md.OriginalMaterial == null)
                    md.OriginalMaterial = MeshHelper.CreaMaterialeWPF(md, cartellaAttuale, cartellaPadre);

                GeometryModel3D modello = new GeometryModel3D(md.Geometry, md.OriginalMaterial)
                {
                    BackMaterial = md.OriginalMaterial
                };

                md.Model3D = modello;
                AggiornaTrasformazioneModello(md); // Centralizzo la logica della matrice

                modelGroup.Children.Add(modello);
            }
        }

        // Ricostruisco le trasformazioni 3D dell'oggetto
        private void AggiornaTrasformazioneModello(MeshData md)
        {
            if (md.Model3D == null) return;

            Transform3DGroup tGroup = new Transform3DGroup();
            // Utilizzo X, Y e Z separati per permettere la deformazione fisica
            tGroup.Children.Add(new ScaleTransform3D(md.ScaleX, md.ScaleY, md.ScaleZ));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), md.RotX)));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), md.RotY)));
            tGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), md.RotZ)));
            tGroup.Children.Add(new TranslateTransform3D(md.PosX, md.PosY, md.PosZ));

            md.Model3D.Transform = tGroup;
        }

        private void ImpostaVistaU()
        {
            Point3D cameraPos = new Point3D(0, 0, 100);
            Vector3D lookDir = new Vector3D(0, 0, -100);
            Vector3D upDir = new Vector3D(0, 1, 0);

            viewPort.SetView(cameraPos, lookDir, upDir, 500);
            viewPort.ZoomExtents();
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
            MeshData nuovoFileAttivo = MeshHelper.TrovaFileRootDiAppartenenza(alberoGerarchico.Count > 0 ? alberoGerarchico[0] : null, nuovoSelezionato);

            if (nuovoFileAttivo != null && nuovoFileAttivo != fileAttivoVisibile)
            {
                fileAttivoVisibile = nuovoFileAttivo;
                MostraNelViewport();
            }

            pezzoSelezionato = nuovoSelezionato;
            if (pezzoSelezionato != null && pezzoSelezionato.IsGroup)
                pezzoSelezionato = null;

            AggiornaPannelloProprieta();
            ApplicaTrasparenzaAlModello3D();
        }

        private void ViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(viewPort);
            HitTestResult result = VisualTreeHelper.HitTest(viewPort, p);

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

        // Questo è il nuovo evento per il pulsante della modifica fisica
        private void BtnModificaFisica_Click(object sender, RoutedEventArgs e)
        {
            // Blocco l'azione se non c'è nulla di selezionato
            if (pezzoSelezionato == null) return;

            // Creo dinamicamente la finestra per non obbligarti ad aggiungere nuovi file XAML
            Window dimensionEditor = new Window
            {
                Title = $"Modifica Dimensioni: {pezzoSelezionato.Name}",
                Width = 350,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Padding = new Thickness(15)
            };

            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical };

            // Funzione locale per generare uno slider per ogni asse
            Slider CreaSlider(string etichetta, double valoreIniziale, Action<double> onValueChanged)
            {
                panel.Children.Add(new TextBlock { Text = etichetta, Margin = new Thickness(0, 5, 0, 0), FontWeight = FontWeights.Bold });

                Slider sl = new Slider
                {
                    Minimum = 0.1,
                    Maximum = 5.0,
                    Value = valoreIniziale,
                    TickFrequency = 0.1,
                    IsSnapToTickEnabled = true,
                    Margin = new Thickness(0, 5, 0, 10)
                };

                TextBlock txtVal = new TextBlock { Text = valoreIniziale.ToString("0.0"), HorizontalAlignment = HorizontalAlignment.Right };
                panel.Children.Add(txtVal);

                // Associo l'evento di movimento per aggiornare in tempo reale la grafica
                sl.ValueChanged += (s, ev) =>
                {
                    txtVal.Text = sl.Value.ToString("0.0");
                    onValueChanged(sl.Value);
                    AggiornaTrasformazioneModello(pezzoSelezionato);
                };

                panel.Children.Add(sl);
                return sl;
            }

            CreaSlider("Larghezza (Asse X)", pezzoSelezionato.ScaleX, val => pezzoSelezionato.ScaleX = val);
            CreaSlider("Altezza (Asse Y)", pezzoSelezionato.ScaleY, val => pezzoSelezionato.ScaleY = val);
            CreaSlider("Profondità (Asse Z)", pezzoSelezionato.ScaleZ, val => pezzoSelezionato.ScaleZ = val);

            dimensionEditor.Content = panel;
            dimensionEditor.ShowDialog();
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
                    txtStatus.Text = $"Materiale applicato a {targetMeshes.Count} elementi dello stile.";
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

            if (fileAttivoVisibile != null) fileAttivoVisibile.IsSelected = true;

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
                MessageBox.Show($"Impossibile sovrascrivere lo stile di sistema '{nomeCartellaAttuale}'.\n\nGli stili da 0 a 12 e gli stili L1, L2, L3 sono protetti.", "Salvataggio Bloccato", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Vuoi davvero sovrascrivere lo stile '{nomeCartellaAttuale}'?",
                "Conferma Salvataggio", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (EseguiSalvataggio(cartellaAttuale)) txtStatus.Text = "Stile sovrascritto con successo!";
            }
        }

        private void BtnSalvaConNome_Click(object sender, RoutedEventArgs e)
        {
            if (alberoGerarchico.Count == 0 || string.IsNullOrEmpty(cartellaAttuale)) return;

            string nuovoNome = GetNextAvailableStyleName();
            MessageBoxResult result = MessageBox.Show(
                $"Il nuovo stile verrà salvato automaticamente come:\n\n{nuovoNome}\n\nVuoi procedere?",
                "Salvataggio Automatico Nuovo Stile", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string cartellaDest = Path.Combine(BaseStylesPath, nuovoNome);

                try { Directory.CreateDirectory(cartellaDest); }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Errore di permessi. Avvia il programma come Amministratore.", "Accesso Negato", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (EseguiSalvataggio(cartellaDest))
                {
                    try
                    {
                        string[] xmlOriginari = Directory.GetFiles(cartellaAttuale, "*.xml", SearchOption.AllDirectories);
                        foreach (string xml in xmlOriginari)
                        {
                            string percorsoRelativoXml = xml.Replace(cartellaAttuale, "").TrimStart('\\', '/');
                            string destinazioneXml = Path.Combine(cartellaDest, percorsoRelativoXml);
                            Directory.CreateDirectory(Path.GetDirectoryName(destinazioneXml));
                            File.Copy(xml, destinazioneXml, true);
                        }
                    }
                    catch { }

                    cartellaAttuale = cartellaDest;
                    alberoGerarchico[0].Name = nuovoNome.ToUpper();
                    btnSalva.IsEnabled = true;
                    txtStatus.Text = $"Nuovo stile '{nuovoNome}' salvato e attivato con successo!";
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
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Accesso Negato a Program Files. Avvia come Amministratore.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Si è verificato un errore:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
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