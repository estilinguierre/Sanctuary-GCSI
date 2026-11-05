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

    /// <summary>Tôle extrudée : contour dans le plan XY (vue de face), extrudée selon Z.</summary>
    public sealed class Plate
    {
        public string Mark { get; set; }
        public string Role { get; set; }
        public List<Vec3> Outline { get; set; } = new List<Vec3>(); // Z ignoré
        public double Depth { get; set; }         // extrusion selon Z (mm)
        public double ZCenter { get; set; }       // plan médian
        public double Thickness { get; set; }     // épaisseur matière (pour nomenclature)

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
        public double Mass => Volume * 1e-9 * Profile.SteelDensity;   // kg
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
            Members.Sum(m => m.PaintArea) + Plates.Sum(p => 2 * p.OutlineArea * 1e-6 + 2 * p.Depth * p.Thickness * 1e-6);
    }
}
