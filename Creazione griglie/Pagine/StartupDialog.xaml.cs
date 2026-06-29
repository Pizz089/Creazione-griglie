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

            // Mostro nel combo la lingua attualmente attiva (preferenza salvata)
            LinguaSelezionata = App.LinguaCorrente;
            cmbLingua.SelectedIndex = App.LinguaCorrente == "EN" ? 1 : 0;
        }

        // Intercetto il cambio lingua in tempo reale nel pop-up
        private void CmbLingua_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLingua == null) return;
            LinguaSelezionata = cmbLingua.SelectedIndex == 0 ? "IT" : "EN";
            App.ImpostaLingua(LinguaSelezionata);
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