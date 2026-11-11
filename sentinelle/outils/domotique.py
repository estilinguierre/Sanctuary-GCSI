"""Domotique et électronique : MQTT, Home Assistant, GPIO du Raspberry Pi.

Sécurité : le modèle ne peut agir que sur les appareils déclarés dans
config.toml, et uniquement avec les actions qui y sont listées. Il n'a
aucun moyen de publier sur un topic arbitraire.
"""
from __future__ import annotations

import os
import threading

import httpx

from . import Contexte, outil


class LienMqtt:
    """Connexion MQTT paresseuse qui mémorise le dernier état des topics suivis."""

    def __init__(self, cfg: dict, topics_etat: list[str]):
        import paho.mqtt.client as mqtt
        self.etats: dict[str, str] = {}
        self._pret = threading.Event()
        self._topics = topics_etat
        self.client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="sentinelle")
        if cfg.get("utilisateur"):
            self.client.username_pw_set(cfg["utilisateur"], os.environ.get("MQTT_MOT_DE_PASSE", ""))
        self.client.on_connect = self._connecte
        self.client.on_message = lambda c, u, m: self.etats.__setitem__(m.topic, m.payload.decode(errors="replace"))
        self.client.connect(cfg.get("hote", "127.0.0.1"), int(cfg.get("port", 1883)), keepalive=30)
        self.client.loop_start()
        if not self._pret.wait(5):
            raise ConnectionError("broker MQTT injoignable")

    def _connecte(self, client, userdata, flags, code, proprietes):
        for t in self._topics:
            client.subscribe(t)
        self._pret.set()

    def publier(self, topic: str, charge: str):
        self.client.publish(topic, charge, qos=1).wait_for_publish(5)


def _mqtt(ctx: Contexte) -> LienMqtt:
    if "mqtt" not in ctx.cache:
        topics = [a["topic_etat"] for a in ctx.config.appareils.values() if a.get("topic_etat")]
        ctx.cache["mqtt"] = LienMqtt(ctx.config.mqtt, topics)
    return ctx.cache["mqtt"]


def _gpio(ctx: Contexte, broche: int):
    from gpiozero import OutputDevice  # présent sur Raspberry Pi OS
    sorties = ctx.cache.setdefault("gpio", {})
    if broche not in sorties:
        sorties[broche] = OutputDevice(broche)
    return sorties[broche]


def _ha(ctx: Contexte) -> httpx.Client:
    return httpx.Client(base_url=ctx.config.homeassistant_url, timeout=8,
                        headers={"Authorization": f"Bearer {os.environ.get('HA_JETON', '')}"})


def actions_de(appareil: dict) -> list[str]:
    if appareil.get("type") == "gpio":
        return ["allumer", "eteindre"]
    return list(appareil.get("actions", {}))


def commander(ctx: Contexte, nom: str, action: str) -> dict:
    a = ctx.config.appareils.get(nom)
    if a is None:
        return {"erreur": f"appareil inconnu : {nom}", "appareils": list(ctx.config.appareils)}
    if action not in actions_de(a):
        return {"erreur": f"action non autorisée pour {nom}", "actions": actions_de(a)}
    genre = a.get("type")
    if genre == "mqtt":
        _mqtt(ctx).publier(a["topic"], str(a["actions"][action]))
    elif genre == "homeassistant":
        with _ha(ctx) as c:
            c.post(f"/api/services/{a['actions'][action]}", json={"entity_id": a["entite"]}).raise_for_status()
    elif genre == "gpio":
        sortie = _gpio(ctx, int(a["broche"]))
        sortie.on() if action == "allumer" else sortie.off()
    else:
        return {"erreur": f"type d'appareil inconnu : {genre}"}
    return {"appareil": nom, "action": action, "fait": True}


def lire_etat(ctx: Contexte, nom: str) -> dict:
    a = ctx.config.appareils.get(nom)
    if a is None:
        return {"erreur": f"appareil inconnu : {nom}", "appareils": list(ctx.config.appareils)}
    genre = a.get("type")
    if genre == "mqtt":
        if not a.get("topic_etat"):
            return {"appareil": nom, "etat": "inconnu (pas de topic_etat configuré)"}
        return {"appareil": nom, "etat": _mqtt(ctx).etats.get(a["topic_etat"], "aucun message reçu")}
    if genre == "homeassistant":
        with _ha(ctx) as c:
            r = c.get(f"/api/states/{a['entite']}")
            r.raise_for_status()
            return {"appareil": nom, "etat": r.json().get("state")}
    if genre == "gpio":
        return {"appareil": nom, "etat": "allumé" if _gpio(ctx, int(a["broche"])).value else "éteint"}
    return {"erreur": f"type d'appareil inconnu : {genre}"}


@outil("appareils_lister", "Liste les appareils pilotables (lampes, prises, relais…) et leurs actions autorisées.")
def appareils_lister(ctx: Contexte):
    return [{"nom": n, "description": a.get("description", ""), "actions": actions_de(a)}
            for n, a in ctx.config.appareils.items()]


@outil("appareil_commander", "Exécute une action sur un appareil déclaré. Appeler appareils_lister en cas de doute sur le nom.",
       {"nom": {"type": "string"}, "action": {"type": "string"}}, ["nom", "action"])
def appareil_commander(ctx: Contexte, nom: str, action: str):
    return commander(ctx, nom, action)


@outil("appareil_etat", "Lit l'état courant d'un appareil déclaré.", {"nom": {"type": "string"}}, ["nom"])
def appareil_etat(ctx: Contexte, nom: str):
    return lire_etat(ctx, nom)
