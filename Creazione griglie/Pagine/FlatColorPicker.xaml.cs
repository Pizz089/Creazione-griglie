using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DirectXEditor
{
    public partial class FlatColorPicker : Window
    {
        public Color FinalColor { get; private set; }
        private bool isUpdating = false;

        // Aggiunte le proprietà per il binding XAML
        public List<SolidColorBrush> ListaPredefiniti { get; set; }
        public ObservableCollection<SolidColorBrush> ListaRecenti { get; set; }

        public FlatColorPicker(Color startColor)
        {
            InitializeComponent();

            // Copio la lista dei predefiniti (che è fissa, ma per sicurezza la ricreo)
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
                new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };

            // Copio la lista dei recenti da ModernColorPicker per creare una lista indipendente
            ListaRecenti = new ObservableCollection<SolidColorBrush>(ModernColorPicker.ListaRecenti);

            // Imposto il DataContext per far funzionare i binding in XAML
            this.DataContext = this;

            // Imposto i valori iniziali senza far scattare gli eventi a catena
            isUpdating = true;
            slA.Value = startColor.A;
            slR.Value = startColor.R;
            slG.Value = startColor.G;
            slB.Value = startColor.B;
            isUpdating = false;

            AggiornaTestiColori();
            AggiornaAnteprima();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdating) return;
            AggiornaAnteprima();
            AggiornaTestiColori();
        }

        private void AggiornaAnteprima()
        {
            // Genero il nuovo colore leggendo i 4 slider
            FinalColor = Color.FromArgb(
                (byte)slA.Value,
                (byte)slR.Value,
                (byte)slG.Value,
                (byte)slB.Value
            );

            // Aggiorno il rettangolo visivo
            rectPreview.Background = new SolidColorBrush(FinalColor);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Aggiungo il colore alla lista locale dei recenti (non a quella di ModernColorPicker)
            var existing = ListaRecenti.FirstOrDefault(b => b.Color == FinalColor);
            if (existing != null)
            {
                ListaRecenti.Remove(existing);
            }
            ListaRecenti.Insert(0, new SolidColorBrush(FinalColor));
            if (ListaRecenti.Count > 10)
            {
                ListaRecenti.RemoveAt(ListaRecenti.Count - 1);
            }

            DialogResult = true;
            Close();
        }

        private void AggiornaTestiColori()
        {
            if (txtR != null && !txtR.IsFocused) txtR.Text = ((int)slR.Value).ToString();
            if (txtG != null && !txtG.IsFocused) txtG.Text = ((int)slG.Value).ToString();
            if (txtB != null && !txtB.IsFocused) txtB.Text = ((int)slB.Value).ToString();
            if (txtA != null && !txtA.IsFocused) txtA.Text = ((int)slA.Value).ToString();
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

                    AggiornaTestiColori();

                    isUpdating = false;
                }
            }
        }

        // Metodo per gestire il clic sui campioni di colore
        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                Color SelectedColor = brush.Color;

                isUpdating = true;
                slR.Value = SelectedColor.R;
                slG.Value = SelectedColor.G;
                slB.Value = SelectedColor.B;
                // Mantengo l'opacità corrente o la imposto a 255 se preferisci
                // slA.Value = SelectedColor.A; 
                isUpdating = false;

                AggiornaAnteprima();
                AggiornaTestiColori();
            }
        }
    }
}