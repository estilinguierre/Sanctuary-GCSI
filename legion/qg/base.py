"""Socle : chemins, registre JSON, couche de connaissance, journal des agents."""
from __future__ import annotations

import datetime as dt
import json
import os
import re
import unicodedata
from pathlib import Path

RACINE = Path(__file__).resolve().parent.parent
EXEMPLES = RACINE / "exemples"
JOURS = ["Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim"]


def dossier_donnees() -> Path:
    d = Path(os.environ.get("QG_DONNEES") or RACINE / "donnees")
    d.mkdir(parents=True, exist_ok=True)
    return d


def dossier_connaissance() -> Path:
    return Path(os.environ.get("QG_CONNAISSANCE") or RACINE / "connaissance")


def charger_env() -> None:
    """Lit legion/.env (jamais versionné) sans écraser l'environnement."""
    f = RACINE / ".env"
    if not f.exists():
        return
    for ligne in f.read_text(encoding="utf-8").splitlines():
        ligne = ligne.strip()
        if ligne and not ligne.startswith("#") and "=" in ligne:
            k, v = ligne.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))


def aujourdhui() -> dt.date:
    forcee = os.environ.get("QG_DATE")
    return dt.date.fromisoformat(forcee) if forcee else dt.date.today()


def maintenant() -> dt.datetime:
    return dt.datetime.combine(aujourdhui(), dt.datetime.now().time()).replace(microsecond=0)


# ---------- registre JSON -------------------------------------------------

def _dates_relatives(x):
    """Dans les exemples, « J-3 » signifie « il y a trois jours »."""
    if isinstance(x, dict):
        return {k: _dates_relatives(v) for k, v in x.items()}
    if isinstance(x, list):
        return [_dates_relatives(v) for v in x]
    if isinstance(x, str):
        m = re.fullmatch(r"J-(\d+)", x)
        if m:
            return (aujourdhui() - dt.timedelta(days=int(m.group(1)))).isoformat()
    return x


def est_exemple(nom: str) -> bool:
    """Vrai si la donnée lue vient des exemples et non de vos comptes."""
    return not (dossier_donnees() / f"{nom}.json").exists() and (EXEMPLES / f"{nom}.json").exists()


def charger(nom: str, defaut=None):
    f = dossier_donnees() / f"{nom}.json"
    if f.exists():
        return json.loads(f.read_text(encoding="utf-8"))
    e = EXEMPLES / f"{nom}.json"
    if e.exists() and os.environ.get("QG_SANS_EXEMPLES") != "1":
        return _dates_relatives(json.loads(e.read_text(encoding="utf-8")))
    return [] if defaut is None else defaut


def sauver(nom: str, donnees) -> Path:
    f = dossier_donnees() / f"{nom}.json"
    tmp = f.with_suffix(".tmp")
    tmp.write_text(json.dumps(donnees, ensure_ascii=False, indent=1), encoding="utf-8")
    tmp.replace(f)
    return f


def ecrire_fichier(relatif: str, texte: str) -> Path:
    f = dossier_donnees() / relatif
    f.parent.mkdir(parents=True, exist_ok=True)
    f.write_text(texte, encoding="utf-8")
    return f


def journal(agent: str, action: str, ok: bool = True, cout: float = 0.0, **details) -> None:
    ligne = {"quand": maintenant().isoformat(), "agent": agent, "action": action, "ok": ok, "cout": cout, **details}
    with (dossier_donnees() / "journal.jsonl").open("a", encoding="utf-8") as f:
        f.write(json.dumps(ligne, ensure_ascii=False) + "\n")


def lire_journal() -> list[dict]:
    f = dossier_donnees() / "journal.jsonl"
    if not f.exists():
        return []
    return [json.loads(l) for l in f.read_text(encoding="utf-8").splitlines() if l.strip()]


# ---------- couche de connaissance ----------------------------------------

def savoir(relatif: str) -> str:
    f = dossier_connaissance() / relatif
    return f.read_text(encoding="utf-8") if f.exists() else ""


