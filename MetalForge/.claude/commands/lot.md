---
description: Démarrer ou poursuivre un lot MetalForge (ex. /lot L1)
argument-hint: <L0|L1|...|L7>
---
Lot demandé : $ARGUMENTS

1. Lis `CLAUDE.md`, puis la ligne du lot $ARGUMENTS dans `docs/02_SPEC_METALFORGE.md`.
2. Pour chaque identifiant `F-XXX-nn` du lot, relis sa description dans `docs/01_FONCTIONNALITES_METALCAD.md` et ouvre les captures citées (`captures/…`) pour comprendre l'ergonomie.
3. Relis les formules concernées dans `docs/03_REGLES_METIER.md`.
4. Propose un plan de travail (fichiers, classes, tests) et **attends ma validation** avant d'écrire du code.
5. Après validation : Core + tests → SwAdapter → UI → exports.
6. En fin de lot : liste des `F-XXX-nn` couverts, captures/visualisations des sorties, guide rapide + guide détaillé du lot.
