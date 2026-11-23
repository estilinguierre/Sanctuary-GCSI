"""Publication et communauté : file d'approbation, programmateur, multi-plateformes,
réponses publiques, messages privés, marqueur de prospects, capture d'e-mails, partenariats."""
from __future__ import annotations

import datetime as dt
import re
import urllib.parse

from . import base, cerveau, connecteurs, studio

PLATEFORMES = ["tiktok", "instagram", "youtube", "facebook"]
MOT_INSCRIPTION = "STAGE"
FICHIERS_REGLES = {"tiktok": "regles-tiktok.md", "instagram": "regles-instagram.md",
                   "youtube": "regles-youtube.md", "facebook": "regles-facebook.md"}


def regles(plateforme: str) -> dict[str, str]:
    return base.reglages(f"plateformes/{FICHIERS_REGLES[plateforme]}")


def creneaux(plateforme: str) -> list[str]:
    return [c.strip() for c in regles(plateforme).get("creneaux", "").split(",") if c.strip()]


# ---------- File d'approbation ------------------------------------------------

def file() -> list[dict]:
    return [p for p in base.posts() if p.get("etat") == "en_attente"]


def texte_file() -> str:
    f = file()
    if not f:
        return "La file est vide. « qg semaine » écrit la semaine suivante."
    s = f"FILE D'APPROBATION · {len(f)} posts attendent\n" + base.table(
        ["Id", "Jour", "Heure", "Format", "Titre", "Accroche"],
        [[p["id"], p["jour"], p["heure"], p["format"], p["titre"], p["accroche"][:58]] for p in f])
    signales = [p for p in base.posts() if p.get("etat") == "signale"]
    for p in signales:
        s += f"\n⚠ {p['id']} « {p['titre']} » : {p['controle']['motifs'][0]} (hors file)"
    return s + "\n\n« qg valider <id> » · « qg valider --tout » · « qg refuser <id> » · « qg voir <id> »"


def valider(ids: list[str] | None = None, tout: bool = False, par: str = "vous") -> list[dict]:
    tous, valides = base.posts(), []
    cibles = ([p for p in tous if p.get("etat") == "en_attente"] if tout
              else [p for i in (ids or []) if (p := base.trouver(tous, i))])
    for p in cibles:
        if p.get("etat") == "signale":
            continue
        p["etat"] = "approuve"
        p["approuve_le"] = base.maintenant().isoformat()
        p["approuve_par"] = par
        valides.append(p)
    base.sauver_posts(tous)
    base.journal("File d'approbation", f"{len(valides)} posts approuvés", par=par)
    return valides


def refuser(ids: list[str], motif: str = "") -> list[dict]:
    tous, refuses = base.posts(), []
    for i in ids:
        p = base.trouver(tous, i)
        if p:
            p["etat"] = "refuse"
            p["motif_refus"] = motif
            refuses.append(p)
    base.sauver_posts(tous)
    base.journal("File d'approbation", f"{len(refuses)} posts refusés", motif=motif)
    return refuses


def texte_post(ident: str) -> str:
    p = base.trouver(base.posts(), ident)
    if not p:
        return f"Post « {ident} » inconnu."
    s = [f"{p['id']} · {p['titre']}  [{p.get('etat')}]",
         f"{p['jour']} {p.get('date', '')} à {p['heure']} · {p['format']} · pilier {p.get('pilier', '')}",
         "", f"ACCROCHE  {p['accroche']}"]
    if p.get("script"):
        s += ["", "SCRIPT", p["script"]]
    s += ["", "LÉGENDE", p.get("legende", ""), "", "MOTS-DIÈSE  " + " ".join(p.get("mots_diese", []))]
    c = p.get("controle", {})
    s += ["", "CONTRÔLE  " + c.get("verdict", "?")]
    s += [f"  ⚠ {m}" for m in c.get("motifs", [])] + [f"  ✓ {r}" for r in c.get("retouches", [])]
    if p.get("lien"):
        s += ["", "LIEN SUIVI  " + p["lien"]]
    return "\n".join(s)


