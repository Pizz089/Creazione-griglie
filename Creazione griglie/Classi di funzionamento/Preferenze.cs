using System;
using System.IO;
using System.Text.Json;

namespace Creazione_griglie
{
    // Preferenze utente persistite tra gli avvii in %AppData%\CreazioneGriglie\settings.json.
    public static class Preferenze
    {
        private static readonly string CartellaPref =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CreazioneGriglie");
        private static readonly string FilePref = Path.Combine(CartellaPref, "settings.json");

        private class Dati
        {
            public string Lingua { get; set; } = "IT";
        }

        private static Dati _cache;

        private static Dati Carica()
        {
            if (_cache != null) return _cache;
            try
            {
                _cache = File.Exists(FilePref)
                    ? JsonSerializer.Deserialize<Dati>(File.ReadAllText(FilePref)) ?? new Dati()
                    : new Dati();
            }
            catch { _cache = new Dati(); }
            return _cache;
        }

        private static void Salva()
        {
            try
            {
                Directory.CreateDirectory(CartellaPref);
                File.WriteAllText(FilePref, JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static string Lingua
        {
            get => Carica().Lingua;
            set
            {
                var d = Carica();
                if (string.Equals(d.Lingua, value, StringComparison.OrdinalIgnoreCase)) return;
                d.Lingua = value;
                Salva();
            }
        }
    }
}
