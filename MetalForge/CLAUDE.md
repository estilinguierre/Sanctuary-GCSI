# CLAUDE.md — Projet MetalForge

## Contexte
MetalForge est un **complément (add-in) Windows pour SolidWorks** destiné à la métallerie / serrurerie / ferronnerie. Il reprend les **fonctionnalités** du logiciel MétalCad (Média Softs) : escaliers, limons, garde-corps, portails, clôtures, menuiseries acier, platines, développés, feuilles de débit, mises en barres, exports DXF/SVG et impressions PDF à l'échelle.
Utilisateur final : métallier expert SolidWorks (Normandie). Langue de l'interface, du code métier et de la doc : **français** (accents corrects).

## Où trouver quoi
| Dossier | Contenu | Quand le lire |
|---|---|---|
| `docs/01_FONCTIONNALITES_METALCAD.md` | Inventaire exhaustif, identifiants `F-XXX-nn` | **Toujours** avant de coder une fonction |
| `docs/02_SPEC_METALFORGE.md` | Architecture, lots L0→L7, correspondance SolidWorks | Avant tout choix technique |
| `docs/03_REGLES_METIER.md` | Formules (Blondel, échappée…), normes, algorithmes débit / mise en barres | Pour tout calcul |
| `captures/INDEX.md` + `captures/NN_module/` | ~250 captures d'écran MétalCad classées par module | Pour comprendre l'ergonomie attendue (ouvrir les images avec l'outil de lecture) |
| `sources/` | Textes bruts du site éditeur, 55 astuces officielles, plaquettes PDF | Pour vérifier un détail |
| `docs/GUIDE_RAPIDE.md` / `docs/GUIDE_DETAILLE.md` | Mode d'emploi de ce dossier | Au démarrage |

## Stack imposée
- C# — .NET Framework **4.8**, **x64**, SolidWorks **2022+** (interop `SolidWorks.Interop.sldworks`, `swconst`, `swpublished`).
- Calculs dans `MetalForge.Core` (.NET Standard 2.0), **aucune dépendance SolidWorks**, couverts par tests xUnit.
- UI : WPF dans un TaskPane SolidWorks, MVVM (CommunityToolkit.Mvvm).
- Exports : netDxf (DXF), ClosedXML (XLSX), SVG maison, PDF via mise en plan SolidWorks.
- Unités internes **mm** ; conversion en **m** uniquement dans `SwAdapter`.

## Règles de travail
1. Chaque tâche cite le ou les identifiants `F-XXX-nn` traités (commit, PR, commentaire de classe).
2. Écrire d'abord le calcul pur + tests dans `Core`, ensuite l'adaptateur SolidWorks, puis l'UI.
3. Réutiliser le natif SolidWorks (mécano-soudé, tôlerie, liste des pièces soudées, configurations) plutôt que réinventer.
4. Nommage de l'arbre et des propriétés personnalisées en français.
5. Toute modification de document, prompt ou spécification existante : **demander confirmation** à l'utilisateur avant.
6. Fin de lot : produire un **guide rapide** et un **guide détaillé** du lot livré.
7. Toute réponse portant sur un plan s'accompagne d'une visualisation (SVG/PNG du développé ou du plan généré).

## Propriété intellectuelle — impératif
Les captures et textes MétalCad sont des **références d'étude, à usage interne**. Ne jamais copier dans MetalForge : icônes, visuels, textes, catalogues, book de découpes, polices, marques MétalCad / MétaluSoft / Média Softs. Reproduire des **fonctions**, pas des **actifs**.

## Commandes utiles (une fois la solution créée)
```powershell
dotnet test src/MetalForge.Tests
msbuild MetalForge.sln /p:Configuration=Debug /p:Platform=x64
# Enregistrement COM (admin) :
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /codebase bin\x64\Debug\MetalForge.AddIn.dll
```

## Priorité actuelle
**Lot L1 — MVP Escalier** : escalier droit + quart tournant, limons tôle gauche + droit, développés DXF/SVG, PDF 1:1 et 1:10 en A4→A0. Voir `.claude/commands/lot.md`.
