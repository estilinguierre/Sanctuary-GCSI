# ⚖️ Normes et textes appliqués par MetalForge

Recherche documentaire du **5 novembre 2026**. Les normes AFNOR sont payantes : les valeurs codées proviennent
de **sources secondaires** (fabricants, organismes, guides) recoupées entre elles. Chaque valeur est classée :

| Fiabilité | Signification |
|---|---|
| ✅ Concordant | plusieurs sources indépendantes donnent la même valeur |
| ⚠️ À confirmer | source unique ou divergences : vérifier sur le texte officiel |
| 🛠️ Règle de l'art | usage professionnel, pas d'exigence chiffrée trouvée |

Le registre complet (valeurs + liens) est dans `src/MetalForge.Core/Standards/Norms.cs` et repris
automatiquement dans chaque **note de calcul** générée.

---

## 🛡️ Garde-corps — NF P01-012 (version du 22/11/2024) ✅

**Calendrier :**
- dossiers d'urbanisme déposés depuis le 01/06/2025 : version 2024 obligatoire ;
- tous les projets depuis le 01/01/2026 : version 2024 obligatoire.

| Règle | Valeur | Contrôle MetalForge |
|---|---|---|
| Hauteur de protection | ≥ 1,00 m | ✔ |
| Dispositif épais (≥ 0,50 m) | hauteur réduite à 0,80 m ⚠️ | ✔ (paramètre « Épaisseur du dispositif ») |
| Rampant d'escalier | ≥ 0,90 m à l'aplomb du nez ⚠️ | ✔ |
| **Zone basse sans point d'appui** | **0 – 0,60 m** (était 0,45 m) | ✔ lisses horizontales refusées dans la zone |
| Gabarit T1 (cas général) | Ø 110 mm | ✔ vides de remplissage |
| Gabarit T2 (parties hautes) | Ø 180 mm | info |
| Gabarit T3 (mailles répétitives) | Ø 50 mm | info |
| Vide en partie basse | ≤ 110 mm | ✔ |
| Entraxe poteaux (chute ≥ 1 m) | ≤ 1,50 m (fiches fabricants) | ✔ |

**Charges (NF EN 1991-1-1 + NF P06-111-2/A1, tableau 6.12)** ✅ :
- logements et bureaux privés (catégories A et B) : **0,6 kN/m** ;
- lieux publics et ERP (catégories C et D) : **1,0 kN/m** ;
- application à 1 m au-dessus de la zone de stationnement normal.

MetalForge vérifie :
- le poteau en console : M = 1,5·q·e·H, comparé à W_pl·fy ;
- la main courante ;
- la flèche en tête de poteau ;
- l'effort de traction par cheville de platine.

