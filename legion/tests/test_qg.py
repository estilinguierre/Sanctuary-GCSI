"""Tests du QG. Lancer : python3 -m pytest legion/tests -q  (ou python3 legion/tests/test_qg.py)"""
from __future__ import annotations

import os
import sys
import tempfile
import unittest
from pathlib import Path

RACINE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(RACINE))


class Socle(unittest.TestCase):
    """Chaque test travaille sur un dossier de données neuf, sur les exemples."""

    def setUp(self):
        self.dossier = tempfile.TemporaryDirectory()
        os.environ["QG_DONNEES"] = self.dossier.name
        os.environ["QG_DATE"] = "2026-03-11"
        os.environ["QG_CERVEAU"] = "modeles"
        for m in [m for m in list(sys.modules) if m == "qg" or m.startswith("qg.")]:
            del sys.modules[m]
        global base, cerveau, cli, croissance, publication, recherche, studio
        from qg import base, cerveau, cli, croissance, publication, recherche, studio

    def tearDown(self):
        self.dossier.cleanup()
        os.environ.pop("QG_DONNEES", None)
        os.environ.pop("QG_DATE", None)


class TestBase(Socle):
    def test_sans_accents_et_mot_entier(self):
        self.assertTrue(base.contient("Le tir à sec", "tir a sec"))
        self.assertTrue(base.contient("C'est GARANTI", "garanti"))
        self.assertFalse(base.contient("une garantie partielle", "garanti"))
        self.assertFalse(base.contient("stagiaire", "stage"))

    def test_exemples_marques_comme_tels(self):
        self.assertTrue(base.est_exemple("commentaires"))
        base.sauver("commentaires", [])
        self.assertFalse(base.est_exemple("commentaires"))

    def test_dates_relatives_des_exemples(self):
        dates = {c["date"] for c in base.charger("commandes")}
        self.assertTrue(all(d <= "2026-03-11" for d in dates), dates)

    def test_section_remplacee_sans_perdre_le_reste(self):
        base.maj_section("public/profils.md", "Essai", "- un")
        base.maj_section("public/profils.md", "Essai", "- deux")
        t = base.savoir("public/profils.md")
        self.assertEqual(t.count("## Essai"), 1)
        self.assertIn("- deux", t)
        self.assertNotIn("- un\n", t)
        self.assertIn("# Profils du public", t)


class TestControleDeMarque(Socle):
    def test_promesse_bloquee(self):
        c = studio.controle("Groupement divisé par deux en 3 jours, garanti.")
        self.assertEqual(c["verdict"], "signale")
        self.assertIn("garanti", c["motifs"][0])

    def test_mot_corrige_tout_seul(self):
        c = studio.controle("Une astuce incroyable et un secret.")
        self.assertEqual(c["verdict"], "corrige")
        self.assertNotIn("incroyable", c["texte"])
        self.assertIn("net", c["texte"])

    def test_prix_inconnu_signale(self):
        self.assertEqual(studio.controle("C'est 150 € la journée.")["verdict"], "signale")
        self.assertEqual(studio.controle("C'est 190 € la journée.")["verdict"], "conforme")

    def test_exclamations_limitees(self):
        c = studio.controle("Viens !!! C'est ouvert ! Vraiment !")
        self.assertEqual(c["texte"].count("!"), 1)

    def test_accroche_trop_longue(self):
        self.assertEqual(studio.controle("x" * 200, "accroche")["verdict"], "signale")


class TestRecherche(Socle):
    def test_tendance_hors_niche_ecartee(self):
        t = {x["terme"]: x["pour_vous"] for x in recherche.tendances()}
        self.assertFalse(t["Défi viral de danse"])
        self.assertTrue(t["Tir à sec à la maison"])

    def test_concurrent_qui_explose(self):
        c = recherche.concurrents()
        self.assertTrue(c[0]["explose"])
        self.assertFalse(c[-1]["explose"])
        self.assertEqual(c[0]["forme"], "Preuve / résultat")

    def test_semaine_ecrite_et_controlee(self):
        posts = recherche.semaine(14)
        self.assertEqual(len(posts), 14)
        signales = [p for p in posts if p["etat"] == "signale"]
        self.assertEqual(len(signales), 1)
        self.assertEqual(signales[0]["titre"], "Promesse de résultat")
        self.assertEqual(len(publication.file()), 13)
        self.assertTrue(all(p["accroche"] for p in posts))
        self.assertTrue(all(p["script"] for p in posts if p["format"] == "Reel"))

    def test_une_idee_ne_sert_qu_une_fois(self):
        recherche.semaine(5)
        encore = recherche.semaine(5)
        self.assertEqual(len({p["idee"] for p in encore} & {"I001", "I002", "I003", "I004", "I005"}), 0)


