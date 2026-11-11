"""Résistance des matériaux : pré-dimensionnement rapide.

Unités : mm, N, MPa. Valeurs matériaux indicatives ; ces calculs sont un
ordre de grandeur d'atelier, pas une note de calcul réglementaire.
"""
from __future__ import annotations

import math

from . import Contexte, outil

# E (MPa), limite élastique Re (MPa), masse volumique (kg/m3)
MATERIAUX = {
    "acier_s235": (210000, 235, 7850), "acier_s355": (210000, 355, 7850),
    "inox_304": (193000, 210, 7900), "alu_6060_t6": (69500, 150, 2700),
    "alu_6082_t6": (70000, 250, 2700), "bois_c24": (11000, 24, 420),
    "pla": (3000, 50, 1240), "petg": (2100, 48, 1270), "abs": (2200, 40, 1040),
}
# Section résistante As (mm2) du filetage ISO à pas gros
VIS_AS = {"M3": 5.03, "M4": 8.78, "M5": 14.2, "M6": 20.1, "M8": 36.6, "M10": 58.0,
          "M12": 84.3, "M16": 157.0, "M20": 245.0}
# classe -> (Rm, Re) en MPa
VIS_CLASSES = {"4.6": (400, 240), "8.8": (800, 640), "10.9": (1000, 900),
               "12.9": (1200, 1080), "A2-70": (700, 450)}

_FORMES = ["rectangle", "rond", "tube_rond", "tube_rectangulaire"]
_CAS = ["console_charge_en_bout", "console_charge_repartie", "appuis_charge_centree", "appuis_charge_repartie"]
_DIM = {"forme": {"type": "string", "enum": _FORMES},
        "largeur": {"type": "number", "description": "Largeur b en mm (rectangle, tube rectangulaire)."},
        "hauteur": {"type": "number", "description": "Hauteur h en mm, dans le sens de la charge."},
        "diametre": {"type": "number", "description": "Diamètre extérieur en mm (rond, tube rond)."},
        "epaisseur": {"type": "number", "description": "Épaisseur de paroi en mm (tubes)."}}


def section(forme: str, largeur=0.0, hauteur=0.0, diametre=0.0, epaisseur=0.0) -> dict:
    """Aire (mm2), moment quadratique I (mm4) et module de flexion W (mm3)."""
    if forme == "rectangle":
        _positifs(largeur=largeur, hauteur=hauteur)
        aire, inertie, v = largeur * hauteur, largeur * hauteur**3 / 12, hauteur / 2
    elif forme == "rond":
        _positifs(diametre=diametre)
        aire, inertie, v = math.pi * diametre**2 / 4, math.pi * diametre**4 / 64, diametre / 2
    elif forme == "tube_rond":
        _positifs(diametre=diametre, epaisseur=epaisseur)
        d = diametre - 2 * epaisseur
        _positifs(diametre_interieur=d)
        aire, inertie, v = math.pi * (diametre**2 - d**2) / 4, math.pi * (diametre**4 - d**4) / 64, diametre / 2
    elif forme == "tube_rectangulaire":
        _positifs(largeur=largeur, hauteur=hauteur, epaisseur=epaisseur)
        b, h = largeur - 2 * epaisseur, hauteur - 2 * epaisseur
        _positifs(largeur_interieure=b, hauteur_interieure=h)
        aire, inertie, v = largeur * hauteur - b * h, (largeur * hauteur**3 - b * h**3) / 12, hauteur / 2
    else:
        raise ValueError(f"forme inconnue : {forme}")
    return {"aire_mm2": aire, "inertie_mm4": inertie, "module_flexion_mm3": inertie / v}


def _positifs(**valeurs):
    for nom, v in valeurs.items():
        if v <= 0:
            raise ValueError(f"{nom} doit être strictement positif (reçu {v})")


def _materiau(nom: str):
    if nom not in MATERIAUX:
        raise ValueError(f"matériau inconnu : {nom} ; disponibles : {sorted(MATERIAUX)}")
    return MATERIAUX[nom]


def flexion(cas: str, portee: float, charge: float, e: float, inertie: float, module: float) -> tuple[float, float]:
    """Renvoie (moment max N.mm, flèche max mm). charge = N (ponctuelle) ou N/mm (répartie)."""
    if cas == "console_charge_en_bout":
        return charge * portee, charge * portee**3 / (3 * e * inertie)
    if cas == "console_charge_repartie":
        return charge * portee**2 / 2, charge * portee**4 / (8 * e * inertie)
    if cas == "appuis_charge_centree":
        return charge * portee / 4, charge * portee**3 / (48 * e * inertie)
    if cas == "appuis_charge_repartie":
        return charge * portee**2 / 8, 5 * charge * portee**4 / (384 * e * inertie)
    raise ValueError(f"cas inconnu : {cas}")