# ---------- Suivi des liens ----------------------------------------------------

def lien_suivi(post: dict, plateforme: str) -> str:
    base_lien = next((p.split(" : ", 1)[1] for p in base.puces(base.savoir("offre/offre.md"))
                      if p.startswith("Lien de réservation")), "https://exemple.fr/stages")
    q = urllib.parse.urlencode({"utm_source": plateforme, "utm_medium": "social",
                                "utm_campaign": "legion", "utm_content": post["id"]})
    return f"{base_lien}?{q}"


# ---------- Programmateur et multi-plateformes ------------------------------------

def adapter(post: dict, plateforme: str) -> dict:
    r = regles(plateforme)
    legende = post.get("legende", "")
    limite = base.entier(r.get("legende maximale", ""), 2200)
    diese = post.get("mots_diese", [])[:base.entier(r.get("mots-diese maximum", ""), 5)]
    lien = lien_suivi(post, plateforme)
    place = max(0, limite - len(lien) - 3)
    texte = legende[:place].rstrip() + ("…" if len(legende) > place else "")
    return {"plateforme": plateforme, "format_video": r.get("format", "").split(" (")[0],
            "duree_max": r.get("duree maximale", ""), "legende": f"{texte}\n{lien}",
            "mots_diese": diese, "lien": lien,
            "simulation": "simulation" in r.get("publication", "")}


def programmer(plateformes: list[str] | None = None) -> list[dict]:
    """Met les posts approuvés au programme, un créneau par plateforme."""
    plateformes = plateformes or PLATEFORMES
    tous, programmes = base.posts(), []
    for p in tous:
        if p.get("etat") != "approuve":
            continue
        p["sorties"] = []
        for n, plateforme in enumerate(plateformes):
            c = creneaux(plateforme)
            heure = c[n % len(c)] if c else p["heure"]
            sortie = adapter(p, plateforme)
            sortie["quand"] = f"{p.get('date', '')} {heure}"
            etat = connecteurs.n8n_publier({"post": p["id"], **sortie})
            sortie["etat"] = "simulation (application non auditée)" if sortie["simulation"] else etat
            p["sorties"].append(sortie)
        p["lien"] = p["sorties"][0]["lien"]
        p["etat"] = "programme"
        programmes.append(p)
    base.sauver_posts(tous)
    base.journal("Programmateur", f"{len(programmes)} posts programmés",
                 sorties=sum(len(p["sorties"]) for p in programmes))
    return programmes


def texte_programme() -> str:
    programmes = programmer()
    if not programmes:
        p = [x for x in base.posts() if x.get("etat") == "programme"]
        if not p:
            return "Aucun post approuvé à programmer. « qg file » puis « qg valider »."
        programmes = p
    lignes = [[x["id"], s["quand"], s["plateforme"], s["format_video"], s["etat"]]
              for x in programmes for s in x.get("sorties", [])]
    return ("PROGRAMME\n" + base.table(["Post", "Quand", "Plateforme", "Format", "État"], lignes)
            + "\n\nLes créneaux viennent de connaissance/plateformes/. Branchez N8N_WEBHOOK_PUBLICATION pour publier.")


# ---------- Étiquetage et réponses --------------------------------------------------

def etiquettes() -> list[tuple[str, int, list[str]]]:
    res = []
    for titre, corps in base.sections(base.savoir("communaute/etiquettes-prospects.md")).items():
        m = re.match(r"(\S+) \((\d+)\)", titre)
        if m:
            mots = [x.strip() for x in corps.strip().splitlines()[0].split(",") if x.strip()] if corps.strip() else []
            res.append((m.group(1), int(m.group(2)), mots))
    return res


def etiqueter(texte: str) -> dict:
    for nom, points, mots in etiquettes():
        if any(base.contient(texte, m) for m in mots):
            return {"etiquette": nom, "intention": points}
    return {"etiquette": "autre", "intention": 0}


def scripts() -> dict[str, str]:
    return {base.norm(k): v.strip() for k, v in base.sections(base.savoir("communaute/scripts-messages.md")).items()}


