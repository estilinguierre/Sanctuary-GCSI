"""Cerveau : choisit le fournisseur (local ou Claude) et tient les conversations.

Chaque fournisseur garde son historique dans son format natif (Claude doit
revoir ses propres blocs inchangés pendant une boucle d'outils). Un journal
neutre, texte seul, sert de passerelle quand on bascule de l'un à l'autre.
"""
from __future__ import annotations

import threading
import time
from dataclasses import dataclass, field

from ..config import Config
from ..outils import REGISTRE, Contexte, executer

MAX_TOURS_OUTILS = 8
JOURNAL_MAX = 40          # au-delà, on repart des 20 derniers échanges


class ErreurCerveau(Exception):
    """Erreur présentable telle quelle à l'utilisateur."""


def consigne(cfg: Config) -> str:
    adresse = (f"Tu t'adresses à l'utilisateur en l'appelant « {cfg.appellation} »."
               if cfg.appellation else "Tu vouvoies l'utilisateur.")
    return f"""Tu es {cfg.nom}, l'assistant personnel d'un atelier de fabrication : conception mécanique, \
impression 3D, électronique embarquée, métallerie et travail du bois. Tu tournes sur la machine de \
l'utilisateur, au cœur de son installation, et tu agis sur elle par tes outils. {adresse}

Tes réponses sont le plus souvent lues à voix haute par une synthèse vocale. Écris donc comme on parle : \
des phrases courtes et complètes, sans listes à puces, sans tableaux ni symboles de mise en forme, avec les \
unités dites en toutes lettres. Va droit au résultat ; une ou deux phrases suffisent pour la plupart des \
demandes. Le ton est calme, précis, avec une pointe d'humour sec quand la situation s'y prête.

Tout ce qui dépend du monde réel (heure, état de la machine, météo, appareils, imprimantes, notes, \
nomenclatures) se lit avec un outil : tu n'as aucune autre source pour ces informations, et une valeur \
devinée induirait l'utilisateur en erreur. Pour un calcul de résistance des matériaux, utilise les outils \
de calcul plutôt que le calcul mental, donne le chiffre clé et le coefficient de sécurité, et rappelle en \
une phrase qu'il s'agit d'un pré-dimensionnement quand la sécurité des personnes est en jeu.

Tu ne peux commander que les appareils déclarés dans la configuration. Si un outil renvoie une erreur, dis \
simplement ce qui n'a pas fonctionné et ce qu'il faudrait vérifier. Si une demande dépasse tes outils, \
dis-le plutôt que de faire semblant."""


@dataclass
class Session:
    journal: list[tuple[str, str]] = field(default_factory=list)   # (role, texte)
    natif: dict[str, list] = field(default_factory=dict)
    synchro: dict[str, int] = field(default_factory=dict)
    verrou: threading.Lock = field(default_factory=threading.Lock)


class Cerveau:
    def __init__(self, cfg: Config, ctx: Contexte, fournisseurs: dict | None = None):
        self.cfg, self.ctx = cfg, ctx
        if fournisseurs is None:
            from .claude import Claude
            from .ollama import Ollama
            fournisseurs = {"ollama": Ollama(cfg), "claude": Claude(cfg)}
        self.fournisseurs = fournisseurs
        self.choix = cfg.fournisseur
        self.sessions: dict[str, Session] = {}
        self._dispo: tuple[float, dict] | None = None

    def disponibilites(self, fraicheur_s: float = 20) -> dict[str, dict]:
        """État des fournisseurs, mémorisé quelques secondes pour ne pas sonder Ollama à chaque message."""
        if self._dispo and time.monotonic() - self._dispo[0] < fraicheur_s:
            return self._dispo[1]
        etat = {}
        for nom, f in self.fournisseurs.items():
            ok, detail = f.disponible()
            etat[nom] = {"disponible": ok, "detail": detail, "modele": f.modele}
        self._dispo = (time.monotonic(), etat)
        return etat

    def actif(self) -> str:
        """Fournisseur réellement utilisé : le choix explicite, sinon le local d'abord."""
        if self.choix != "auto":
            return self.choix
        for nom, etat in self.disponibilites().items():
            if etat["disponible"]:
                return nom
        return next(iter(self.fournisseurs))

    def choisir(self, nom: str):
        if nom != "auto" and nom not in self.fournisseurs:
            raise ErreurCerveau(f"Fournisseur inconnu : {nom}")
        self.choix = nom

    def repondre(self, session_id: str, texte: str) -> dict:
        s = self.sessions.setdefault(session_id, Session())
        nom = self.actif()
        f = self.fournisseurs[nom]
        appels: list[dict] = []

        def lancer_outil(nom_outil: str, args: dict) -> tuple[str, bool]:
            resultat, erreur = executer(self.ctx, nom_outil, args)
            appels.append({"nom": nom_outil, "args": args, "resultat": resultat, "erreur": erreur})
            return resultat, erreur

        with s.verrou:
            if len(s.journal) > JOURNAL_MAX:
                s.journal = s.journal[-20:]
                s.synchro.clear()
            if s.synchro.get(nom) != len(s.journal):
                s.natif[nom] = f.amorcer(s.journal)
            try:
                reponse = f.tour(s.natif[nom], texte, lancer_outil, consigne(self.cfg), list(REGISTRE.values()))
            except Exception:
                s.synchro.pop(nom, None)      # l'historique natif est peut-être incomplet : on le reconstruira
                raise
            s.journal += [("user", texte), ("assistant", reponse)]
            s.synchro[nom] = len(s.journal)
        return {"reponse": reponse, "fournisseur": nom, "modele": f.modele, "outils": appels}
