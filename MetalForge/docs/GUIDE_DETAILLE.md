# Guide détaillé — Dossier MetalForge pour Claude Code

## 1. Contenu du dossier
```
MetalForge-ClaudeCode/
├── CLAUDE.md                     ← mémoire projet lue par Claude Code à chaque session
├── .claude/commands/
│   ├── lot.md                    ← /lot L1 : démarre un lot de la spec
│   └── fonction.md               ← /fonction F-ESC-28 : étude d'une fonction
├── docs/
│   ├── 01_FONCTIONNALITES_METALCAD.md   ← inventaire (≈130 fonctions identifiées)
│   ├── 02_SPEC_METALFORGE.md            ← architecture + lots L0→L7
│   ├── 03_REGLES_METIER.md              ← Blondel, normes, nesting, débit
│   ├── GUIDE_RAPIDE.md
│   └── GUIDE_DETAILLE.md
├── captures/
│   ├── INDEX.md                  ← tableau fichier / contenu / source
│   ├── 00_interface_generale/    ← vues de l'interface actuelle (2025-2026)
│   ├── 01_escaliers/ 02_garde-corps/ 03_portails/ 04_clotures/ 05_menuiseries/
│   ├── 06_fonctions_communes/    ← débit, mise en barres, développés, platines, book découpes
│   ├── 07_mediacad/              ← DAO intégré
│   ├── 08_galerie_rendus/        ← rendus 3D (niveau de qualité visé)
│   ├── 09_metalcad_v2.7_historique/ ← ancienne version : écrans de saisie très lisibles
│   ├── 10_astuces_interface/     ← fragments d'UI réels (fenêtres, menus, inspecteur)
│   └── 11_plaquettes_pdf/        ← pages des plaquettes officielles (150 dpi)
└── sources/
    ├── pages_web/                ← textes extraits des pages éditeur + 55 astuces
    └── plaquettes_pdf/           ← PDF originaux
```

## 2. Installation
1. Prérequis : Windows 10/11 x64, SolidWorks 2022+, Visual Studio 2022 (charge « Développement .NET Desktop »), .NET 8 SDK (pour les tests), Node + Claude Code.
2. Dézipper dans `C:\Dev\MetalForge`, puis `git init` et premier commit (le dossier sert de dépôt).
3. `claude` dans ce dossier.

## 3. Flux de travail conseillé
1. **`/lot L0`** — Claude propose la solution `MetalForge.sln` (6 projets). Tu valides → il génère le squelette, l'enregistrement COM et un TaskPane « Bonjour MetalForge ».
2. Test dans SolidWorks : *Outils > Compléments* → cocher MetalForge.
3. **`/lot L1`** — moteur escalier :
   - `Core` : `StairDefinition`, `BlondelSolver`, `WinderSolver` (balancement), `StringerUnfolder` (développé limon) + tests.
   - `SwAdapter` : esquisse d'épure, limons en tôlerie, marches, assemblage.
   - `Export` : DXF/SVG des développés G+D, PDF 1:1 (tuilage A4 avec repères) et 1:10 (A4→A0).
4. À chaque étape, exiger une **visualisation** (SVG du développé) pour contrôle avant passage atelier.
5. Fin de lot : Claude rédige guide rapide + guide détaillé du lot.

## 4. Utiliser les captures
- Demander à Claude d'**ouvrir** l'image (`captures/01_escaliers/escalier_popup_cotations_precises.png`) : il lit les champs exacts de la fenêtre Paramètres (giron/hauteur/pas min-max-préféré, profils ERP / Habitat collectif / Maison individuelle).
- `09_metalcad_v2.7_historique` montre le déroulé en 4 étapes (forme → cotes → décor → plan) : bon modèle pour l'assistant MetalForge.
- `10_astuces_interface` donne les noms réels des options (Jonctions, Dépassement fort/faible, Marquage des marches, Fixation spécifique…).

## 5. Points d'attention
- **API SolidWorks en mètres** : toute erreur ×1000 vient de là.
- **Développés** : vérifier les DXF en les réimportant dans SolidWorks (esquisse) et en mesurant une cote témoin.
- **PDF 1:1** : imprimer sans « ajuster à la page » ; contrôler la mire de 100 mm.
- **Normes** : les valeurs de `03_REGLES_METIER.md` sont à confirmer avant usage client.
- **Propriété intellectuelle** : reproduire les fonctions, jamais les actifs (icônes, motifs, catalogues, textes).

## 6. Sources
- Site éditeur : https://www.metalusoft.fr/nos-logiciels/metalcad/ (+ pages escaliers, garde-corps, portails, clôtures, menuiseries, MédiaCad)
- Astuces : https://www.metalusoft.fr/centre-de-ressources/trucs-et-astuces/
- Plaquettes : https://www.metalusoft.fr/centre-de-ressources/plaquettes/
- Ancien site (V2.7) : http://www.metalcad.be/
