using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Creazione_griglie
{
    public enum StartupAction { Nessuna, CreaNuovo, Modifica }

    public partial class StartupDialog : Window
    {
        public StartupAction SceltaUtente { get; private set; } = StartupAction.Nessuna;
        public string LinguaSelezionata { get; private set; } = "IT";

        public StartupDialog()
        {
            InitializeComponent();
        }

        // Intercetto il cambio lingua in tempo reale nel pop-up
        private void CmbLingua_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLingua == null) return;
            LinguaSelezionata = cmbLingua.SelectedIndex == 0 ? "IT" : "EN";
            CambiaLinguaDizionario(LinguaSelezionata);
        }

        // Sostituisco il dizionario risorse a caldo puntando alla cartella 'Lingue'
        private void CambiaLinguaDizionario(string lingua)
        {
            var oldDict = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Stringhe_"));
            if (oldDict != null) Application.Current.Resources.MergedDictionaries.Remove(oldDict);

            // Inserisco il percorso aggiornato per caricare il dizionario corretto
            var newDict = new ResourceDictionary { Source = new Uri($"Lingue/Stringhe_{lingua}.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(newDict);
        }

        private void BtnCreaNuovo_Click(object sender, RoutedEventArgs e)
        {
            SceltaUtente = StartupAction.CreaNuovo;
            this.Close();
        }

        private void BtnModifica_Click(object sender, RoutedEventArgs e)
        {
            SceltaUtente = StartupAction.Modifica;
            this.Close();
        }

        private void BtnChiudi_Click(object sender, RoutedEventArgs e)
        {
            SceltaUtente = StartupAction.Nessuna;
            this.Close();
        }
    }
}