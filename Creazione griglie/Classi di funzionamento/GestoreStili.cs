using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DirectXEditor
{
    public class GestoreStili
    {
        private string cartellaBaseStili;

        public GestoreStili(string percorsoRoot)
        {
            // Imposto la cartella principale dove risiederanno tutti gli stili
            cartellaBaseStili = Path.Combine(percorsoRoot, "Stili");

            // Creo la directory principale se non esiste sul disco
            if (!Directory.Exists(cartellaBaseStili))
            {
                Directory.CreateDirectory(cartellaBaseStili);
            }
        }

        // Leggo tutti gli stili presenti cercando le sottocartelle
        public List<string> OttieniStiliDisponibili()
        {
            List<string> stili = new List<string>();
            foreach (string dir in Directory.GetDirectories(cartellaBaseStili))
            {
                stili.Add(Path.GetFileName(dir));
            }
            return stili;
        }

        // Carico la configurazione XML di uno stile specifico deserializzandolo
        public StileConfig CaricaConfigurazione(string nomeStile)
        {
            string pathXml = Path.Combine(cartellaBaseStili, nomeStile, "config.xml");

            // Ritorno una configurazione vuota se il file non esiste ancora
            if (!File.Exists(pathXml)) return new StileConfig { Nome = nomeStile };

            XmlSerializer serializer = new XmlSerializer(typeof(StileConfig));
            using (StreamReader reader = new StreamReader(pathXml))
            {
                // Estraggo l'oggetto C# dal testo XML
                return (StileConfig)serializer.Deserialize(reader);
            }
        }

        // Creo un nuovo stile clonando fisicamente la cartella di uno stile base
        public bool CreaNuovoStileDaBase(string stileBase, string nomeNuovoStile)
        {
            string pathBase = Path.Combine(cartellaBaseStili, stileBase);
            string pathNuovo = Path.Combine(cartellaBaseStili, nomeNuovoStile);

            // Blocco l'operazione se la base non esiste o il nuovo stile esiste già
            if (!Directory.Exists(pathBase) || Directory.Exists(pathNuovo)) return false;

            // Genero la nuova directory per lo stile
            Directory.CreateDirectory(pathNuovo);

            // Copio tutti i file (.x, immagini texture e config.xml) dalla base al nuovo stile
            foreach (string file in Directory.GetFiles(pathBase))
            {
                string nomeFile = Path.GetFileName(file);
                File.Copy(file, Path.Combine(pathNuovo, nomeFile));
            }

            // Aggiorno il file XML con il nuovo nome e memorizzo da dove sono partito
            StileConfig config = CaricaConfigurazione(nomeNuovoStile);
            config.Nome = nomeNuovoStile;
            config.BaseDiPartenza = stileBase;

            SalvaConfigurazione(nomeNuovoStile, config);
            return true;
        }

        // Salvo le modifiche alle coordinate o all'aggiunta di modelli nel file XML
        public void SalvaConfigurazione(string nomeStile, StileConfig config)
        {
            string pathXml = Path.Combine(cartellaBaseStili, nomeStile, "config.xml");
            XmlSerializer serializer = new XmlSerializer(typeof(StileConfig));

            using (StreamWriter writer = new StreamWriter(pathXml))
            {
                // Scrivo fisicamente il file XML su disco
                serializer.Serialize(writer, config);
            }
        }
    }
}