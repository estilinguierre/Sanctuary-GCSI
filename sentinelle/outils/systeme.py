"""Outils système : heure, état machine, météo, minuteurs, notes."""
from __future__ import annotations

import datetime as dt
import time

import httpx
import psutil

from . import Contexte, outil

_JOURS = ["lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi", "dimanche"]
_MOIS = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août",
         "septembre", "octobre", "novembre", "décembre"]
_CIEL = {0: "ciel dégagé", 1: "peu nuageux", 2: "partiellement nuageux", 3: "couvert", 45: "brouillard",
         48: "brouillard givrant", 51: "bruine légère", 53: "bruine", 55: "bruine dense", 61: "pluie faible",
         63: "pluie", 65: "pluie forte", 71: "neige faible", 73: "neige", 75: "neige forte", 80: "averses",
         81: "averses", 82: "fortes averses", 95: "orage", 96: "orage avec grêle", 99: "orage avec grêle"}


@outil("heure", "Date et heure locales actuelles de la machine.")
def heure(ctx: Contexte):
    n = dt.datetime.now()
    return {"date": f"{_JOURS[n.weekday()]} {n.day} {_MOIS[n.month - 1]} {n.year}", "heure": n.strftime("%H:%M")}


def etat_machine_brut() -> dict:
    mem, disque = psutil.virtual_memory(), psutil.disk_usage("/")
    temp = None
    try:
        capteurs = psutil.sensors_temperatures()
        for nom in ("cpu_thermal", "coretemp", "k10temp"):
            if capteurs.get(nom):
                temp = round(capteurs[nom][0].current, 1)
                break
    except (AttributeError, OSError):
        pass
    return {"cpu_pct": psutil.cpu_percent(interval=0.1), "ram_pct": mem.percent,
            "ram_libre_go": round(mem.available / 2**30, 1), "disque_pct": disque.percent,
            "disque_libre_go": round(disque.free / 2**30, 1), "temperature_c": temp,
            "allume_depuis_h": round((time.time() - psutil.boot_time()) / 3600, 1)}


@outil("etat_machine", "Charge processeur, mémoire, disque et température de la machine qui héberge l'assistant.")
def etat_machine(ctx: Contexte):
    return etat_machine_brut()


@outil("meteo", "Météo actuelle et prévision du jour et du lendemain (service Open-Meteo, nécessite Internet).",
       {"ville": {"type": "string", "description": "Ville ; par défaut celle de la configuration."}})
def meteo(ctx: Contexte, ville: str = ""):
    ville = ville or ctx.config.ville
    with httpx.Client(timeout=8) as c:
        geo = c.get("https://geocoding-api.open-meteo.com/v1/search",
                    params={"name": ville, "count": 1, "language": "fr"}).json().get("results")
        if not geo:
            return {"erreur": f"ville introuvable : {ville}"}
        g = geo[0]
        r = c.get("https://api.open-meteo.com/v1/forecast", params={
            "latitude": g["latitude"], "longitude": g["longitude"], "timezone": "auto", "forecast_days": 2,
            "current": "temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code",
            "daily": "temperature_2m_max,temperature_2m_min,precipitation_probability_max,weather_code"}).json()
    a, j = r["current"], r["daily"]
    jours = [{"jour": nom, "min_c": j["temperature_2m_min"][i], "max_c": j["temperature_2m_max"][i],
              "risque_pluie_pct": j["precipitation_probability_max"][i],
              "ciel": _CIEL.get(j["weather_code"][i], "variable")} for i, nom in enumerate(["aujourd'hui", "demain"])]
    return {"ville": g["name"], "temperature_c": a["temperature_2m"], "humidite_pct": a["relative_humidity_2m"],
            "vent_kmh": a["wind_speed_10m"], "ciel": _CIEL.get(a["weather_code"], "variable"), "previsions": jours}


@outil("minuteur_creer", "Lance un minuteur ; l'utilisateur est prévenu à l'échéance.",
       {"duree_s": {"type": "number", "description": "Durée en secondes."},
        "libelle": {"type": "string", "description": "À quoi sert le minuteur."}}, ["duree_s", "libelle"])
def minuteur_creer(ctx: Contexte, duree_s: float, libelle: str):
    if not 0 < duree_s <= 7 * 86400:
        return {"erreur": "durée hors limites (1 s à 7 jours)"}
    m = ctx.minuteurs.creer(duree_s, libelle)
    return {"id": m["id"], "libelle": libelle, "duree_s": duree_s}


@outil("minuteur_lister", "Liste les minuteurs en cours avec le temps restant.")
def minuteur_lister(ctx: Contexte):
    return ctx.minuteurs.lister()


@outil("minuteur_annuler", "Annule un minuteur en cours.", {"id": {"type": "integer"}}, ["id"])
def minuteur_annuler(ctx: Contexte, id: int):
    return {"annule": ctx.minuteurs.annuler(id)}


@outil("note_ajouter", "Enregistre une note durable (idée, mesure, rappel).",
       {"texte": {"type": "string"}}, ["texte"])
def note_ajouter(ctx: Contexte, texte: str):
    ctx.stockage.executer("INSERT INTO notes(ts, texte) VALUES(?, ?)", (time.time(), texte))
    return {"enregistre": True}


@outil("note_lister", "Relit les notes enregistrées, de la plus récente à la plus ancienne.",
       {"recherche": {"type": "string", "description": "Mot-clé facultatif."}})
def note_lister(ctx: Contexte, recherche: str = ""):
    lignes = ctx.stockage.executer(
        "SELECT id, ts, texte FROM notes WHERE texte LIKE ? ORDER BY ts DESC LIMIT 20", (f"%{recherche}%",))
    return [{"id": l["id"], "date": dt.datetime.fromtimestamp(l["ts"]).strftime("%d/%m/%Y %H:%M"),
             "texte": l["texte"]} for l in lignes]


@outil("note_supprimer", "Supprime une note par son identifiant.", {"id": {"type": "integer"}}, ["id"])
def note_supprimer(ctx: Contexte, id: int):
    ctx.stockage.executer("DELETE FROM notes WHERE id = ?", (id,))
    return {"supprime": True}
