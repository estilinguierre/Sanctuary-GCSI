using System.Reflection;

namespace MetalForge.Core
{
    /// <summary>Identité du logiciel (affichée dans le volet et le journal).</summary>
    public static class MetalForgeInfo
    {
        public const string Nom = "MetalForge";
        public static string Version => typeof(MetalForgeInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
