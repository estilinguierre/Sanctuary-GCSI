"""Chargement de la configuration (TOML). Les secrets restent dans l'environnement."""
from __future__ import annotations

import os
import tomllib
from dataclasses import dataclass, field
from pathlib import Path

RACINE = Path(__file__).resolve().parent.parent


@dataclass
class Config:
    nom: str = "Sentinelle"
    appellation: str = ""            # façon de s'adresser à l'utilisateur ("" = aucune)
    ville: str = "Paris"
    donnees: Path = RACINE / "donnees"
    # serveur
    hote: str = "127.0.0.1"
    port: int = 8765
    jeton: str = ""                  # obligatoire dès que l'on écoute ailleurs que sur la boucle locale
    # cerveau
    fournisseur: str = "auto"        # auto | ollama | claude
    ollama_url: str = "http://127.0.0.1:11434"
    ollama_modele: str = "llama3.1"
    claude_modele: str = "claude-opus-5-5"
    claude_effort: str = "low"       # réponses vocales : la latence prime
    # voix (mode embarqué)
    mot_activation: str = "sentinelle"
    vosk_modele: Path = RACINE / "modeles" / "vosk-model-small-fr-0.22"
    piper_voix: Path = RACINE / "modeles" / "fr_FR-siwis-medium.onnx"
    # domotique / atelier
    mqtt: dict = field(default_factory=dict)
    homeassistant_url: str = ""
    appareils: dict = field(default_factory=dict)
    imprimantes: dict = field(default_factory=dict)

    @property
    def jeton_effectif(self) -> str:
        return os.environ.get("SENTINELLE_JETON", self.jeton)


def charger(chemin: str | os.PathLike | None = None) -> Config:
    chemin = Path(chemin or os.environ.get("SENTINELLE_CONFIG", RACINE / "config.toml"))
    cfg = Config()
    if not chemin.exists():
        return cfg
    brut = tomllib.loads(chemin.read_text(encoding="utf-8"))
    plat: dict = {}
    for cle, val in brut.items():
        if cle in ("general", "serveur", "voix"):
            plat.update(val)
        elif cle == "cerveau":
            for k, v in val.items():
                if isinstance(v, dict):          # [cerveau.ollama] url=... -> ollama_url
                    plat.update({f"{k}_{kk}": vv for kk, vv in v.items()})
                else:
                    plat[k] = v
        elif cle == "homeassistant":
            plat["homeassistant_url"] = val.get("url", "")
        else:
            plat[cle] = val
    for k, v in plat.items():
        if not hasattr(cfg, k):
            raise ValueError(f"Clé de configuration inconnue : {k}")
        actuel = getattr(cfg, k)
        setattr(cfg, k, Path(v).expanduser() if isinstance(actuel, Path) else v)
    return cfg
