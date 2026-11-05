using System;
using System.ComponentModel;
using System.Linq;
using MetalForge.Core.Atomic;
using MetalForge.Core.Export;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Model;
using MetalForge.Core.Reports;
using Xunit;
using Xunit.Abstractions;

namespace MetalForge.Core.Tests
{
    /// <summary>Le moteur JavaScript partagé, exécuté en C# (Jint), et sa conversion en modèle MetalForge.</summary>
    public class AtomicTests
    {
        private static readonly Lazy<ConfiguratorEngine> Engine = new Lazy<ConfiguratorEngine>(() => new ConfiguratorEngine());
        private readonly ITestOutputHelper _out;
        public AtomicTests(ITestOutputHelper output) { _out = output; }

        public static TheoryData<string> Families()
        {
            var d = new TheoryData<string>();
            foreach (var f in Engine.Value.Families) d.Add(f.Id);
            return d;
        }

        [Fact]
        public void Engine_exposes_all_families()
        {
            Assert.Equal(21, Engine.Value.Families.Count);
            Assert.Contains(Engine.Value.Families, f => f.Id == "escalier-helicoidal");
            Assert.Contains(Engine.Value.Families, f => f.Id == "pliage");
            Assert.All(Engine.Value.Families, f => Assert.NotEmpty(f.Params));
        }

        [Theory, MemberData(nameof(Families))]
        public void Every_family_converts_to_a_consistent_model(string id)
        {
            var r = Engine.Value.Build(id);
            Assert.True(r.Design.IsCompliant, string.Join(" | ", r.Design.Checks.Where(c => c.Level == CheckLevel.NonConforme).Select(c => c.Rule)));
            Assert.True(r.Model.Members.Count + r.Model.Plates.Count + r.SurfaceCount > 0);
            // Les catalogues de profilés C# et JavaScript doivent donner la même masse (± 4 %)
            double cs = r.Model.TotalMass, js = r.EngineMass;
            double surfaces = js - cs; // la chaudronnerie de révolution n'existe que côté moteur
            if (r.SurfaceCount == 0) Assert.InRange(cs, js * 0.96, js * 1.04);
            else Assert.True(surfaces > 0);
            _out.WriteLine($"{id}: C# {cs:0.0} kg / moteur {js:0.0} kg");
            // Débit et devis C# fonctionnent sur le modèle importé
            var n = BarNester.Nest(CutPiece.FromModel(r.Model));
            Assert.All(n.Profiles.SelectMany(p => p.Bars), b => Assert.True(b.Remaining >= -0.5));
        }

        [Fact]
        public void Plates_keep_plane_material_and_marks()
        {
            var r = Engine.Value.Build("escalier-quart");
            var tread = r.Model.Plates.First(p => p.Role == "Marche");
            Assert.Equal(PlatePlane.XZ, tread.Plane);
            Assert.StartsWith("M", tread.Mark);
            var g = Engine.Value.Build("garde-corps", new System.Collections.Generic.Dictionary<string, object> { ["infill"] = "verre" });
            Assert.Contains(g.Model.Plates, p => p.Material == "verre" && Math.Abs(p.Density - 2500) < 1e-9);
        }

        [Fact]
        public void Flats_export_to_dxf_and_note_builds()
        {
            var r = Engine.Value.Build("pliage");
            Assert.Single(r.Flats);
            var dxf = r.FlatsDxf().ToString();
            Assert.Contains("PLIAGE", dxf);
            Assert.Contains("DECOUPE", dxf);
            var html = CalculationNote.Build("Pliage", r.Design);
            Assert.Contains("<svg", html);
            Assert.Contains(Engine.Value.DxfFromResultJson(r.Json).Split('\n')[0], "0");
        }

        [Fact]
        public void Parameter_bag_follows_schema_and_show_rules()
        {
            var d = new AtomicDesigner(Engine.Value, "garde-corps");
            var props = TypeDescriptor.GetProperties(d.Bag);
            Assert.NotNull(props["H"]);
            Assert.Null(props["glass"]); // visible seulement pour le remplissage verre
            props["infill"].SetValue(d.Bag, "Verre feuilleté");
            Assert.Equal("verre", d.Bag.Values["infill"]);
            Assert.NotNull(TypeDescriptor.GetProperties(d.Bag)["glass"]);
            var res = d.Compute();
            Assert.Contains(res.Model.Plates, p => p.Material == "verre");
            props["__family"].SetValue(d.Bag, d.Bag.Family.ToString().Replace("Garde-corps", "x")); // valeur inconnue : ignorée
            Assert.Equal("garde-corps", d.Bag.Family.Id);
        }

        [Fact]
        public void Same_engine_as_web_reports_din_bend()
        {
            var r = Engine.Value.Build("pliage", new System.Collections.Generic.Dictionary<string, object> { ["preset"] = "corniere", ["a"] = 50.0, ["b"] = 50.0, ["t"] = 2.0, ["Ri"] = 2.0 });
            Assert.Contains(r.Design.Values, v => v.Key == "Développé" && v.Value.StartsWith("96.2"));
        }

        [Fact]
        public void Front_projection_of_horizontal_plate()
        {
            var r = Engine.Value.Build("mezzanine");
            var deck = r.Model.Plates.First(p => p.Plane == PlatePlane.XZ && p.Role == "Caillebotis");
            var proj = deck.FrontProjection();
            Assert.Equal(4, proj.Count);
            Assert.Equal(2800, proj.Max(q => q.Y), 3);
            Assert.Contains("<polygon", SvgDrawings.FrontView(r.Model));
        }
    }
}
