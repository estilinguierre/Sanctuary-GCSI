# Inventaire exhaustif des fonctionnalités MétalCad

> Éditeur : **Média Softs** (gamme **MétaluSoft**), Vigneux-de-Bretagne / Nantes (44).
> Suite : **MétalCad** (conception automatisée 2D/3D) + **MédiaCad** (DAO DWG/DXF, intégré) + **MétalContacts** (gestion commerciale).
> Plateforme : Windows 10/11 x64 uniquement (pas d'ARM, pas de macOS).
> Sources : site officiel metalusoft.fr (pages modules, 55 astuces officielles), plaquettes PDF 2023/2025, ancien site metalcad.be (version V2.7). Textes bruts dans `sources/`.

Chaque fonctionnalité porte un identifiant `F-XXX-nn` pour la traçabilité dans la spécification MetalForge (`02_SPEC_METALFORGE.md`).
Les captures citées sont dans `captures/` (voir `captures/INDEX.md`).

---

## 0. Architecture du logiciel

| ID | Fonction | Détail | Captures |
|---|---|---|---|
| F-GEN-01 | Écran d'accueil | 3 entrées : *Créer un nouveau projet* / *Ouvrir un projet* / *Créer un projet à partir de l'album* ; icône de demande de rappel assistance | `09_…/accueil.jpg` |
| F-GEN-02 | Modules automatiques | Escaliers, Garde-corps, Portails, Clôtures, Menuiseries (châssis/façades), Platines, Plans libres | `00_interface_generale/*` |
| F-GEN-03 | Interface type | Arbre de projet/étapes à gauche (Forme → Dimensions → Sections → Remplissage → Décoration → Plan) ; vue centrale 2D/3D ; inspecteur « Caractéristiques » à droite ; barre de commande en bas qui guide l'outil actif | `00_interface_generale/39d3ba…jpg` |
| F-GEN-04 | Conception pas à pas | Assistant par onglets : Forme, Choix, Plan, Dimensions, Remplissage-Choix, Implantation, Visualisation (Développés, Débit, Mise en barres) | `10_astuces_interface/*` |
| F-GEN-05 | Édition directe en 3D | **CTRL + clic** sur un élément 3D → ses caractéristiques s'affichent dans le bandeau droit, modification en direct | astuce « modifiez vos plans en vue 3D » |
| F-GEN-06 | Mode modèle | Mode d'édition paramétrique global (requis pour la gestion des angles) | astuce « metalcad-garde-corps » |
| F-GEN-07 | Album / catalogue de modèles | Enregistrer un projet type (sections, remplissage…) puis le réutiliser en ne changeant que les cotes / le RAL | `10_…/album-metalcad__*` |
| F-GEN-08 | Navigateur MétalCad | Arborescence des sorties par projet : Plans 2D, Plans 3D, Débit, Étiquettes, Mise en barres, Développés (Limon 1, Limon 1 – Fixation, Copie de Limon 1, Marches) ; cases à cocher → *Créer un plan avec la sélection* (export groupé vers MédiaCad) | `10_…/astuce-metalcad-exporter-plusi…` |
| F-GEN-09 | Imposer une cote | Double-clic sur une cote dans « Châssis – Dimensions » → case rose = cote verrouillée, le solveur adapte les autres | `10_…/metalcad-comment-imposer-les-c…` |
| F-GEN-10 | Mise à l'échelle d'un DWG importé | Outil **U** : 2 points sur une ligne + nouvelle longueur → tout le projet s'adapte proportionnellement | astuce « adaptez les dimensions » |
| F-GEN-11 | Import d'environnement | Import DWG/DXF d'un relevé (trémie, murs, balcon) ; import d'un fichier de points **x,y,z** (relevé d'escalier existant) | `06_…/648x417import-plan.png` |
| F-GEN-12 | Murs et environnement | Ajout de murs (côté, dépassement haut/bas, texture), trémie, palier, maçonnerie, piliers | `10_…/savez-vous-que-vous-pouvez-ajo…` |
| F-GEN-13 | Multi-projets | Plusieurs ouvrages dans un même projet (escaliers + passerelles + paliers + rampes, multi-étages) ; calculs de débit / mise en barres multi-projets | `02_garde-corps/*`, plaquette p.4 |
| F-GEN-14 | Rendu réaliste | Textures (bois, métal, verre), teintes RAL, ombres et reflets, surbrillance de l'élément sélectionné | `08_galerie_rendus/*` |
| F-GEN-15 | Insertion sur photo | Incrustation du modèle 3D sur la photo du site client (présentation commerciale) | `06_…/onglets-general-presentation.png`, `04_…/4ff83…png` |
| F-GEN-16 | Export image | Icône disquette → JPG/BMP pour présentation sur tablette | astuce « vos projets sur tablette » |
| F-GEN-17 | Sauvegarde | Fichier → Sauvegarder/restaurer → Sauvegarder mes données ; sauvegarde auto paramétrable (ex. toutes les 5 min) | plaquette p.3 (fenêtre Paramètres) |
| F-GEN-18 | Aide | Manuel PDF intégré (menu ?), *Book découpes*, demande de rappel hotline | — |
| F-GEN-19 | Installation réseau | Logiciel en local sur chaque poste, bibliothèques + plans partagés sur serveur | `sources/pages_web/materiels-requis.txt` |

