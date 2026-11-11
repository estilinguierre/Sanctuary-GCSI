"""Atelier maker : suivi d'imprimantes 3D et nomenclatures (BOM)."""
from __future__ import annotations

import csv
import json
import os
import ssl
import threading

import httpx

from . import Contexte, outil


def _secret(prefixe: str, nom: str) -> str:
    return os.environ.get(f"{prefixe}_{nom.upper()}", "")


def _octoprint(nom: str, p: dict) -> dict:
    with httpx.Client(base_url=p["url"], timeout=6, headers={"X-Api-Key": _secret("OCTOPRINT_CLE", nom)}) as c:
        tache = c.get("/api/job")
        tache.raise_for_status()
        t = tache.json()
        r = c.get("/api/printer")
        temp = r.json().get("temperature", {}) if r.status_code == 200 else {}
    avance, reste = t["progress"].get("completion"), t["progress"].get("printTimeLeft")
    return {"etat": t.get("state"), "fichier": t["job"]["file"].get("name"),
            "avancement_pct": round(avance, 1) if avance is not None else None,
            "reste_min": round(reste / 60) if reste else None,
            "buse_c": temp.get("tool0", {}).get("actual"), "plateau_c": temp.get("bed", {}).get("actual")}


def _moonraker(nom: str, p: dict) -> dict:
    with httpx.Client(base_url=p["url"], timeout=6) as c:
        r = c.get("/printer/objects/query", params="print_stats&display_status&extruder&heater_bed")
        r.raise_for_status()
        s = r.json()["result"]["status"]
    return {"etat": s["print_stats"].get("state"), "fichier": s["print_stats"].get("filename") or None,
            "avancement_pct": round(s["display_status"].get("progress", 0) * 100, 1),
            "duree_min": round(s["print_stats"].get("print_duration", 0) / 60),
            "buse_c": s["extruder"].get("temperature"), "plateau_c": s["heater_bed"].get("temperature")}


def _bambu(nom: str, p: dict) -> dict:
    """Lecture seule via le MQTT local de l'imprimante (mode LAN, port 8883, code d'accès)."""
    import paho.mqtt.client as mqtt
    recu: dict = {}
    pret = threading.Event()
    serie = p["serie"]

    def connecte(client, userdata, flags, code, proprietes):
        client.subscribe(f"device/{serie}/report")
        client.publish(f"device/{serie}/request",
                       json.dumps({"pushing": {"sequence_id": "0", "command": "pushall"}}))

    def message(client, userdata, msg):
        etat = json.loads(msg.payload).get("print", {})
        if "gcode_state" in etat:
            recu.update(etat)
            pret.set()

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.username_pw_set("bblp", _secret("BAMBU_CODE", nom))
    contexte = ssl.create_default_context()      # l'imprimante présente un certificat auto-signé
    contexte.check_hostname = False
    contexte.verify_mode = ssl.CERT_NONE
    client.tls_set_context(contexte)
    client.on_connect, client.on_message = connecte, message
    client.connect(p["hote"], 8883, keepalive=20)
    client.loop_start()
    try:
        if not pret.wait(8):
            return {"erreur": "aucune réponse de l'imprimante (mode LAN activé ? code d'accès ?)"}
    finally:
        client.loop_stop()
        client.disconnect()
    return {"etat": recu.get("gcode_state"), "fichier": recu.get("subtask_name"),
            "avancement_pct": recu.get("mc_percent"), "reste_min": recu.get("mc_remaining_time"),
            "couche": f"{recu.get('layer_num')}/{recu.get('total_layer_num')}",
            "buse_c": recu.get("nozzle_temper"), "plateau_c": recu.get("bed_temper")}


_PILOTES = {"octoprint": _octoprint, "moonraker": _moonraker, "bambu": _bambu}


@outil("imprimante_etat", "État d'une imprimante 3D déclarée : avancement, temps restant, températures. "
                          "Sans nom, interroge toutes les imprimantes.",
       {"nom": {"type": "string"}})
