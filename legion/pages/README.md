# Pages publiées

La source des trois pages publiées comme artefacts, gardée ici pour ne pas dépendre d'une session.

| Fichier | Page | Lien |
|---|---|---|
| `guide-mise-en-route.html` | Les 25 étapes de mise en route | https://claude.ai/artifact/2819de5eUhYgEwbkySSSPF |
| `plaquettes-propositions.html` | 5 planches × 4 propositions | https://claude.ai/artifact/XVg2nPknHG6FS6JsygKdvv |
| `tableau-de-suivi.html` | Tableau de suivi | https://claude.ai/artifact/5Nj5mFHAWz8DP3QXUxQ4Wn |

`tableau-de-suivi.html` n'est pas une page autonome : c'est le fichier `index.html` d'un
artefact de type Dashboard. Il attend l'objet `dash` de ce type et ne s'ouvre pas seul
dans un navigateur. Ses données sont dans `donnees/` et sont montées sur l'artefact.

`donnees/*.json` : les 25 étapes, les 6 connecteurs, et trois jeux de chiffres d'exemple
issus de `qg entonnoir` et `qg commentaire`. Ce ne sont pas de vrais chiffres.