---

## 1. Module ESCALIERS

### 1.1 Formes
| ID | Fonction |
|---|---|
| F-ESC-01 | Escalier **droit** |
| F-ESC-02 | **Quart tournant** (balancé ou avec palier) — gauche/droite |
| F-ESC-03 | **Deux quarts tournants** (U), demi-tournant |
| F-ESC-04 | **Hélicoïdal** (avec **fût** central paramétrable : rayon, fixation des marches au fût) |
| F-ESC-05 | Escalier **balancé** (balancement marche par marche modifiable) |
| F-ESC-06 | Escalier **suspendu**, à **limon central**, à **pas japonais** |
| F-ESC-07 | Formes combinées (volées + paliers, angles entre volées, angles de départ/arrivée) — 8 pictogrammes de formes |
| F-ESC-08 | Forme des angles (coin, arrondi…) par bord gauche / droit, nombre de marches par tour |

Captures : `01_escaliers/escalier_popup_forme_d_escaliers.png`, `1-logiciel-calcul-escalier…jpg`, `10_…/comment-modifier-un-angle-dans…`.

### 1.2 Calcul automatique
| ID | Fonction |
|---|---|
| F-ESC-10 | Saisie : **hauteur à monter**, dimensions de **trémie**, **emmarchement**, épaisseur des marches, épaisseur/position des **contremarches**, épaisseur de dalle |
| F-ESC-11 | Calcul automatique selon **Blondel** (2H + G) : nombre de marches, hauteur de marche, **giron**, **pas de foulée**, **pente**, **ligne de foulée**, **échappée** |
| F-ESC-12 | Fenêtre **Paramètres** : tableau Minimum / Maximum / Préféré pour Giron, Hauteur de marche, Pas de foulée, Pente ; profils de règles : *Modèle par défaut, ERP, Habitat collectif, Maison individuelle* |
| F-ESC-13 | Paramètres avancés : collet minimum, recouvrement, position épure (%), distance max épure, largeur d'escalier, hauteur d'échappée minimum, angle max des coins, épaisseur dalle, position du recouvrement (nez), calcul du giron (sur épure) |
| F-ESC-14 | Onglet **Choix** : tableau des solutions (Note %, Hauteurs, Hauteur marche, Giron, Pas, Pente) ; double-clic = ajouter/retirer une marche ; options *Afficher les aperçus*, *Afficher les possibilités erronées* |
| F-ESC-15 | Saisie libre forcée par l'utilisateur (nb marches, giron, etc.) |

Captures : `01_escaliers/escalier_popup_cotations_precises.png`, `11_plaquettes_pdf/metalcad_plaquette-3.jpg`, `10_…/astuce-metalcad-comment-ajoute…` (tableau Choix).

