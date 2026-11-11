"""Registre d'outils partagé par les deux cerveaux (Ollama et Claude).

Un outil = une fonction Python + un schéma JSON. Le registre valide les
arguments avant exécution et renvoie toujours un résultat sérialisable :
une erreur d'outil est rendue au modèle, elle ne fait jamais tomber la boucle.
"""
from __future__ import annotations

import json
import sqlite3
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Callable

from ..config import Config

_TYPES = {"string": str, "number": (int, float), "integer": int, "boolean": bool}


@dataclass
class Outil:
    nom: str
    description: str
    proprietes: dict
    requis: list[str]
    fonction: Callable[..., Any]

    @property
    def schema(self) -> dict:
        return {"type": "object", "properties": self.proprietes,
                "required": self.requis, "additionalProperties": False}


REGISTRE: dict[str, Outil] = {}


def outil(nom: str, description: str, proprietes: dict | None = None, requis: list[str] | None = None):
    def deco(f):
        REGISTRE[nom] = Outil(nom, description, proprietes or {}, requis or [], f)
        return f
    return deco


class Stockage:
    """Notes et nomenclatures dans un fichier SQLite local."""

    def __init__(self, chemin):
        chemin.parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(chemin, check_same_thread=False)
        self._db.row_factory = sqlite3.Row
        self._verrou = threading.Lock()
        self.executer("CREATE TABLE IF NOT EXISTS notes(id INTEGER PRIMARY KEY, ts REAL, texte TEXT)")
        self.executer("CREATE TABLE IF NOT EXISTS nomenclature(id INTEGER PRIMARY KEY, projet TEXT,"
                      " designation TEXT, quantite REAL, prix_unitaire REAL, fournisseur TEXT, reference TEXT)")

    def executer(self, sql: str, params: tuple = ()) -> list[dict]:
        with self._verrou, self._db:
            return [dict(r) for r in self._db.execute(sql, params).fetchall()]


class Minuteurs:
    """Minuteurs en mémoire ; à échéance, un événement est publié pour le HUD et la voix."""

    def __init__(self):
        self._verrou = threading.Lock()
        self._actifs: dict[int, dict] = {}
        self._evenements: list[dict] = []
        self._suivant = 1

    def creer(self, duree_s: float, libelle: str) -> dict:
        with self._verrou:
            ident = self._suivant
            self._suivant += 1
            m = {"id": ident, "libelle": libelle, "fin": time.time() + duree_s}
            self._actifs[ident] = m
        t = threading.Timer(duree_s, self._echeance, args=(ident,))
        t.daemon = True
        t.start()
        return m

    def _echeance(self, ident: int):
        with self._verrou:
            m = self._actifs.pop(ident, None)
            if m:
                self._evenements.append({"id": len(self._evenements) + 1, "type": "minuteur",
                                         "texte": f"Minuteur terminé : {m['libelle']}."})

    def annuler(self, ident: int) -> bool:
        with self._verrou:
            return self._actifs.pop(ident, None) is not None

    def lister(self) -> list[dict]:
        maintenant = time.time()
        with self._verrou:
            return [{"id": m["id"], "libelle": m["libelle"], "reste_s": max(0, round(m["fin"] - maintenant))}
                    for m in self._actifs.values()]

    def evenements(self, depuis: int = 0) -> list[dict]:
        with self._verrou:
            return [e for e in self._evenements if e["id"] > depuis]


@dataclass
class Contexte:
    config: Config
    stockage: Stockage
    minuteurs: Minuteurs = field(default_factory=Minuteurs)
    cache: dict = field(default_factory=dict)   # connexions paresseuses (MQTT, GPIO…)


def valider(o: Outil, args: dict) -> str | None:
    if not isinstance(args, dict):
        return "les arguments doivent être un objet JSON"
    inconnus = set(args) - set(o.proprietes)
    if inconnus:
        return f"arguments inconnus : {sorted(inconnus)}"
    manquants = [r for r in o.requis if r not in args]
    if manquants:
        return f"arguments manquants : {manquants}"
    for k, v in list(args.items()):
        spec = o.proprietes[k]
        # les petits modèles locaux envoient souvent "300" ou 300.0 pour un nombre
        if spec.get("type") in ("number", "integer") and isinstance(v, str):
            try:
                v = args[k] = float(v.replace(",", "."))
            except ValueError:
                pass
        if spec.get("type") == "integer" and isinstance(v, float) and v.is_integer():
            v = args[k] = int(v)
        attendu = _TYPES.get(spec.get("type"))
        if attendu and (not isinstance(v, attendu) or (isinstance(v, bool) and spec["type"] != "boolean")):
            return f"« {k} » doit être de type {spec['type']}"
        if "enum" in spec and v not in spec["enum"]:
            return f"« {k} » doit valoir l'une de : {spec['enum']}"
    return None


def executer(ctx: Contexte, nom: str, args: dict) -> tuple[str, bool]:
    """Exécute un outil. Renvoie (résultat JSON, est_erreur)."""
    o = REGISTRE.get(nom)
    if o is None:
        return json.dumps({"erreur": f"outil inconnu : {nom}"}, ensure_ascii=False), True
    probleme = valider(o, args)
    if probleme:
        return json.dumps({"erreur": probleme}, ensure_ascii=False), True
    try:
        resultat = o.fonction(ctx, **args)
        echec = isinstance(resultat, dict) and "erreur" in resultat
        return json.dumps(resultat, ensure_ascii=False, default=str), echec
    except Exception as e:  # rendu au modèle, qui peut corriger ou expliquer
        return json.dumps({"erreur": f"{type(e).__name__} : {e}"}, ensure_ascii=False), True


from . import atelier, domotique, rdm, systeme  # noqa: E402,F401  (enregistrement des outils)
