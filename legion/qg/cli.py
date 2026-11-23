"""La ligne de commande du QG : « qg <commande> ».

Chaque commande est un agent de la carte. Les commandes courtes
(accroches, semaine, file, commentaire, entonnoir, matin) sont celles du tableau de bord.
"""
from __future__ import annotations

import argparse
import sys

from . import base, cerveau, connecteurs, croissance, publication, recherche, studio

AIDE = """Opération Légion · le QG

Recherche          tendances  concurrents  accroches  questions  motscles  profils  idees
Studio             semaine  voir  carrousel  miniature  decouper  monter  voix  plans
Publication        file  valider  refuser  programme  commentaire  capture  partenariats
Croissance         entonnoir  notes  test  pub  interruptions  journal  matin  tableau
Réglages           etat  releve
"""


def commandes(p: argparse.ArgumentParser) -> None:
    s = p.add_subparsers(dest="cmd")

    def c(nom, aide, **kw):
        return s.add_parser(nom, help=aide, **kw)

    # Recherche
    c("tendances", "les tendances de la semaine, triées pour votre niche")
    c("concurrents", "les posts concurrents qui explosent et leur forme d'accroche")
    a = c("accroches", "labo d'accroches : ce qui gagne, et des accroches écrites")
    a.add_argument("--ecrire", type=int, nargs="?", const=3, default=0, metavar="N",
                   help="écrire des accroches pour les N tendances les plus fortes")
    c("questions", "ce que demande votre public, par thème")
    c("motscles", "les requêtes de recherche notées")
    c("profils", "les profils du public et la conduite à tenir")
    i = c("idees", "la banque d'idées")
    i.add_argument("--ajouter", metavar="TITRE", help="ajouter une idée à vous")
    i.add_argument("--format", default="Reel", help="Reel, Carrousel ou Légende")
    i.add_argument("--pilier", default="Technique")

    # Studio
    sem = c("semaine", "écrire, contrôler et mettre en file la semaine suivante")
    sem.add_argument("-n", "--nombre", type=int, help="nombre de posts (défaut : objectifs.md)")
    v = c("voir", "voir un post en entier")
    v.add_argument("id")
    ca = c("carrousel", "fabriquer les pages d'un carrousel")
    ca.add_argument("id")
    mi = c("miniature", "fabriquer la miniature d'un post")
    mi.add_argument("id")
    de = c("decouper", "découper une vidéo longue en clips")
    de.add_argument("video")
    de.add_argument("-s", "--secondes", type=int, default=30)
    mo = c("monter", "monter une vidéo en 9:16 avec sous-titres")
    mo.add_argument("video")
    mo.add_argument("--srt", help="fichier de sous-titres (sinon Whisper s'il est installé)")
    vo = c("voix", "préparer le texte de la voix off et la synthétiser")
    vo.add_argument("id")
    pl = c("plans", "la liste des plans de coupe à tourner ou à chercher")
    pl.add_argument("id")

    # Publication
    c("file", "la file d'approbation")
    val = c("valider", "approuver des posts")
    val.add_argument("ids", nargs="*")
    val.add_argument("--tout", action="store_true", help="approuver toute la file")
    val.add_argument("--telegram", action="store_true", help="envoyer la file sur Telegram pour approbation")
    ref = c("refuser", "refuser des posts")
    ref.add_argument("ids", nargs="+")
    ref.add_argument("-m", "--motif", default="")
    c("programme", "programmer les posts approuvés sur chaque plateforme")
    com = c("commentaire", "trier la boîte et préparer les réponses")
    com.add_argument("--envoyer", action="store_true", help="envoyer les réponses pour de vrai")
    cap = c("capture", "relever les inscriptions par mot-clé")
    cap.add_argument("--mot", default="STAGE")
    c("partenariats", "les offres de marques dans la boîte")

    # Croissance
    e = c("entonnoir", "du post au client payant, et ce que chaque post a rapporté")
    e.add_argument("-j", "--jours", type=int, default=7)
    c("notes", "vos accroches classées par ce qu'elles rapportent")
    t = c("test", "créer deux variantes d'accroche pour un post")
    t.add_argument("id")
    pu = c("pub", "répartir le budget publicité sur les posts gagnants")
    pu.add_argument("-b", "--budget", type=float)
    c("interruptions", "les cas qui justifient de vous réveiller")
    j = c("journal", "le journal des agents")
    j.add_argument("-n", type=int, default=25)
    m = c("matin", "la tournée de la nuit, puis le résumé du matin")
    m.add_argument("--telegram", action="store_true", help="envoyer le résumé sur Telegram")
    c("tableau", "écrire le tableau de bord HTML")

    # Réglages
    c("etat", "l'état des connecteurs et du cerveau")
    c("releve", "relever vos comptes et remplacer les données d'exemple")