### 1.3 Limons
| ID | Fonction |
|---|---|
| F-ESC-20 | Limon **à la française** (latéral) |
| F-ESC-21 | Limon **crémaillère** |
| F-ESC-22 | Limon **porteur** (avec support de marche) / limon central |
| F-ESC-23 | Limon **tôle découpée laser** (motifs) |
| F-ESC-24 | Limon **tube**, **caissonné**, **caissonné reconstitué**, **caissonné débillardé**, **fer plat**, **profil UPN** |
| F-ESC-25 | **Jonctions** : dépassement fort / faible, longueur minimale, fixations du limon ; **prolongement** horizontal et **vertical** dans le mur |
| F-ESC-26 | Découpe personnalisée du limon : export de la *Forme personnalisée* vers MédiaCad → dessin → réimport |
| F-ESC-27 | Percements et fixations des limons |
| F-ESC-28 | **Développé de limon** (gauche et droit, copie de limon) avec **marquage des marches** (masquable : Visualisation > Développés > Limon > Paramètres des limons) et fenêtre *Cotation des limons* (dessin, percements, gravures, remplissage, cotations contours, angles…) |

Captures : `01_escaliers/escalier_popup_choix_de_limons.png`, `09_…/limon-*.jpg`, `10_…/metalcad-comment-masquer-le-ma…`.

### 1.4 Marches
| ID | Fonction |
|---|---|
| F-ESC-30 | Types : marche simple, **tôle larmée avec gousset**, **verre**, **sur pivot**, **support de marche plié**, **console** ; bois / tôle pliée / tôle découpée ; avec ou sans support |
| F-ESC-31 | Marches **encastrées** dans le limon (profondeur, découpe) |
| F-ESC-32 | Fixations de marche par défaut ou **spécifiques** par marche (clic droit > Spécifique) |
| F-ESC-33 | Support de marche avant/arrière, **partie droite** pour le pli gousset/support |
| F-ESC-34 | Textures par élément (limons, marches, supports, contremarches, trémie) : couleur, reflet, transparence, échelle |
| F-ESC-35 | **Développé de marche** coté (vue de dessous) |

### 1.5 Garde-corps d'escalier
F-ESC-40 : garde-corps sur marche, sur limon, sur palier, sur trémie (lien avec module Garde-corps).

---

## 2. Module GARDE-CORPS
| ID | Fonction |
|---|---|
| F-GC-01 | Garde-corps **droits** et **rampants** ; balcon, terrasse, escalier, piscine, muret, balustrade, passerelle, rampe |
| F-GC-02 | Remplissages : **barreaudage**, **câbles**, **tôle** (pleine, perforée, découpée), **vitrage**, **barreaux ouvragés**, **croix de Saint-André**, lisses |
| F-GC-03 | Paramètres par défaut : type de renforts (double…), espacement renforts doubles, espacement mesuré (développé), lisses découpées, jointure par défaut (onglet) |
| F-GC-04 | Fixations, **supports de main courante**, **platines**, percements |
| F-GC-05 | **Implantation sur plan 2D** : tracé saisi ou importé DWG ; implantation automatique ; affichage/masquage des noms des garde-corps |
| F-GC-06 | **Gestion des angles** en 3D temps réel : décalage des montants, platines, remplissage, sections, percements |
| F-GC-07 | Gestion des **longueurs continues** (main courante filante) |
| F-GC-08 | Ajout de **fixations sur une tôle** (Remplissage > Section > Caractéristiques > Fixations) |
| F-GC-09 | Copier/coller de zones de remplissage |
| F-GC-10 | Implantation sur escalier existant via fichier **x,y,z** |
| F-GC-11 | Bibliothèque fournisseurs dont **Q-Railing** (mains courantes, tubes à fond de gorge, raccords, embases) |

Captures : `02_garde-corps/*`, `11_plaquettes_pdf/metalcad_plaquette-4.jpg`, `09_…/garde-corps*.jpg`.

---

