import json
import math
import time
from types import SimpleNamespace

import httpx
import pytest
from fastapi.testclient import TestClient

from sentinelle import voix
from sentinelle.cerveau import Cerveau, ErreurCerveau
from sentinelle.cerveau.claude import Claude
from sentinelle.cerveau.ollama import Ollama
from sentinelle.config import Config, charger
from sentinelle.outils import REGISTRE, Contexte, Stockage, executer
from sentinelle.outils import rdm
from sentinelle.serveur import creer_app


@pytest.fixture
def ctx(tmp_path):
    cfg = Config(donnees=tmp_path, appareils={
        "lampe": {"type": "mqtt", "topic": "atelier/lampe/set", "topic_etat": "atelier/lampe/state",
                  "actions": {"allumer": "ON", "eteindre": "OFF"}}})
    return Contexte(cfg, Stockage(tmp_path / "t.db"))


def appel(ctx, outil_, /, **args):
    resultat, erreur = executer(ctx, outil_, args)
    return json.loads(resultat), erreur


# ---------- registre ----------
def test_schemas_valides():
    assert len(REGISTRE) >= 20
    for o in REGISTRE.values():
        assert set(o.requis) <= set(o.proprietes), o.nom
        assert o.description


def test_validation_et_tolerance(ctx):
    assert appel(ctx, "inexistant")[1]
    assert appel(ctx, "note_ajouter")[1]                          # argument requis manquant
    assert appel(ctx, "note_ajouter", texte="a", autre=1)[1]      # argument inconnu
    assert appel(ctx, "rdm_vis", diametre="M7", classe="8.8")[1]  # hors énumération
    r, erreur = appel(ctx, "minuteur_creer", duree_s="300", libelle="colle")   # nombre envoyé en texte
    assert not erreur and r["duree_s"] == 300


# ---------- système ----------
def test_notes(ctx):
    appel(ctx, "note_ajouter", texte="Entraxe rail : 120 mm")
    appel(ctx, "note_ajouter", texte="Commander du PETG")
    assert len(appel(ctx, "note_lister")[0]) == 2
    trouve = appel(ctx, "note_lister", recherche="petg")[0]
    assert len(trouve) == 1
    appel(ctx, "note_supprimer", id=trouve[0]["id"])
    assert len(appel(ctx, "note_lister")[0]) == 1


def test_minuteur_echeance(ctx):
    appel(ctx, "minuteur_creer", duree_s=0.05, libelle="essai")
    assert appel(ctx, "minuteur_lister")[0][0]["libelle"] == "essai"
    time.sleep(0.2)
    assert appel(ctx, "minuteur_lister")[0] == []
    evenements = ctx.minuteurs.evenements()
    assert "essai" in evenements[0]["texte"]
    assert ctx.minuteurs.evenements(depuis=evenements[0]["id"]) == []


def test_etat_machine(ctx):
    r, erreur = appel(ctx, "etat_machine")
    assert not erreur and 0 <= r["ram_pct"] <= 100


# ---------- résistance des matériaux ----------
def test_section_rectangle_et_tube():
    s = rdm.section("rectangle", largeur=20, hauteur=40)
    assert s["inertie_mm4"] == pytest.approx(20 * 40**3 / 12)
    assert s["module_flexion_mm3"] == pytest.approx(20 * 40**2 / 6)
    t = rdm.section("tube_rond", diametre=30, epaisseur=2)
    assert t["aire_mm2"] == pytest.approx(math.pi * (30**2 - 26**2) / 4)


def test_flexion_console_reference(ctx):
    # Plat acier 40x8 à plat, console de 500 mm, 100 N en bout : M = 50 N.m,
    # W = 40*8²/6 = 426,7 mm3 -> 117,2 MPa ; I = 1706,7 mm4 -> f = FL³/3EI = 11,63 mm
    r, erreur = appel(ctx, "rdm_flexion", cas="console_charge_en_bout", portee_mm=500, charge_n=100,
                      materiau="acier_s235", forme="rectangle", largeur=40, hauteur=8)
    assert not erreur
    assert r["moment_max_n_m"] == 50
    assert r["contrainte_mpa"] == pytest.approx(117.2, abs=0.1)
    assert r["fleche_mm"] == pytest.approx(11.63, abs=0.02)
    assert r["coefficient_securite"] == pytest.approx(2.01, abs=0.01) and r["tient"]


