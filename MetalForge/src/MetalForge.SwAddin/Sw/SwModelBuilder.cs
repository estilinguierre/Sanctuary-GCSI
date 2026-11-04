using System;
using System.Collections.Generic;
using System.Linq;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.SwAddin.Settings;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace MetalForge.SwAddin.Sw
{
    /// <summary>
    /// Traduit un <see cref="StructureModel"/> en pièce mécano-soudée SolidWorks :
    ///  • une esquisse 3D par profilé + une fonction « Membre structurel » ;
    ///  • les tôles (marches, limons tôle, platines) en extrusions multicorps depuis le plan de face.
    /// Les corps apparaissent dans la liste des pièces soudées (nomenclature, débit).
    /// </summary>
    public sealed class SwModelBuilder
    {
        private const double MM = 0.001;               // l'API SolidWorks travaille en mètres
        private const int JunctionMiter = 1;           // ConnectedSegmentsJunctionType : 1 = onglet (exemples API)
        private const int CornerTreatmentMiter = 1;    // swSolidworksWeldmentEndCondOptions_e : 1 = onglet

        private readonly ISldWorks _sw;
        private readonly AddinSettings _settings;

        public List<string> Warnings { get; } = new List<string>();

        public SwModelBuilder(ISldWorks sw, AddinSettings settings)
        {
            _sw = sw;
            _settings = settings;
        }

        public ModelDoc2 Build(StructureModel model)
        {
            Warnings.Clear();
            var template = string.IsNullOrWhiteSpace(_settings.PartTemplate)
                ? _sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart)
                : _settings.PartTemplate;
            var doc = (ModelDoc2)_sw.NewDocument(template, 0, 0, 0);
            if (doc == null) throw new InvalidOperationException("Impossible de créer la pièce : vérifier le gabarit de pièce par défaut (Options > Emplacements des fichiers).");

            var sk = doc.SketchManager;
            bool addToDb = sk.AddToDB, display = sk.DisplayWhenAdded;
            sk.AddToDB = true;            // pas d'inférences ni de relations automatiques
            sk.DisplayWhenAdded = false;  // tracé beaucoup plus rapide
            try
            {
                doc.FeatureManager.InsertWeldmentFeature();

                foreach (var grp in model.Members.GroupBy(m => m.ProfileId, StringComparer.OrdinalIgnoreCase))
                    BuildMembers(doc, grp.Key, grp.ToList());

                foreach (var grp in model.Plates.GroupBy(p => (Math.Round(p.Depth, 3), Math.Round(p.ZCenter, 3))))
                    BuildPlates(doc, grp.ToList());
            }
            finally
            {
                sk.AddToDB = addToDb;
                sk.DisplayWhenAdded = display;
            }

            WriteProperties(doc, model);
            doc.ForceRebuild3(false);
            doc.ShowNamedView2("", (int)swStandardViews_e.swIsometricView);
            doc.ViewZoomtofit2();
            return doc;
        }

        // ----------------------------------------------------------------------------- membres

        private void BuildMembers(ModelDoc2 doc, string profileId, List<Member> members)
        {
            var sk = doc.SketchManager;
            sk.Insert3DSketch(true);
            var segs = new List<(Member M, SketchSegment S)>();
            foreach (var m in members)
            {
                var s = sk.CreateLine(m.Start.X * MM, m.Start.Y * MM, m.Start.Z * MM, m.End.X * MM, m.End.Y * MM, m.End.Z * MM);
                if (s != null) segs.Add((m, s));
            }
            sk.Insert3DSketch(true);
            doc.ClearSelection2(true);
            RenameLastFeature(doc, "MF_" + profileId);

            var file = _settings.ResolveProfileFile(profileId);
            if (file == null)
            {
                Warnings.Add($"{profileId} : fichier .sldlfp introuvable — esquisse créée, membres à insérer manuellement (Réglages > Associations).");
                return;
            }

            var groups = MakeGroups(segs);
            var fm = doc.FeatureManager;
            var swGroups = groups.Select(g => (object)MakeGroup(fm, g)).ToArray(); // tableau VARIANT attendu par COM
            var feat = fm.InsertStructuralWeldment4(file, JunctionMiter, true, swGroups);
            if (feat != null) { feat.Name = "MF " + profileId; return; }

            // Repli : une fonction par groupe, puis par segment (cas d'intersections refusées par SolidWorks)
            int failed = 0;
            foreach (var g in groups)
            {
                if (fm.InsertStructuralWeldment4(file, JunctionMiter, true, new object[] { MakeGroup(fm, g) }) != null) continue;
                foreach (var one in g.Segments)
                    if (fm.InsertStructuralWeldment4(file, JunctionMiter, true,
                            new object[] { MakeGroup(fm, new SegGroup { Segments = { one }, RotationDeg = g.RotationDeg }) }) == null)
                        failed++;
            }
            if (failed > 0) Warnings.Add($"{profileId} : {failed} membre(s) non créés — vérifier dans l'esquisse MF_{profileId}.");
        }

        private sealed class SegGroup
        {
            public List<(Member M, SketchSegment S)> Segments { get; } = new List<(Member, SketchSegment)>();
            public bool Chain;
            public double RotationDeg;
        }

        private static StructuralMemberGroup MakeGroup(IFeatureManager fm, SegGroup g)
        {
            var smg = (StructuralMemberGroup)fm.CreateStructuralMemberGroup();
            smg.Segments = g.Segments.Select(s => (object)s.S).ToArray();
            smg.Angle = g.RotationDeg * Math.PI / 180.0;
            smg.ApplyCornerTreatment = g.Chain;
            if (g.Chain) smg.CornerTreatmentType = CornerTreatmentMiter;
            return smg;
        }

        /// <summary>
        /// Règles SolidWorks : un groupe = segments contigus (chaîne, ex. cadre de portail → onglets)
        /// ou segments parallèles disjoints (barreaux, poteaux…).
        /// </summary>
        private static List<SegGroup> MakeGroups(List<(Member M, SketchSegment S)> segs)
        {
            var result = new List<SegGroup>();
            var left = segs.ToList();

            // 1. Chaînes (extrémités communes)
            while (true)
            {
                var seed = left.FirstOrDefault(a => left.Any(b => !ReferenceEquals(a.S, b.S) && Touch(a.M, b.M)));
                if (seed.S == null) break;
                var chain = new SegGroup { Chain = true, RotationDeg = seed.M.RotationDeg };
                chain.Segments.Add(seed); left.Remove(seed);
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (var c in left.ToList())
                        if (chain.Segments.Any(x => Touch(x.M, c.M)) && !BranchesChain(chain, c.M))
                        { chain.Segments.Add(c); left.Remove(c); grew = true; }
                }
                result.Add(chain);
            }

            // 2. Segments parallèles regroupés par direction et rotation
            foreach (var c in left)
            {
                var dir = (c.M.End - c.M.Start).Normalized();
                var g = result.FirstOrDefault(x => !x.Chain && x.RotationDeg == c.M.RotationDeg &&
                                                   Vec3.Cross((x.Segments[0].M.End - x.Segments[0].M.Start).Normalized(), dir).Length < 1e-6);
                if (g == null) { g = new SegGroup { RotationDeg = c.M.RotationDeg }; result.Add(g); }
                g.Segments.Add(c);
            }
            return result;
        }

        private static bool Touch(Member a, Member b) =>
            a.Start.Equals(b.Start) || a.Start.Equals(b.End) || a.End.Equals(b.Start) || a.End.Equals(b.End);

        /// <summary>Une chaîne SolidWorks ne peut pas avoir plus de 2 segments par sommet.</summary>
        private static bool BranchesChain(SegGroup chain, Member c)
        {
            foreach (var p in new[] { c.Start, c.End })
                if (chain.Segments.Count(x => x.M.Start.Equals(p) || x.M.End.Equals(p)) >= 2) return true;
            return false;
        }

        // ----------------------------------------------------------------------------- tôles

        private void BuildPlates(ModelDoc2 doc, List<Plate> plates)
        {
            var front = FirstRefPlane(doc);
            if (front == null) { Warnings.Add("Plan de face introuvable : tôles non créées."); return; }
            doc.ClearSelection2(true);
            front.Select2(false, 0);

            var sk = doc.SketchManager;
            sk.InsertSketch(true);
            foreach (var pl in plates)
                for (int i = 0; i < pl.Outline.Count; i++)
                {
                    var a = pl.Outline[i]; var b = pl.Outline[(i + 1) % pl.Outline.Count];
                    sk.CreateLine(a.X * MM, a.Y * MM, 0, b.X * MM, b.Y * MM, 0);
                }

            double depth = plates[0].Depth * MM, zc = plates[0].ZCenter * MM;
            bool mid = Math.Abs(zc) < 1e-9;
            double startOffset = zc - depth / 2;
            var feat = doc.FeatureManager.FeatureExtrusion2(
                true, false, false,                                   // un seul sens, pas d'inversion
                mid ? (int)swEndConditions_e.swEndCondMidPlane : (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth, 0,
                false, false, false, false, 0, 0,                     // pas de dépouille
                false, false, false, false,
                false,                                                // Merge = false : un corps par tôle
                true, true,
                mid ? (int)swStartConditions_e.swStartSketchPlane : (int)swStartConditions_e.swStartOffset,
                mid ? 0 : Math.Abs(startOffset), !mid && startOffset < 0);

            if (feat == null) { sk.InsertSketch(true); Warnings.Add($"Extrusion des tôles « {plates[0].Role} » échouée."); return; }
            feat.Name = "MF " + plates[0].Role + (mid ? "" : (zc > 0 ? " D" : " G"));
        }

        private static Feature FirstRefPlane(ModelDoc2 doc)
        {
            var f = (Feature)doc.FirstFeature();
            while (f != null)
            {
                if (f.GetTypeName2() == "RefPlane") return f; // 1er plan = Plan de face (indépendant de la langue)
                f = (Feature)f.GetNextFeature();
            }
            return null;
        }

        private static void RenameLastFeature(ModelDoc2 doc, string name)
        {
            try { var f = (Feature)doc.FeatureByPositionReverse(0); if (f != null) f.Name = name; } catch { /* nom déjà pris */ }
        }

        private static void WriteProperties(ModelDoc2 doc, StructureModel model)
        {
            var cpm = doc.Extension.get_CustomPropertyManager("");
            void Set(string n, string v) =>
                cpm.Add3(n, (int)swCustomInfoType_e.swCustomInfoText, v, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            Set("Ouvrage", model.Name);
            Set("Masse calculée MetalForge (kg)", model.TotalMass.ToString("0.0"));
            Set("Surface à traiter (m²)", model.TotalPaintArea.ToString("0.00"));
            Set("Généré par", "MetalForge pour SolidWorks");
        }
    }
}