def reponse(c: dict) -> dict:
    """Prépare la réponse d'un commentaire ou d'un message. « a_vous » = vous répondez."""
    from .recherche import themes_faq
    e = etiqueter(c["texte"])
    prenom = c["auteur"].split("_")[0].title()
    s, res = scripts(), {"id": c["id"], **e, "a_vous": False, "texte": ""}
    if e["etiquette"] in ("autre", "question") and inscription(c["texte"]):
        res.update(etiquette="inscription", intention=70,
                   texte=s.get("inscription", "").format(prenom=prenom))
        return res
    if e["etiquette"] == "spam":
        res["texte"] = ""
        return res
    if e["etiquette"] == "plainte":
        res.update(a_vous=True, texte="")
        return res
    if e["etiquette"] == "prospect":
        prix = studio.prix_connus()
        def vise(nom: str) -> bool:
            return any(base.contient(c["texte"], m) for m in nom.split() if len(m) >= 3)
        nom, infos = next(((n, v) for n, v in prix.items() if vise(n)),
                          next(iter(prix.items()), ("Stage niveau 1", {"prix": 0, "detail": ""})))
        res["texte"] = s.get("prospect", "").format(prenom=prenom, produit=nom, prix=base.eur(infos["prix"]),
                                                    detail=infos["detail"], lien=lien_suivi({"id": c["id"]}, c["plateforme"]))
        res["produit"] = nom
    elif e["etiquette"] == "question":
        t = next((t for t in themes_faq() if any(base.contient(c["texte"], m) for m in t["mots"])), None)
        if not t:
            res.update(a_vous=True)
            return res
        res["texte"] = s.get("question", "").format(prenom=prenom, reponse=t["reponse"])
    elif e["etiquette"] == "eloge":
        res["texte"] = s.get("eloge", "").format(prenom=prenom)
    else:
        res.update(a_vous=True)
        return res
    brut = cerveau.ecrire("Réponses publiques", f"Réponds à « {c['texte']} » de {prenom}.", res["texte"])
    verif = studio.controle(brut, "legende")
    res["texte"] = res["texte"] if verif["verdict"] == "signale" else verif["texte"]
    return res


def traiter(envoyer: bool = False) -> dict:
    """Trie et répond à tous les commentaires et messages."""
    com = connecteurs.instagram_commentaires() or base.charger("commentaires")
    deja = {r["id"] for r in base.charger("reponses", [])}
    reponses, prospects, compte = base.charger("reponses", []), base.charger("prospects", []), {}
    vus = {p["id"] for p in prospects}
    for c in com:
        r = reponse(c)
        compte[r["etiquette"]] = compte.get(r["etiquette"], 0) + 1
        if c["id"] in deja:
            continue
        r.update(auteur=c["auteur"], plateforme=c["plateforme"], canal=c.get("canal", "commentaire"),
                 question=c["texte"], quand=base.maintenant().isoformat(), envoyee=False)
        if r["texte"] and envoyer and c.get("canal") == "commentaire":
            r["envoyee"] = connecteurs.instagram_repondre(c["id"], r["texte"]) == "envoyé"
        reponses.append(r)
        if r["etiquette"] == "prospect" and c["id"] not in vus:
            prospects.append({"id": c["id"], "auteur": c["auteur"], "plateforme": c["plateforme"],
                              "canal": c.get("canal", "commentaire"), "demande": c["texte"],
                              "intention": r["intention"], "produit": r.get("produit", ""),
                              "lien": lien_suivi({"id": c["id"]}, c["plateforme"]), "quand": r["quand"], "suivi": "à faire"})
    base.sauver("reponses", reponses)
    base.sauver("prospects", prospects)
    base.journal("Réponses publiques", f"{len(com)} messages triés", prospects=compte.get("prospect", 0),
                 a_vous=sum(1 for r in reponses if r.get("a_vous")))
    return {"total": len(com), "compte": compte, "reponses": reponses, "prospects": prospects}


