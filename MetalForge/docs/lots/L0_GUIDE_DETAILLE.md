# Lot L0 — guide détaillé (squelette de la solution)

Fonction couverte : **F-GEN-03** (socle de l'interface : add-in chargé, volet dans SolidWorks, journal).

## 1. Solution `MetalForge.sln`

```
MetalForge/
├─ CLAUDE.md · docs/ · .claude/commands/      kit de conception (référence)
├─ build/SolidWorks.props                      références interop SolidWorks (ou maquette)
├─ src/
│  ├─ MetalForge.Core       .NET Standard 2.0  calculs purs : Units (mm ↔ m), Journal, MetalForgeInfo
│  ├─ MetalForge.Library    .NET Standard 2.0  dossiers de bibliothèque (profils, modèles, couleurs)
│  ├─ MetalForge.Export     .NET 4.8 + .NET 8  DXF R2013 (netDxf), XLSX (ClosedXML), PDF (PDFsharp)
│  ├─ MetalForge.SwAdapter  .NET 4.8 x64       SwSession : pièces, assemblages, opérations transactionnelles
│  ├─ MetalForge.AddIn      .NET 4.8 x64       ISwAddin, enregistrement COM, volet WPF (MVVM)
│  └─ MetalForge.Tests      .NET 8 xUnit       tests du Core et des exports
├─ tools/MetalForge.SwStubs                    maquette de l'API SolidWorks (compilation sans SolidWorks)
└─ _ancien/                                    configurateur précédent (code et calculs réutilisés dans les lots)
```

Dépendances : `AddIn → SwAdapter → Core` ; `AddIn → Export → Core` ; `Library → Core`. Le Core ne dépend de rien.

## 2. Ce qui est en place

- **Unités** (`Core/Units.cs`) : mm et degrés partout ; conversion en m et en radians uniquement à l'entrée de l'API SolidWorks.
- **Journal** (`Core/Diagnostics/Journal.cs`) : un fichier par jour dans `%LOCALAPPDATA%\MetalForge\journal`, niveaux INFO / ATTENTION / ERREUR. Il ne lève jamais d'exception, pour ne jamais faire tomber SolidWorks.
- **Calques DXF** (`Export/Dxf/CalquesDxf.cs`) : document R2013 en mm avec les calques CONTOUR (rouge), PERCAGE (vert), GRAVURE_MARQUAGE (magenta), COTATION (bleu) et TEXTE.
- **SwSession** (`SwAdapter/SwSession.cs`) : nouvelle pièce ou assemblage depuis les modèles par défaut de l'utilisateur. `Operation(nom, doc, action)` journalise, signale l'échec à l'utilisateur et termine toujours par `EditRebuild3`.
- **Add-in** (`AddIn/MetalForgeAddIn.cs`) :
  - GUID `a6fba09a-5265-40fd-8d1c-7e766f8b72eb`, ProgId `MetalForge.AddIn` ;
  - enregistrement dans `HKLM\SOFTWARE\SolidWorks\Addins` (titre, description) et chargement au démarrage (`HKCU\…\AddInsStartup`).
- **Volet** : contrôle WinForms → `ElementHost` → vue WPF `AccueilView` liée à `AccueilViewModel` (CommunityToolkit.Mvvm). Les vues sont écrites en C#, sans XAML, pour compiler aussi en intégration continue. L'icône est dessinée par le code : aucun visuel tiers.

## 3. Intégration continue

`.github/workflows/metalforge-core.yml` compile toute la solution (Release x64, avec la maquette SolidWorks) et lance les tests à chaque push.

## 4. Points d'attention

- **Ne jamais enregistrer** une DLL compilée avec la maquette : elle ne fonctionne pas dans SolidWorks. La compilation affiche alors l'avertissement « SolidWorks introuvable ».
- SolidWorks installé ailleurs : `msbuild … /p:SolidWorksDir="D:\SOLIDWORKS Corp\SOLIDWORKS"`.
- Le dossier `captures/` et `sources/` (références MétalCad) est ignoré par git : à garder en local uniquement.

## 5. Suite : lot L1 (MVP escalier)

`StairDefinition` + profils de règles, `BlondelSolver` (onglet Choix), `WinderSolver`, `StringerUnfolder` ; limons en tôlerie ; exports DXF / SVG / PDF 1:1 et 1:10.
