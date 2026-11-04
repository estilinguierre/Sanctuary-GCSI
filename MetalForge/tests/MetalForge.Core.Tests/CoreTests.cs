using System;
using System.Linq;
using MetalForge.Core.Costing;
using MetalForge.Core.Export;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Gates;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Railings;
using MetalForge.Core.Stairs;
using Xunit;

namespace MetalForge.Core.Tests
{
    public class ProfileTests
    {
        [Theory]
        [InlineData("TC40x40x2", 2.31)]   // EN 10219-2 tabulé : 2,31 kg/m
        [InlineData("TC50x50x3", 4.25)]   // 4,25 kg/m
        [InlineData("TR60x40x2", 2.93)]   // 2,93 kg/m
        [InlineData("PL40x8", 2.51)]
        [InlineData("RP12", 0.888)]
        [InlineData("IPE200", 22.4)]
        [InlineData("UPN120", 13.4)]
        public void MassPerMeter_matches_tables(string id, double expected)
        {
            Assert.Equal(expected, ProfileCatalog.Get(id).MassPerMeter, 1);
        }

        [Fact]
        public void Unknown_but_wellformed_ids_are_parsed()
        {
            Assert.True(ProfileCatalog.TryGet("TR90x45x3", out var p));
            Assert.Equal(ProfileFamily.TubeRectangulaire, p.Family);
            Assert.True(ProfileCatalog.TryGet("trd42,4x2", out var r));
            Assert.Equal(ProfileFamily.TubeRond, r.Family);
            Assert.False(ProfileCatalog.TryGet("XYZ12", out _));
        }
    }

    public class StairTests
    {
        [Fact]
        public void Standard_house_stair_respects_blondel()
        {
            var d = new StairDesigner(new StairParameters { FloorToFloor = 2700, TargetRiser = 175, BlondelModule = 630, OpeningLength = 0 });
            var g = d.Solve();
            Assert.Equal(15, g.Risers);
            Assert.Equal(14, g.Treads);
            Assert.Equal(180, g.Riser, 3);
            Assert.Equal(270, g.Going, 3);
            Assert.InRange(g.Blondel, 600, 650);
            var r = d.Compute();
            Assert.True(r.IsCompliant, r.ToReport());
            Assert.Equal(14, r.Model.Plates.Count(p => p.Role == "Marche"));
            Assert.Equal(2, r.Model.Plates.Count(p => p.Role == "Limon tôle"));
        }

        [Fact]
        public void Imposed_run_sets_going()
        {
            var g = new StairDesigner(new StairParameters { FloorToFloor = 2800, TargetRiser = 175, ImposedRun = 3000 }).Solve();
            Assert.Equal(16, g.Risers);
            Assert.Equal(3000.0 / 15, g.Going, 6);
        }

