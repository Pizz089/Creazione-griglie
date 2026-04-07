using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Creazione_griglie
{
    public partial class StyleSelectorWindow : Window
    {
        public string SelectedStyleName { get; private set; }
        private string _baseStylesPath;

        public StyleSelectorWindow(string baseStylesPath)
        {
            InitializeComponent();
            _baseStylesPath = baseStylesPath;
            CaricaListaStili(_baseStylesPath);
        }

        private void CaricaListaStili(string baseStylesPath)
        {
            lbStili.Items.Clear();

            string[] dirs = Directory.GetDirectories(baseStylesPath, "style#*");
            var stiliOrdinati = dirs.Select(d => Path.GetFileName(d))
                                    .Where(IsStyleLoadable)
                                    .OrderBy(name => EstraiNumeroStile(name))
                                    .ToList();

            foreach (string folderName in stiliOrdinati)
            {
                string fullPath = Path.Combine(baseStylesPath, folderName);
                StackPanel itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };

                Image iconImage = new Image
                {
                    Width = 200,
                    Margin = new Thickness(0, 0, 20, 0),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                string iconPath = null;
                try
                {
                    var files = Directory.GetFiles(fullPath, "*icon*.*", SearchOption.TopDirectoryOnly);
                    iconPath = files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));
                }
                catch { }

                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    try
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(iconPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        iconImage.Source = bmp;
                    }
                    catch { }
                }

                TextBlock textBlock = new TextBlock
                {
                    Text = folderName,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold
                };

                itemPanel.Children.Add(iconImage);
                itemPanel.Children.Add(textBlock);

                ListBoxItem lbi = new ListBoxItem
                {
                    Content = itemPanel,
                    Tag = folderName,
                    Padding = new Thickness(5),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2E38")),
                    Cursor = Cursors.Hand
                };

                lbStili.Items.Add(lbi);
            }
        }

        private bool IsStyleLoadable(string folderName)
        {
            string name = folderName.ToLower();
            if (name == "style#l1" || name == "style#l2" || name == "style#l3") return false;
            return true;
        }

        private int EstraiNumeroStile(string folderName)
        {
            string name = folderName.ToLower().Replace("style#", "");
            if (int.TryParse(name, out int num)) return num;

            if (name == "l1") return 10000;
            if (name == "l2") return 10001;
            if (name == "l3") return 10002;
            return 99999;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ConfermaSelezione();
        }

        private void LbStili_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfermaSelezione();
        }

        // Recupero i messaggi tradotti tramite il dizionario
        private void ConfermaSelezione()
        {
            if (lbStili.SelectedItem != null)
            {
                SelectedStyleName = ((ListBoxItem)lbStili.SelectedItem).Tag.ToString();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgSelezionaPrima") as string ?? "Seleziona uno stile!",
                                Application.Current.TryFindResource("MsgAttenzione") as string ?? "Attenzione",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnElimina_Click(object sender, RoutedEventArgs e)
        {
            if (lbStili.SelectedItem != null)
            {
                string stileSelezionato = ((ListBoxItem)lbStili.SelectedItem).Tag.ToString();

                if (StyleEnvironment.IsStyleProtected(stileSelezionato))
                {
                    MessageBox.Show(Application.Current.TryFindResource("MsgStileProtetto") as string ?? "Stile protetto",
                                    Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(Application.Current.TryFindResource("MsgEliminaConferma") as string ?? "Vuoi eliminare?",
                                                          Application.Current.TryFindResource("MsgConferma") as string ?? "Conferma",
                                                          MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string cartellaDaEliminare = Path.Combine(_baseStylesPath, stileSelezionato);
                        if (Directory.Exists(cartellaDaEliminare))
                        {
                            Directory.Delete(cartellaDaEliminare, true);

                            // Messaggio semplice per l'avvenuta eliminazione
                            MessageBox.Show("Stile eliminato con successo / Style successfully deleted.",
                                            Application.Current.TryFindResource("MsgInfo") as string ?? "Info",
                                            MessageBoxButton.OK, MessageBoxImage.Information);

                            CaricaListaStili(_baseStylesPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{(Application.Current.TryFindResource("MsgErrore") as string)}:\n{ex.Message}",
                                        Application.Current.TryFindResource("MsgErrore") as string ?? "Errore",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(Application.Current.TryFindResource("MsgSelezionaPrima") as string ?? "Seleziona uno stile!",
                                Application.Current.TryFindResource("MsgAttenzione") as string ?? "Attenzione",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}