ORDRE = ["prospect", "inscription", "question", "eloge", "plainte", "spam", "autre"]
NOMS_ETIQ = {"prospect": "Prospects", "inscription": "Inscriptions", "question": "Questions",
             "eloge": "Éloges", "plainte": "Plaintes", "spam": "Spam", "autre": "Autres"}


def texte_commentaires(envoyer: bool = False) -> str:
    r = traiter(envoyer)
    s = f"BOÎTE · {r['total']} messages triés\n" + "   ".join(
        f"{NOMS_ETIQ[e]} {r['compte'].get(e, 0)}" for e in ORDRE if r["compte"].get(e))
    recents = r["reponses"][-r["total"]:] if r["total"] else []
    auto = [x for x in recents if x["texte"]]
    s += "\n\nRÉPONSES PRÉPARÉES\n" + base.table(["Auteur", "Étiquette", "Demande", "Réponse"],
        [[x["auteur"], x["etiquette"], x["question"][:34], x["texte"][:60]] for x in auto])
    avous = [x for x in recents if x.get("a_vous")]
    if avous:
        s += f"\n\n⚠ {len(avous)} ont besoin de vous personnellement\n" + base.table(
            ["Auteur", "Canal", "Message"], [[x["auteur"], x["canal"], x["question"][:70]] for x in avous])
    s += f"\n\nPROSPECTS · {len(r['prospects'])}\n" + base.table(["Auteur", "Plateforme", "Intention", "Demande"],
        [[p["auteur"], p["plateforme"], f"{p['intention']} %", p["demande"][:50]] for p in r["prospects"]])
    if not envoyer:
        s += "\n\nRien n'a été envoyé. « qg commentaire --envoyer » pour répondre pour de vrai."
    return s + base.mention_exemple("commentaires")


# ---------- Capture d'e-mails ---------------------------------------------------------

def inscription(texte: str, mot: str = MOT_INSCRIPTION) -> bool:
    """Le mot-clé seul (« STAGE », « stage svp ») vaut inscription ; noyé dans une phrase, non."""
    return base.contient(texte, mot) and len(base.norm(texte).split()) <= 3


def capture(mot: str = MOT_INSCRIPTION) -> list[dict]:
    com = base.charger("commentaires")
    liste, vus = base.charger("inscrits", []), {i["auteur"] for i in base.charger("inscrits", [])}
    for c in com:
        if inscription(c["texte"], mot) and c["auteur"] not in vus:
            liste.append({"auteur": c["auteur"], "plateforme": c["plateforme"], "mot": mot,
                          "quand": base.maintenant().isoformat(),
                          "message": scripts().get("inscription", "").format(prenom=c["auteur"].title())})
            vus.add(c["auteur"])
    base.sauver("inscrits", liste)
    base.journal("Capture d'e-mails", f"{len(liste)} inscrits", mot=mot)
    return liste


# ---------- Partenariats ----------------------------------------------------------------

MOTS_PARTENARIAT = ["partenariat", "collaboration", "sponsoris", "placement", "ambassadeur", "devis"]


def partenariats() -> list[dict]:
    res = []
    for m in base.charger("courriels"):
        if any(base.contient(m["objet"] + " " + m["texte"], x) for x in MOTS_PARTENARIAT):
            montants = [base.entier(x) for x in re.findall(r"(\d[\d  ]*) ?€", m["texte"])]
            res.append({"id": m["id"], "de": m["de"], "objet": m["objet"],
                        "budget": max(montants) if montants else 0, "date": m["date"], "suivi": "à décider"})
    base.sauver("partenariats", res)
    base.journal("Partenariats", f"{len(res)} offres repérées")
    return res


def texte_partenariats() -> str:
    p = partenariats()
    if not p:
        return "Aucune offre de partenariat dans la boîte." + base.mention_exemple("courriels")
    return ("OFFRES DE PARTENARIAT · vous décidez\n" + base.table(["Id", "De", "Objet", "Budget", "Reçue"],
            [[x["id"], x["de"], x["objet"], base.eur(x["budget"]) if x["budget"] else "—", x["date"]] for x in p])
            + base.mention_exemple("courriels"))
