using System;
using System.Linq;
using MetalForge.Core.Costing;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Gates;
using MetalForge.Core.Ladders;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Railings;
using MetalForge.Core.Reports;
using MetalForge.Core.Stairs;
using MetalForge.Core.Standards;
using MetalForge.Core.Structural;
using Xunit;

namespace MetalForge.Core.Tests
{
    public class SectionTests
    {
        [Fact]
        public void Ipe200_inertia_close_to_catalogue()
        {
            var s = SectionProperties.Of(ProfileCatalog.Get("IPE200"));
            // Catalogue (avec congés) : Iy = 1943 cm⁴, Iz = 142 cm⁴ — sans congés : légèrement inférieur
            Assert.InRange(s.Iy / 1e4, 1943 * 0.92, 1943);
            Assert.InRange(s.Iz / 1e4, 142 * 0.90, 142);
        }

        [Fact]
        public void Square_tube_properties()
        {
            var s = SectionProperties.Of(ProfileCatalog.Get("TC40x40x4"));
            Assert.Equal((Math.Pow(40, 4) - Math.Pow(32, 4)) / 12, s.Iy, 3);
            Assert.Equal((40 * 1600 - 32 * 1024) / 4.0, s.WplY, 3);
            Assert.True(s.WplY > s.Wy);
        }

        [Fact]
        public void Flat_bar_strong_and_weak_axis()
        {
            var s = SectionProperties.Of(ProfileCatalog.Get("PL60x10"));
            Assert.Equal(10 * 60 * 60 / 6.0, s.Wy, 3);
            Assert.Equal(60 * 10 * 10 / 6.0, s.Wz, 3);
        }

        [Fact]
        public void Bending_ratio_uses_grade()
        {
            Assert.Equal(1.0, SteelCheck.BendingRatio(235_000, 1000, SteelGrade.S235), 6);
            Assert.True(SteelCheck.BendingRatio(235_000, 1000, SteelGrade.S355) < 1);
        }
    }

    public class RailingStandardsTests
    {
        [Fact]
        public void Default_railing_passes_including_strength()
        {
            var r = new RailingDesigner().Compute();
            Assert.True(r.IsCompliant, r.ToReport());
            Assert.Contains(r.Checks, c => c.Rule == "Résistance poteau (console)" && c.Level == CheckLevel.Ok);
        }

        [Fact]
        public void Line_load_depends_on_use()
        {
            Assert.Equal(0.6, new RailingParameters { Use = RailingUse.Prive }.EffectiveLineLoad);
            Assert.Equal(1.0, new RailingParameters { Use = RailingUse.Public }.EffectiveLineLoad);
            Assert.Equal(2.5, new RailingParameters { LineLoad = 2.5 }.EffectiveLineLoad);
        }

