using System;
using System.IO;
using System.Linq;
using MetalForge.Core;
using MetalForge.Core.Diagnostics;
using MetalForge.Export.Dxf;
using Xunit;

namespace MetalForge.Tests
{
    /// <summary>Lot L0 (F-GEN-03) : unités, journal, calques DXF.</summary>
    public class SqueletteTests
    {
        [Theory]
        [InlineData(1000, 1)]
        [InlineData(2.5, 0.0025)]
        [InlineData(0, 0)]
        public void Conversion_mm_metre_aller_retour(double mm, double m)
        {
            Assert.Equal(m, Units.MmToM(mm), 12);
            Assert.Equal(mm, Units.MToMm(Units.MmToM(mm)), 9);
        }

        [Fact]
        public void Conversion_degres_radians()
        {
            Assert.Equal(Math.PI / 2, Units.DegToRad(90), 12);
            Assert.Equal(45, Units.RadToDeg(Math.PI / 4), 12);
        }

        [Fact]
        public void Journal_ecrit_dans_le_fichier_du_jour()
        {
            var dir = Path.Combine(Path.GetTempPath(), "mf-journal-" + Guid.NewGuid().ToString("N"));
            var ancien = Journal.Dossier;
            try
            {
                Journal.Dossier = dir;
                Journal.Info("Démarrage MetalForge");
                Journal.Erreur("Échec de test", new InvalidOperationException("détail"));
                var txt = File.ReadAllText(Journal.FichierDuJour);
                Assert.Contains("[INFO] Démarrage MetalForge", txt);
                Assert.Contains("[ERREUR] Échec de test", txt);
                Assert.Contains("détail", txt);
            }
            finally
            {
                Journal.Dossier = ancien;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Journal_ne_leve_jamais_d_exception()
        {
            var ancien = Journal.Dossier;
            try
            {
                Journal.Dossier = Path.Combine(Path.GetTempPath(), "\0invalide");
                Journal.Info("ignoré");
            }
            finally { Journal.Dossier = ancien; }
        }

        [Fact]
        public void Document_dxf_R2013_avec_les_cinq_calques()
        {
            var doc = CalquesDxf.NouveauDocument();
            using (var ms = new MemoryStream())
            {
                Assert.True(doc.Save(ms));
                ms.Position = 0;
                var relu = netDxf.DxfDocument.Load(ms);
                Assert.Equal(netDxf.Header.DxfVersion.AutoCad2013, relu.DrawingVariables.AcadVer);
                foreach (var nom in CalquesDxf.Tous) Assert.True(relu.Layers.Contains(nom), nom);
            }
        }

        [Fact]
        public void Version_du_logiciel_lisible()
        {
            Assert.Equal("MetalForge", MetalForgeInfo.Nom);
            Assert.Equal(3, MetalForgeInfo.Version.Split('.').Length);
        }
    }
}
