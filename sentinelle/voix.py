"""Mode embarqué (Raspberry Pi, cyberdeck) : mot d'activation, écoute et parole hors ligne.

Client vocal du serveur local :  python -m sentinelle.voix
Écoute : Vosk (micro via sounddevice).  Parole : Piper si une voix est installée, sinon espeak-ng.
"""
from __future__ import annotations

import difflib
import json
import queue
import shutil
import subprocess
import threading
import time
import unicodedata

import httpx

from .config import Config, charger

FENETRE_ECOUTE_S = 8     # après le mot d'activation seul, délai pour formuler la demande


def _normaliser(texte: str) -> str:
    sans = unicodedata.normalize("NFD", texte.lower())
    return "".join(c for c in sans if unicodedata.category(c) != "Mn")


def extraire_commande(phrase: str, mot: str) -> str | None:
    """Renvoie ce qui suit le mot d'activation, "" s'il est seul, None s'il est absent.

    La comparaison tolère les approximations de la reconnaissance vocale.
    """
    mots = _normaliser(phrase).split()
    cible = _normaliser(mot)
    for i, m in enumerate(mots):
        if difflib.SequenceMatcher(None, m, cible).ratio() >= 0.8:
            return " ".join(mots[i + 1:])
    return None


class Bouche:
    def __init__(self, cfg: Config):
        self.parle = threading.Event()
        self._piper = shutil.which("piper") if cfg.piper_voix.exists() else None
        self._voix = cfg.piper_voix
        self._frequence = 22050
        meta = cfg.piper_voix.with_suffix(".onnx.json")
        if meta.exists():
            self._frequence = json.loads(meta.read_text()).get("audio", {}).get("sample_rate", 22050)
        if not self._piper and not shutil.which("espeak-ng"):
            raise SystemExit("Aucune synthèse vocale : installez Piper (voix dans modeles/) ou espeak-ng.")

    def dire(self, texte: str):
        if not texte:
            return
        self.parle.set()         # le micro est ignoré pendant la parole, sinon l'assistant s'entend
        try:
            if self._piper:
                synthese = subprocess.Popen([self._piper, "--model", str(self._voix), "--output-raw"],
                                            stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                            stderr=subprocess.DEVNULL)
                lecture = subprocess.Popen(["aplay", "-q", "-r", str(self._frequence), "-f", "S16_LE", "-t", "raw", "-"],
                                           stdin=synthese.stdout)
                synthese.stdin.write(texte.encode())
                synthese.stdin.close()
                lecture.wait()
            else:
                subprocess.run(["espeak-ng", "-v", "fr", "-s", "160", texte], check=False)
        finally:
            time.sleep(0.3)
            self.parle.clear()


class Oreille:
    def __init__(self, cfg: Config, bouche: Bouche):
        import sounddevice
        import vosk
        if not cfg.vosk_modele.is_dir():
            raise SystemExit(f"Modèle Vosk introuvable : {cfg.vosk_modele} (voir README).")
        vosk.SetLogLevel(-1)
        self._rec = vosk.KaldiRecognizer(vosk.Model(str(cfg.vosk_modele)), 16000)
        self._file: queue.Queue[bytes] = queue.Queue()
        self._bouche = bouche
        self._flux = sounddevice.RawInputStream(samplerate=16000, blocksize=4000, dtype="int16", channels=1,
                                                callback=lambda data, n, t, etat: self._file.put(bytes(data)))

    def phrases(self):
        """Génère les phrases reconnues, une par silence."""
        with self._flux:
            while True:
                bloc = self._file.get()
                if self._bouche.parle.is_set():
                    self._rec.Reset()
                    continue
                if self._rec.AcceptWaveform(bloc):
                    texte = json.loads(self._rec.Result()).get("text", "")
                    if texte:
                        yield texte


def boucle(cfg: Config, phrases, dire, demander, horloge=time.monotonic):
    """Cœur du mode vocal, indépendant du matériel (donc testable)."""
    eveil = -1e9
    for phrase in phrases:
        commande = extraire_commande(phrase, cfg.mot_activation)
        if commande is None and horloge() - eveil <= FENETRE_ECOUTE_S:
            commande = phrase
        if commande is None:
            continue
        if not commande:
            eveil = horloge()
            dire("Oui ?")
            continue
        eveil = -1e9
        dire(demander(commande))


def main():
    cfg = charger()
    base = f"http://{'127.0.0.1' if cfg.hote in ('0.0.0.0', '::') else cfg.hote}:{cfg.port}"
    client = httpx.Client(base_url=base, timeout=200, headers={"x-jeton": cfg.jeton_effectif})
    bouche = Bouche(cfg)

    def demander(texte: str) -> str:
        try:
            r = client.post("/api/message", json={"texte": texte, "session": "voix"})
            return r.json().get("reponse") or r.json().get("detail", "Je n'ai pas de réponse.")
        except (httpx.HTTPError, ValueError):
            return "Le serveur ne répond pas."

    def annonces():
        dernier = None           # au démarrage, on saute les annonces déjà passées
        while True:
            time.sleep(2)
            try:
                nouveaux = client.get("/api/evenements", params={"depuis": dernier or 0}).json()
                for e in nouveaux:
                    if dernier is not None:
                        bouche.dire(e["texte"])
                dernier = nouveaux[-1]["id"] if nouveaux else (dernier or 0)
            except (httpx.HTTPError, ValueError):
                pass

    threading.Thread(target=annonces, daemon=True).start()
    print(f"À l'écoute — dites « {cfg.mot_activation} ».")
    boucle(cfg, Oreille(cfg, bouche).phrases(), bouche.dire, demander)


if __name__ == "__main__":
    main()