class TestFileEtPublication(Socle):
    def setUp(self):
        super().setUp()
        recherche.semaine(14)

    def test_un_post_signale_n_entre_jamais_en_file(self):
        signale = next(p for p in base.posts() if p["etat"] == "signale")
        publication.valider([signale["id"]], tout=False)
        self.assertEqual(base.trouver(base.posts(), signale["id"])["etat"], "signale")
        publication.valider(tout=True)
        self.assertEqual(base.trouver(base.posts(), signale["id"])["etat"], "signale")

    def test_approbation_puis_programme(self):
        publication.valider(tout=True)
        self.assertEqual(len(publication.file()), 0)
        programmes = publication.programmer()
        self.assertEqual(len(programmes), 13)
        sorties = programmes[0]["sorties"]
        self.assertEqual({s["plateforme"] for s in sorties}, set(publication.PLATEFORMES))
        self.assertTrue(all("utm_content=" + programmes[0]["id"] in s["lien"] for s in sorties))

    def test_legende_coupee_aux_limites_de_la_plateforme(self):
        p = base.posts()[0]
        p["legende"] = "x" * 3000
        a = publication.adapter(p, "youtube")
        self.assertLessEqual(len(a["legende"]) - len(a["lien"]), 100)
        self.assertLessEqual(len(a["mots_diese"]), 3)

    def test_refus(self):
        p = publication.file()[0]
        publication.refuser([p["id"]], "pas cette semaine")
        self.assertEqual(base.trouver(base.posts(), p["id"])["etat"], "refuse")


class TestCommunaute(Socle):
    def test_etiquettes(self):
        cas = {"C'est quoi le prix ?": "prospect", "Quanto custa o curso?": "prospect",
               "C'est adapté à une débutante ?": "question", "Merci, très clair": "eloge",
               "Commande pas reçue depuis 10 jours": "plainte", "Gagne 500 € par jour, clique ici": "spam"}
        for texte, attendu in cas.items():
            self.assertEqual(publication.etiqueter(texte)["etiquette"], attendu, texte)

    def test_une_plainte_n_a_jamais_de_reponse_automatique(self):
        r = publication.reponse({"id": "C1", "auteur": "Léo", "plateforme": "instagram",
                                 "canal": "message", "texte": "Je veux un remboursement"})
        self.assertTrue(r["a_vous"])
        self.assertEqual(r["texte"], "")

    def test_un_prospect_recoit_le_prix_exact_et_un_lien_suivi(self):
        r = publication.reponse({"id": "C2", "auteur": "Théo", "plateforme": "instagram",
                                 "canal": "message", "texte": "C'est quoi le prix du stage niveau 1 ?"})
        self.assertIn("190 €", r["texte"])
        self.assertIn("utm_content=C2", r["texte"])
        self.assertEqual(studio.controle(r["texte"])["verdict"], "conforme")

    def test_le_bon_produit(self):
        r = publication.reponse({"id": "C3", "auteur": "Mei", "plateforme": "tiktok",
                                 "canal": "commentaire", "texte": "le sac existe en coffret ? prix ?"})
        self.assertIn("89 €", r["texte"])

    def test_question_hors_faq_remonte_a_vous(self):
        r = publication.reponse({"id": "C4", "auteur": "Omar", "plateforme": "tiktok",
                                 "canal": "commentaire", "texte": "Comment tu règles ta hausse ?"})
        self.assertTrue(r["a_vous"])

    def test_mot_d_inscription_seul(self):
        self.assertTrue(publication.inscription("STAGE"))
        self.assertTrue(publication.inscription("stage svp"))
        self.assertFalse(publication.inscription("C'est quoi le prix du stage niveau 1 ?"))

    def test_tri_complet_sans_double_emploi(self):
        r = publication.traiter()
        self.assertEqual(r["total"], 22)
        self.assertEqual(r["compte"]["prospect"], 9)
        self.assertEqual(len(r["prospects"]), 9)
        encore = publication.traiter()
        self.assertEqual(len(encore["prospects"]), 9, "un prospect ne doit pas être compté deux fois")

    def test_rien_n_est_envoye_sans_le_dire(self):
        r = publication.traiter(envoyer=False)
        self.assertTrue(all(not x.get("envoyee") for x in r["reponses"]))