## 3. Module PORTAILS
| ID | Fonction |
|---|---|
| F-PO-01 | **Battant**, **coulissant**, **autoportant**, **portillon** |
| F-PO-02 | Châssis **standard**, **sur cadre visible** ou **intégré** |
| F-PO-03 | Saisie **entre piliers** ; formes de traverse haute (droite, chapeau de gendarme, cintrée, biaise…) |
| F-PO-04 | Sections, barreaudage, remplissage (lames PVC/alu, tôle découpée, fer forgé) |
| F-PO-05 | Éléments décoratifs fer forgé (volutes, rosaces, fers de lance, palmettes) |
| F-PO-06 | Accessoires : visserie, écrous, **galets**, **butées**, gonds |
| F-PO-07 | **Jeux maîtrisés** (fonctionnement) |
| F-PO-08 | Catalogue de modèles modernes/traditionnels + ajout de ses propres modèles |
| F-PO-09 | Ouverture d'un plan DWG pour positionner portails & clôtures |
| F-PO-10 | **+10 000 références** (sections, profils, éléments décoratifs) ; fournisseurs : Bosi, FAP, Feba, India, Profil Systèmes, Schutt (Grande Forge), Sofradef, Triebenbacher |

Captures : `03_portails/*`, `09_…/portail-*.jpg`, `11_plaquettes_pdf/metalcad_plaquette-2.jpg`.

## 4. Module CLÔTURES
| ID | Fonction |
|---|---|
| F-CL-01 | Clôtures **linéaires**, **sur mesure**, **en escalier**, **sur différents niveaux**, **cintrées** |
| F-CL-02 | Tracé suivant cotes et **pentes du terrain** |
| F-CL-03 | Panneaux et modules paramétrables ; poteaux |
| F-CL-04 | Remplissages : barreaudages, tôles perforées, motifs découpe |
| F-CL-05 | Synergie avec Portails (alignement poteaux / portails, ensemble muret + portail + clôture) |
| F-CL-06 | Catalogue de modèles standards |

Captures : `04_clotures/*`.

## 5. Module MENUISERIES (châssis, façades)
| ID | Fonction |
|---|---|
| F-ME-01 | Portes, fenêtres, **verrières**, vitrines, châssis, façades acier, multi-ouvrants |
| F-ME-02 | Élévation à partir des **cotes hors tout** : forme du châssis → montants/traverses intermédiaires → affectation des profils → accessoires (joints, parcloses) → dimensionnement selon le **type de pose** → ouvrants → remplissages |
| F-ME-03 | **Coupes** A-A, B-B… illimitées, positionnables, mises à jour auto, imprimables à toute échelle |
| F-ME-04 | Gammes : standard acier (profils à ailettes, T, cornières), **Forster, Jansen, RP Technik (Mannesmann), Voestalpine, Ottostum, Seed** ; ~100 assemblages ; pentures, paumelles Maroc®, visserie ; import de toute gamme DWG/DXF |
| F-ME-05 | Réservations, jeux de fonctionnement, sens INT./EXT. |
| F-ME-06 | Prévisualisation des ouvertures en 3D, intégration au mur de façade |
| F-ME-07 | Récapitulatif référence / désignation (ex. L30X50X20X2 RAL 7016) |

Captures : `05_menuiseries/*`, `09_…/*menuiser*`, `11_plaquettes_pdf/metalcad_plaquette-5.jpg`.

## 6. Module PLATINES
| ID | Fonction |
|---|---|
| F-PL-01 | Création de la **forme** (rectangulaire, ronde, oblongue, libre) |
| F-PL-02 | Saisie des dimensions, épaisseur |
| F-PL-03 | **Positionnement des percements** (gabarits de trous prédéfinis) |
| F-PL-04 | Vues face / côté / dessus, impression à l'échelle |
| F-PL-05 | Album de platines → réutilisables dans **tous** les modules |

Captures : `06_…/onglets-general-platine.png`, `09_…/platine_*`, `formes_platine.jpg`, `percements.jpg`.

## 7. Module PLANS LIBRES (fer forgé, pièces, grilles)
F-PLL-01 pièces à l'échelle prêtes pour laser ; F-PLL-02 grilles de défense ; F-PLL-03 création fer forgé ; F-PLL-04 création 3D libre (kiosques, pergolas…) ; F-PLL-05 impression 1:1 (gabarit).
Captures : `09_…/plan-libre-*.jpg`.

---

