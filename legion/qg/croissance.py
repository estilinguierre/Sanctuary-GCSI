"""Croissance : statistiques, suivi des liens, revenus, note des accroches,
tests A/B, publicité, résumé du matin, journal des agents, tableau de bord."""
from __future__ import annotations

import datetime as dt
import html
import re

from . import base, connecteurs, publication, studio

# ---------- Suivi des statistiques et des liens -------------------------------

def stats() -> list[dict]:
    s = base.charger("stats")
    base.journal("Suivi des statistiques", f"{len(s)} posts relevés")
    return s


def clics() -> dict[str, dict]:
    return {c["post_id"]: c for c in base.charger("clics")}


# ---------- Suivi des revenus (attribution) ------------------------------------

def post_de(page: str) -> str:
    m = re.search(r"utm_content=([A-Za-z0-9_-]+)", page or "")
    return m.group(1) if m else ""


def revenus(jours: int = 7) -> dict:
    cmds = connecteurs.shopify_commandes(jours) or base.charger("commandes")
    limite = base.aujourdhui() - dt.timedelta(days=jours)
    cmds = [c for c in cmds if dt.date.fromisoformat(c["date"]) >= limite]
    par, non_rattachees = {}, []
    for c in cmds:
        pid = post_de(c.get("page_arrivee", ""))
        if pid:
            e = par.setdefault(pid, {"commandes": 0, "ca": 0.0})
            e["commandes"] += 1
            e["ca"] += c["total"]
        else:
            non_rattachees.append(c)
    cl = clics()
    lignes = []
    for s in stats():
        pid = s["post_id"]
        v = par.get(pid, {"commandes": 0, "ca": 0.0})
        lignes.append({**s, **v, "clics": cl.get(pid, {}).get("clics", 0)})
    lignes.sort(key=lambda x: -x["ca"])
    total = {"vues": sum(l["vues"] for l in lignes), "jaime": sum(l["jaime"] for l in lignes),
             "abonnes": sum(l["abonnes"] for l in lignes), "clics": sum(l["clics"] for l in lignes),
             "commandes": sum(l["commandes"] for l in lignes), "ca": sum(l["ca"] for l in lignes),
             "non_rattachees": len(non_rattachees)}
    total["panier"] = total["ca"] / total["commandes"] if total["commandes"] else 0
    for l in lignes:
        l["a_ne_pas_refaire"] = l["ca"] == 0 and l["clics"] == 0 and l["jaime"] >= 0.3 * max(1, total["jaime"])
    res = {"lignes": lignes, "total": total, "jours": jours}
    base.sauver("entonnoir", res)
    base.journal("Suivi des revenus", f"{total['commandes']} commandes rattachées", ca=total["ca"])
    return res


def texte_entonnoir(jours: int = 7) -> str:
    r = revenus(jours)
    t = r["total"]
    s = [f"DU POST AU CLIENT PAYANT · {jours} jours",
         base.table(["Étape", "Valeur", "Taux"], [
             ["Vues", base.nombre(t["vues"]), "100 %"],
             ["Abonnés", base.nombre(t["abonnes"]), base.pct(t["abonnes"], t["vues"]) + " des vues"],
             ["Clics", base.nombre(t["clics"]), base.pct(t["clics"], t["vues"]) + " des vues"],
             ["Clients", base.nombre(t["commandes"]), base.pct(t["commandes"], t["clics"]) + " des clics"],
             ["Chiffre d'affaires", base.eur(t["ca"]), f"panier moyen {base.eur(t['panier'])}"]]),
         "", "QUELS POSTS ONT RAPPORTÉ",
         base.table(["Post", "Titre", "Vues", "J'aime", "Clics", "Commandes", "Revenus"],
                    [[l["post_id"], l["titre"], base.nombre(l["vues"]), base.nombre(l["jaime"]),
                      base.nombre(l["clics"]), l["commandes"], base.eur(l["ca"]) + (" ⚠" if l["a_ne_pas_refaire"] else "")]
                     for l in r["lignes"]])]
    for l in r["lignes"]:
        if l["a_ne_pas_refaire"]:
            s.append(f"\n⚠ « {l['titre']} » : {base.nombre(l['jaime'])} j'aime, 0 clic, 0 €. À ne pas refaire.")
    if t["non_rattachees"]:
        s.append(f"\n{t['non_rattachees']} commande(s) sans UTM : non attribuées.")
    return "\n".join(s) + base.mention_exemple("stats", "commandes", "clics")


# ---------- Note des accroches et tests A/B ---------------------------------------

