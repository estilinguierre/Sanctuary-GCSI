# Flux n8n

`publication.json` s'importe dans n8n (menu **Workflows → Import from File**).

1. Posez `META_JETON`, `META_COMPTE_IG` et `META_PAGE` dans les variables d'environnement de n8n.
2. Activez le flux : son webhook donne une URL.
3. Posez cette URL dans `legion/.env` sous `N8N_WEBHOOK_PUBLICATION=…`.
4. `qg programme` envoie alors une entrée par post et par plateforme.

Ce que le flux reçoit du QG :

```json
{"post": "P003", "plateforme": "instagram", "quand": "2026-12-02 08:00",
 "legende": "…\nhttps://…?utm_content=P003", "mots_diese": ["#tir"],
 "lien": "https://…", "format_video": "9:16, 1080x1920", "simulation": false}
```

`media_url` est l'adresse publique de votre vidéo montée : ajoutez devant le nœud de dépôt
l'envoi du fichier que `qg monter` a produit (Drive, S3 ou votre serveur).

**TikTok et YouTube** restent en simulation : leurs API de publication demandent une
application auditée. Le flux le dit au lieu de faire semblant de publier.
