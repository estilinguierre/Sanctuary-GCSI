"""Le cerveau : qui écrit quand un agent a besoin d'un texte.

QG_CERVEAU=modeles (défaut) : gabarits de la couche de connaissance, hors ligne, gratuit.
QG_CERVEAU=ollama           : modèle local (QG_OLLAMA_MODELE, défaut « mistral »).
QG_CERVEAU=claude           : Claude Code en ligne de commande (« claude -p »).

Quel que soit le cerveau, le texte repasse ensuite par le Contrôle de marque.
"""
from __future__ import annotations

import json
import os
import subprocess
import urllib.request

from . import base


def mode() -> str:
    return os.environ.get("QG_CERVEAU", "modeles").lower()


def _consigne(tache: str) -> str:
    return (
        "Tu écris pour un instructeur. Respecte strictement ce guide de voix.\n\n"
        + base.savoir("voix/guide-de-voix.md") + "\n" + base.savoir("voix/regles-de-ton.md")
        + "\nMots interdits :\n" + base.savoir("voix/mots-interdits.md")
        + "\n\nTâche : " + tache + "\nRéponds par le texte seul, sans commentaire."
    )


def ecrire(agent: str, tache: str, repli: str) -> str:
    """Renvoie le texte du cerveau choisi, ou « repli » (gabarit) s'il est absent ou en panne."""
    m = mode()
    if m == "modeles":
        return repli
    try:
        if m == "ollama":
            req = urllib.request.Request(
                os.environ.get("QG_OLLAMA_URL", "http://127.0.0.1:11434") + "/api/generate",
                data=json.dumps({"model": os.environ.get("QG_OLLAMA_MODELE", "mistral"),
                                 "prompt": _consigne(tache), "stream": False}).encode(),
                headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=120) as r:
                texte = json.loads(r.read())["response"].strip()
        elif m == "claude":
            texte = subprocess.run(["claude", "-p", _consigne(tache)], capture_output=True, text=True,
                                   timeout=180, check=True).stdout.strip()
        else:
            raise ValueError(f"cerveau inconnu : {m}")
        if not texte:
            raise ValueError("réponse vide")
        base.journal(agent, f"cerveau {m}", details=tache[:80])
        return texte
    except Exception as e:  # le QG ne s'arrête jamais sur une panne de cerveau
        base.journal(agent, f"cerveau {m} en panne, gabarit utilisé", ok=False, erreur=str(e)[:200])
        return repli