def note_accroches() -> list[dict]:
    r = revenus()
    res = []
    for l in r["lignes"]:
        res.append({"accroche": l.get("accroche", l["titre"]), "forme": studio.forme_de(l.get("accroche", l["titre"])),
                    "retention": l.get("retention_3s", 0), "ca": l["ca"], "post": l["post_id"]})
    par = {}
    for x in res:
        e = par.setdefault(x["forme"], {"forme": x["forme"], "posts": 0, "retention": 0.0, "ca": 0.0})
        e["posts"] += 1
        e["retention"] += x["retention"]
        e["ca"] += x["ca"]
    classement = sorted(({**v, "retention": v["retention"] / v["posts"]} for v in par.values()),
                        key=lambda x: (-x["ca"], -x["retention"]))
    base.maj_section("accroches/formes-gagnantes.md", "Classement de vos propres accroches",
                     "\n".join(f"- {c['forme']} : {base.eur(c['ca'])} sur {c['posts']} post(s), "
                               f"rétention 3 s {c['retention'] * 100:.0f} %" for c in classement))
    base.journal("Note des accroches", f"{len(classement)} formes classées")
    return classement


def texte_notes() -> str:
    c = note_accroches()
    return ("VOS ACCROCHES, CLASSÉES PAR CE QU'ELLES RAPPORTENT\n" + base.table(
        ["Forme", "Posts", "Rétention 3 s", "Revenus"],
        [[x["forme"], x["posts"], f"{x['retention'] * 100:.0f} %", base.eur(x["ca"])] for x in c])
        + "\n(rangé dans connaissance/accroches/formes-gagnantes.md)" + base.mention_exemple("stats"))


def test_ab(ident: str) -> dict:
    """Crée deux variantes d'accroche pour un post, dans les deux formes qui rapportent le plus."""
    tous = base.posts()
    p = base.trouver(tous, ident)
    if not p:
        return {"erreur": f"Post « {ident} » inconnu."}
    gagnantes = [c["forme"] for c in note_accroches() if c["forme"] != "Autre"][:2] or None
    variantes = studio.variantes_accroche(p.get("sujet") or p["titre"], 2, gagnantes)
    p["test_ab"] = [{"bras": chr(65 + i), **v} for i, v in enumerate(variantes)]
    base.sauver_posts(tous)
    base.journal("Tests A/B", f"{len(variantes)} variantes", post=p["id"])
    return p


def texte_test(ident: str) -> str:
    p = test_ab(ident)
    if "erreur" in p:
        return p["erreur"]
    return (f"TEST A/B · {p['id']} « {p['titre']} »\n" + base.table(["Bras", "Forme", "Accroche"],
            [[v["bras"], v["forme"], v["texte"]] for v in p["test_ab"]])
            + "\n\nMême vidéo, deux accroches. À publier en Reels d'essai, puis « qg notes » tranche.")


# ---------- Publicité ---------------------------------------------------------------

def publicite(budget: float | None = None) -> list[dict]:
    budget = budget or base.entier(base.reglages("objectifs.md").get("budget publicite par jour", ""), 15)
    r = revenus()
    gagnants = [l for l in r["lignes"] if l["ca"] > 0][:3]
    total = sum(l["ca"] for l in gagnants) or 1
    res = [{"post": l["post_id"], "titre": l["titre"], "revenus": l["ca"],
            "budget_jour": round(budget * l["ca"] / total, 2),
            "canal": "Meta (publication existante) · TikTok Spark Ads"} for l in gagnants]
    base.journal("Publicité", f"{len(res)} posts à pousser", budget=budget)
    return res


def texte_publicite(budget: float | None = None) -> str:
    p = publicite(budget)
    if not p:
        return "Aucun post n'a encore rapporté : rien à pousser."
    return ("POUSSER LES GAGNANTS · budget réparti au prorata des revenus\n" + base.table(
        ["Post", "Titre", "Revenus", "Budget / jour", "Canal"],
        [[x["post"], x["titre"], base.eur(x["revenus"]), base.eur(x["budget_jour"]), x["canal"]] for x in p])
        + "\n\nÀ créer à la main dans le gestionnaire de publicités : aucun euro n'est dépensé par le QG.")


# ---------- Interruptions de boucle ---------------------------------------------------