def texte_etat() -> str:
    s = [f"CERVEAU  {cerveau.mode()}" + ("  (gabarits de la couche de connaissance, hors ligne)"
                                         if cerveau.mode() == "modeles" else ""),
         "", "CONNECTEURS", base.table(["Service", "État", "Variables à poser"],
         [[n, "branché" if ok else "simulation", v] for n, ok, v in connecteurs.etat()]),
         "", f"DONNÉES     {base.dossier_donnees()}",
         f"CONNAISSANCE {base.dossier_connaissance()}",
         "", "Les données d'exemple servent tant que vos comptes ne sont pas branchés.",
         "Posez les variables dans legion/.env (jamais versionné), puis « qg releve »."]
    return "\n".join(s)


def releve() -> str:
    """Remplace les données d'exemple par ce que les comptes branchés renvoient."""
    lignes = []
    cmds = connecteurs.shopify_commandes()
    if cmds is None:
        lignes.append("Shopify : non branché, les commandes d'exemple restent en place.")
    else:
        base.sauver("commandes", cmds)
        lignes.append(f"Shopify : {len(cmds)} commandes relevées.")
    com = connecteurs.instagram_commentaires()
    if com is None:
        lignes.append("Instagram : non branché, les commentaires d'exemple restent en place.")
    else:
        base.sauver("commentaires", com)
        lignes.append(f"Instagram : {len(com)} commentaires relevés.")
    rappels = connecteurs.telegram_relever()
    for r in rappels:
        if r == "valider:tout":
            publication.valider(tout=True, par="telegram")
            lignes.append("Telegram : toute la file approuvée depuis votre téléphone.")
        elif r.startswith("valider:"):
            publication.valider([r.split(":", 1)[1]], par="telegram")
            lignes.append(f"Telegram : {r.split(':', 1)[1]} approuvé depuis votre téléphone.")
    base.journal("Relève", f"{len(lignes)} sources")
    return "\n".join(lignes)


def main(argv: list[str] | None = None) -> int:
    base.charger_env()
    p = argparse.ArgumentParser(prog="qg", description="Opération Légion · le QG",
                               formatter_class=argparse.RawDescriptionHelpFormatter, epilog=AIDE)
    commandes(p)
    a = p.parse_args(argv if argv is not None else sys.argv[1:])
    if not a.cmd:
        print(AIDE)
        return 0
    try:
        print(executer(a))
    except Exception as e:
        print(f"Erreur : {e}", file=sys.stderr)
        base.journal("QG", f"commande {a.cmd}", ok=False, erreur=str(e)[:200])
        return 1
    return 0


