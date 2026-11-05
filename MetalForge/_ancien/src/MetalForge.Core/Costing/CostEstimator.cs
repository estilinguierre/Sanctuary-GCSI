using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Model;

namespace MetalForge.Core.Costing
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum Finish
    {
        [Description("Brut")] Brut,
        [Description("Peinture (primaire + finition)")] Peinture,
        [Description("Thermolaquage")] Thermolaquage,
        [Description("Galvanisation à chaud")] Galvanisation,
        [Description("Galva + thermolaquage")] GalvaThermolaquage
    }

    /// <summary>Hypothèses de chiffrage (HT). Valeurs par défaut indicatives — à adapter à l'atelier.</summary>
    public sealed class CostParameters
    {
        [Category("Matière"), DisplayName("Prix profilés (€/kg)")] public double SteelPricePerKg { get; set; } = 1.80;
        [Category("Matière"), DisplayName("Prix tôle (€/kg)")] public double PlatePricePerKg { get; set; } = 1.60;
        [Category("Matière"), DisplayName("Découpe laser tôle (€/kg)")] public double LaserPerKg { get; set; } = 1.20;
        [Category("Matière"), DisplayName("Consommables (% matière)")] public double ConsumablesPercent { get; set; } = 5;

        [Category("Main d'œuvre"), DisplayName("Taux horaire atelier (€/h)")] public double ShopRate { get; set; } = 55;
        [Category("Main d'œuvre"), DisplayName("Temps par coupe (min)")] public double MinutesPerCut { get; set; } = 4;
        [Category("Main d'œuvre"), DisplayName("Temps par assemblage soudé (min)")] public double MinutesPerJoint { get; set; } = 10;
        [Category("Main d'œuvre"), DisplayName("Meulage / finition (% soudure)")] public double GrindingPercent { get; set; } = 30;
        [Category("Main d'œuvre"), DisplayName("Forfait préparation (h)")] public double SetupHours { get; set; } = 1.5;

        [Category("Finition"), DisplayName("Traitement")] public Finish Finish { get; set; } = Finish.Thermolaquage;
        [Category("Finition"), DisplayName("Peinture (€/m²)")] public double PaintPerM2 { get; set; } = 18;
        [Category("Finition"), DisplayName("Thermolaquage (€/m²)")] public double PowderPerM2 { get; set; } = 25;
        [Category("Finition"), DisplayName("Galvanisation (€/kg)")] public double GalvaPerKg { get; set; } = 0.95;

        [Category("Pose"), DisplayName("Temps de pose (h)")] public double InstallHours { get; set; } = 0;
        [Category("Pose"), DisplayName("Taux horaire pose (€/h)")] public double InstallRate { get; set; } = 50;
        [Category("Pose"), DisplayName("Déplacement (€)")] public double Travel { get; set; } = 0;

        [Category("Commercial"), DisplayName("Coefficient de marge (%)")] public double MarginPercent { get; set; } = 25;
        [Category("Commercial"), DisplayName("TVA (%)"), Description("20 % ; 10 % rénovation logement > 2 ans sous conditions.")] public double VatPercent { get; set; } = 20;
    }

    public sealed class CostLine
    {
        public string Label; public string Detail; public double Amount;
    }

    public sealed class CostEstimate
    {
        public List<CostLine> Lines { get; } = new List<CostLine>();
        public double SubTotal => Lines.Sum(l => l.Amount);
        public double Margin;
        public double TotalHT => SubTotal + Margin;
        public double Vat;
        public double TotalTTC => TotalHT + Vat;
        public double ShopHours;

        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ESTIMATION DE PRIX (indicative) ===");
            foreach (var l in Lines) sb.AppendLine($"{l.Label,-28} {l.Detail,-38} {l.Amount,10:0.00} €");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"{"Déboursé sec",-67} {SubTotal,10:0.00} €");
            sb.AppendLine($"{"Marge",-67} {Margin,10:0.00} €");
            sb.AppendLine($"{"TOTAL HT",-67} {TotalHT,10:0.00} €");
            sb.AppendLine($"{"TVA",-67} {Vat,10:0.00} €");
            sb.AppendLine($"{"TOTAL TTC",-67} {TotalTTC,10:0.00} €");
            sb.AppendLine($"Temps atelier estimé : {ShopHours:0.0} h");
            return sb.ToString();
        }
    }

    public static class CostEstimator
    {
        /// <summary>
        /// Chiffre un ouvrage : matière achetée (d'après le plan de débit, chutes comprises), tôles, main d'œuvre, finition, pose.
        /// </summary>
        public static CostEstimate Estimate(StructureModel model, CostParameters c, NestingResult nesting = null)
        {
            nesting = nesting ?? BarNester.Nest(CutPiece.FromModel(model));
            var e = new CostEstimate();

            double barKg = nesting.PurchasedMass;
            e.Lines.Add(new CostLine { Label = "Profilés (barres achetées)", Detail = $"{barKg:0.0} kg x {c.SteelPricePerKg:0.00} €/kg", Amount = barKg * c.SteelPricePerKg });

            double plateKg = model.Plates.Sum(p => p.Mass);
            if (plateKg > 0)
            {
                e.Lines.Add(new CostLine { Label = "Tôles", Detail = $"{plateKg:0.0} kg x {c.PlatePricePerKg:0.00} €/kg", Amount = plateKg * c.PlatePricePerKg });
                e.Lines.Add(new CostLine { Label = "Découpe laser", Detail = $"{plateKg:0.0} kg x {c.LaserPerKg:0.00} €/kg", Amount = plateKg * c.LaserPerKg });
            }
            double matter = e.Lines.Sum(l => l.Amount);
            e.Lines.Add(new CostLine { Label = "Consommables", Detail = $"{c.ConsumablesPercent:0} % matière", Amount = matter * c.ConsumablesPercent / 100 });

            int cuts = model.Members.Count * 2;      // 2 coupes par barre
            int joints = model.Members.Count * 2 + model.Plates.Count; // 2 extrémités soudées par barre (approx.)
            double weldMin = joints * c.MinutesPerJoint;
            double hours = c.SetupHours + (cuts * c.MinutesPerCut + weldMin * (1 + c.GrindingPercent / 100)) / 60.0;
            e.ShopHours = hours;
            e.Lines.Add(new CostLine { Label = "Fabrication atelier", Detail = $"{hours:0.0} h x {c.ShopRate:0} €/h ({cuts} coupes, {joints} soudures)", Amount = hours * c.ShopRate });

            double area = model.TotalPaintArea, kg = model.TotalMass;
            switch (c.Finish)
            {
                case Finish.Peinture: e.Lines.Add(new CostLine { Label = "Peinture", Detail = $"{area:0.00} m² x {c.PaintPerM2} €", Amount = area * c.PaintPerM2 }); break;
                case Finish.Thermolaquage: e.Lines.Add(new CostLine { Label = "Thermolaquage", Detail = $"{area:0.00} m² x {c.PowderPerM2} €", Amount = area * c.PowderPerM2 }); break;
                case Finish.Galvanisation: e.Lines.Add(new CostLine { Label = "Galvanisation", Detail = $"{kg:0.0} kg x {c.GalvaPerKg} €", Amount = kg * c.GalvaPerKg }); break;
                case Finish.GalvaThermolaquage:
                    e.Lines.Add(new CostLine { Label = "Galvanisation", Detail = $"{kg:0.0} kg x {c.GalvaPerKg} €", Amount = kg * c.GalvaPerKg });
                    e.Lines.Add(new CostLine { Label = "Thermolaquage", Detail = $"{area:0.00} m² x {c.PowderPerM2} €", Amount = area * c.PowderPerM2 }); break;
            }

            if (c.InstallHours > 0)
                e.Lines.Add(new CostLine { Label = "Pose", Detail = $"{c.InstallHours:0.0} h x {c.InstallRate:0} €/h", Amount = c.InstallHours * c.InstallRate });
            if (c.Travel > 0)
                e.Lines.Add(new CostLine { Label = "Déplacement", Detail = "", Amount = c.Travel });

            e.Margin = e.SubTotal * c.MarginPercent / 100;
            e.Vat = e.TotalHT * c.VatPercent / 100;
            return e;
        }
    }
}
