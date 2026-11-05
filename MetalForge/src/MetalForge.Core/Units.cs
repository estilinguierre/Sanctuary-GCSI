using System;

namespace MetalForge.Core
{
    /// <summary>
    /// Unités : MetalForge calcule en millimètres et en degrés ; l'API SolidWorks travaille en mètres et en radians.
    /// Toute conversion passe par ici (une erreur ×1000 vient toujours d'un oubli de conversion).
    /// </summary>
    public static class Units
    {
        public const double MmParMetre = 1000.0;

        public static double MmToM(double mm) => mm / MmParMetre;
        public static double MToMm(double m) => m * MmParMetre;
        public static double DegToRad(double deg) => deg * Math.PI / 180.0;
        public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
