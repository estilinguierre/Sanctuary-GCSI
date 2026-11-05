using System;
using System.Collections.Generic;
using System.Linq;
using MetalForge.Core.Geometry;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Model
{
    /// <summary>Barre (membre mécano-soudé) : un profilé suivant un segment.</summary>
    public sealed class Member
    {
        public string Mark { get; set; }          // repère : P1, B12, MC1...
        public string Role { get; set; }          // Poteau, Barreau, Main courante, Limon...
        public string ProfileId { get; set; }
        public Vec3 Start { get; set; }
        public Vec3 End { get; set; }
        /// <summary>Rotation du profil autour de son axe (degrés).</summary>
        public double RotationDeg { get; set; }

        /// <summary>Surlongueur de débit (ex. coupe d'onglet : + largeur du profil).</summary>
        public double CutAllowance { get; set; }

        /// <summary>Longueur sur axe.</summary>
        public double Length => (End - Start).Length;
        /// <summary>Longueur de débit (axe + surlongueur).</summary>
        public double CutLength => Length + CutAllowance;
        public Profile Profile => ProfileCatalog.Get(ProfileId);
        public double Mass => Profile.MassPerMeter * Length / 1000.0;
        public double PaintArea => Profile.PaintPerimeter * Length * 1e-6; // m²
    }

    /// <summary>Plan d'esquisse d'une tôle : XY = plan de face (extrusion Z), XZ = plan de dessus (extrusion Y), YZ = plan de droite (extrusion X).</summary>
    public enum PlatePlane { XY, XZ, YZ }

    /// <summary>
    /// Tôle / verre / platelage extrudé. Contour (u, v) dans le plan : XY → (x, y), XZ → (x, z), YZ → (z, y).
    /// Extrusion de <see cref="Depth"/> centrée sur <see cref="ZCenter"/> (cote le long de la normale du plan).
    /// </summary>
    public sealed class Plate
    {
        public string Mark { get; set; }
        public string Role { get; set; }
        public PlatePlane Plane { get; set; } = PlatePlane.XY;
        public List<Vec3> Outline { get; set; } = new List<Vec3>(); // (u, v) ; Z ignoré
        public double Depth { get; set; }         // extrusion (mm)
        public double ZCenter { get; set; }       // plan médian, le long de la normale
        public double Thickness { get; set; }     // épaisseur matière (pour nomenclature)
        public string Material { get; set; } = "acier";
        /// <summary>Masse volumique (kg/m³). Grillage / caillebotis : voir <see cref="KgPerM2"/>.</summary>
        public double Density { get; set; } = Profile.SteelDensity;
        public double FillRatio { get; set; } = 1;  // tôle perforée / découpée
        public double KgPerM2 { get; set; }         // > 0 : masse surfacique (grillage, caillebotis)

        /// <summary>Point 3D d'un sommet du contour, posé sur le plan médian.</summary>
        public Vec3 ToWorld(Vec3 uv, double normal = double.NaN)
        {
            double n = double.IsNaN(normal) ? ZCenter : normal;
            switch (Plane)
            {
                case PlatePlane.XZ: return new Vec3(uv.X, n, uv.Y);
                case PlatePlane.YZ: return new Vec3(n, uv.Y, uv.X);
                default: return new Vec3(uv.X, uv.Y, n);
            }
        }

        /// <summary>Contour projeté en vue de face (XY), pour les aperçus.</summary>
        public List<Vec3> FrontProjection()
        {
            if (Plane == PlatePlane.XY) return Outline;
            double u0 = Outline.Min(p => p.X), u1 = Outline.Max(p => p.X), v0 = Outline.Min(p => p.Y), v1 = Outline.Max(p => p.Y);
            double a = ZCenter - Depth / 2, b = ZCenter + Depth / 2;
            return Plane == PlatePlane.XZ
                ? new List<Vec3> { new Vec3(u0, a, 0), new Vec3(u1, a, 0), new Vec3(u1, b, 0), new Vec3(u0, b, 0) }
                : new List<Vec3> { new Vec3(a, v0, 0), new Vec3(b, v0, 0), new Vec3(b, v1, 0), new Vec3(a, v1, 0) };
        }

        public double OutlineArea
        {
            get
            {
                double a = 0;
                for (int i = 0; i < Outline.Count; i++)
                {
                    var p = Outline[i]; var q = Outline[(i + 1) % Outline.Count];
                    a += p.X * q.Y - q.X * p.Y;
                }
                return Math.Abs(a) / 2.0;
            }
        }

        public double Volume => OutlineArea * Depth;                 // mm³
        public double Mass => KgPerM2 > 0 ? OutlineArea * 1e-6 * KgPerM2 : Volume * 1e-9 * Density * FillRatio;   // kg
        /// <summary>Dimensions brutes de la tôle (L x l) pour la nomenclature.</summary>
        public (double L, double W) BlankSize
        {
            get
            {
                var xs = Outline.Select(p => p.X).ToList(); var ys = Outline.Select(p => p.Y).ToList();
                double dx = xs.Max() - xs.Min(), dy = ys.Max() - ys.Min();
                var dims = new[] { dx, dy, Depth }.OrderByDescending(v => v).ToArray();
                return (dims[0], dims[1]);
            }
        }
    }

    /// <summary>Modèle neutre d'un ouvrage : transmis au générateur SolidWorks, au débit et au chiffrage.</summary>
    public sealed class StructureModel
    {
        public string Name { get; set; } = "Ouvrage";
        public List<Member> Members { get; } = new List<Member>();
        public List<Plate> Plates { get; } = new List<Plate>();

        private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        public Member AddMember(string prefix, string role, string profileId, Vec3 a, Vec3 b, double rotationDeg = 0)
        {
            if (!ProfileCatalog.TryGet(profileId, out _))
                throw new ArgumentException($"Profilé inconnu : {profileId}");
            if ((b - a).Length < 0.5)
                throw new ArgumentException($"{role} : longueur nulle ou négative — vérifier les paramètres.");
            var m = new Member { Mark = NextMark(prefix), Role = role, ProfileId = profileId, Start = a, End = b, RotationDeg = rotationDeg };
            Members.Add(m);
            return m;
        }

        public Plate AddPlate(string prefix, string role, IEnumerable<Vec3> outline, double depth, double zCenter, double thickness)
        {
            var p = new Plate { Mark = NextMark(prefix), Role = role, Depth = depth, ZCenter = zCenter, Thickness = thickness };
            p.Outline.AddRange(outline);
            Plates.Add(p);
            return p;
        }

        /// <summary>Ajoute une copie translatée d'un autre modèle (repères renumérotés).</summary>
        public void Merge(StructureModel other, Vec3 offset)
        {
            foreach (var m in other.Members)
            {
                var c = AddMember(new string(m.Mark.TakeWhile(char.IsLetter).ToArray()), m.Role, m.ProfileId, m.Start + offset, m.End + offset, m.RotationDeg);
                c.CutAllowance = m.CutAllowance;
            }
            foreach (var p in other.Plates)
                AddPlate(new string(p.Mark.TakeWhile(char.IsLetter).ToArray()), p.Role,
                         p.Outline.Select(v => new Vec3(v.X + offset.X, v.Y + offset.Y, 0)), p.Depth, p.ZCenter + offset.Z, p.Thickness);
        }

        private string NextMark(string prefix)
        {
            _counters.TryGetValue(prefix, out var n);
            _counters[prefix] = ++n;
            return prefix + n;
        }

        public double TotalMass => Members.Sum(m => m.Mass) + Plates.Sum(p => p.Mass);

        public double TotalPaintArea =>
            Members.Sum(m => m.PaintArea) + Plates.Where(p => p.Material == "acier").Sum(p => 2 * p.OutlineArea * 1e-6);
    }
}
