using System;
using System.Linq;
using System.Windows;

namespace Creazione_griglie
{
    public partial class App : Application
    {
        // Lingua attualmente attiva ("IT" / "EN"). Default IT come da App.xaml.
        public static string LinguaCorrente { get; private set; } = "IT";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Estraggo i file BaseStyles incorporati nella cartella Temp prima di avviare l'UI
            EmbeddedResourceManager.EstraiRisorseSeNecessario();

            // Applico la lingua salvata prima che la UI venga costruita (senza riscrivere il file)
            ImpostaLingua(Preferenze.Lingua, salva: false);

            base.OnStartup(e);
        }

        // Sostituisce a caldo il dizionario delle stringhe. Tutte le UI usano DynamicResource,
        // quindi il cambio si propaga immediatamente a finestre e dialog già aperti.
        // Con salva=true la scelta viene persistita tra gli avvii.
        public static void ImpostaLingua(string lingua, bool salva = true)
        {
            if (string.IsNullOrWhiteSpace(lingua)) return;
            lingua = lingua.Trim().ToUpperInvariant() == "EN" ? "EN" : "IT";

            var dicts = Current.Resources.MergedDictionaries;
            var vecchio = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Stringhe_"));
            var nuovo = new ResourceDictionary { Source = new Uri($"Lingue/Stringhe_{lingua}.xaml", UriKind.Relative) };

            if (vecchio != null)
            {
                // Sostituisco mantenendo la stessa posizione nella lista (le stringhe restano in coda).
                int idx = dicts.IndexOf(vecchio);
                dicts[idx] = nuovo;
            }
            else
            {
                dicts.Add(nuovo);
            }

            LinguaCorrente = lingua;
            if (salva) Preferenze.Lingua = lingua;
        }
    }
}
