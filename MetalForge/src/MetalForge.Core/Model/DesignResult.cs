using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MetalForge.Core.Model
{
    public enum CheckLevel { Ok, Avertissement, NonConforme }

    /// <summary>Résultat d'une vérification réglementaire ou de bonne pratique.</summary>
    public sealed class Check
    {
        public CheckLevel Level { get; set; }
        public string Rule { get; set; }      // ex. "Loi de Blondel"
        public string Message { get; set; }
        public string Reference { get; set; } // ex. "NF P01-012"

        public static Check Ok(string rule, string msg, string reference = null) =>
            new Check { Level = CheckLevel.Ok, Rule = rule, Message = msg, Reference = reference };
        public static Check Warn(string rule, string msg, string reference = null) =>
            new Check { Level = CheckLevel.Avertissement, Rule = rule, Message = msg, Reference = reference };
        public static Check Fail(string rule, string msg, string reference = null) =>
            new Check { Level = CheckLevel.NonConforme, Rule = rule, Message = msg, Reference = reference };

        public override string ToString()
        {
            var icon = Level == CheckLevel.Ok ? "[OK]" : Level == CheckLevel.Avertissement ? "[ATTENTION]" : "[NON CONFORME]";
            return $"{icon} {Rule} : {Message}" + (string.IsNullOrEmpty(Reference) ? "" : $" ({Reference})");
        }
    }

    /// <summary>Sortie commune de tous les assistants (escalier, garde-corps, portail…).</summary>
    public sealed class DesignResult
    {
        public StructureModel Model { get; set; }
        public List<Check> Checks { get; } = new List<Check>();
        /// <summary>Valeurs calculées clés (libellé → valeur formatée).</summary>
        public List<KeyValuePair<string, string>> Values { get; } = new List<KeyValuePair<string, string>>();

        public bool IsCompliant => Checks.All(c => c.Level != CheckLevel.NonConforme);

        public void Add(string label, string value) => Values.Add(new KeyValuePair<string, string>(label, value));

        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {Model?.Name} ===");
            foreach (var kv in Values) sb.AppendLine($"{kv.Key,-32} {kv.Value}");
            sb.AppendLine();
            sb.AppendLine("--- Vérifications ---");
            foreach (var c in Checks) sb.AppendLine(c.ToString());
            if (Model != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Masse totale : {Model.TotalMass:0.0} kg   |   Surface à traiter : {Model.TotalPaintArea:0.00} m²");
            }
            return sb.ToString();
        }
    }

    /// <summary>Contrat commun des assistants paramétriques.</summary>
    public interface IDesigner
    {
        string Title { get; }
        /// <summary>Objet de paramètres éditable (PropertyGrid).</summary>
        object Parameters { get; }
        DesignResult Compute();
    }
}
