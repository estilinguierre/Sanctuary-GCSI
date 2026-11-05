using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MetalForge.Core.Export;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;

namespace MetalForge.Core.Atomic
{
    /// <summary>Développé 2D (pliage, chaudronnerie, limon tôle).</summary>
    public sealed class FlatPattern
    {
        public string Name { get; set; }
        public List<Vec3> Outline { get; } = new List<Vec3>();
        public List<(Vec3 A, Vec3 B, string Layer, string Label)> Lines { get; } = new List<(Vec3, Vec3, string, string)>();
        /// <summary>Cotes de traçage (repère, nez, dessus, profondeur…).</summary>
        public List<string[]> Table { get; } = new List<string[]>();
    }

    /// <summary>Résultat du moteur atomique, converti en modèle neutre MetalForge (SolidWorks, débit, devis, note de calcul).</summary>
    public sealed class AtomicResult
    {
        public string Json { get; private set; }
        public string Family { get; private set; }
        public DesignResult Design { get; private set; }
        public StructureModel Model => Design.Model;
        public List<FlatPattern> Flats { get; } = new List<FlatPattern>();
        /// <summary>Contexte maçonné (dalles, murs, piliers…) : prismes verticaux, à générer dans une pièce séparée.</summary>
        public StructureModel Masonry { get; } = new StructureModel { Name = "Maçonnerie" };
        public List<(string Choice, string Profile)> AutoChoices { get; } = new List<(string, string)>();
        public int SurfaceCount { get; private set; }
        public List<string> SurfaceLabels { get; } = new List<string>();
        public double EngineMass { get; private set; }
        public List<string> Notes { get; } = new List<string>();

        private static readonly Dictionary<string, double> Densities = new Dictionary<string, double>
        {
            ["acier"] = 7850, ["inox"] = 7900, ["alu"] = 2700, ["verre"] = 2500, ["bois"] = 550, ["grillage"] = 7850, ["beton"] = 2400, ["polycarbonate"] = 1200
        };

        public static AtomicResult FromJson(string json, ConfiguratorEngine engine)
        {
            var root = (IDictionary<string, object>)engine.ParseJson(json);
            return FromObject(root, json);
        }