        [Fact]
        public void Short_opening_fails_headroom()
        {
            var r = new StairDesigner(new StairParameters { OpeningLength = 1200 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Échappée" && c.Level == CheckLevel.NonConforme);
        }

        [Fact]
        public void Erp_stair_with_house_dimensions_is_not_compliant()
        {
            var r = new StairDesigner(new StairParameters { Usage = StairUsage.ERP, OpeningLength = 0 }).Compute();
            Assert.False(r.IsCompliant);
        }

        [Fact]
        public void Stringer_outline_top_edge_is_above_nosing_line()
        {
            var d = new StairDesigner(new StairParameters { OpeningLength = 0, StringerAboveNosing = 40 });
            var g = d.Solve();
            var o = d.StringerOutline(g);
            // rive haute au droit du nez de la marche 3 = nez + 40 mm
            double x = g.NoseX(3);
            double yTop = o.Tan * (x - o.XA) + 40;
            Assert.Equal(g.NoseY(3) + 40, yTop, 6);
            Assert.Equal(0, o.BottomStart.Y, 6);
            Assert.True(o.TopEnd.Y > o.BottomEnd.Y);
        }

        [Fact]
        public void Channel_stringer_becomes_member()
        {
            var r = new StairDesigner(new StairParameters { StringerProfile = "UPN160", OpeningLength = 0 }).Compute();
            Assert.Equal(2, r.Model.Members.Count(m => m.Role == "Limon"));
            Assert.All(r.Model.Members.Where(m => m.Role == "Limon"), m => Assert.True(m.Length > 3000));
        }
    }

    public class RailingTests
    {
        [Fact]
        public void Bars_keep_opening_under_110()
        {
            var d = new RailingDesigner(new RailingParameters { Length = 3000 });
            var r = d.Compute();
            Assert.Equal(2, d.Spans);
            Assert.True(d.ActualOpening <= 110);
            Assert.True(d.ActualOpening > 80, "pas de barreau superflu");
            Assert.Equal(3, r.Model.Members.Count(m => m.Role == "Poteau"));
            Assert.Equal(1, r.Model.Members.Count(m => m.Role == "Main courante"));
            Assert.True(r.IsCompliant, r.ToReport());
        }

        [Fact]
        public void Low_railing_is_flagged()
        {
            var r = new RailingDesigner(new RailingParameters { Height = 900 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Hauteur de protection" && c.Level == CheckLevel.NonConforme);
        }

        [Fact]
        public void Sloped_railing_rises_with_stair()
        {
            var r = new RailingDesigner(new RailingParameters { Slope = RailingSlope.Rampant, Height = 900, SlopeDeg = 30, Length = 2000 }).Compute();
            var hr = r.Model.Members.Single(m => m.Role == "Main courante");
            Assert.Equal(2000 * Math.Tan(Math.PI / 6), hr.End.Y - hr.Start.Y, 3);
        }

        [Fact]
        public void Horizontal_infill_warns_about_climbing()
        {
            var r = new RailingDesigner(new RailingParameters { Infill = RailingInfill.LissesHorizontales }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Escaladabilité");
            Assert.Contains(r.Model.Members, m => m.Role == "Lisse intermédiaire");
        }
    }

    public class GateTests
    {
        [Fact]
        public void Double_gate_has_two_symmetric_leaves()
        {
            var r = new GateDesigner(new GateParameters { Type = GateType.Battant2, ClearWidth = 3500 }).Compute();
            Assert.Equal(8, r.Model.Members.Count(m => m.Role == "Montant" || m.Role.StartsWith("Cadre")));
            Assert.Equal(2, r.Model.Members.Count(m => m.Role == "Pilier"));
            var frameBottom = r.Model.Members.First(m => m.Role == "Cadre bas");
            Assert.Equal(frameBottom.Length + 40, frameBottom.CutLength, 6); // onglet
        }

        [Fact]
        public void Sliding_gate_has_tail_brace()
        {
            var r = new GateDesigner(new GateParameters { Type = GateType.Coulissant }).Compute();
            Assert.Contains(r.Model.Members, m => m.Role == "Écharpe");
        }
    }

    public class NestingTests
    {
        [Fact]
        public void Best_fit_packs_pieces_into_minimum_bars()
        {
            var pieces = new[]
            {
                new CutPiece { Mark = "A", ProfileId = "TC40x40x2", Length = 2900, Quantity = 2 },
                new CutPiece { Mark = "B", ProfileId = "TC40x40x2", Length = 2000, Quantity = 3 },
            };
            var r = BarNester.Nest(pieces, new NestingSettings { StockLength = 6000, Kerf = 3, EndTrim = 10 });
            var p = r.Profiles.Single();
            Assert.Equal(3, p.NewBars); // [2900+2900] [2000+2000] [2000] : 3x2000 + traits + équerrage > 6000
            Assert.All(p.Bars, b => Assert.True(b.Used <= b.StockLength + 1e-6));
            Assert.Equal(5, p.Bars.Sum(b => b.Cuts.Count));
        }

        [Fact]
        public void Existing_offcuts_are_used_first_and_oversize_reported()
        {
            var s = new NestingSettings();
            s.ExistingOffcuts.Add(("PL40x8", 1500));
            var pieces = new[]
            {
                new CutPiece { Mark = "X", ProfileId = "PL40x8", Length = 1400 },
                new CutPiece { Mark = "Y", ProfileId = "PL40x8", Length = 7000 },
            };
            var r = BarNester.Nest(pieces, s).Profiles.Single();
            Assert.Equal(0, r.NewBars);
            Assert.True(r.Bars.Single().FromOffcut);
            Assert.Single(r.Oversize);
        }

        [Fact]
        public void Model_cut_list_groups_identical_bars()
        {
            var r = new RailingDesigner(new RailingParameters { Length = 3000 }).Compute();
            var cl = CutPiece.FromModel(r.Model);
            var bars = cl.Single(c => c.Role == "Barreau");
            Assert.Equal(r.Model.Members.Count(m => m.Role == "Barreau"), bars.Quantity);
        }
    }

    public class CostAndExportTests
    {
        [Fact]
        public void Estimate_totals_are_consistent()
        {
            var model = new RailingDesigner().Compute().Model;
            var e = CostEstimator.Estimate(model, new CostParameters { MarginPercent = 20, VatPercent = 20 });
            Assert.True(e.SubTotal > 0);
            Assert.Equal(e.SubTotal * 1.2 * 1.2, e.TotalTTC, 6);
        }

        [Fact]
        public void Csv_and_dxf_are_produced()
        {
            var d = new StairDesigner();
            var r = d.Compute();
            var csv = CsvExporter.BillOfMaterials(r.Model);
            Assert.Contains("Marche", csv);
            Assert.Contains("TOTAL", csv);
            var dxf = DxfDrawings.Stringer(d).ToString();
            Assert.StartsWith("  0\nSECTION", dxf);
            Assert.EndsWith("EOF\n", dxf);
            Assert.Contains("DECOUPE", dxf);
            Assert.DoesNotContain("é", dxf);
            Assert.Contains("LINE", DxfDrawings.FrontView(new GateDesigner().Compute().Model).ToString());
        }
    }
}

namespace MetalForge.Core.Tests
{
    public class MatcherTests
    {
        [Theory]
        [InlineData("SQUARE TUBE 40 X 40 X 4", "TC40x40x4")]
        [InlineData("square tube 40 x 40 x 4.sldlfp", "TC40x40x4")]
        [InlineData("Tube carré 40x40x2", "TC40x40x2")]
        [InlineData("rectangular tube 60 x 40 x 2", "TR60x40x2")]
        [InlineData("RECT TUBE 40 X 60 X 3", "TR60x40x3")]
        [InlineData("pipe 42.4 x 2.6", "TRD42.4x2.6")]
        [InlineData("Tube rond 42,4x2", "TRD42.4x2")]
        [InlineData("angle iron 40 x 40 x 4", "L40x40x4")]
        [InlineData("L 50 X 50 X 5", "L50x50x5")]
        [InlineData("Cornière 40x40x4", "L40x40x4")]
        [InlineData("c channel UPN 120", "UPN120")]
        [InlineData("IPE 200", "IPE200")]
        [InlineData("HEA140", "HEA140")]
        [InlineData("Fer plat 40 x 8", "PL40x8")]
        [InlineData("Rond plein 12", "RP12")]
        [InlineData("TC40x40x2", "TC40x40x2")]
        [InlineData("SB BEAM", null)]
        public void Matches_solidworks_descriptions(string text, string expected)
        {
            Assert.Equal(expected, MetalForge.Core.Profiles.ProfileNameMatcher.Match(text));
        }
    }
}