def sections(texte: str) -> dict[str, str]:
    """Découpe un fichier Markdown par titres « ## »."""
    res, titre, lignes = {}, None, []
    for l in texte.splitlines():
        if l.startswith("## "):
            if titre is not None:
                res[titre] = "\n".join(lignes).strip()
            titre, lignes = l[3:].strip(), []
        elif titre is not None:
            lignes.append(l)
    if titre is not None:
        res[titre] = "\n".join(lignes).strip()
    return res


def puces(texte: str) -> list[str]:
    return [l.strip()[2:].strip() for l in texte.splitlines() if l.strip().startswith("- ")]


def reglages(relatif: str) -> dict[str, str]:
    """Lit les puces « - nom : valeur » d'un fichier de connaissance."""
    res = {}
    for p in puces(savoir(relatif)):
        if " : " in p:
            k, v = p.split(" : ", 1)
            res[norm(k)] = v.strip()
    return res


def entier(valeur: str, defaut: int = 0) -> int:
    m = re.search(r"\d[\d  ]*", valeur or "")
    return int(re.sub(r"\D", "", m.group(0))) if m else defaut


def maj_section(relatif: str, titre: str, corps: str) -> Path:
    """Remplace (ou ajoute) la section « ## titre » d'un fichier de connaissance."""
    f = dossier_connaissance() / relatif
    texte = f.read_text(encoding="utf-8") if f.exists() else f"# {titre}\n"
    bloc = f"## {titre}\n{corps.strip()}\n"
    motif = re.compile(rf"^## {re.escape(titre)}\n.*?(?=^## |\Z)", re.S | re.M)
    texte = motif.sub(lambda _: bloc + "\n", texte) if motif.search(texte) else texte.rstrip() + "\n\n" + bloc
    f.parent.mkdir(parents=True, exist_ok=True)
    f.write_text(texte.rstrip() + "\n", encoding="utf-8")
    return f


# ---------- texte ----------------------------------------------------------

def norm(s: str) -> str:
    s = unicodedata.normalize("NFD", str(s).lower())
    return "".join(c for c in s if unicodedata.category(c) != "Mn").replace("’", "'")


def contient(texte: str, mot: str) -> bool:
    """Recherche sans accents ; les mots simples sont cherchés en mot entier."""
    t, m = norm(texte), norm(mot).strip()
    if not m:
        return False
    if re.fullmatch(r"[a-z0-9]+", m):
        return re.search(rf"(?<![a-z0-9]){re.escape(m)}(?![a-z0-9])", t) is not None
    return m in t


def eur(n: float) -> str:
    return f"{n:,.0f}".replace(",", " ") + " €"


def nombre(n: float) -> str:
    return f"{n:,.0f}".replace(",", " ")


def pct(a: float, b: float) -> str:
    return "–" if not b else f"{100 * a / b:.1f} %".replace(".", ",")


def table(entetes: list[str], lignes: list[list]) -> str:
    lignes = [[str(c) for c in l] for l in lignes]
    larg = [max(len(e), *(len(l[i]) for l in lignes)) if lignes else len(e) for i, e in enumerate(entetes)]
    fmt = lambda l: "  ".join(c.ljust(larg[i]) for i, c in enumerate(l)).rstrip()
    return "\n".join([fmt(entetes), fmt(["─" * w for w in larg]), *map(fmt, lignes)])


def mention_exemple(*noms: str) -> str:
    ex = [n for n in noms if est_exemple(n)]
    return f"\n(données d'exemple : {', '.join(ex)} — branchez vos comptes avec « qg releve »)" if ex else ""


# ---------- posts -----------------------------------------------------------

def posts() -> list[dict]:
    return charger("posts", [])


def sauver_posts(p: list[dict]) -> None:
    sauver("posts", p)


def nouvel_id(existants: list[dict], prefixe: str = "P") -> str:
    n = max([int(x["id"][len(prefixe):]) for x in existants if x.get("id", "").startswith(prefixe)] or [0])
    return f"{prefixe}{n + 1:03d}"


def trouver(liste: list[dict], ident: str) -> dict | None:
    return next((x for x in liste if x.get("id", "").lower() == ident.lower()), None)