        [Fact]
        public void Weak_post_fails_under_public_load()
        {
            var r = new RailingDesigner(new RailingParameters { Use = RailingUse.Public, PostProfile = "TC30x30x2" }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Résistance poteau (console)" && c.Level == CheckLevel.NonConforme);
        }

        [Fact]
        public void Thick_parapet_allows_800()
        {
            var thin = new RailingDesigner(new RailingParameters { Height = 850 }).Compute();
            var thick = new RailingDesigner(new RailingParameters { Height = 850, Thickness = 500 }).Compute();
            Assert.Contains(thin.Checks, c => c.Rule == "Hauteur de protection" && c.Level == CheckLevel.NonConforme);
            Assert.Contains(thick.Checks, c => c.Rule == "Hauteur de protection" && c.Level == CheckLevel.Ok);
        }

        [Fact]
        public void Horizontal_rails_in_low_zone_are_climbable_2024()
        {
            var r = new RailingDesigner(new RailingParameters { Infill = RailingInfill.LissesHorizontales }).Compute();
            var c = r.Checks.Single(x => x.Rule.StartsWith("Zone basse"));
            Assert.Equal(CheckLevel.NonConforme, c.Level);
            Assert.Contains("600", c.Rule);
        }

        [Fact]
        public void Industrial_railing_has_toe_board_and_knee_rail()
        {
            var r = new RailingDesigner(new RailingParameters
            {
                Use = RailingUse.Industriel, Infill = RailingInfill.Industriel, Height = 1100, PostProfile = "TC50x50x4"
            }).Compute();
            Assert.Contains(r.Model.Members, m => m.Role == "Plinthe");
            Assert.Contains(r.Model.Members, m => m.Role == "Sous-lisse");
            Assert.True(r.IsCompliant, r.ToReport());
            Assert.All(r.Model.Members.Where(m => m.Role == "Plinthe"), m => Assert.Equal(60, m.Start.Y, 3)); // 10 + 100/2
        }

        [Fact]
        public void Industrial_infill_refused_in_housing()
        {
            var r = new RailingDesigner(new RailingParameters { Use = RailingUse.Prive, Infill = RailingInfill.Industriel, Height = 1000 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Usage" && c.Level == CheckLevel.NonConforme);
        }

        [Fact]
        public void Industrial_height_window_1000_1100()
        {
            var r = new RailingDesigner(new RailingParameters { Use = RailingUse.Industriel, Infill = RailingInfill.Industriel, Height = 1200 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Hauteur lisse supérieure" && c.Level == CheckLevel.NonConforme);
        }
    }

    public class StairStandardsTests
    {
        [Fact]
        public void Industrial_stair_limited_to_38_degrees()
        {
            // h = 200, g = 230 → ≈ 41°
            var r = new StairDesigner(new StairParameters { Usage = StairUsage.Industriel, FloorToFloor = 3000, TargetRiser = 200, BlondelModule = 630, OpeningLength = 0 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Inclinaison" && c.Level == CheckLevel.NonConforme && c.Message.Contains("30–38"));
        }

        [Fact]
        public void Collective_housing_requires_170_and_280()
        {
            var r = new StairDesigner(new StairParameters { Usage = StairUsage.HabitationCollective, OpeningLength = 0 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Hauteur de marche" && c.Level == CheckLevel.NonConforme); // 180 > 170
            Assert.Contains(r.Checks, c => c.Rule == "Giron" && c.Level == CheckLevel.NonConforme);             // 270 < 280
        }

        [Fact]
        public void Erp_compliant_stair()
        {
            var r = new StairDesigner(new StairParameters
            {
                Usage = StairUsage.ERP, FloorToFloor = 2880, TargetRiser = 160, BlondelModule = 620, Width = 1200,
                Railing = StairRailing.DeuxCotes, HandrailHeight = 900, OpeningLength = 0, StringerProfile = "PL300x12", TreadThickness = 5
            }).Compute();
            Assert.True(r.IsCompliant, r.ToReport());
            Assert.Equal(2, r.Model.Members.Count(m => m.Role == "Main courante"));
        }

        [Fact]
        public void Erp_needs_two_handrails()
        {
            var r = new StairDesigner(new StairParameters { Usage = StairUsage.ERP, Railing = StairRailing.Droite }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Mains courantes" && c.Level == CheckLevel.NonConforme);
        }

        [Fact]
        public void Flat_thin_tread_fails_folded_tread_passes()
        {
            var flat = new StairDesigner(new StairParameters { Tread = TreadType.TolePlane, TreadThickness = 4, OpeningLength = 0 }).Compute();
            var folded = new StairDesigner(new StairParameters { Tread = TreadType.TolePliee, TreadThickness = 4, FoldHeight = 40, OpeningLength = 0 }).Compute();
            Assert.Contains(flat.Checks, c => c.Rule.StartsWith("Flèche marche") && c.Level == CheckLevel.NonConforme);
            Assert.Contains(folded.Checks, c => c.Rule.StartsWith("Flèche marche") && c.Level == CheckLevel.Ok);
            Assert.Equal(8, folded.Model.Plates.First(p => p.Role == "Marche").Outline.Count); // profil en U
        }

        [Fact]
        public void Stair_railing_follows_slope_outside_stringer()
        {
            var d = new StairDesigner(new StairParameters { Railing = StairRailing.Droite, OpeningLength = 0 });
            var g = d.Solve();
            var r = d.Compute();
            var hr = r.Model.Members.Single(m => m.Role == "Main courante");
            Assert.Equal(g.Riser / g.Going, (hr.End.Y - hr.Start.Y) / (hr.End.X - hr.Start.X), 6);
            Assert.True(hr.Start.Z > d.P.Width / 2);
        }

        [Fact]
        public void Undersized_stringer_fails()
        {
            var r = new StairDesigner(new StairParameters { StringerProfile = "PL100x8", OpeningLength = 0 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule.Contains("limon") && c.Level == CheckLevel.NonConforme);
        }
    }

    public class GateStandardsTests
    {
        [Fact]
        public void Hinge_reactions_reported()
        {
            var r = new GateDesigner(new GateParameters { Type = GateType.Battant1, ClearWidth = 1200, Height = 1000 }).Compute();
            Assert.Contains(r.Values, v => v.Key.Contains("effort horizontal par gond"));
        }

        [Fact]
        public void Motorized_gate_checks_finger_gaps_and_shear()
        {
            var r = new GateDesigner(new GateParameters { Type = GateType.Battant2, Motorized = true, MeetingGap = 10 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Écrasement des doigts" && c.Level == CheckLevel.Avertissement);
            var s = new GateDesigner(new GateParameters { Type = GateType.Coulissant, Motorized = true }).Compute();
            Assert.Contains(s.Checks, c => c.Rule == "Cisaillement au pilier");
        }
    }

    public class LadderTests
    {
        [Fact]
        public void Default_caged_ladder_is_compliant()
        {
            var d = new LadderDesigner();
            var r = d.Compute();
            Assert.True(r.IsCompliant, r.ToReport());
            Assert.InRange(d.Pitch, 225, 300);
            Assert.True(d.ActualHoopSpacing <= 1500);
            Assert.True(d.ActualBarSpacing < 300);
            Assert.Equal(d.Rungs, r.Model.Members.Count(m => m.Role == "Échelon"));
        }

        [Fact]
        public void Hoops_attach_to_stiles_and_go_away_from_wall()
        {
            var r = new LadderDesigner().Compute();
            var hoop = r.Model.Members.Where(m => m.Role == "Arceau").Take(10).ToList();
            var stiles = r.Model.Members.Where(m => m.Role == "Montant").ToList();
            Assert.Equal(stiles[1].Start.X, hoop.First().Start.X, 6);
            Assert.Equal(0, hoop.First().Start.Z, 6);
            Assert.Equal(stiles[0].Start.X, hoop.Last().End.X, 6);
            Assert.True(hoop.Max(h => h.End.Z) > 500); // diamètre 700
        }

        [Fact]
        public void Tall_building_ladder_needs_landing()
        {
            var r = new LadderDesigner(new LadderParameters { ClimbHeight = 9000 }).Compute();
            Assert.Contains(r.Checks, c => c.Rule == "Volée unique" && c.Level == CheckLevel.NonConforme);
            var m = new LadderDesigner(new LadderParameters { ClimbHeight = 9000, Use = LadderUse.Machine }).Compute();
            Assert.Contains(m.Checks, c => c.Rule == "Volée unique" && c.Level == CheckLevel.Ok);
        }

        [Fact]
        public void Ladder_without_cage_above_3m_fails()
        {
            var r = new LadderDesigner(new LadderParameters { Cage = false, ClimbHeight = 4000 }).Compute();
            Assert.False(r.IsCompliant);
        }
    }

    public class GalvanizingAndReportTests
    {
        [Fact]
        public void Vent_holes_match_iso_14713_example()
        {
            var a = GalvanizingAdvisor.ForProfile(ProfileCatalog.Get("TR60x40x2"));
            Assert.Equal(16, a.SingleHole);
            Assert.Equal(12, a.TwoHoles);
            Assert.Equal(10, a.FourHoles);
        }

        [Fact]
        public void Only_hollow_sections_need_vents()
        {
            var m = new StructureModel();
            m.AddMember("A", "x", "PL40x8", Geometry.Vec3.Zero, new Geometry.Vec3(500, 0, 0));
            m.AddMember("B", "x", "TC40x40x2", Geometry.Vec3.Zero, new Geometry.Vec3(500, 0, 0));
            var list = GalvanizingAdvisor.Advise(m);
            Assert.Single(list);
            Assert.Equal("TC40x40x2", list[0].ProfileId);
        }

        [Fact]
        public void Calculation_note_contains_drawing_checks_and_sources()
        {
            var r = new RailingDesigner().Compute();
            var nest = BarNester.Nest(CutPiece.FromModel(r.Model));
            var cost = CostEstimator.Estimate(r.Model, new CostParameters(), nest);
            var html = CalculationNote.Build("Garde-corps", r, nest, cost, galvanized: true, project: "Test");
            Assert.StartsWith("<!doctype html>", html);
            Assert.Contains("<svg", html);
            Assert.Contains("NF P01-012", html);
            Assert.Contains("Textes de référence", html);
            Assert.Contains("https://", html);
            Assert.Contains("trous d'évent", html);
            Assert.Contains("Total TTC", html);
        }

        [Fact]
        public void Norm_registry_is_complete()
        {
            foreach (var code in new[] { Norms.GardeCorps, Norms.ChargesGardeCorps, Norms.AccesMachines3, Norms.EchelleBatiment,
                                         Norms.AccessibiliteErp, Norms.AccessibiliteLogement, Norms.Execution, Norms.Portails, Norms.Galvanisation })
            {
                var n = Norms.Get(code);
                Assert.NotNull(n);
                Assert.NotEmpty(n.Values);
            }
            Assert.All(Norms.All.SelectMany(n => n.Sources), s => Assert.StartsWith("https://", s.Url));
        }
    }
}