def imprimante_etat(ctx: Contexte, nom: str = ""):
    parc = ctx.config.imprimantes
    if not parc:
        return {"erreur": "aucune imprimante déclarée dans config.toml"}
    if nom and nom not in parc:
        return {"erreur": f"imprimante inconnue : {nom}", "imprimantes": list(parc)}
    resultat = {}
    for n in ([nom] if nom else parc):
        pilote = _PILOTES.get(parc[n].get("type"))
        try:
            resultat[n] = pilote(n, parc[n]) if pilote else {"erreur": f"type inconnu : {parc[n].get('type')}"}
        except Exception as e:
            resultat[n] = {"erreur": f"injoignable ({type(e).__name__})"}
    return resultat


@outil("nomenclature_ajouter", "Ajoute une ligne à la nomenclature (BOM) d'un projet.",
       {"projet": {"type": "string"}, "designation": {"type": "string"}, "quantite": {"type": "number"},
        "prix_unitaire": {"type": "number", "description": "En euros, 0 si inconnu."},
        "fournisseur": {"type": "string"}, "reference": {"type": "string"}},
       ["projet", "designation", "quantite"])
def nomenclature_ajouter(ctx: Contexte, projet: str, designation: str, quantite: float,
                         prix_unitaire: float = 0, fournisseur: str = "", reference: str = ""):
    ctx.stockage.executer(
        "INSERT INTO nomenclature(projet, designation, quantite, prix_unitaire, fournisseur, reference)"
        " VALUES(?,?,?,?,?,?)", (projet.strip().lower(), designation, quantite, prix_unitaire, fournisseur, reference))
    return {"ajoute": True, "projet": projet.strip().lower()}


def _lignes(ctx: Contexte, projet: str) -> list[dict]:
    return ctx.stockage.executer(
        "SELECT id, designation, quantite, prix_unitaire, fournisseur, reference FROM nomenclature"
        " WHERE projet = ? ORDER BY id", (projet.strip().lower(),))


@outil("nomenclature_lister", "Affiche la nomenclature d'un projet avec le coût total. Sans projet, liste les projets.",
       {"projet": {"type": "string"}})
def nomenclature_lister(ctx: Contexte, projet: str = ""):
    if not projet:
        return {"projets": [l["projet"] for l in ctx.stockage.executer(
            "SELECT DISTINCT projet FROM nomenclature ORDER BY projet")]}
    lignes = _lignes(ctx, projet)
    return {"projet": projet, "lignes": lignes,
            "total_eur": round(sum(l["quantite"] * l["prix_unitaire"] for l in lignes), 2)}


@outil("nomenclature_retirer", "Retire une ligne de nomenclature par son identifiant.", {"id": {"type": "integer"}}, ["id"])
def nomenclature_retirer(ctx: Contexte, id: int):
    ctx.stockage.executer("DELETE FROM nomenclature WHERE id = ?", (id,))
    return {"retire": True}


@outil("nomenclature_exporter", "Exporte la nomenclature d'un projet en fichier CSV (séparateur point-virgule).",
       {"projet": {"type": "string"}}, ["projet"])
def nomenclature_exporter(ctx: Contexte, projet: str):
    lignes = _lignes(ctx, projet)
    if not lignes:
        return {"erreur": f"aucune ligne pour le projet {projet}"}
    propre = "".join(c if c.isalnum() else "_" for c in projet.strip().lower())
    chemin = ctx.config.donnees / "exports" / f"nomenclature_{propre}.csv"
    chemin.parent.mkdir(parents=True, exist_ok=True)
    with chemin.open("w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f, delimiter=";")
        w.writerow(["Repère", "Désignation", "Quantité", "Prix unitaire (€)", "Total (€)", "Fournisseur", "Référence"])
        for i, l in enumerate(lignes, 1):
            w.writerow([i, l["designation"], l["quantite"], l["prix_unitaire"],
                        round(l["quantite"] * l["prix_unitaire"], 2), l["fournisseur"], l["reference"]])
    return {"fichier": str(chemin), "lignes": len(lignes)}
