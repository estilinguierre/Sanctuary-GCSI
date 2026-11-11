"""Cerveau local : un modèle servi par Ollama sur la machine ou le réseau local."""
from __future__ import annotations

import json
import re

import httpx

from ..config import Config
from . import MAX_TOURS_OUTILS, ErreurCerveau


class Ollama:
    def __init__(self, cfg: Config):
        self.url, self.modele = cfg.ollama_url.rstrip("/"), cfg.ollama_modele

    def disponible(self) -> tuple[bool, str]:
        try:
            noms = [m["name"] for m in httpx.get(f"{self.url}/api/tags", timeout=1.5).json().get("models", [])]
        except (httpx.HTTPError, ValueError):
            return False, f"Ollama ne répond pas sur {self.url}"
        if not any(n == self.modele or n.split(":")[0] == self.modele for n in noms):
            return False, f"modèle absent : lancer « ollama pull {self.modele} »"
        return True, "prêt"

    def amorcer(self, journal: list[tuple[str, str]]) -> list:
        return [{"role": role, "content": texte} for role, texte in journal]

    def tour(self, natif: list, texte: str, lancer_outil, consigne: str, outils: list) -> str:
        definitions = [{"type": "function", "function": {
            "name": o.nom, "description": o.description, "parameters": o.schema}} for o in outils]
        natif.append({"role": "user", "content": texte})
        for _ in range(MAX_TOURS_OUTILS):
            try:
                r = httpx.post(f"{self.url}/api/chat", timeout=180, json={
                    "model": self.modele, "stream": False, "tools": definitions,
                    "messages": [{"role": "system", "content": consigne}, *natif]})
                r.raise_for_status()
            except httpx.HTTPStatusError as e:
                raise ErreurCerveau(f"Ollama a refusé la requête ({e.response.status_code}) : "
                                    f"{e.response.text[:200]}") from e
            except httpx.HTTPError as e:
                raise ErreurCerveau(f"Le cerveau local est injoignable ({type(e).__name__}).") from e
            message = r.json()["message"]
            natif.append(message)
            appels = message.get("tool_calls") or []
            if not appels:
                return re.sub(r"<think>.*?</think>", "", message.get("content", ""), flags=re.S).strip()
            for a in appels:
                nom, args = a["function"]["name"], a["function"].get("arguments") or {}
                if isinstance(args, str):
                    try:
                        args = json.loads(args)
                    except ValueError:
                        args = {}
                resultat, _ = lancer_outil(nom, args)
                natif.append({"role": "tool", "tool_name": nom, "content": resultat})
        raise ErreurCerveau("Le modèle local enchaîne les outils sans conclure ; reformulez la demande.")