## 8. Fonctions communes de production (le cœur métier)
| ID | Fonction | Détail |
|---|---|---|
| F-PR-01 | **Bibliothèque** | Profils et sections du commerce **acier, alu, PVC, inox** ; profils fournisseurs ; ajout de ses propres gammes ; symboles serrurerie |
| F-PR-02 | **Plans techniques automatiques** | Élévations, vues de dessus/côté/perspective, **coupes**, **développés**, plans cotés ; cotes complémentaires |
| F-PR-03 | **Développés** | Limons, marches, tôles pliées, gabarits ; cotations, angles, rayons |
| F-PR-04 | **Feuille de débit** (Excel/LibreOffice) | Article, n°, réf. commerciale, section, matière, quantité, désignation, RAL ; **total déboursé**, **poids total**, **surfaces développées** par section, **longueurs utiles**, **angles de coupe** ; multi-projets ; repères reportés sur le plan |
| F-PR-05 | **Mise en barres** | Longueur de barre (ex. 6 m), **épaisseur du trait de coupe**, regroupement par **RAL**, plusieurs projets ; visuel graphique (pièces en bleu, **chutes en rouge**) ; liste des barres à approvisionner |
| F-PR-06 | **Book de découpes** | +400 motifs DWG/DXF (animaux, géométriques, végétaux, chiffres, **polices pochoir** : Allerta Stencil, Big Shoulders Stencil, Broshk, Gunplay, SirinStencil) ; compatibles laser/plasma/jet d'eau |
| F-PR-07 | **Vectorisation** d'image | Via MédiaCad (bitmap → contour vectoriel pour découpe) |
| F-PR-08 | **Étiquettes** | Repérage des pièces |
| F-PR-09 | **Cartouche** | Cartouche d'impression automatique personnalisable (titre, projet, date, échelle, logo) ; formats A4/A3 portrait/paysage ; toute échelle |
| F-PR-10 | **Export** | DWG, DXF, PDF, **IFC (BIM)**, STL, STEP (via MédiaCad), image JPG/BMP |
| F-PR-11 | **Transmission machine** | Envoi direct DXF aux machines de découpe laser |
| F-PR-12 | **Chiffrage** | Transfert automatique de la feuille de débit vers **MétalContacts** (devis préremplis : quantitatifs, surfaces, développés) |
| F-PR-13 | **Couleurs** | Nuancier RAL complet + textures |

Captures : `06_fonctions_communes/*`, `11_plaquettes_pdf/metalcad_plaquette-6.jpg`.

---

## 9. MédiaCad (DAO intégré, à reproduire = SolidWorks lui-même)
Outils 2D (ligne, polyligne, spline, arc, cercle, boîte) ; ortho/polaire ; **accroche objet** (extrémité, milieu, centre, nodal, quadrant, intersection, insertion, perpendiculaire, tangente, proche, intersection projetée) ; calques ; cotations simples/multiples/cumulées (*ligne de base*), associatives, surcharge de cote ; styles de cote (Standard, Détail, 1-100 … ISO-25) ; texte et styles ; raccord / **raccord zéro** / chanfrein ; copies linéaires/circulaires ; **réseaux** rectangulaire/polaire/sur trajectoire ; blocs à échelle non uniforme ; sélection filtrée ; métré (surfaces, périmètres) ; vues nommées ; présentations multiples + fenêtres d'espace objet ; échelles personnalisées ; import/export DWG, DXF, PDF, IFC, STL, STEP.

Captures : `07_mediacad/*`, `11_plaquettes_pdf/mediacad_plaquette-2.jpg`.

## 10. MétalContacts (hors périmètre MetalForge, pour mémoire)
Devis/factures/situations, catalogue 10 000 sections avec poids/surfaces, prix au poids, signature électronique, imports DPGF/BPU/SPIGAO/Logikal, Chorus Pro, suivi de chantier, achats/stock, application mobile, facturation électronique 2026.

---

## 11. Configuration requise (référence)
MétalCad : 8 Go RAM (16 recommandés), SSD 20 Go, GPU 4 Go (GTX/RTX), CPU i5/i7 3 GHz, Office ou LibreOffice.