def executer(a) -> str:
    match a.cmd:
        # Recherche
        case "tendances":
            return recherche.texte_tendances()
        case "concurrents":
            return recherche.texte_concurrents()
        case "accroches":
            return recherche.texte_accroches(a.ecrire)
        case "questions":
            return recherche.texte_questions()
        case "motscles":
            return recherche.texte_mots_cles()
        case "profils":
            return recherche.texte_profils()
        case "idees":
            if a.ajouter:
                i = recherche.ajouter_idee(a.ajouter, a.format, a.pilier)
                return f"Idée {i['id']} ajoutée : « {i['titre']} » ({i['format']}, {i['pilier']}).\n\n" + recherche.texte_idees()
            return recherche.texte_idees()

        # Studio
        case "semaine":
            return recherche.texte_semaine(a.nombre)
        case "voir":
            return publication.texte_post(a.id)
        case "carrousel":
            p = base.trouver(base.posts(), a.id)
            if not p:
                return f"Post « {a.id} » inconnu."
            f = studio.carrousel(p)
            return f"{len(f)} pages écrites :\n" + "\n".join(f"  {x}" for x in f)
        case "miniature":
            p = base.trouver(base.posts(), a.id)
            return f"Miniature écrite : {studio.miniature(p)}" if p else f"Post « {a.id} » inconnu."
        case "decouper":
            c = studio.decouper(a.video, a.secondes)
            return f"{len(c)} clips :\n" + "\n".join(f"  {x}" for x in c)
        case "monter":
            return f"Montage écrit : {studio.monter(a.video, srt=a.srt)}"
        case "voix":
            p = base.trouver(base.posts(), a.id)
            if not p:
                return f"Post « {a.id} » inconnu."
            r = studio.voix_off(p)
            s = f"Texte à dire : {r['texte']}\nMoteur : {r['moteur']}"
            return s + (f"\nAudio : {r['audio']}" if r["audio"] else
                        "\n(posez donnees/voix/reference.wav et installez Chatterbox, ou branchez ElevenLabs)")
        case "plans":
            p = base.trouver(base.posts(), a.id)
            if not p:
                return f"Post « {a.id} » inconnu."
            pl = studio.plans_de_coupe(p)
            return base.table(["Plan à tourner", "Recherche d'archive", "Liens"],
                              [[x["plan"], x["recherche"], " ".join(x["liens"]) or "—"] for x in pl])

        # Publication
        case "file":
            return publication.texte_file()
        case "valider":
            if a.telegram:
                return connecteurs.telegram(
                    "File d'approbation :\n" + "\n".join(f"· {p['id']} {p['titre']} — {p['accroche']}"
                                                         for p in publication.file()),
                    [(f"✓ Approuver les {len(publication.file())} posts", "valider:tout")])
            v = publication.valider(a.ids, a.tout)
            return (f"{len(v)} posts approuvés : " + ", ".join(p["id"] for p in v) +
                    "\n« qg programme » pour les mettre au programme.") if v else "Rien à approuver."
        case "refuser":
            r = publication.refuser(a.ids, a.motif)
            return f"{len(r)} posts refusés : " + ", ".join(p["id"] for p in r)
        case "programme":
            return publication.texte_programme()
        case "commentaire":
            return publication.texte_commentaires(a.envoyer)
        case "capture":
            i = publication.capture(a.mot)
            return (f"{len(i)} inscriptions au mot « {a.mot} »\n" + base.table(
                ["Auteur", "Plateforme", "Message envoyé"],
                [[x["auteur"], x["plateforme"], x["message"]] for x in i])) if i else f"Aucune inscription au mot « {a.mot} »."
        case "partenariats":
            return publication.texte_partenariats()

        # Croissance
        case "entonnoir":
            return croissance.texte_entonnoir(a.jours)
        case "notes":
            return croissance.texte_notes()
        case "test":
            return croissance.texte_test(a.id)
        case "pub":
            return croissance.texte_publicite(a.budget)
        case "interruptions":
            return croissance.texte_interruptions()
        case "journal":
            return croissance.texte_journal(a.n)
        case "matin":
            return croissance.matin() if a.telegram else croissance.resume()
        case "tableau":
            return f"Tableau de bord écrit : {croissance.tableau()}"

        # Réglages
        case "etat":
            return texte_etat()
        case "releve":
            return releve()
    return f"Commande inconnue : {a.cmd}"