def test_flexion_repartie_sur_appuis(ctx):
    # Tube 40x40x2 S235, 2 m sur deux appuis, 1000 N répartis : M = qL²/8 = 250 N.m
    r, _ = appel(ctx, "rdm_flexion", cas="appuis_charge_repartie", portee_mm=2000, charge_n=1000,
                 materiau="acier_s235", forme="tube_rectangulaire", largeur=40, hauteur=40, epaisseur=2)
    inertie = (40**4 - 36**4) / 12
    assert r["moment_max_n_m"] == 250
    assert r["fleche_mm"] == pytest.approx(5 * 0.5 * 2000**4 / (384 * 210000 * inertie), abs=0.01)


def test_dimensions_invalides(ctx):
    r, erreur = appel(ctx, "rdm_section", forme="tube_rond", diametre=10, epaisseur=6)
    assert erreur and "diametre_interieur" in r["erreur"]


def test_vis_et_couts(ctx):
    v, _ = appel(ctx, "rdm_vis", diametre="M8", classe="8.8")
    assert v["traction_limite_elastique_n"] == round(36.6 * 640)
    c, _ = appel(ctx, "cout_impression", masse_g=250, prix_bobine_eur_kg=20, duree_h=10)
    assert c == {"matiere_eur": 5.0, "electricite_eur": 0.3, "total_eur": 5.3}
    assert appel(ctx, "masse_piece", volume_cm3=100, materiau="pla")[0]["masse_g"] == 124.0


# ---------- atelier ----------
def test_nomenclature(ctx):
    appel(ctx, "nomenclature_ajouter", projet="Cyberdeck", designation="Écran 7 pouces", quantite=1, prix_unitaire=62.5)
    appel(ctx, "nomenclature_ajouter", projet="cyberdeck", designation="Insert M3", quantite=20, prix_unitaire=0.12)
    r, _ = appel(ctx, "nomenclature_lister", projet="cyberdeck")
    assert len(r["lignes"]) == 2 and r["total_eur"] == 64.9
    assert appel(ctx, "nomenclature_lister")[0] == {"projets": ["cyberdeck"]}
    export, _ = appel(ctx, "nomenclature_exporter", projet="cyberdeck")
    contenu = open(export["fichier"], encoding="utf-8-sig").read()
    assert "Insert M3;20.0;0.12;2.4" in contenu


def test_imprimantes(ctx, monkeypatch):
    assert "erreur" in appel(ctx, "imprimante_etat")[0]
    ctx.config.imprimantes = {"ender": {"type": "octoprint", "url": "http://octo"},
                              "voron": {"type": "moonraker", "url": "http://klipper"}}

    def faux(request: httpx.Request):
        if request.url.host == "klipper":
            return httpx.Response(200, json={"result": {"status": {
                "print_stats": {"state": "printing", "filename": "boitier.gcode", "print_duration": 1800},
                "display_status": {"progress": 0.425}, "extruder": {"temperature": 215.2},
                "heater_bed": {"temperature": 60.1}}}})
        if request.url.path == "/api/job":
            return httpx.Response(200, json={"state": "Printing", "job": {"file": {"name": "support.gcode"}},
                                             "progress": {"completion": 61.234, "printTimeLeft": 1500}})
        return httpx.Response(200, json={"temperature": {"tool0": {"actual": 210}, "bed": {"actual": 60}}})

    vrai = httpx.Client
    monkeypatch.setattr(httpx, "Client", lambda **kw: vrai(transport=httpx.MockTransport(faux), **kw))
    r, _ = appel(ctx, "imprimante_etat")
    assert r["ender"] == {"etat": "Printing", "fichier": "support.gcode", "avancement_pct": 61.2,
                          "reste_min": 25, "buse_c": 210, "plateau_c": 60}
    assert r["voron"]["avancement_pct"] == 42.5 and r["voron"]["duree_min"] == 30