Sources :
- [FFB — norme révisée applicable au 01/01/2026](https://www.ffbatiment.fr/actualites-batiment/actualite-bam/norme-garde-corps-revisee-novembre-2024-applicable-janvier-2026)
- [FFB — changements pour les constructeurs](https://www.ffbatiment.fr/actualites-batiment/actualite-bam/norme-des-garde-corps-revisee-quels-changements-pour-les-constructeurs)
- [Hilti — NF P01-012 (2024)](https://www.hilti.fr/engineering/article/nouvelle-norme-nf-p-01-012-2024-garantir-la-conformite-de-vos-projets-de-garde-corps-grace-a-profis-engineering/votjll)
- [Horizal — fiche normes](https://www.horizal.com/data/medias/2413/style/default/HORIZAL_NORME_NF.pdf)
- [Horizal — charges](https://www.horizal.com/data/medias/1815/style/default/NORMES.pdf)
- [AFNOR — NF P01-012](https://www.boutique.afnor.org/fr-fr/norme/nf-p01012/travaux-de-batiment-solutions-techniques-relatives-aux-elements-de-protecti/fa179331/428035)
- [AFNOR — NF P06-111-2/A1](https://www.boutique.afnor.org/en-gb/standard/nf-p061112-a1/eurocode-1-actions-on-structures-part-11-general-actions-densities-self-wei/fa161515/32867)

## 🏭 Accès machines — NF EN ISO 14122-3 (03/2017) ✅

| Règle | Valeur |
|---|---|
| Inclinaison escalier | **30° à 38°** (au-delà : échelle à marches) |
| Module | 600 ≤ g + 2h ≤ 660 mm |
| Recouvrement | ≥ 10 mm |
| Garde-corps | requis dès 500 mm de chute ; lisse supérieure 1000 à 1100 mm |
| Sous-lisse | vides ≤ 500 mm |
| Plinthe | ≥ 100 mm, à ≤ 10 mm du sol (une source indique 12 mm) |

Sources : [Suva — escaliers de machines](https://www.suva.ch/fr-ch/prevention/par-danger/materiaux-rayonnements-et-situations-a-risque/stockage-et-transport-en-entreprise-securises/voies-de-circulation-sures-a-linterieur-de-lentreprise/escaliers-de-machines-surs-pour-des-entreprises-securisees) ·
[L'Échelle Européenne — garde-corps acier](https://www.echelle-europeenne.com/media/12398874/3/200/1/400885_fp_garde-corps_acier6.pdf)

## 🪜 Escaliers — accessibilité

| Usage | h max | g min | Largeur | Main courante | Texte |
|---|---|---|---|---|---|
| ERP neuf | 16 cm | 28 cm | 1,20 m entre MC | 2 côtés, 0,80–1,00 m, prolongée d'un giron | Arrêté 20/04/2017 art. 7-1 ✅ |
| Logement collectif (parties communes) | 17 cm | 28 cm | — | obligatoire | Arrêté 24/12/2015 ⚠️ |
| Intérieur logement neuf | 18 cm | 24 cm | 0,80 m | — | Arrêté 24/12/2015 art. 12 ⚠️ |

Sources : [ABC Travaux](https://abctravaux.org/norme-escalier/) ·
[Delabie — accessibilité](https://www.delabie.fr/nos-services/support-technique/normes-et-reglementation/accessibilite-et-autonomie) ·
[Arrêté du 20/04/2017](https://www.drome.gouv.fr/contenu/telechargement/10291/82403/file/arrete_du_20_avril_2017_relatif_a_l_accessibilite_aux_personnes_handicapees_des_erp_lors_de_leur_construction_et_des_iop.pdf)

Résistance (hypothèses modifiables, ⚠️ à confirmer avec NF P06-111-2) :
- charges : qk = 2,5 kN/m² en logement, 4,0 kN/m² en ERP et industriel, Qk = 2 kN ;
- limons : poutre bi-appuyée, flèche ≤ L/300 (usuel) ;
- marches : tôle plane ou tôle pliée en U, flèche sous Qk ≤ L/250 (usuel).

## 🪜 Échelles fixes — NF E85-016 / NF EN ISO 14122-4 ⚠️

| Règle | Valeur |
|---|---|
| Pas des échelons | 225 – 300 mm, constant |
| Largeur entre montants | 400 – 600 mm |
| Dégagement mur / échelon | ≥ 200 mm |
| Premier échelon (machines) | 100 – 400 mm |
| 1er arceau de crinoline | 2200 – 3000 mm du sol |
| Espacement arceaux | ≤ 1500 mm |
| Filants | espacement < 300 mm, Ø intérieur ≈ 700 mm |
| Volée unique | ≤ 8 m (bâtiment) / ≤ 10 m (machine, selon notice) ; paliers usuellement tous les 6 m |

Sources : [HelloPro — réglementation](https://conseils.hellopro.fr/echelle-a-crinoline-reglementation-et-normes-applicables-4128.html) ·
[Fiche produit crinolines](https://res.cloudinary.com/sonepar-fr/raw/upload/s--IqVkIgpJ--/v1/documents/6024/Fiche_produit_crinolines.pdf) ·
[Notice L'Échelle Européenne](https://www.echelle-europeenne.com/media/12499375/3/200/1/64985643_not-fr-057_notice_c.pdf)

## 🚪 Portails — NF EN 13241, NF EN 12604, NF EN 12453 ⚠️

- EN 12604:2017+A1:2020 : chapitre « protection mécanique et distances de sécurité ». Les valeurs exactes n'ont pas été trouvées en accès libre.
- Écartements mini (NF EN ISO 13854, ex-EN 349) : **doigt 25 mm**, main 100 mm. MetalForge signale les jeux < 25 mm aux bords de fermeture des portails motorisés.
- Coulissant motorisé : risque de cisaillement au droit du pilier, protection à prévoir.

Sources : [EN 12604](https://genorma.com/en/standards/en-12604-2017-a1-2020) ·
[AFNOR NF EN ISO 13854](https://www.boutique.afnor.org/en-gb/standard/nf-en-iso-13854/safety-of-machinery-minimum-gaps-to-avoid-crushing-of-parts-of-the-human-bo/fa191736/84559) ·
[Suva — écartements](https://www.suva.ch/fr-ch/prevention/par-branche/securite-au-travail-dans-l-artisanat-et-l-industrie/metallurgie/mesures-de-protection-dans-la-metallurgie/presses-securisees-dispositifs-de-protection)

## 🔥 Galvanisation — NF EN ISO 14713-2 (2019) ⚠️

- Trous d'évent d'environ 25 % du diamètre équivalent de la section (périmètre / π), minimum 10 mm.
- Exemple 60x40 : 1 × Ø16, ou 2 × Ø12, ou 4 × Ø10 à chaque extrémité. Ce cas est vérifié par un test unitaire.
- Aucun volume creux fermé.

Sources : [Coatinc](https://www.coatinc.com/wp-content/uploads/2018/09/tcc-telechargement-affiche-fabrication-et-construction.pdf) ·
[Thomas Graham](https://thomas-graham.co.uk/files/ww/Minimum-Hole-Size-Note-with-Table-1.pdf) ·
[ISO 14713-2:2019](https://standards.iteh.ai/catalog/standards/iso/38aecb26-82ea-4c96-a898-8baa1f45a33e/iso-14713-2-2019)

## 🏗️ Exécution — NF EN 1090-2 ✅

- Classes EXC1 à EXC4 ; **EXC2 par défaut**.
- Vise les structures porteuses : un escalier l'est.
- Garde-corps non porteur : NF EN 1090 non exigée selon la FFB, mais des sources commerciales affirment le contraire, et le marché peut l'imposer.

Source : [FFB — mettre en œuvre la NF EN 1090-2](https://www.ffbatiment.fr/techniques-batiment/gros-oeuvre-structure/construction-charpente-metallique/dossier-bam/comment-la-mettre-en-uvre)

## 📐 Résistance — NF EN 1993-1-1

Vérifications de pré-dimensionnement :
- formule : M_Ed ≤ W·fy/γM0, avec γM0 = 1,0, γG = 1,35, γQ = 1,5 ;
- module W : plastique pour les tubes et barres pleines, élastique pour les sections ouvertes ;
- **non pris en compte :** déversement, voilement, assemblages.

---

> ⚠️ **Avant un usage contractuel**, confirmer chaque valeur sur le texte officiel (AFNOR, Légifrance) et faire valider
> les justifications par un bureau d'études. Les essais NF P01-013 restent la référence pour la résistance des garde-corps.