        public static AtomicResult FromObject(IDictionary<string, object> root, string json)
        {
            var r = new AtomicResult { Json = json, Family = Str(root, "family") };
            var m = new StructureModel { Name = Str(root, "name") ?? "Ouvrage" };
            foreach (IDictionary<string, object> x in Arr(root, "members"))
            {
                var mem = m.AddMember("A", Str(x, "role"), Str(x, "profile"), V3(x["a"]), V3(x["b"]), Num(x, "rot"));
                mem.Mark = Str(x, "mark"); mem.CutAllowance = Num(x, "cut");
            }
            foreach (IDictionary<string, object> x in Arr(root, "plates"))
            {
                var outline = ((object[])x["outline"]).Select(o => { var a = (object[])o; return new Vec3(D(a[0]), D(a[1]), 0); });
                var pl = m.AddPlate("T", Str(x, "role"), outline, Num(x, "depth"), Num(x, "offset"), Num(x, "thickness"));
                pl.Mark = Str(x, "mark");
                pl.Plane = (PlatePlane)Enum.Parse(typeof(PlatePlane), Str(x, "plane") ?? "XY");
                pl.Material = Str(x, "material") ?? "acier";
                pl.Density = Densities.TryGetValue(pl.Material, out var rho) ? rho : 7850;
                if (x.ContainsKey("fillRatio")) pl.FillRatio = Num(x, "fillRatio");
                if (x.ContainsKey("kgm2")) pl.KgPerM2 = Num(x, "kgm2");
                if (pl.Material == "grillage" && pl.KgPerM2 <= 0) pl.KgPerM2 = 4;
            }
            var d = new DesignResult { Model = m };
            foreach (IDictionary<string, object> c in Arr(root, "checks"))
            {
                var lvl = Str(c, "level");
                d.Checks.Add(new Check
                {
                    Level = lvl == "ok" ? CheckLevel.Ok : lvl == "warn" ? CheckLevel.Avertissement : lvl == "fail" ? CheckLevel.NonConforme : CheckLevel.Info,
                    Rule = Str(c, "rule"), Message = Str(c, "msg"), Reference = Str(c, "ref")
                });
            }
            foreach (object[] kv in Arr(root, "values")) d.Add(Convert.ToString(kv[0], CultureInfo.InvariantCulture), Convert.ToString(kv[1], CultureInfo.InvariantCulture));
            r.Design = d;
            foreach (IDictionary<string, object> f in Arr(root, "flats"))
            {
                var fp = new FlatPattern { Name = Str(f, "name") };
                foreach (object[] q in (object[])f["outline"]) fp.Outline.Add(new Vec3(D(q[0]), D(q[1]), 0));
                    if (f.TryGetValue("table", out var tb) && tb is object[] rows)
                    foreach (object[] row in rows) fp.Table.Add(row.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture)).ToArray());
                if (f.TryGetValue("lines", out var ls) && ls is object[] la)
                    foreach (IDictionary<string, object> l in la)
                    {
                        var a = (object[])l["a"]; var b = (object[])l["b"];
                        fp.Lines.Add((new Vec3(D(a[0]), D(a[1]), 0), new Vec3(D(b[0]), D(b[1]), 0), Str(l, "layer") ?? "PLIAGE", Str(l, "label")));
                    }
                r.Flats.Add(fp);
            }
            foreach (IDictionary<string, object> s in Arr(root, "surfaces")) { r.SurfaceCount++; r.SurfaceLabels.Add(Str(s, "label") ?? Str(s, "kind")); }
            r.Masonry.Name = m.Name + " — maçonnerie";
            foreach (IDictionary<string, object> x in Arr(root, "masonry"))
            {
                var poly = ((object[])x["poly"]).Select(o => { var a = (object[])o; return new Vec3(D(a[0]), D(a[1]), 0); });
                double y0 = Num(x, "y0"), h = Num(x, "h");
                var pl = r.Masonry.AddPlate("MA", Str(x, "role"), poly, h, y0 + h / 2, h);
                pl.Plane = PlatePlane.XZ; pl.Material = Str(x, "material") ?? "beton"; pl.Density = 2300; pl.Mark = Str(x, "mark");
            }
            foreach (IDictionary<string, object> a in Arr(root, "auto")) r.AutoChoices.Add((Str(a, "label"), Str(a, "id")));
            foreach (var n in Arr(root, "notes")) r.Notes.Add(Convert.ToString(n, CultureInfo.InvariantCulture));
            r.EngineMass = root.ContainsKey("mass") ? Num(root, "mass") : m.TotalMass;
            return r;
        }

        /// <summary>DXF R12 des développés (calques DECOUPE, PLIAGE, TRACAGE, TEXTE), disposés côte à côte.</summary>
        public DxfWriter FlatsDxf()
        {
            var dxf = new DxfWriter(); double x0 = 0;
            foreach (var f in Flats)
            {
                if (f.Outline.Count == 0) continue;
                double mx = f.Outline.Min(p => p.X), my = f.Outline.Min(p => p.Y), w = f.Outline.Max(p => p.X) - mx;
                Vec3 T(Vec3 p) => new Vec3(p.X - mx + x0, p.Y - my, 0);
                dxf.Polygon(f.Outline.Select(T), "DECOUPE");
                foreach (var l in f.Lines) { dxf.Line(T(l.A), T(l.B), l.Layer); if (!string.IsNullOrEmpty(l.Label)) dxf.Text(T(new Vec3((l.A.X + l.B.X) / 2 + 4, (l.A.Y + l.B.Y) / 2, 0)), l.Label, 10, l.Layer); }
                dxf.Text(new Vec3(x0, -40, 0), f.Name, 14);
                x0 += w + 200;
            }
            return dxf;
        }

        private static IEnumerable<object> Arr(IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is object[] a ? a : Enumerable.Empty<object>();
        private static string Str(IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        private static double Num(IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? D(v) : 0;
        private static double D(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);
        private static Vec3 V3(object o) { var a = (object[])o; return new Vec3(D(a[0]), D(a[1]), D(a[2])); }
    }
}
