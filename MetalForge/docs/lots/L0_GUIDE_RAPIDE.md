# Lot L0 — guide rapide (squelette, F-GEN-03)

1. Ouvrir `MetalForge.sln` dans Visual Studio 2022, configuration **Debug | x64**.
2. Compiler : `msbuild MetalForge.sln /p:Configuration=Debug /p:Platform=x64`.
   Sans SolidWorks installé, la compilation passe sur une maquette d'API : l'avertissement « SolidWorks introuvable » s'affiche et la DLL ne doit pas être enregistrée.
3. Enregistrer l'add-in (en administrateur) : `src\MetalForge.AddIn\bin\x64\Debug\Install\Enregistrer.bat`.
4. Lancer SolidWorks → *Outils > Compléments* → **MetalForge** est coché.
5. Ouvrir le panneau des tâches : onglet **MetalForge — Métallerie** → « Bonjour MetalForge », versions, bouton *Ouvrir le journal*.
6. Tests : `dotnet test src/MetalForge.Tests` (8 tests).

| Problème | Solution |
|---|---|
| Add-in absent de la liste | Relancer `Enregistrer.bat` en administrateur, SolidWorks fermé |
| Volet vide | Lire le journal `%LOCALAPPDATA%\MetalForge\journal\metalforge-AAAAMMJJ.log` |
| Désinstaller | `Install\Desinscrire.bat` en administrateur |
