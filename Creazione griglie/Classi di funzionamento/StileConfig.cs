using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Creazione_griglie
{
    // Definisco la radice del documento XML che descrive lo stile
    [XmlRoot("Stile")]
    public class StileConfig
    {
        [XmlElement("Nome")]
        public string Nome { get; set; } = "Nuovo Stile";

        [XmlElement("BaseDiPartenza")]
        public string BaseDiPartenza { get; set; } = "Nessuna";

        [XmlArray("Modelli")]
        [XmlArrayItem("Modello")]
        public List<ModelloStile> Modelli { get; set; } = new List<ModelloStile>();
    }

    // Rappresento il singolo file .x e le sue coordinate nello spazio
    public class ModelloStile
    {
        [XmlAttribute("File")]
        public string File { get; set; } = "";

        [XmlAttribute("X")]
        public double X { get; set; } = 0;

        [XmlAttribute("Y")]
        public double Y { get; set; } = 0;

        [XmlAttribute("Z")]
        public double Z { get; set; } = 0;

        [XmlAttribute("RotX")]
        public double RotX { get; set; } = 0;

        [XmlAttribute("RotY")]
        public double RotY { get; set; } = 0;

        [XmlAttribute("RotZ")]
        public double RotZ { get; set; } = 0;

        [XmlAttribute("Scala")]
        public double Scala { get; set; } = 1;
    }
}