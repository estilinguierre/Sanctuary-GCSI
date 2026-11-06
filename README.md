# Sanctuary-GCSI
Création d'un écosystème souverain.

## 🪐 Orbite — second cerveau souverain

> *« Imagine an app that brings together Obsidian + Jev + Anki + NotebookLM + AI (ChatGPT, Claude or else). Just imagine… »*

Orbite réunit dans **un seul fichier** (`index.html`, zéro dépendance, zéro serveur) :

| Inspiration | Dans Orbite |
|---|---|
| **Obsidian** | Notes Markdown, `[[liens]]`, `#tags`, rétroliens, mentions non liées, renommage qui met à jour les liens, import d'un coffre (`.md`) |
| **Graphe** | Vue **Planetary** : 1er tag racine = planète, tag complet = îlot ; vue **Libre** (forces) ; *ZOOM FAR*, *Tous les liens*, *Verrouiller* ; point orange = note « à relier » |
| **Anki** | Toute ligne `Question :: Réponse` devient une carte ; planification SM-2 ; compteur *dues* |
| **NotebookLM** | Mode *Notebook* : l'IA répond **uniquement** à partir des sources cochées, avec citations `[[Titre]]` |
| **IA** | Claude (Anthropic), ChatGPT ou tout endpoint compatible OpenAI — y compris **Ollama en local** (100 % souverain) ; génération de cartes, suggestion de liens, résumés |
| **Objectifs** | 3 objectifs du jour, report automatique |

### Démarrer
Ouvrir `index.html` dans un navigateur (ou activer GitHub Pages sur le dépôt). Des notes de démonstration sont chargées.
Clé API : onglet **✱ Réglages** — elle reste dans le navigateur.

### Données
Tout est stocké en `localStorage`. Export/import JSON, import de fichiers `.md` ou d'un dossier Obsidian complet, export d'une note en `.md`.

### Raccourcis
`Ctrl/⌘+K` recherche · `Échap` fermer · en révision : `Espace` révéler, `1-4` noter · double-clic dans le vide : nouvelle note.
