"""Serveur local : API HTTP + interface web (HUD).

Lancement :  python -m sentinelle.serveur
"""
from __future__ import annotations

import hmac
import io
import ipaddress
import json
import wave
from pathlib import Path

from fastapi import Depends, FastAPI, HTTPException, Request
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from .cerveau import Cerveau, ErreurCerveau
from .config import Config, charger
from .outils import Contexte, Stockage
from .outils.domotique import actions_de, commander
from .outils.systeme import etat_machine_brut

WEB = Path(__file__).parent / "web"


class Message(BaseModel):
    texte: str = Field(min_length=1, max_length=4000)
    session: str = Field(default="defaut", max_length=64)


class Choix(BaseModel):
    nom: str


def creer_app(cfg: Config | None = None, cerveau: Cerveau | None = None) -> FastAPI:
    cfg = cfg or charger()
    ctx = cerveau.ctx if cerveau else Contexte(cfg, Stockage(cfg.donnees / "sentinelle.db"))
    cerveau = cerveau or Cerveau(cfg, ctx)
    oreille: dict = {}

    def garde(request: Request):
        attendu = cfg.jeton_effectif
        if attendu and not hmac.compare_digest(request.headers.get("x-jeton", ""), attendu):
            raise HTTPException(401, "Jeton manquant ou invalide.")

    app = FastAPI(title=cfg.nom, docs_url=None, redoc_url=None)
    api = Depends(garde)

    @app.get("/api/etat", dependencies=[api])
    def etat():
        return {"nom": cfg.nom, "choix": cerveau.choix, "actif": cerveau.actif(),
                "fournisseurs": cerveau.disponibilites(), "machine": etat_machine_brut(),
                "appareils": [{"nom": n, "description": a.get("description", ""), "actions": actions_de(a)}
                              for n, a in cfg.appareils.items()],
                "minuteurs": ctx.minuteurs.lister(), "ecoute_locale": _vosk_pret(cfg)}

    @app.post("/api/message", dependencies=[api])
    def message(m: Message):
        try:
            return cerveau.repondre(m.session, m.texte.strip())
        except ErreurCerveau as e:
            raise HTTPException(502, str(e))

    @app.post("/api/fournisseur", dependencies=[api])
    def fournisseur(c: Choix):
        try:
            cerveau.choisir(c.nom)
        except ErreurCerveau as e:
            raise HTTPException(400, str(e))
        return {"choix": cerveau.choix, "actif": cerveau.actif()}

    @app.post("/api/appareils/{nom}/{action}", dependencies=[api])
    def appareil(nom: str, action: str):
        try:
            r = commander(ctx, nom, action)
        except Exception as e:
            raise HTTPException(502, f"Commande impossible : {type(e).__name__} : {e}")
        if "erreur" in r:
            raise HTTPException(400, r["erreur"])
        return r

    @app.get("/api/evenements", dependencies=[api])
    def evenements(depuis: int = 0):
        return ctx.minuteurs.evenements(depuis)

    @app.post("/api/ecoute", dependencies=[api])
    async def ecoute(request: Request):
        """Transcription locale (Vosk) d'un enregistrement WAV mono 16 bits envoyé par le HUD."""
        if not _vosk_pret(cfg):
            raise HTTPException(503, "Reconnaissance vocale locale non installée (voir README).")
        import vosk
        if "modele" not in oreille:
            vosk.SetLogLevel(-1)
            oreille["modele"] = vosk.Model(str(cfg.vosk_modele))
        try:
            with wave.open(io.BytesIO(await request.body())) as w:
                if w.getnchannels() != 1 or w.getsampwidth() != 2:
                    raise HTTPException(400, "WAV mono 16 bits attendu.")
                rec = vosk.KaldiRecognizer(oreille["modele"], w.getframerate())
                rec.AcceptWaveform(w.readframes(w.getnframes()))
        except (wave.Error, EOFError):
            raise HTTPException(400, "Fichier WAV illisible.")
        return {"texte": json.loads(rec.FinalResult()).get("text", "")}

    app.mount("/", StaticFiles(directory=WEB, html=True), name="web")
    return app


def _vosk_pret(cfg: Config) -> bool:
    try:
        import vosk  # noqa: F401
    except ImportError:
        return False
    return cfg.vosk_modele.is_dir()


def main():
    import uvicorn
    cfg = charger()
    local = cfg.hote == "localhost" or _est_boucle(cfg.hote)
    if not local and not cfg.jeton_effectif:
        raise SystemExit("Refus de démarrer : écouter sur le réseau exige un jeton "
                         "(variable SENTINELLE_JETON ou [serveur] jeton). L'assistant pilote des appareils réels.")
    print(f"{cfg.nom} — interface : http://{cfg.hote}:{cfg.port}/")
    uvicorn.run(creer_app(cfg), host=cfg.hote, port=cfg.port, log_level="warning")


def _est_boucle(hote: str) -> bool:
    try:
        return ipaddress.ip_address(hote).is_loopback
    except ValueError:
        return False


if __name__ == "__main__":
    main()
