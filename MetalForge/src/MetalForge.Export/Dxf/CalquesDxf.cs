using netDxf;
using netDxf.Header;
using netDxf.Tables;

namespace MetalForge.Export.Dxf
{
    /// <summary>
    /// Calques imposés par la spécification (§ 4 Exports) : DXF R2013 relisible par SolidWorks et les machines de découpe.
    /// </summary>
    public static class CalquesDxf
    {
        public const string Contour = "CONTOUR";
        public const string Percage = "PERCAGE";
        public const string GravureMarquage = "GRAVURE_MARQUAGE";
        public const string Cotation = "COTATION";
        public const string Texte = "TEXTE";

        public static readonly string[] Tous = { Contour, Percage, GravureMarquage, Cotation, Texte };

        /// <summary>Nouveau document DXF R2013, unités millimètres, avec les cinq calques MetalForge.</summary>
        public static DxfDocument NouveauDocument()
        {
            var doc = new DxfDocument(DxfVersion.AutoCad2013);
            doc.DrawingVariables.InsUnits = netDxf.Units.DrawingUnits.Millimeters;
            Ajouter(doc, Contour, AciColor.Red);
            Ajouter(doc, Percage, AciColor.Green);
            Ajouter(doc, GravureMarquage, AciColor.Magenta);
            Ajouter(doc, Cotation, AciColor.Blue);
            Ajouter(doc, Texte, AciColor.Default);
            return doc;
        }

        private static void Ajouter(DxfDocument doc, string nom, AciColor couleur)
        {
            if (!doc.Layers.Contains(nom)) doc.Layers.Add(new Layer(nom) { Color = couleur });
        }
    }
}
