using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DirectXEditor
{
    public partial class StyleSelectorWindow : Window
    {
        // Espongo il nome dello stile selezionato
        public string SelectedStyleName { get; private set; }

        public StyleSelectorWindow(string baseStylesPath)
        {
            // Inizializzo l'interfaccia XAML
            InitializeComponent();

            // Popolo la lista appena la finestra viene creata
            CaricaListaStili(baseStylesPath);
        }

        private void CaricaListaStili(string baseStylesPath)
        {
            // Filtro e ordino le cartelle in modo intelligente
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

                // Cerco l'icona dello stile
                string iconPath = null;
                try
                {
                    var files = Directory.GetFiles(fullPath, "*icon*.*", SearchOption.TopDirectoryOnly);
                    iconPath = files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));
                }
                catch { }

                // Carico l'immagine se l'ho trovata
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

                // Aggiungo l'elemento alla ListBox definita nello XAML
                lbStili.Items.Add(lbi);
            }
        }

        // Gestisco il click sul bottone
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ConfermaSelezione();
        }

        // Gestisco il doppio clic sulla riga
        private void LbStili_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfermaSelezione();
        }

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
                MessageBox.Show("Seleziona prima uno stile dalla lista!", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    }
}