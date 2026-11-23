"""Connecteurs vers vos comptes. Sans identifiants, tout reste en simulation.

Variables (dans l'environnement ou legion/.env) :
  TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID
  SHOPIFY_BOUTIQUE (ex. ma-boutique.myshopify.com), SHOPIFY_JETON
  META_JETON, META_COMPTE_IG
  N8N_WEBHOOK_PUBLICATION
  PEXELS_CLE, ELEVENLABS_CLE, ELEVENLABS_VOIX

ÉTAT : écrits d'après la documentation publique des services, éprouvés ici en
simulation seulement. Le premier appel réel est à vérifier avec vos comptes.
"""
from __future__ import annotations

import json
import os
import urllib.parse
import urllib.request

from . import base


def branche(*variables: str) -> bool:
    return all(os.environ.get(v) for v in variables)


def _http(url: str, corps: dict | None = None, entetes: dict | None = None, brut: bool = False):
    donnees = json.dumps(corps).encode() if corps is not None else None
    h = {"Content-Type": "application/json", **(entetes or {})}
    with urllib.request.urlopen(urllib.request.Request(url, data=donnees, headers=h), timeout=30) as r:
        contenu = r.read()
    return contenu if brut else json.loads(contenu or b"{}")


# ---------- Telegram --------------------------------------------------------

def telegram(texte: str, boutons: list[tuple[str, str]] | None = None) -> str:
    """Envoie un message ; « boutons » = [(libellé, donnée de rappel)]."""
    if not branche("TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"):
        base.ecrire_fichier("telegram-simulation.txt", texte + "\n")
        return "simulation (TELEGRAM_BOT_TOKEN absent) : message écrit dans donnees/telegram-simulation.txt"
    corps = {"chat_id": os.environ["TELEGRAM_CHAT_ID"], "text": texte[:4000]}
    if boutons:
        corps["reply_markup"] = {"inline_keyboard": [[{"text": t, "callback_data": d}] for t, d in boutons]}
    try:
        _http(f"https://api.telegram.org/bot{os.environ['TELEGRAM_BOT_TOKEN']}/sendMessage", corps)
        base.journal("Telegram", "message envoyé")
        return "envoyé sur Telegram"
    except Exception as e:
        base.journal("Telegram", "envoi", ok=False, erreur=str(e)[:200])
        return f"échec Telegram : {e}"


def telegram_relever() -> list[str]:
    """Relève les appuis sur les boutons (« valider:P003 »…)."""
    if not branche("TELEGRAM_BOT_TOKEN"):
        return []
    etat = base.charger("telegram-etat", {"offset": 0})
    rep = _http(f"https://api.telegram.org/bot{os.environ['TELEGRAM_BOT_TOKEN']}/getUpdates?offset={etat['offset']}")
    rappels = []
    for u in rep.get("result", []):
        etat["offset"] = u["update_id"] + 1
        cb = u.get("callback_query")
        if cb and str(cb.get("from", {}).get("id")) == os.environ.get("TELEGRAM_CHAT_ID", ""):
            rappels.append(cb.get("data", ""))
    base.sauver("telegram-etat", etat)
    return rappels


# ---------- Shopify ---------------------------------------------------------

def shopify_commandes(jours: int = 30) -> list[dict] | None:
    if not branche("SHOPIFY_BOUTIQUE", "SHOPIFY_JETON"):
        return None
    depuis = (base.aujourdhui() - __import__("datetime").timedelta(days=jours)).isoformat()
    url = (f"https://{os.environ['SHOPIFY_BOUTIQUE']}/admin/api/2024-10/orders.json?status=any&limit=250"
           f"&created_at_min={depuis}&fields=id,created_at,total_price,landing_site,email")
    rep = _http(url, entetes={"X-Shopify-Access-Token": os.environ["SHOPIFY_JETON"]})
    return [{"id": str(o["id"]), "date": o["created_at"][:10], "total": float(o["total_price"]),
             "page_arrivee": o.get("landing_site") or ""} for o in rep.get("orders", [])]


# ---------- Meta (Instagram) -------------------------------------------------

GRAPH = "https://graph.facebook.com/v21.0"


def instagram_commentaires() -> list[dict] | None:
    if not branche("META_JETON", "META_COMPTE_IG"):
        return None
    q = urllib.parse.urlencode({"fields": "id,caption,comments{id,text,username,timestamp}",
                                "limit": 25, "access_token": os.environ["META_JETON"]})
    rep = _http(f"{GRAPH}/{os.environ['META_COMPTE_IG']}/media?{q}")
    res = []
    for m in rep.get("data", []):
        for c in m.get("comments", {}).get("data", []):
            res.append({"id": c["id"], "auteur": c.get("username", "?"), "plateforme": "instagram",
                        "canal": "commentaire", "texte": c.get("text", ""), "post_externe": m["id"]})
    return res


def instagram_repondre(id_commentaire: str, texte: str) -> str:
    if not branche("META_JETON"):
        return "simulation"
    q = urllib.parse.urlencode({"message": texte, "access_token": os.environ["META_JETON"]})
    _http(f"{GRAPH}/{id_commentaire}/replies?{q}", corps={})
    return "envoyé"


# ---------- n8n, Pexels, ElevenLabs -------------------------------------------

def n8n_publier(entree: dict) -> str:
    if not branche("N8N_WEBHOOK_PUBLICATION"):
        return "simulation"
    _http(os.environ["N8N_WEBHOOK_PUBLICATION"], entree)
    return "transmis à n8n"


def pexels(requete: str) -> list[str] | None:
    if not branche("PEXELS_CLE"):
        return None
    q = urllib.parse.urlencode({"query": requete, "per_page": 3, "orientation": "portrait"})
    rep = _http(f"https://api.pexels.com/videos/search?{q}", entetes={"Authorization": os.environ["PEXELS_CLE"]})
    return [v.get("url", "") for v in rep.get("videos", [])]


def elevenlabs(texte: str, sortie) -> bool:
    if not branche("ELEVENLABS_CLE", "ELEVENLABS_VOIX"):
        return False
    son = _http(f"https://api.elevenlabs.io/v1/text-to-speech/{os.environ['ELEVENLABS_VOIX']}",
                {"text": texte, "model_id": "eleven_multilingual_v2"},
                {"xi-api-key": os.environ["ELEVENLABS_CLE"]}, brut=True)
    sortie.write_bytes(son)
    return True


def etat() -> list[tuple[str, bool, str]]:
    return [
        ("Telegram", branche("TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"), "TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID"),
        ("Shopify", branche("SHOPIFY_BOUTIQUE", "SHOPIFY_JETON"), "SHOPIFY_BOUTIQUE, SHOPIFY_JETON"),
        ("Instagram (Meta)", branche("META_JETON", "META_COMPTE_IG"), "META_JETON, META_COMPTE_IG"),
        ("n8n", branche("N8N_WEBHOOK_PUBLICATION"), "N8N_WEBHOOK_PUBLICATION"),
        ("Pexels", branche("PEXELS_CLE"), "PEXELS_CLE"),
        ("ElevenLabs", branche("ELEVENLABS_CLE", "ELEVENLABS_VOIX"), "ELEVENLABS_CLE, ELEVENLABS_VOIX"),
    ]
