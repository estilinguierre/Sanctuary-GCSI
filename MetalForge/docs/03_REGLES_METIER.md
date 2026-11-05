# Règles métier — escaliers, garde-corps, débit

> ⚠️ Valeurs **usuelles** à paramétrer, non contractuelles. Vérifier les textes en vigueur (Code de la construction, arrêtés accessibilité ERP / logements collectifs, NF P01-012 garde-corps, NF P01-013, DTU) avant toute mise en production.

## 1. Escaliers

### Formules
- **Blondel** : `M = 2h + g`, cible 600–650 mm (préféré 630–640).
- Nombre de hauteurs : `n = round(H_monter / h_cible)` puis `h = H_monter / n`.
- Nombre de girons : `n − 1` (si la dernière marche est le palier d'arrivée).
- Pente : `atan(h / g)` — confort 30–37°, max ≈ 45°.
- **Échappée** : distance verticale nez de marche → sous-face de trémie ≥ 2 000 mm (min. toléré 1 900).
- **Ligne de foulée** : à 500–600 mm du limon intérieur (ou au milieu si emmarchement < 1 000 mm).
- Longueur de limon droit (axe) : `L = √(Lh² + H²)` + dépassements haut/bas.

### Profils de règles (paramétrables — à confirmer)
| Profil | h (mm) | g (mm) | Emmarchement | Remarques |
|---|---|---|---|---|
| Maison individuelle | 170–200 | 240–280 | ≥ 800 | Libre, confort Blondel |
| Habitat collectif | ≤ 170 | ≥ 280 | ≥ 1 000 | Main courante, nez contrastés |
| ERP | ≤ 160 | ≥ 280 | ≥ 900 (1 UP) | Mains courantes 0,80–1,00 m, prolongées |
| Personnalisé | min/max/préféré | min/max/préféré | — | Comme la fenêtre Paramètres MétalCad |

### Balancement (quart / deux-quarts tournants)
Méthodes à proposer : **épure de balancement** (méthode de la herse / du triangle), **quartier tournant régulier**, **balancement manuel marche par marche**. Paramètres : collet minimum (≈100 mm), position de l'épure (%), distance max épure, angle max des coins.

### Développé de limon
Le limon est déplié dans son plan ; sur le développé : contour, trous de fixation, **marquage des marches** (traits de nez + dessus), repères `Limon G` / `Limon D`, cotes cumulées depuis le pied. Le limon gauche est le miroir du droit seulement pour un escalier droit.

## 2. Garde-corps (repères NF P01-012 — à vérifier)
- Hauteur ≥ 1 000 mm (≥ 800 mm si partie supérieure épaisse ≥ 500 mm).
- Vides ≤ 110 mm entre barreaux / sous la lisse basse.
- Zone de 450 mm au-dessus de la zone de stationnement : pas d'élément escaladable.
- Rampant d'escalier : hauteur ≥ 900 mm mesurée à l'aplomb du nez de marche.

## 3. Mise en barres (nesting 1D)
- Entrées : pièces (longueur, angle coupe G/D, section, RAL, projet), longueur de barre (6 000 / 6 500 / 12 000), **trait de coupe** (scie : 2–4 mm), chute mini réutilisable.
- Algorithme : *First-Fit Decreasing* puis amélioration (*Best-Fit*, recherche locale) ; option multi-projets ; regroupement par section + RAL.
- Sorties : graphique par barre (pièces bleues, chute rouge), taux de chute, liste des barres à approvisionner.

## 4. Feuille de débit
Colonnes : Repère · Désignation · Section · Matière · Longueur utile · Angle G · Angle D · Qté · Poids unitaire · Poids total · Surface développée (m², pour thermolaquage) · RAL · Projet.
Totaux : poids total, surface totale, **total déboursé** (prix au kg ou au mètre).
