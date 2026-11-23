"""Recherche : tendances, concurrents, accroches, questions, mots-clés, profils,
banque d'idées, planificateur."""
from __future__ import annotations

import datetime as dt

from . import base, studio

SEUIL_CONCURRENT = 3.0


def vocabulaire() -> list[str]:
    return base.puces(base.sections(base.savoir("contenu/piliers.md")).get("Vocabulaire de la niche", ""))


def pertinent(texte: str) -> bool:
    return any(base.contient(texte, v) for v in vocabulaire())


# ---------- Éclaireur de tendances ---------------------------------------------

def tendances() -> list[dict]:
    t = sorted(base.charger("tendances"), key=lambda x: -x["variation"])
    for x in t:
        x["pour_vous"] = pertinent(x["terme"])
    base.journal("Éclaireur de tendances", f"{len(t)} tendances")
    return t


def texte_tendances() -> str:
    t = tendances()
    return ("TENDANCES DE LA SEMAINE\n" + base.table(["Tendance", "Variation", "Verdict"],
            [[x["terme"], f"+{x['variation']} %", "Pour vous" if x["pour_vous"] else "Passer"] for x in t])
            + base.mention_exemple("tendances"))


# ---------- Veille concurrents ---------------------------------------------------

def concurrents() -> list[dict]:
    c = base.charger("concurrents")
    for x in c:
        x["ratio"] = round(x["vues"] / max(1, x.get("moyenne_compte") or 1), 1)
        x["forme"] = studio.forme_de(x["accroche"])
        x["explose"] = x["ratio"] >= SEUIL_CONCURRENT
    c.sort(key=lambda x: -x["ratio"])
    base.journal("Veille concurrents", f"{sum(x['explose'] for x in c)} posts qui explosent")
    return c


def texte_concurrents() -> str:
    c = concurrents()
    return ("POSTS CONCURRENTS (rapport à la moyenne du compte)\n" + base.table(
        ["Compte", "Vues", "× moyenne", "Forme", "Accroche"],
        [[x["compte"], base.nombre(x["vues"]), f"×{x['ratio']}".replace(".", ",") + (" ▲" if x["explose"] else ""),
          x["forme"], x["accroche"][:60]] for x in c]) + base.mention_exemple("concurrents"))


# ---------- Analyste d'accroches -------------------------------------------------

def analyse_accroches() -> list[dict]:
    """Classe les formes d'accroche par performance chez les concurrents."""
    par = {}
    for x in concurrents():
        par.setdefault(x["forme"], []).append(x)
    res = [{"forme": f, "posts": len(l), "ratio_moyen": round(sum(x["ratio"] for x in l) / len(l), 1),
            "exemple": max(l, key=lambda x: x["ratio"])["accroche"]} for f, l in par.items()]
    res.sort(key=lambda x: -x["ratio_moyen"])
    corps = "\n".join(f"- {r['forme']} : ×{r['ratio_moyen']} en moyenne sur {r['posts']} post(s). Exemple : « {r['exemple']} »" for r in res)
    base.maj_section("accroches/analyses.md", f"Analyse du {base.aujourdhui().isoformat()}", corps)
    base.journal("Analyste d'accroches", f"{len(res)} formes classées")
    return res


def texte_accroches(ecrire: int = 0) -> str:
    a = analyse_accroches()
    s = "LABO D'ACCROCHES · formes qui gagnent chez les concurrents\n" + base.table(
        ["Forme", "Posts", "× moyenne", "Meilleur exemple"],
        [[r["forme"], r["posts"], f"×{r['ratio_moyen']}".replace(".", ","), r["exemple"][:64]] for r in a])
    if ecrire:
        gagnantes = [r["forme"] for r in a if r["forme"] != "Autre"]
        sujets = [t["terme"].lower() for t in tendances() if t["pour_vous"]][:ecrire]
        lignes, biblio = [], []
        for sujet in sujets:
            for v in studio.variantes_accroche(sujet, 2, gagnantes):
                lignes.append([sujet, v["forme"], v["texte"]])
                biblio.append(f"- [{v['forme']}] {v['texte']}")
        base.maj_section("accroches/bibliotheque.md", f"Accroches du {base.aujourdhui().isoformat()}", "\n".join(biblio))
        base.journal("Rédacteur d'accroches", f"{len(lignes)} accroches écrites")
        s += "\n\nACCROCHES ÉCRITES (rangées dans connaissance/accroches/bibliotheque.md)\n" + base.table(["Sujet", "Forme", "Accroche"], lignes)
    return s + base.mention_exemple("concurrents", "tendances")


# ---------- Chercheur de questions ------------------------------------------------

def themes_faq() -> list[dict]:
    res, q = [], None
    for l in base.savoir("offre/faq.md").splitlines():
        if l.startswith("Q:"):
            q = [m.strip() for m in l[2:].split(",") if m.strip()]
        elif l.startswith("R:") and q:
            res.append({"mots": q, "reponse": l[2:].strip(), "theme": q[0]})
            q = None
    return res


