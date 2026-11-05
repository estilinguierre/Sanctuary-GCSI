using System.Collections.Generic;
using System.Linq;

namespace MetalForge.Core.Standards
{
    /// <summary>Fiabilité de la valeur codée.</summary>
    public enum NormConfidence
    {
        /// <summary>Plusieurs sources secondaires concordantes (fabricants, organismes, guides).</summary>
        Concordant,
        /// <summary>Source unique ou sources divergentes : à confirmer sur le texte officiel.</summary>
        AConfirmer,
        /// <summary>Usage professionnel / règle de l'art, pas d'exigence normative chiffrée.</summary>
        RegleDeLArt
    }

    public sealed class NormReference
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Edition { get; set; }
        public string Scope { get; set; }
        public NormConfidence Confidence { get; set; }
        public List<string> Values { get; } = new List<string>();
        public List<(string Label, string Url)> Sources { get; } = new List<(string, string)>();
        public override string ToString() => Code + (string.IsNullOrEmpty(Edition) ? "" : ":" + Edition);
    }

    /// <summary>
    /// Registre des normes et textes utilisés par les vérifications (recherche documentaire de novembre 2026).
    /// Les textes AFNOR sont payants : les valeurs proviennent de sources secondaires citées ci-dessous.
    /// Toujours vérifier sur le texte officiel avant un usage contractuel.
    /// </summary>
    public static class Norms
    {
        public const string GardeCorps = "NF P01-012:2024";
        public const string ChargesGardeCorps = "NF EN 1991-1-1 + NF P06-111-2/A1 (tab. 6.12)";
        public const string AccesMachines3 = "NF EN ISO 14122-3:2017";
        public const string AccesMachines4 = "NF EN ISO 14122-4";
        public const string EchelleBatiment = "NF E85-016";
        public const string AccessibiliteErp = "Arrêté du 20/04/2017 (ERP neufs)";
        public const string AccessibiliteLogement = "Arrêté du 24/12/2015 (logements neufs)";
        public const string Eurocode3 = "NF EN 1993-1-1";
        public const string Execution = "NF EN 1090-2";
        public const string Portails = "NF EN 13241 / NF EN 12604 / NF EN 12453";
        public const string Ecartements = "NF EN ISO 13854";
        public const string Galvanisation = "NF EN ISO 14713-2";

        public static IReadOnlyList<NormReference> All { get; } = Build();

        public static NormReference Get(string code) => All.FirstOrDefault(n => n.Code == code);

        private static List<NormReference> Build()
        {
            var l = new List<NormReference>();
            NormReference N(string code, string title, string edition, string scope, NormConfidence c, string[] values, params (string, string)[] sources)
            {
                var n = new NormReference { Code = code, Title = title, Edition = edition, Scope = scope, Confidence = c };
                n.Values.AddRange(values); n.Sources.AddRange(sources); l.Add(n); return n;
            }

            N(GardeCorps, "Éléments de protection contre les chutes (garde-corps, rampes, allèges)", "22/11/2024",
              "Bâtiment. Version 2024 obligatoire pour les dossiers d'urbanisme déposés depuis le 01/06/2025 et pour tous les projets depuis le 01/01/2026.",
              NormConfidence.Concordant,
              new[]
              {
                  "Hauteur de protection ≥ 1,00 m (cas général).",
                  "Hauteur réduite à 0,80 m admise pour un dispositif d'épaisseur ≥ 0,50 m (à confirmer).",
                  "Zone basse sans point d'appui portée de 0,45 m à 0,60 m ; appuis recherchés jusqu'à 0,60 m du nu intérieur et 0,30 m latéralement.",
                  "Gabarits de passage : T1 = Ø 110 mm (cas général), T2 = Ø 180 mm (certaines parties hautes), T3 = Ø 50 mm (mailles répétitives).",
                  "Vide en partie basse ≤ 110 mm, y compris entre le sol et le bas du dispositif.",
                  "Rampant d'escalier : hauteur ≥ 0,90 m mesurée à l'aplomb du nez de marche (usage courant, à confirmer en 2024).",
                  "Entraxe des poteaux ≤ 1,50 m pour hauteur de chute ≥ 1 m (fiches fabricants)."
              },
              ("AFNOR — NF P01-012", "https://www.boutique.afnor.org/fr-fr/norme/nf-p01012/travaux-de-batiment-solutions-techniques-relatives-aux-elements-de-protecti/fa179331/428035"),
              ("FFB — norme révisée applicable au 01/01/2026", "https://www.ffbatiment.fr/actualites-batiment/actualite-bam/norme-garde-corps-revisee-novembre-2024-applicable-janvier-2026"),
              ("FFB — quels changements pour les constructeurs", "https://www.ffbatiment.fr/actualites-batiment/actualite-bam/norme-des-garde-corps-revisee-quels-changements-pour-les-constructeurs"),
              ("Hilti — NF P01-012 (2024) et PROFIS", "https://www.hilti.fr/engineering/article/nouvelle-norme-nf-p-01-012-2024-garantir-la-conformite-de-vos-projets-de-garde-corps-grace-a-profis-engineering/votjll"),
              ("Horizal — fiche normes", "https://www.horizal.com/data/medias/2413/style/default/HORIZAL_NORME_NF.pdf"),
              ("Apave / CAPEB — webinaire garde-corps 2024", "https://capeb.fr/www/capeb/media//somme/document/apave-presentation-webinaire-garde-corps-7-novembre-2024.pptx.pdf"));

            N(ChargesGardeCorps, "Charges horizontales sur garde-corps", "NF P06-111-2/A1 (03/2009)",
              "Poussée linéique appliquée à 1 m au-dessus de la zone de stationnement normal.",
              NormConfidence.Concordant,
              new[]
              {
                  "Catégories A et B (logements, bureaux privés) : 0,6 kN/m (60 daN/ml).",
                  "Catégories C1 à C4, D1, D2 (lieux publics, ERP) : 1,0 kN/m (100 daN/ml).",
                  "Coefficient ELU γQ = 1,5 (NF EN 1990)."
              },
              ("AFNOR — NF P06-111-2/A1", "https://www.boutique.afnor.org/en-gb/standard/nf-p061112-a1/eurocode-1-actions-on-structures-part-11-general-actions-densities-self-wei/fa161515/32867"),
              ("Horizal — fiche normes", "https://www.horizal.com/data/medias/1815/style/default/NORMES.pdf"),
              ("Hilti — note garde-corps", "https://files-ask.hilti.com/original/ch/ch1twaxhbv.pdf"));

            N(AccesMachines3, "Moyens d'accès permanents aux machines — escaliers, échelles à marches, garde-corps", "03/2017",
              "Accès machines et installations industrielles.",
              NormConfidence.Concordant,
              new[]
              {
                  "Escalier : inclinaison 30° à 38°, 600 ≤ g + 2h ≤ 660 mm, recouvrement ≥ 10 mm.",
                  "Garde-corps requis dès 500 mm de hauteur de chute ; lisse supérieure entre 1000 et 1100 mm.",
                  "Sous-lisse : vides lisse/sous-lisse et sous-lisse/plinthe ≤ 500 mm.",
                  "Plinthe ≥ 100 mm de haut, à ≤ 10 mm du sol (une source indique 12 mm)."
              },
              ("Suva — escaliers de machines", "https://www.suva.ch/fr-ch/prevention/par-danger/materiaux-rayonnements-et-situations-a-risque/stockage-et-transport-en-entreprise-securises/voies-de-circulation-sures-a-linterieur-de-lentreprise/escaliers-de-machines-surs-pour-des-entreprises-securisees"),
              ("L'Échelle Européenne — notice garde-corps", "https://www.echelle-europeenne.com/media/12398874/3/200/1/400885_fp_garde-corps_acier6.pdf"));

            N(EchelleBatiment, "Échelles fixes — bâtiment (complétée par NF EN ISO 14122-4 pour les machines)", "",
              "Accès toiture, terrasses techniques, réservoirs.",
              NormConfidence.AConfirmer,
              new[]
              {
                  "Échelons espacés de 225 à 300 mm, régulièrement.",
                  "Largeur entre montants 400 à 600 mm ; dégagement mur/échelon ≥ 200 mm.",
                  "Premier arceau de crinoline entre 2200 et 3000 mm du sol ; arceaux espacés ≤ 1500 mm.",
                  "Filants (lisses verticales) espacés < 300 mm ; diamètre intérieur de crinoline ≈ 700 mm.",
                  "Volée unique ≤ 8 m (NF E85-016) / ≤ 10 m (ISO 14122-4 selon notice) ; paliers usuellement tous les 6 m.",
                  "Premier échelon à 100–400 mm de l'aire de départ (ISO 14122-4)."
              },
              ("HelloPro — réglementation échelles à crinoline", "https://conseils.hellopro.fr/echelle-a-crinoline-reglementation-et-normes-applicables-4128.html"),
              ("Fiche produit crinolines", "https://res.cloudinary.com/sonepar-fr/raw/upload/s--IqVkIgpJ--/v1/documents/6024/Fiche_produit_crinolines.pdf"),
              ("L'Échelle Européenne — notice crinoline", "https://www.echelle-europeenne.com/media/12499375/3/200/1/64985643_not-fr-057_notice_c.pdf"),
              ("ISO 14122-4:2016 (extrait)", "https://cdn.standards.iteh.ai/samples/61283/00e3849738db43eaaf00b3019cb4b512/ISO-14122-4-2016.pdf"));

            N(AccessibiliteErp, "Accessibilité des ERP neufs — escaliers (art. 7-1)", "JORF 26/04/2017",
              "ERP et IOP neufs.",
              NormConfidence.Concordant,
              new[]
              {
                  "Hauteur de marche ≤ 16 cm (≤ 17 cm en ERP existant), giron ≥ 28 cm.",
                  "Largeur ≥ 1,20 m entre mains courantes.",
                  "Main courante des deux côtés, entre 0,80 et 1,00 m au-dessus du nez de marche, continue et préhensible.",
                  "Prolongement horizontal de la main courante d'au moins un giron en haut et en bas."
              },
              ("Arrêté du 20 avril 2017 (préfecture Drôme)", "https://www.drome.gouv.fr/contenu/telechargement/10291/82403/file/arrete_du_20_avril_2017_relatif_a_l_accessibilite_aux_personnes_handicapees_des_erp_lors_de_leur_construction_et_des_iop.pdf"),
              ("Delabie — accessibilité", "https://www.delabie.fr/nos-services/support-technique/normes-et-reglementation/accessibilite-et-autonomie"),
              ("ABC Travaux — normes escalier", "https://abctravaux.org/norme-escalier/"));

            N(AccessibiliteLogement, "Accessibilité des bâtiments d'habitation neufs — escaliers", "",
              "Parties communes des logements collectifs, escaliers intérieurs des logements.",
              NormConfidence.AConfirmer,
              new[]
              {
                  "Parties communes : hauteur de marche ≤ 17 cm, giron ≥ 28 cm.",
                  "Escalier intérieur d'un logement neuf sur plusieurs niveaux (art. 12) : largeur ≥ 0,80 m, h ≤ 18 cm, g ≥ 24 cm."
              },
              ("ABC Travaux — normes escalier", "https://abctravaux.org/norme-escalier/"),
              ("Calculateur escalier — travail-industrie", "https://travail-industrie.com/outils/calculateur-escalier"));

            N(Eurocode3, "Calcul des structures en acier", "", "Pré-dimensionnement élastique : σ = M/W ≤ fy/γM0 (γM0 = 1,0).",
              NormConfidence.Concordant, new[] { "S235 : fy = 235 MPa ; S355 : fy = 355 MPa ; E = 210 000 MPa." });

            N(Execution, "Exécution des structures en acier", "2018",
              "S'applique aux structures porteuses ; un garde-corps non porteur n'est pas visé selon la FFB (point débattu).",
              NormConfidence.Concordant,
              new[] { "Classes EXC1 à EXC4 ; EXC2 par défaut si aucune classe n'est spécifiée.", "Classe déterminée par CC, SC et PC (annexe B)." },
              ("FFB — mettre en œuvre la NF EN 1090-2", "https://www.ffbatiment.fr/techniques-batiment/gros-oeuvre-structure/construction-charpente-metallique/dossier-bam/comment-la-mettre-en-uvre"));

            N(Portails, "Portes et portails — aspects mécaniques, sécurité d'utilisation des portails motorisés", "EN 12604:2017+A1:2020",
              "Portails industriels, commerciaux, résidentiels ; NF EN 12453 pour la motorisation.",
              NormConfidence.AConfirmer,
              new[] { "Chapitre « protection mécanique et distances de sécurité » : écartements à vérifier sur le texte.",
                      "Écartements mini (NF EN ISO 13854) : doigt 25 mm, main 100 mm, corps 500 mm." },
              ("EN 12604:2017+A1:2020", "https://genorma.com/en/standards/en-12604-2017-a1-2020"),
              ("NF EN ISO 13854 (AFNOR)", "https://www.boutique.afnor.org/en-gb/standard/nf-en-iso-13854/safety-of-machinery-minimum-gaps-to-avoid-crushing-of-parts-of-the-human-bo/fa191736/84559"),
              ("Suva — écartements doigt / main", "https://www.suva.ch/fr-ch/prevention/par-branche/securite-au-travail-dans-l-artisanat-et-l-industrie/metallurgie/mesures-de-protection-dans-la-metallurgie/presses-securisees-dispositifs-de-protection"));

            N(Galvanisation, "Galvanisation à chaud — conception des pièces", "2019",
              "Trous d'évent et de vidange des corps creux.",
              NormConfidence.AConfirmer,
              new[] { "Ouvertures ≈ 25 % du diamètre équivalent de la section (exemple 60x40 : 1 x Ø16, 2 x Ø12 ou 4 x Ø10 à chaque extrémité).",
                      "Diamètre minimal pratiqué : 10 mm (galvaniseurs)." },
              ("Coatinc — affiche fabrication et construction", "https://www.coatinc.com/wp-content/uploads/2018/09/tcc-telechargement-affiche-fabrication-et-construction.pdf"),
              ("Thomas Graham — minimum hole size", "https://thomas-graham.co.uk/files/ww/Minimum-Hole-Size-Note-with-Table-1.pdf"),
              ("ISO 14713-2:2019", "https://standards.iteh.ai/catalog/standards/iso/38aecb26-82ea-4c96-a898-8baa1f45a33e/iso-14713-2-2019"));
            return l;
        }
    }
}