def interruptions() -> list[dict]:
    """Les cas où l'équipe doit vous réveiller."""
    from .recherche import SEUIL_CONCURRENT, concurrents, tendances
    res = []
    for t in tendances():
        if t["pour_vous"] and t["variation"] >= 100:
            res.append({"quoi": "Une tendance explose", "detail": f"{t['terme']} +{t['variation']} %",
                        "action": "Brouillon express : « qg idees --ajouter »"})
    for c in concurrents():
        if c["explose"]:
            res.append({"quoi": "Un concurrent explose", "detail": f"{c['compte']} ×{str(c['ratio']).replace('.', ',')} · forme {c['forme']}",
                        "action": "Analyser l'accroche : « qg accroches »"})
            break
    pr = base.charger("prospects", [])
    if pr:
        res.append({"quoi": "« Combien ? »", "detail": f"{len(pr)} prospects chauds",
                    "action": "Réponses prêtes : « qg commentaire »"})
    for l in revenus()["lignes"]:
        if l.get("premiere_heure", {}).get("retention_3s", 1) < 0.4:
            res.append({"quoi": "Post dans la 1re heure", "detail": f"{l['titre']} : rétention faible",
                        "action": "Variante d'accroche : « qg test <id> »"})
            break
    for pid, c in clics().items():
        if c.get("aujourdhui", 0) >= 3 * max(1, c.get("hier", 0)):
            res.append({"quoi": "Pic de clics", "detail": f"{pid} : ×{c['aujourdhui'] // max(1, c['hier'])} depuis hier",
                        "action": "Pousser le post : « qg pub »"})
            break
    plaintes = [r for r in base.charger("reponses", []) if r.get("etiquette") == "plainte"]
    if plaintes:
        res.append({"quoi": "Commentaire négatif", "detail": f"{len(plaintes)} plainte(s)", "action": "Vous répondez"})
    for p in base.charger("partenariats", []) or publication.partenariats():
        res.append({"quoi": "Offre de marque", "detail": f"{p['de']} · {base.eur(p['budget']) if p['budget'] else 'budget non dit'}",
                    "action": "Vous décidez"})
        break
    return res


def texte_interruptions() -> str:
    i = interruptions()
    if not i:
        return "Rien ne justifie de vous réveiller."
    return "CE QUI INTERROMPT LA BOUCLE\n" + base.table(["Cas", "Détail", "Action"],
                                                        [[x["quoi"], x["detail"], x["action"]] for x in i])


# ---------- Journal des agents --------------------------------------------------------

def texte_journal(n: int = 25) -> str:
    j = base.lire_journal()
    if not j:
        return "Journal vide : lancez une commande du QG."
    erreurs = [x for x in j if not x["ok"]]
    s = (f"JOURNAL DES AGENTS · {len(j)} lignes, {len(erreurs)} erreur(s), "
         f"coût cumulé {base.eur(sum(x.get('cout', 0) for x in j))}\n")
    return s + base.table(["Quand", "Agent", "Action", "OK"],
                          [[x["quand"][5:16].replace("T", " "), x["agent"], x["action"], "✓" if x["ok"] else "✗"]
                           for x in j[-n:]])


# ---------- Résumé du matin -------------------------------------------------------------

def resume() -> str:
    r = revenus()
    t = r["total"]
    pr = base.charger("prospects", [])
    avous = [x for x in base.charger("reponses", []) if x.get("a_vous")]
    attente = publication.file()
    meilleur = next((l for l in r["lignes"] if l["ca"] > 0), None)
    rate = next((l for l in r["lignes"] if l["a_ne_pas_refaire"]), None)
    from .recherche import tendances
    tend = next((x for x in tendances() if x["pour_vous"]), None)
    l = ["Bonjour, chef ☀", "", f"CA 7 jours {base.eur(t['ca'])}   Prospects {len(pr)}   Abonnés {base.nombre(t['abonnes'])}", ""]
    if meilleur:
        l.append(f"€ Meilleur post : « {meilleur['titre']} » a rapporté {base.eur(meilleur['ca'])} "
                 f"sur {meilleur['commandes']} commandes. Faites une suite.")
    if rate:
        l.append(f"△ Le post le plus aimé a rapporté 0 €. {base.nombre(rate['jaime'])} j'aime, aucun clic. À ne pas refaire.")
    if attente:
        l.append(f"✎ {len(attente)} posts sont écrits et vérifiés. Ils attendent votre approbation : « qg file ».")
    if avous:
        l.append(f"◯ {len(avous)} messages ont besoin de vous (remboursements, réclamations). Le reste est traité.")
    if tend:
        l.append(f"↗ Tendance à saisir : {tend['terme'].lower()}, +{tend['variation']} %.")
    return "\n".join(l)


def envoyer_resume() -> str:
    texte = resume()
    boutons = [(f"✓ Approuver les {len(publication.file())} posts", "valider:tout")] if publication.file() else None
    etat = connecteurs.telegram(texte, boutons)
    base.ecrire_fichier(f"resumes/{base.aujourdhui().isoformat()}.txt", texte + "\n")
    base.journal("Résumé du matin", etat)
    return texte + f"\n\n[{etat}]"


def matin() -> str:
    """La tournée complète de la nuit, puis le résumé."""
    from . import recherche
    recherche.tendances()
    recherche.concurrents()
    recherche.questions()
    recherche.profils()
    recherche.idees()
    publication.traiter()
    publication.capture()
    publication.partenariats()
    revenus()
    note_accroches()
    return envoyer_resume()