def questions() -> list[dict]:
    from .publication import etiqueter
    compte = {}
    for c in base.charger("commentaires"):
        e = etiqueter(c["texte"])["etiquette"]
        if e == "prospect":
            theme = "prix, places, inscription"
        elif e == "question":
            theme = next((t["theme"] for t in themes_faq() if any(base.contient(c["texte"], m) for m in t["mots"])), "hors FAQ")
        else:
            continue
        compte.setdefault(theme, []).append(c["texte"])
    res = sorted(({"theme": t, "nombre": len(l), "exemples": l[:3]} for t, l in compte.items()), key=lambda x: -x["nombre"])
    base.maj_section("public/questions.md", "Ce que demande votre public",
                     "\n".join(f"- {r['theme']} : {r['nombre']} (ex. « {r['exemples'][0]} »)" for r in res))
    base.journal("Chercheur de questions", f"{sum(r['nombre'] for r in res)} questions classées")
    return res


def texte_questions() -> str:
    q = questions()
    return ("CE QUE DEMANDE VOTRE PUBLIC\n" + base.table(["Thème", "Nombre", "Exemple"],
            [[r["theme"], r["nombre"], r["exemples"][0][:60]] for r in q]) + base.mention_exemple("commentaires"))


# ---------- Mots-clés --------------------------------------------------------------

def mots_cles() -> list[dict]:
    variation = {base.norm(t["terme"]): t["variation"] for t in base.charger("tendances")}
    res = []
    for r in base.charger("recherches"):
        hausse = max([v for k, v in variation.items() if any(base.contient(r["requete"], m) for m in k.split() if len(m) > 3)] or [0])
        res.append({"requete": r["requete"], "volume": r["volume"], "hausse": hausse,
                    "note": round(r["volume"] * (1 + hausse / 100) * (1 if pertinent(r["requete"]) else 0.2))})
    res.sort(key=lambda x: -x["note"])
    base.journal("Mots-clés", f"{len(res)} requêtes notées")
    return res


def texte_mots_cles() -> str:
    return ("MOTS-CLÉS (volume × hausse × pertinence)\n" + base.table(["Requête", "Volume", "Hausse", "Note"],
            [[m["requete"], base.nombre(m["volume"]), f"+{m['hausse']} %", base.nombre(m["note"])] for m in mots_cles()])
            + base.mention_exemple("recherches"))


# ---------- Profil du public ---------------------------------------------------------

PROFILS = [("Le débutant qui hésite", ["débutant", "débutante", "commencer", "âge", "licence", "adapté"],
            "Rassurer : on part de zéro, matériel fourni, tout commence à sec."),
           ("L'acheteur prêt", ["prix", "combien", "places", "inscrire", "je prends", "custa", "coffret"],
            "Répondre vite avec le prix exact et le lien suivi."),
           ("Le pratiquant qui stagne", ["comment", "règles", "routine", "matin", "soir", "visée", "détente"],
            "Donner une correction précise, une routine courte.")]


def profils() -> list[dict]:
    com = base.charger("commentaires")
    res = []
    for nom, mots, conduite in PROFILS:
        voix = [c["texte"] for c in com if any(base.contient(c["texte"], m) for m in mots)]
        res.append({"profil": nom, "messages": len(voix), "conduite": conduite, "exemples": voix[:2]})
    res.sort(key=lambda x: -x["messages"])
    base.maj_section("public/profils.md", "Profils observés", "\n".join(
        f"- {r['profil']} ({r['messages']} messages). {r['conduite']}" + (f" Ex. « {r['exemples'][0]} »" if r["exemples"] else "") for r in res))
    base.journal("Profil du public", f"{len(res)} profils mis à jour")
    return res


def texte_profils() -> str:
    return ("PROFILS DU PUBLIC (rangés dans connaissance/public/profils.md)\n" + base.table(
        ["Profil", "Messages", "Conduite à tenir"], [[p["profil"], p["messages"], p["conduite"]] for p in profils()])
        + base.mention_exemple("commentaires"))


# ---------- Banque d'idées ------------------------------------------------------------

