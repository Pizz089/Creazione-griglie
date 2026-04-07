using System;
using System.Windows;

namespace Creazione_griglie
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Estraggo i file BaseStyles incorporati nella cartella Temp prima di avviare l'UI
            EmbeddedResourceManager.EstraiRisorseSeNecessario();
            base.OnStartup(e);
        }
    }
}