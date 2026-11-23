# 🛡️ Opération Légion · le QG

> L'équipe sociale d'un seul homme. 33 agents, une seule commande : `qg`.
> Rien ne part sans votre approbation.

![Python](https://img.shields.io/badge/Python-3.11+-3776AB?logo=python&logoColor=white)
![Hors ligne](https://img.shields.io/badge/par%20défaut-hors%20ligne-1e5a3c)
![Tests](https://img.shields.io/badge/tests-38%20✓-d9f35b)

---

## 🚀 En trois minutes

```bash
cd legion
alias qg="python3 -m qg"        # ou : ./qg.sh <commande>

qg etat                          # ce qui est branché, ce qui est en simulation
qg semaine                       # écrit et contrôle 14 posts pour la semaine suivante
qg file                          # ce qui attend votre approbation
qg valider --tout                # vous approuvez
qg programme                     # créneaux par plateforme
qg matin                         # le résumé de 7 h 30
qg tableau                       # tableau de bord HTML
```

Sans aucun compte branché, **tout tourne sur les données d'exemple** d'un instructeur de tir :
vous voyez exactement ce que le QG produira avec vos vraies données.

---

## 🧭 La carte en une page

| Module | Agents | Commandes |
|---|---|---|
| 🔍 **Recherche** | Éclaireur de tendances · Veille concurrents · Analyste et Rédacteur d'accroches · Chercheur de questions · Mots-clés · Profil du public · Banque d'idées · Planificateur | `tendances` `concurrents` `accroches` `questions` `motscles` `profils` `idees` `semaine` |
| 🎬 **Studio** | Scénariste · Légendes · Carrousels · Miniatures · Découpeur · Monteur · Voix off · Plans de coupe · **Contrôle de marque** | `voir` `carrousel` `miniature` `decouper` `monter` `voix` `plans` |
| 📣 **Publication** | File d'approbation · Programmateur · Multi-plateformes · Réponses publiques · Messages privés · Marqueur de prospects · Capture d'e-mails · Partenariats | `file` `valider` `refuser` `programme` `commentaire` `capture` `partenariats` |
| 📈 **Croissance** | Statistiques · Suivi des liens · Revenus · Note des accroches · Tests A/B · Publicité · Résumé du matin | `entonnoir` `notes` `test` `pub` `interruptions` `journal` `matin` `tableau` |

---

## 🧠 La couche de connaissance

Tout ce que le QG sait de vous tient dans `connaissance/`, en Markdown lisible et modifiable.
**Changez un fichier, le comportement des agents change** — aucun code à toucher.

```
connaissance/
├── voix/          guide-de-voix.md · mots-interdits.md · regles-de-ton.md
├── offre/         offre.md · tarifs.md · faq.md
├── contenu/       piliers.md · modeles-de-posts.md · scripts-video.md
├── plateformes/   regles-tiktok.md · regles-instagram.md · regles-youtube.md · regles-facebook.md
├── public/        profils.md · questions.md · points-de-douleur.md
├── accroches/     bibliotheque.md · analyses.md · formes-gagnantes.md
├── communaute/    regles-de-reponse.md · scripts-messages.md · etiquettes-prospects.md
└── rapports/      modele-indicateurs.md · regles-attribution.md · modele-resume-matin.md
```

Les agents **écrivent aussi dedans** : les profils observés, les questions du public et le
classement de vos accroches sont remis à jour à chaque passage.

---

## 🚨 Le Contrôle de marque

Aucun brouillon n'atteint la file sans passer par là. Trois verdicts :

| Verdict | Ce qui se passe |
|---|---|
| **Conforme** | entre dans la file |
| **Corrigé ✓** | un mot de `mots-interdits.md` a été remplacé, puis entre dans la file |
| **SIGNALÉ** | promesse, garantie, prix inconnu ou texte trop long → **bloqué, hors file** |

```
⚠ P011 « Promesse de résultat » signalé : terme interdit « garanti ». Il n'entre pas dans la file.
✓ P006 corrigé : « incroyable » → « net ».
```

Un prix qui n'est pas dans `offre/tarifs.md` est toujours signalé : le QG ne peut pas
inventer un tarif.

---

## 🧩 Le cerveau, au choix

| `QG_CERVEAU` | Ce qui écrit | Coût | Réseau |
|---|---|---|---|
| `modeles` *(défaut)* | les gabarits de `connaissance/` | 0 € | aucun |
| `ollama` | un modèle local (`QG_OLLAMA_MODELE`) | 0 € | local |
| `claude` | Claude Code en ligne de commande | votre abonnement | oui |

Quel que soit le cerveau, le texte **repasse toujours** par le Contrôle de marque, et une
panne de cerveau retombe sur le gabarit : le QG ne s'arrête jamais là-dessus.

---

## 🔌 Brancher vos comptes

Posez vos clés dans `legion/.env` (jamais versionné), puis `qg releve`.

```ini
TELEGRAM_BOT_TOKEN=…      TELEGRAM_CHAT_ID=…       # approbation depuis le téléphone
SHOPIFY_BOUTIQUE=…        SHOPIFY_JETON=…          # commandes et attribution
META_JETON=…              META_COMPTE_IG=…         # commentaires et réponses Instagram
N8N_WEBHOOK_PUBLICATION=… # publication (voir n8n/)
PEXELS_CLE=…              ELEVENLABS_CLE=… ELEVENLABS_VOIX=…
```

⚠️ **TikTok et YouTube** : leur API de publication demande une application auditée. Le QG
l'annonce (`simulation (application non auditée)`) au lieu de faire semblant.

---

## 💶 Du post au client payant

Chaque post reçoit son lien UTM ; chaque commande est rattachée au post qui l'a causée.

```
Vues                184 000  100 %
Abonnés             620      0,3 % des vues
Clics               1 150    0,6 % des vues
Clients             23       2,0 % des clics
Chiffre d'affaires  4 370 €  panier moyen 190 €

⚠ « Tendance du moment » : 24 600 j'aime, 0 clic, 0 €. À ne pas refaire.
```

Une commande sans UTM est comptée **non rattachée**, jamais attribuée au hasard.

---

## 🙋 Ce qui reste entre vos mains

- **Les approbations.** Rien n'est publié sans `qg valider`.
- **Les plaintes.** Remboursement, réclamation, commande perdue : jamais de réponse automatique.
- **Les questions hors FAQ**, la sécurité, le droit, l'achat d'armes.
- **Les partenariats** et **la publicité** : le QG propose la répartition, aucun euro n'est dépensé par lui.

---

## 🎥 Chaîne vidéo

```bash
qg decouper tournage.mp4 -s 30   # silences retirés (auto-editor) puis clips de 30 s
qg monter clip-00.mp4            # 9:16 1080×1920 + sous-titres Whisper s'il est installé
qg voix P001                     # texte à dire, puis Chatterbox (voix clonée) ou ElevenLabs
qg plans P001                    # plans de coupe à tourner, liens Pexels si la clé est posée
```

`ffmpeg` est le seul outil requis. `auto-editor`, `whisper` et Chatterbox sont facultatifs :
en leur absence, l'étape est simplement sautée et le QG le dit.

---

## 🌙 La tournée de la nuit

```cron
45 6 * * *  cd /chemin/legion && python3 -m qg matin --telegram
```

Relève, tri de la boîte, attribution, classement des accroches, puis un seul message à 7 h 30 :

```
Bonjour, chef ☀
CA 7 jours 4 370 €   Prospects 9   Abonnés 620
€ Meilleur post : « Jour 1 contre jour 30 » a rapporté 1 900 € sur 10 commandes.
△ Le post le plus aimé a rapporté 0 €. 24 600 j'aime, aucun clic. À ne pas refaire.
✎ 13 posts attendent votre approbation.
◯ 3 messages ont besoin de vous.
↗ Tendance à saisir : tir à sec à la maison, +184 %.
```

---

## 🧪 Tests

```bash
python3 legion/tests/test_qg.py      # 38 tests, hors ligne, aucune clé requise
```

---

## 📁 Arborescence

```
legion/
├── qg/            base · cerveau · connecteurs · recherche · studio · publication · croissance · cli
├── connaissance/  ce que le QG sait de vous (Markdown)
├── exemples/      données d'exemple (tant que rien n'est branché)
├── n8n/           flux de publication à importer
├── tests/         38 tests
└── donnees/       créé à l'usage : posts, prospects, journal, tableau.html (hors dépôt)
```