# ---------- domotique ----------
class FauxMqtt:
    def __init__(self):
        self.publies, self.etats = [], {"atelier/lampe/state": "ON"}

    def publier(self, topic, charge):
        self.publies.append((topic, charge))


def test_appareils_liste_blanche(ctx):
    ctx.cache["mqtt"] = FauxMqtt()
    assert appel(ctx, "appareils_lister")[0][0]["actions"] == ["allumer", "eteindre"]
    assert appel(ctx, "appareil_commander", nom="lampe", action="allumer")[0]["fait"]
    assert ctx.cache["mqtt"].publies == [("atelier/lampe/set", "ON")]
    assert "erreur" in appel(ctx, "appareil_commander", nom="lampe", action="exploser")[0]
    assert "erreur" in appel(ctx, "appareil_commander", nom="four", action="allumer")[0]
    assert len(ctx.cache["mqtt"].publies) == 1
    assert appel(ctx, "appareil_etat", nom="lampe")[0]["etat"] == "ON"


# ---------- cerveaux ----------
def serveur_ollama(monkeypatch, reponses, recu):
    def faux_post(url, json=None, timeout=None):
        recu.append(json)
        return httpx.Response(200, json={"message": reponses.pop(0)}, request=httpx.Request("POST", url))
    monkeypatch.setattr(httpx, "post", faux_post)


def test_ollama_boucle_outils(ctx, monkeypatch):
    recu = []
    serveur_ollama(monkeypatch, [
        {"role": "assistant", "content": "", "tool_calls": [
            {"function": {"name": "rdm_vis", "arguments": {"diametre": "M6", "classe": "8.8"}}}]},
        {"role": "assistant", "content": "<think>calcul</think>Environ douze mille huit cents newtons."}], recu)
    c = Cerveau(ctx.config, ctx, {"ollama": Ollama(ctx.config)})
    c.choisir("ollama")
    r = c.repondre("s", "Que tient une vis M6 ?")
    assert r["reponse"] == "Environ douze mille huit cents newtons."
    assert r["outils"][0]["nom"] == "rdm_vis" and not r["outils"][0]["erreur"]
    outil_rendu = recu[1]["messages"][-1]
    assert outil_rendu["role"] == "tool" and "12864" in outil_rendu["content"]
    assert recu[0]["messages"][0]["role"] == "system" and recu[0]["tools"][0]["type"] == "function"


class FauxClaude:
    """Imite client.beta.messages.create et garde les requêtes reçues."""

    def __init__(self, reponses):
        self.reponses, self.requetes = reponses, []
        self.beta = SimpleNamespace(messages=SimpleNamespace(create=self._create))

    def _create(self, **kw):
        self.requetes.append({**kw, "messages": list(kw["messages"])})
        return self.reponses.pop(0)


def bloc(**kw):
    return SimpleNamespace(**kw)


def test_claude_boucle_outils(ctx):
    reflexion = bloc(type="thinking", thinking="", signature="sig")
    faux = FauxClaude([
        SimpleNamespace(stop_reason="tool_use", content=[
            reflexion,
            bloc(type="tool_use", id="tu_1", name="note_ajouter", input={"texte": "Acheter des inserts"}),
            bloc(type="tool_use", id="tu_2", name="appareil_commander", input={"nom": "four", "action": "allumer"})]),
        SimpleNamespace(stop_reason="end_turn", content=[bloc(type="text", text="C'est noté.")])])
    claude = Claude(ctx.config)
    claude._client = faux
    c = Cerveau(ctx.config, ctx, {"claude": claude})
    assert c.repondre("s", "Note : acheter des inserts")["reponse"] == "C'est noté."
    premiere, seconde = faux.requetes
    assert premiere["model"] == "claude-opus-5-5" and premiere["thinking"] == {"type": "adaptive"}
    assert premiere["fallbacks"] == "default" and premiere["tools"][0]["input_schema"]["type"] == "object"
    # le tour assistant est renvoyé tel quel (réflexion comprise), les deux résultats dans un seul message
    assert seconde["messages"][1]["content"][0] is reflexion
    resultats = seconde["messages"][2]["content"]
    assert [r["tool_use_id"] for r in resultats] == ["tu_1", "tu_2"]
    assert not resultats[0]["is_error"] and resultats[1]["is_error"]
    assert appel(ctx, "note_lister")[0][0]["texte"] == "Acheter des inserts"