class TestRevenus(Socle):
    def test_attribution_par_utm(self):
        r = croissance.revenus(7)
        t = r["total"]
        self.assertEqual(t["commandes"], 23)
        self.assertEqual(t["ca"], 4370.0)
        self.assertEqual(t["panier"], 190.0)
        self.assertEqual(t["non_rattachees"], 0)
        self.assertEqual(r["lignes"][0]["post_id"], "X01")
        self.assertEqual(r["lignes"][0]["ca"], 1900.0)

    def test_commande_sans_utm_non_attribuee(self):
        base.sauver("commandes", [{"id": "#1", "date": "2026-03-10", "total": 99.0, "page_arrivee": "/boutique"}])
        r = croissance.revenus(7)
        self.assertEqual(r["total"]["non_rattachees"], 1)
        self.assertEqual(r["total"]["ca"], 0)

    def test_post_tres_aime_sans_clic_signale(self):
        rate = [l for l in croissance.revenus(7)["lignes"] if l["a_ne_pas_refaire"]]
        self.assertEqual([l["post_id"] for l in rate], ["X07"])

    def test_budget_pub_reparti_au_prorata(self):
        p = croissance.publicite(15)
        self.assertEqual(len(p), 3)
        self.assertAlmostEqual(sum(x["budget_jour"] for x in p), 15, places=1)
        self.assertGreater(p[0]["budget_jour"], p[1]["budget_jour"])

    def test_resume_du_matin(self):
        recherche.semaine(14)
        publication.traiter()
        r = croissance.resume()
        self.assertIn("4 370 €", r)
        self.assertIn("Jour 1 contre jour 30", r)
        self.assertIn("13 posts", r)
        self.assertIn("0 €", r)

    def test_tableau_de_bord(self):
        f = Path(croissance.tableau())
        page = f.read_text(encoding="utf-8")
        self.assertIn("<!doctype html>", page)
        self.assertIn("4 370 €", page)
        self.assertIn("Opération Légion", page)


class TestCerveau(Socle):
    def test_mode_modeles_hors_ligne(self):
        self.assertEqual(cerveau.ecrire("Essai", "une tâche", "le gabarit"), "le gabarit")

    def test_panne_de_cerveau_retombe_sur_le_gabarit(self):
        os.environ["QG_CERVEAU"] = "ollama"
        os.environ["QG_OLLAMA_URL"] = "http://127.0.0.1:1"  # personne n'écoute
        try:
            self.assertEqual(cerveau.ecrire("Essai", "une tâche", "le gabarit"), "le gabarit")
            self.assertTrue(any(not x["ok"] for x in base.lire_journal()))
        finally:
            os.environ["QG_CERVEAU"] = "modeles"
            os.environ.pop("QG_OLLAMA_URL", None)


class TestConnecteursEnSimulation(Socle):
    def test_telegram_sans_jeton_ecrit_un_fichier(self):
        etat = cli.connecteurs.telegram("Bonjour chef")
        self.assertIn("simulation", etat)
        self.assertIn("Bonjour chef", (Path(self.dossier.name) / "telegram-simulation.txt").read_text(encoding="utf-8"))

    def test_rien_n_est_branche_par_defaut(self):
        self.assertTrue(all(not ok for _, ok, _ in cli.connecteurs.etat()))


class TestLigneDeCommande(Socle):
    def test_toutes_les_commandes_de_lecture_repondent(self):
        recherche.semaine(14)
        for argv in (["tendances"], ["concurrents"], ["accroches"], ["questions"], ["motscles"], ["profils"],
                     ["idees"], ["file"], ["commentaire"], ["entonnoir"], ["notes"], ["interruptions"],
                     ["journal"], ["pub"], ["partenariats"], ["capture"], ["etat"], ["matin"], ["voir", "P001"]):
            with self.subTest(argv=argv):
                self.assertEqual(cli.main(argv), 0)

    def test_commande_inconnue_signalee_sans_planter(self):
        self.assertEqual(cli.main(["voir", "ZZZ"]), 0)

    def test_journal_tient_les_erreurs(self):
        base.journal("Essai", "une panne", ok=False, erreur="x")
        self.assertIn("✗", croissance.texte_journal())


if __name__ == "__main__":
    unittest.main(verbosity=2)