# ---------- Tableau de bord HTML -----------------------------------------------------------

def tableau() -> str:
    r = revenus()
    t = r["total"]
    pr = base.charger("prospects", [])
    e = html.escape

    def carte(titre, valeur, sous=""):
        return (f'<div class="k"><small>{e(titre)}</small><b>{e(valeur)}</b>'
                f'<span>{e(sous)}</span></div>')

    def tbl(entetes, lignes):
        th = "".join(f"<th>{e(x)}</th>" for x in entetes)
        tr = "".join("<tr>" + "".join(f"<td>{e(str(c))}</td>" for c in l) + "</tr>" for l in lignes)
        return f"<table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table>"

    corps = [
        '<div class="kp">',
        carte("Chiffre d'affaires · 7 jours", base.eur(t["ca"]), "ventes rattachées aux posts"),
        carte("Nouveaux abonnés", base.nombre(t["abonnes"]), "et le post qui les a amenés"),
        carte("Clics sur les liens", base.nombre(t["clics"]), "suivis par liens UTM"),
        carte("Clients payants", base.nombre(t["commandes"]), f"panier moyen {base.eur(t['panier'])}"),
        "</div>",
        "<h2>Quels posts ont rapporté</h2>",
        tbl(["Post", "Titre", "Vues", "J'aime", "Clics", "Commandes", "Revenus"],
            [[l["post_id"], l["titre"], base.nombre(l["vues"]), base.nombre(l["jaime"]), base.nombre(l["clics"]),
              l["commandes"], base.eur(l["ca"])] for l in r["lignes"]]),
        "<h2>Prospects</h2>",
        tbl(["Auteur", "Plateforme", "Intention", "Demande"],
            [[p["auteur"], p["plateforme"], f"{p['intention']} %", p["demande"]] for p in pr]) if pr else "<p>Aucun prospect.</p>",
        "<h2>File d'approbation</h2>",
        tbl(["Id", "Jour", "Heure", "Format", "Titre"],
            [[p["id"], p["jour"], p["heure"], p["format"], p["titre"]] for p in publication.file()]) or "<p>File vide.</p>",
        "<h2>Résumé du matin</h2><pre>" + e(resume()) + "</pre>",
    ]
    page = f"""<!doctype html><html lang="fr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>QG · Opération Légion</title><style>
:root{{--ink:#111;--dim:#8a8a86;--line:#dcdcd8;--lime:#d9f35b;--canvas:#f1f1ee;color-scheme:light}}
*{{box-sizing:border-box}}body{{margin:0;padding:24px 16px 60px;background:var(--canvas);color:var(--ink);
font:14px/1.5 Inter,system-ui,-apple-system,sans-serif;max-width:1100px;margin-inline:auto}}
h1{{font-size:28px;margin:0 0 4px}}h2{{font-size:13px;letter-spacing:.09em;text-transform:uppercase;color:var(--dim);margin:26px 0 8px}}
.kp{{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px}}
.k{{background:#fff;border:1px solid var(--line);border-radius:10px;padding:12px}}
.k small{{font-size:9px;letter-spacing:.09em;text-transform:uppercase;color:var(--dim);display:block}}
.k b{{font-size:26px;display:block;font-variant-numeric:tabular-nums}}.k span{{font-size:10px;color:var(--dim)}}
.k:first-child{{background:var(--ink)}}.k:first-child b{{color:var(--lime)}}.k:first-child small,.k:first-child span{{color:#b9b9b5}}
table{{width:100%;border-collapse:collapse;background:#fff;border:1px solid var(--line);border-radius:10px;overflow:hidden;font-size:12.5px}}
th{{text-align:left;font-size:9.5px;letter-spacing:.07em;text-transform:uppercase;color:var(--dim);padding:8px;border-bottom:1px solid var(--line)}}
td{{padding:7px 8px;border-bottom:1px solid #f0f0ec}}tr:last-child td{{border-bottom:0}}
pre{{background:var(--ink);color:#e9e9e5;padding:14px;border-radius:10px;white-space:pre-wrap;font:12px/1.7 ui-monospace,monospace}}
p.n{{color:var(--dim);font-size:11.5px}}</style></head><body>
<h1>QG · Opération Légion</h1><p class="n">Généré le {base.maintenant().isoformat(sep=" ")} · fenêtre {r['jours']} jours</p>
{''.join(corps)}
<h2>Connecteurs</h2>{tbl(["Service", "État", "Variables"],
   [[n, "branché" if ok else "simulation", v] for n, ok, v in connecteurs.etat()])}
</body></html>"""
    f = base.ecrire_fichier("tableau.html", page)
    base.journal("Tableau de bord", "page écrite")
    return str(f)