@outil("rdm_section", "Caractéristiques d'une section (aire, inertie, module de flexion, masse au mètre).",
       {**_DIM, "materiau": {"type": "string", "enum": sorted(MATERIAUX)}}, ["forme"])
def rdm_section(ctx: Contexte, forme: str, materiau: str = "acier_s235", **dim):
    s = section(forme, **dim)
    rho = _materiau(materiau)[2]
    return {"aire_mm2": round(s["aire_mm2"], 1), "inertie_cm4": round(s["inertie_mm4"] / 1e4, 3),
            "module_flexion_cm3": round(s["module_flexion_mm3"] / 1e3, 3),
            "masse_kg_par_m": round(s["aire_mm2"] * 1e-6 * rho, 3), "materiau": materiau}


@outil("rdm_flexion",
       "Vérifie une poutre en flexion : contrainte, coefficient de sécurité sur la limite élastique et flèche. "
       "Ordre de grandeur d'atelier, pas une note de calcul réglementaire.",
       {"cas": {"type": "string", "enum": _CAS},
        "portee_mm": {"type": "number"},
        "charge_n": {"type": "number", "description": "Charge totale en newtons (1 kg ≈ 9,81 N). "
                                                      "Pour un cas réparti, c'est le total réparti sur la portée."},
        "materiau": {"type": "string", "enum": sorted(MATERIAUX)}, **_DIM},
       ["cas", "portee_mm", "charge_n", "materiau", "forme"])
def rdm_flexion(ctx: Contexte, cas: str, portee_mm: float, charge_n: float, materiau: str, forme: str, **dim):
    _positifs(portee_mm=portee_mm, charge_n=charge_n)
    e, re, _ = _materiau(materiau)
    s = section(forme, **dim)
    charge = charge_n / portee_mm if cas.endswith("repartie") else charge_n
    moment, fleche = flexion(cas, portee_mm, charge, e, s["inertie_mm4"], s["module_flexion_mm3"])
    contrainte = moment / s["module_flexion_mm3"]
    return {"moment_max_n_m": round(moment / 1000, 2), "contrainte_mpa": round(contrainte, 1),
            "limite_elastique_mpa": re, "coefficient_securite": round(re / contrainte, 2),
            "fleche_mm": round(fleche, 2), "rapport_portee_sur_fleche": round(portee_mm / fleche),
            "tient": contrainte < re,
            "remarque": "Valeurs matériau indicatives ; pièces imprimées : résistance très dépendante de "
                        "l'orientation des couches." if materiau in ("pla", "petg", "abs") else
                        "Valeurs matériau indicatives, hors flambement et déversement."}


@outil("rdm_vis", "Efforts admissibles d'une vis ISO à pas gros selon sa classe de qualité.",
       {"diametre": {"type": "string", "enum": list(VIS_AS)},
        "classe": {"type": "string", "enum": list(VIS_CLASSES)}}, ["diametre", "classe"])
def rdm_vis(ctx: Contexte, diametre: str, classe: str):
    surface, (rm, re) = VIS_AS[diametre], VIS_CLASSES[classe]
    return {"section_resistante_mm2": surface, "traction_limite_elastique_n": round(surface * re),
            "traction_rupture_n": round(surface * rm), "cisaillement_rupture_n": round(0.6 * surface * rm),
            "remarque": "Charges ultimes sans coefficient de sécurité ; diviser par 2 à 4 selon l'usage."}


@outil("masse_piece", "Masse d'une pièce à partir de son volume.",
       {"volume_cm3": {"type": "number"}, "materiau": {"type": "string", "enum": sorted(MATERIAUX)}},
       ["volume_cm3", "materiau"])
def masse_piece(ctx: Contexte, volume_cm3: float, materiau: str):
    return {"masse_g": round(volume_cm3 * _materiau(materiau)[2] / 1000, 1), "materiau": materiau}


@outil("cout_impression", "Coût de revient d'une impression 3D (matière + électricité).",
       {"masse_g": {"type": "number"}, "prix_bobine_eur_kg": {"type": "number"}, "duree_h": {"type": "number"},
        "puissance_w": {"type": "number", "description": "Puissance moyenne de l'imprimante, 120 W par défaut."},
        "prix_kwh_eur": {"type": "number", "description": "0,25 € par défaut."}},
       ["masse_g", "prix_bobine_eur_kg", "duree_h"])
def cout_impression(ctx: Contexte, masse_g: float, prix_bobine_eur_kg: float, duree_h: float,
                    puissance_w: float = 120, prix_kwh_eur: float = 0.25):
    matiere = masse_g / 1000 * prix_bobine_eur_kg
    energie = puissance_w / 1000 * duree_h * prix_kwh_eur
    return {"matiere_eur": round(matiere, 2), "electricite_eur": round(energie, 2), "total_eur": round(matiere + energie, 2)}