def idees(actualiser: bool = True) -> list[dict]:
    banque = base.charger("idees")
    titres = {base.norm(i["titre"]) for i in banque}

    def ajouter(titre, sujet, fmt, pilier, source, note):
        if base.norm(titre) not in titres:
            titres.add(base.norm(titre))
            banque.append({"id": base.nouvel_id(banque, "I"), "titre": titre, "format": fmt, "pilier": pilier,
                           "sujet": sujet, "accroche": "", "source": source, "note": note, "etat": "libre"})
    if actualiser:
        for t in tendances():
            if t["pour_vous"]:
                ajouter(t["terme"], t["terme"].lower(), "Reel", "Technique", f"tendance +{t['variation']} %", min(99, 40 + t["variation"] // 4))
        for q in questions():
            if q["theme"] != "hors FAQ":
                ajouter(f"Réponse : {q['theme']}", q["theme"], "Carrousel", "Offre" if "prix" in q["theme"] else "Technique",
                        f"{q['nombre']} questions du public", 40 + 4 * q["nombre"])
        base.sauver("idees", banque)
        base.journal("Banque d'idées", f"{len(banque)} idées en banque")
    return sorted(banque, key=lambda i: -i["note"])


def ajouter_idee(titre: str, fmt: str = "Reel", pilier: str = "Technique") -> dict:
    banque = base.charger("idees")
    idee = {"id": base.nouvel_id(banque, "I"), "titre": titre, "format": fmt, "pilier": pilier, "sujet": titre.lower(),
            "accroche": "", "source": "vous", "note": 80, "etat": "libre"}
    banque.append(idee)
    base.sauver("idees", banque)
    return idee


def texte_idees() -> str:
    return "BANQUE D'IDÉES\n" + base.table(["Id", "Note", "Format", "Pilier", "Titre", "Source", "État"],
                                          [[i["id"], i["note"], i["format"], i["pilier"], i["titre"], i["source"], i["etat"]] for i in idees()])


# ---------- Planificateur ---------------------------------------------------------------

def lundi_prochain() -> dt.date:
    a = base.aujourdhui()
    return a + dt.timedelta(days=7 - a.weekday())


def semaine(nombre: int | None = None) -> list[dict]:
    """Écrit la semaine suivante : choisit les idées, rédige, contrôle, met en file."""
    nombre = nombre or base.entier(base.reglages("objectifs.md").get("posts par semaine", ""), 14)
    banque = idees()
    libres = [i for i in banque if i["etat"] == "libre"]
    # les idées que vous avez apportées gardent leur ordre ; les autres complètent par note
    choisies = ([i for i in sorted(libres, key=lambda i: i["id"]) if i["source"] == "vous"] +
                [i for i in libres if i["source"] != "vous"])[:nombre]
    tous, debut, crees = base.posts(), lundi_prochain(), []
    heures = ["07:30", "19:00"]
    for n, idee in enumerate(choisies):
        jour = n % 7
        post = {"id": base.nouvel_id(tous), "idee": idee["id"], "titre": idee["titre"], "format": idee["format"],
                "pilier": idee["pilier"], "sujet": idee["sujet"], "jour": base.JOURS[jour],
                "date": (debut + dt.timedelta(days=jour)).isoformat(), "heure": heures[(n // 7) % 2]}
        c = studio.controle(studio.ecrire_accroche(idee), "accroche")
        post["accroche"] = c["texte"]
        retouches, motifs = list(c["retouches"]), list(c["motifs"])
        post["script"] = studio.ecrire_script(post) if post["format"] == "Reel" else ""
        l = studio.controle(studio.ecrire_legende(post), "legende")
        post["legende"] = l["texte"]
        post["mots_diese"] = studio.mots_diese(post)
        retouches += [r for r in l["retouches"] if r not in retouches]
        motifs += [m for m in l["motifs"] if m not in motifs]
        verdict = "signale" if motifs else "corrige" if retouches else "conforme"
        post["controle"] = {"verdict": verdict, "motifs": motifs, "retouches": retouches}
        post["etat"] = "signale" if motifs else "en_attente"
        tous.append(post)
        crees.append(post)
        idee["etat"] = "planifiee"
    base.sauver_posts(tous)
    base.sauver("idees", banque)
    base.journal("Planificateur", f"{len(crees)} posts écrits", signales=sum(p["etat"] == "signale" for p in crees))
    base.journal("Contrôle de marque", f"{len(crees)} brouillons contrôlés")
    return crees


VERDICTS = {"conforme": "Conforme", "corrige": "Corrigé ✓", "signale": "SIGNALÉ"}


def texte_semaine(nombre: int | None = None) -> str:
    crees = semaine(nombre)
    if not crees:
        return "Aucune idée libre en banque : « qg idees --ajouter \"…\" » ou attendez la prochaine relève."
    s = f"SEMAINE DU {lundi_prochain().isoformat()} · {len(crees)} posts écrits et contrôlés\n" + base.table(
        ["Id", "Jour", "Heure", "Format", "Titre", "Contrôle", "Accroche"],
        [[p["id"], p["jour"], p["heure"], p["format"], p["titre"], VERDICTS[p["controle"]["verdict"]], p["accroche"][:58]] for p in crees])
    for p in crees:
        for m in p["controle"]["motifs"]:
            s += f"\n⚠ {p['id']} « {p['titre']} » signalé : {m}. Il n'entre pas dans la file."
        for r in p["controle"]["retouches"]:
            s += f"\n✓ {p['id']} corrigé : {r}."
    n = sum(p["etat"] == "en_attente" for p in crees)
    return s + f"\n\n{n} posts attendent votre approbation : « qg file », puis « qg valider <id> » ou « qg valider --tout »."