def test_claude_refus(ctx):
    claude = Claude(ctx.config)
    claude._client = FauxClaude([SimpleNamespace(stop_reason="refusal", content=[])])
    with pytest.raises(ErreurCerveau):
        Cerveau(ctx.config, ctx, {"claude": claude}).repondre("s", "…")


class Echo:
    modele = "echo"

    def __init__(self, ok=True):
        self.ok, self.amorces = ok, []

    def disponible(self):
        return self.ok, "prêt" if self.ok else "éteint"

    def amorcer(self, journal):
        self.amorces.append(list(journal))
        return list(journal)

    def tour(self, natif, texte, lancer_outil, consigne, outils):
        natif.append(("user", texte))
        return f"{len(natif)}:{texte}"


def test_bascule_conserve_la_conversation(ctx):
    a, b = Echo(ok=False), Echo()
    c = Cerveau(ctx.config, ctx, {"local": a, "distant": b})
    assert c.actif() == "distant"                 # auto : premier fournisseur disponible
    c.repondre("s", "bonjour")
    c.choisir("local")
    c.repondre("s", "encore")
    assert a.amorces[-1] == [("user", "bonjour"), ("assistant", "1:bonjour")]
    with pytest.raises(ErreurCerveau):
        c.choisir("inconnu")


# ---------- serveur ----------
def test_api(ctx):
    ctx.cache["mqtt"] = FauxMqtt()
    client = TestClient(creer_app(ctx.config, Cerveau(ctx.config, ctx, {"echo": Echo()})))
    etat = client.get("/api/etat").json()
    assert etat["actif"] == "echo" and etat["appareils"][0]["nom"] == "lampe"
    assert client.post("/api/message", json={"texte": "salut"}).json()["reponse"] == "1:salut"
    assert client.post("/api/message", json={"texte": ""}).status_code == 422
    assert client.post("/api/appareils/lampe/eteindre").json()["fait"]
    assert client.post("/api/appareils/lampe/exploser").status_code == 400
    assert client.post("/api/fournisseur", json={"nom": "x"}).status_code == 400
    assert client.post("/api/ecoute", content=b"x").status_code == 503      # Vosk absent ici
    assert "<title>" in client.get("/").text


def test_api_jeton(ctx):
    ctx.config.jeton = "secret"
    client = TestClient(creer_app(ctx.config, Cerveau(ctx.config, ctx, {"echo": Echo()})))
    assert client.get("/api/etat").status_code == 401
    assert client.get("/api/etat", headers={"x-jeton": "secret"}).status_code == 200


# ---------- voix ----------
def test_mot_activation():
    assert voix.extraire_commande("sentinelle quelle heure est-il", "sentinelle") == "quelle heure est-il"
    assert voix.extraire_commande("dis sentinel allume la lampe", "Sentinelle") == "allume la lampe"
    assert voix.extraire_commande("sentinelle", "sentinelle") == ""
    assert voix.extraire_commande("il fait beau", "sentinelle") is None


def test_boucle_vocale():
    dit, demandes, t = [], [], [0.0]
    phrases = ["bruit de fond", "sentinelle", "allume la lampe", "encore du bruit", "sentinelle quelle heure"]

    def flux():
        for p in phrases:
            t[0] += 2
            yield p

    voix.boucle(Config(), flux(), dit.append, lambda q: demandes.append(q) or f"ok {q}", horloge=lambda: t[0])
    assert demandes == ["allume la lampe", "quelle heure"]
    assert dit == ["Oui ?", "ok allume la lampe", "ok quelle heure"]


# ---------- configuration ----------
def test_config_exemple():
    cfg = charger("config.exemple.toml")
    assert cfg.claude_modele == "claude-opus-5-5" and cfg.ollama_url.startswith("http")
    assert cfg.appareils and cfg.imprimantes


def test_config_cle_inconnue(tmp_path):
    f = tmp_path / "c.toml"
    f.write_text('[general]\nnon_prevu = 1\n')
    with pytest.raises(ValueError):
        charger(f)
