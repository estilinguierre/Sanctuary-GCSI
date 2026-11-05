using System;
using System.ComponentModel;
using MetalForge.Core.Model;

namespace MetalForge.Core.Structural
{
    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum SteelGrade { [Description("S235")] S235, [Description("S275")] S275, [Description("S355")] S355 }

    /// <summary>
    /// Vérifications élastiques simplifiées selon NF EN 1993-1-1 (Eurocode 3) :
    /// M_Ed ≤ M_c,Rd = W · f_y / γM0 (W plastique pour les sections compactes de classe 1-2, élastique sinon), avec M_Ed = γQ · M_k (γQ = 1,5 ; γG = 1,35).
    /// Pas de prise en compte du déversement ni du voilement : pré-dimensionnement uniquement.
    /// </summary>
    public static class SteelCheck
    {
        public const double E = 210000;      // MPa (N/mm²)
        public const double GammaM0 = 1.0;
        public const double GammaQ = 1.5;
        public const double GammaG = 1.35;

        public static double Fy(SteelGrade g) => g == SteelGrade.S355 ? 355 : g == SteelGrade.S275 ? 275 : 235;

        /// <summary>Taux de travail en flexion (M en N·mm, W en mm³). ≤ 1 = vérifié.</summary>
        public static double BendingRatio(double mEd, double wel, SteelGrade g) =>
            wel <= 0 ? double.PositiveInfinity : mEd / wel / (Fy(g) / GammaM0);

        public static Check Bending(string rule, double mEd, double wel, SteelGrade g)
        {
            double r = BendingRatio(mEd, wel, g);
            string msg = $"σ = {mEd / Math.Max(wel, 1e-9):0} MPa / fy {Fy(g):0} MPa → taux {r:P0}";
            const string ec3 = "NF EN 1993-1-1 (pré-dimensionnement)";
            return r <= 0.9 ? Check.Ok(rule, msg, ec3) : r <= 1.0 ? Check.Warn(rule, msg + " (marge faible)", ec3) : Check.Fail(rule, msg, ec3);
        }

        public static Check Deflection(string rule, double delta, double span, double limitRatio, string reference)
        {
            double lim = span / limitRatio;
            string msg = $"flèche {delta:0.0} mm / limite L/{limitRatio:0} = {lim:0.0} mm";
            return delta <= lim ? Check.Ok(rule, msg, reference) : Check.Fail(rule, msg, reference);
        }
    }
}
