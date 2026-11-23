"""Studio de contenu : accroches, scripts, légendes, carrousels, miniatures,
découpe, montage, voix off, plans de coupe, contrôle de marque."""
from __future__ import annotations

import html
import re
import shutil
import subprocess
from pathlib import Path

from . import base, cerveau, connecteurs

# ---------- Contrôle de marque ----------------------------------------------


def _regles_voix():
    s = base.sections(base.savoir("voix/mots-interdits.md"))
    bloquants = base.puces(s.get("Bloquants", ""))
    corrections = [tuple(x.strip() for x in p.split("->", 1)) for p in base.puces(s.get("Corrections", "")) if "->" in p]
    ton = base.reglages("voix/regles-de-ton.md")
    return bloquants, corrections, ton


def prix_connus() -> dict[str, dict]:
    """{nom: {prix, detail}} lu dans offre/tarifs.md."""
    res = {}
    for p in base.puces(base.savoir("offre/tarifs.md")):
        m = re.match(r"(.+?) : (\d[\d  ]*) ?€(?: \((.*)\))?", p)
        if m:
            res[m.group(1).strip()] = {"prix": base.entier(m.group(2)), "detail": m.group(3) or ""}
    return res


def controle(texte: str, genre: str = "legende") -> dict:
    """Verdict : « conforme », « corrige » (retouché tout seul) ou « signale » (bloqué)."""
    bloquants, corrections, ton = _regles_voix()
    motifs, retouches = [], []
    for avant, apres in corrections:
        motif = re.compile(rf"(?<!\w){re.escape(avant)}(?!\w)", re.I)
        if motif.search(texte):
            texte = motif.sub(apres, texte)
            retouches.append(f"« {avant} » → « {apres} »")
    max_excl = base.entier(ton.get("points d'exclamation maximum par texte", ""), 1)
    if texte.count("!") > max_excl:
        garde = [0]

        def _un(m):
            garde[0] += 1
            return "!" if garde[0] <= max_excl else "."
        texte = re.sub(r"!+", _un, texte)
        retouches.append("points d'exclamation en trop retirés")
    for b in bloquants:
        if base.contient(texte, b):
            motifs.append(f"terme interdit « {b} » : aucune promesse ni garantie")
    connus = {v["prix"] for v in prix_connus().values()}
    for m in re.finditer(r"(\d[\d  ]*) ?€", texte):
        if base.entier(m.group(1)) not in connus:
            motifs.append(f"prix « {m.group(0)} » absent de offre/tarifs.md")
    limite = base.entier(ton.get(f"longueur maximale d'une {genre}", ""), 0)
    if limite and len(texte) > limite:
        motifs.append(f"{genre} trop longue ({len(texte)} > {limite} caractères)")
    verdict = "signale" if motifs else ("corrige" if retouches else "conforme")
    return {"verdict": verdict, "texte": texte, "motifs": motifs, "retouches": retouches}


# ---------- Rédacteurs ---------------------------------------------------------


def formes() -> dict[str, dict]:
    res = {}
    for nom, corps in base.sections(base.savoir("accroches/formes-gagnantes.md")).items():
        ind = next((l.split(":", 1)[1] for l in corps.splitlines() if l.startswith("indices:")), "")
        res[nom] = {"indices": [i.strip() for i in ind.split(",") if i.strip()], "gabarits": base.puces(corps)}
    return res


def forme_de(accroche: str) -> str:
    meilleur, score = "Autre", 0
    for nom, f in formes().items():
        s = sum(1 for i in f["indices"] if base.contient(accroche, i))
        if s > score:
            meilleur, score = nom, s
    return meilleur


def _maj(s: str) -> str:
    return s[:1].upper() + s[1:]


def variantes_accroche(sujet: str, n: int = 3, formes_voulues: list[str] | None = None) -> list[dict]:
    """Propose n accroches de formes différentes pour un sujet."""
    res = []
    f = formes()
    for nom in (formes_voulues or list(f)):
        for g in f.get(nom, {}).get("gabarits", [])[:1]:
            brut = g.replace("{sujet}", sujet)
            brut = brut if g.startswith("{sujet}") is False else _maj(brut)
            texte = cerveau.ecrire("Rédacteur d'accroches",
                                   f"Écris une accroche vidéo de 3 secondes, forme « {nom} », sur : {sujet}.", brut)
            c = controle(texte.strip().strip('"«» '), "accroche")
            if c["verdict"] != "signale":
                res.append({"forme": nom, "texte": c["texte"]})
    return res[:n]


def ecrire_accroche(idee: dict) -> str:
    if idee.get("accroche"):
        return idee["accroche"]
    v = variantes_accroche(idee.get("sujet") or idee["titre"], 1)
    return v[0]["texte"] if v else idee["titre"]


def appel_action(pilier: str) -> str:
    appels = base.puces(base.sections(base.savoir("contenu/modeles-de-posts.md")).get("Appels à l'action", ""))
    if not appels:
        return ""
    return appels[0] if pilier == "Offre" else appels[-1] if pilier == "Technique" else appels[1 % len(appels)]


def ecrire_script(post: dict) -> str:
    sujet = post.get("sujet") or post["titre"]
    repli = "\n".join([
        f"[0:00-0:03] ACCROCHE face caméra : « {post['accroche']} »",
        f"[0:03-0:10] LE PROBLÈME : montrer {sujet} en gros plan, l'erreur la plus fréquente.",
        f"[0:10-0:25] LA CORRECTION : {sujet}, geste par geste, une consigne à la fois.",
        "[0:25-0:32] LE RÉSULTAT : la cible, avant et après, sans commentaire.",
        f"[0:32-0:36] APPEL À L'ACTION : {appel_action(post.get('pilier', ''))}",
    ])
    return cerveau.ecrire("Scénariste", f"Écris un script de Reel de 36 secondes en 5 temps horodatés sur : {sujet}. "
                                        f"Accroche imposée : {post['accroche']}", repli)


def mots_diese(post: dict, maximum: int = 5) -> list[str]:
    voc = base.puces(base.sections(base.savoir("contenu/piliers.md")).get("Vocabulaire de la niche", ""))
    texte = f"{post.get('titre', '')} {post.get('sujet', '')} {post.get('accroche', '')}"
    choisis = [v for v in voc if base.contient(texte, v)] + ["tir sportif"]
    vus, res = set(), []
    for c in choisis:
        d = "#" + re.sub(r"[^a-z0-9]", "", base.norm(c))
        if d not in vus:
            vus.add(d)
            res.append(d)
    return res[:maximum]


def ecrire_legende(post: dict) -> str:
    repli = f"{post['accroche']}\n\n{appel_action(post.get('pilier', ''))}"
    return cerveau.ecrire("Rédacteur de légendes", f"Écris la légende du post « {post['titre']} » "
                                                   f"(accroche : {post['accroche']}). Trois lignes maximum.", repli)


# ---------- Carrousels et miniatures (SVG, sans service externe) -----------------


def _lignes(texte: str, largeur: int) -> list[str]:
    res, cur = [], ""
    for mot in texte.split():
        if len(cur) + len(mot) + 1 > largeur and cur:
            res.append(cur)
            cur = mot
        else:
            cur = f"{cur} {mot}".strip()
    return res + ([cur] if cur else [])


def _svg(l: int, h: int, titre: str, pied: str, fond: str = "#111111", accent: str = "#d9f35b", taille: int = 84) -> str:
    lignes = _lignes(titre, max(10, int(l / (taille * 0.56))))
    y0 = h / 2 - (len(lignes) - 1) * taille * 0.6
    tspans = "".join(f'<tspan x="{l * 0.08:.0f}" y="{y0 + i * taille * 1.2:.0f}">{html.escape(t)}</tspan>'
                     for i, t in enumerate(lignes))
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{l}" height="{h}" viewBox="0 0 {l} {h}">'
            f'<rect width="{l}" height="{h}" fill="{fond}"/>'
            f'<rect x="{l * 0.08:.0f}" y="{h * 0.1:.0f}" width="{l * 0.12:.0f}" height="12" fill="{accent}"/>'
            f'<text font-family="Inter,Arial,sans-serif" font-weight="800" font-size="{taille}" fill="#ffffff">{tspans}</text>'
            f'<text x="{l * 0.08:.0f}" y="{h * 0.93:.0f}" font-family="Inter,Arial,sans-serif" font-size="{taille * 0.38:.0f}" '
            f'fill="{accent}">{html.escape(pied)}</text></svg>')


def carrousel(post: dict) -> list[Path]:
    """Plan des pages + une image SVG 1080x1350 par page."""
    sujet = post.get("sujet") or post["titre"]
    pages = [post["accroche"], f"1. Le problème : {sujet}, fait trop vite.", "2. La consigne : un seul geste à la fois.",
             "3. La répétition : dix minutes, tous les jours.", f"À retenir : {post['titre']}.",
             appel_action(post.get("pilier", ""))]
    fichiers = []
    for i, p in enumerate(pages, 1):
        fichiers.append(base.ecrire_fichier(f"carrousels/{post['id']}/page-{i}.svg",
                                            _svg(1080, 1350, p, f"{i} / {len(pages)}")))
    base.ecrire_fichier(f"carrousels/{post['id']}/plan.md",
                        f"# Carrousel {post['id']} · {post['titre']}\n\n" + "\n".join(f"{i}. {p}" for i, p in enumerate(pages, 1)) + "\n")
    base.journal("Créateur de carrousels", f"{len(pages)} pages", post=post["id"])
    return fichiers


def miniature(post: dict) -> Path:
    f = base.ecrire_fichier(f"miniatures/{post['id']}.svg", _svg(1280, 720, post["titre"].upper(), post.get("format", ""), taille=96))
    base.journal("Miniatures", "miniature créée", post=post["id"])
    return f


# ---------- Vidéo : découpe, montage, sous-titres, voix off ----------------------


def _ffmpeg(args: list[str]) -> None:
    if not shutil.which("ffmpeg"):
        raise RuntimeError("ffmpeg est introuvable : installez-le pour le montage.")
    r = subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *args], capture_output=True, text=True)
    if r.returncode:
        raise RuntimeError("ffmpeg : " + r.stderr.strip()[-300:])


def duree(video: Path) -> float:
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(video)],
                       capture_output=True, text=True)
    return float(r.stdout.strip() or 0)


def decouper(video: str, secondes: int = 30) -> list[Path]:
    """Long vers court : retire les silences (auto-editor s'il est installé), puis coupe en clips."""
    src = Path(video)
    sortie = base.dossier_donnees() / "clips" / src.stem
    sortie.mkdir(parents=True, exist_ok=True)
    if shutil.which("auto-editor"):
        net = sortie / f"{src.stem}-sans-silences.mp4"
        if subprocess.run(["auto-editor", str(src), "-o", str(net), "--no-open"], capture_output=True).returncode == 0:
            src = net
    _ffmpeg(["-i", str(src), "-f", "segment", "-segment_time", str(secondes), "-reset_timestamps", "1",
             "-c:v", "libx264", "-c:a", "aac", "-force_key_frames", f"expr:gte(t,n_forced*{secondes})",
             str(sortie / "clip-%02d.mp4")])
    clips = sorted(sortie.glob("clip-*.mp4"))
    base.journal("Découpeur", f"{len(clips)} clips", source=str(video))
    return clips


def sous_titres(video: Path) -> Path | None:
    """Sous-titres par Whisper s'il est installé (« pip install openai-whisper »)."""
    if not shutil.which("whisper"):
        return None
    subprocess.run(["whisper", str(video), "--language", "fr", "--output_format", "srt",
                    "--output_dir", str(video.parent)], capture_output=True)
    srt = video.with_suffix(".srt")
    return srt if srt.exists() else None


def monter(video: str, largeur: int = 1080, hauteur: int = 1920, srt: str | None = None) -> Path:
    """Recadre au centre et met à l'échelle (9:16 par défaut), incruste les sous-titres s'il y en a."""
    src = Path(video)
    sortie = base.dossier_donnees() / "montages" / f"{src.stem}-{largeur}x{hauteur}.mp4"
    sortie.parent.mkdir(parents=True, exist_ok=True)
    filtre = f"scale={largeur}:{hauteur}:force_original_aspect_ratio=increase,crop={largeur}:{hauteur}"
    st = Path(srt) if srt else sous_titres(src)
    if st and st.exists():
        chemin = str(st).replace("\\", "/").replace(":", "\\:").replace("'", "\\'")
        filtre += f",subtitles='{chemin}'"
    _ffmpeg(["-i", str(src), "-vf", filtre, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", str(sortie)])
    base.journal("Monteur", f"montage {largeur}x{hauteur}", source=str(video), sous_titres=bool(st))
    return sortie


def voix_off(post: dict) -> dict:
    """Texte à dire, puis synthèse : Chatterbox (local, voix clonée) ou ElevenLabs si branchés."""
    dit = [re.sub(r"^\[[^\]]*\]\s*[^:]*:\s*", "", l).strip().strip("«»").strip()
           for l in (post.get("script") or post["accroche"]).splitlines()]
    texte = "\n".join(d for d in dit if d)
    f = base.ecrire_fichier(f"voix/{post['id']}.txt", texte + "\n")
    res = {"texte": f, "audio": None, "moteur": "aucun (texte prêt à enregistrer ou à synthétiser)"}
    ref = base.dossier_donnees() / "voix" / "reference.wav"
    try:
        if ref.exists():
            import torchaudio  # type: ignore
            from chatterbox.tts import ChatterboxTTS  # type: ignore
            modele = ChatterboxTTS.from_pretrained(device="cpu")
            son = modele.generate(texte, audio_prompt_path=str(ref))
            audio = f.with_suffix(".wav")
            torchaudio.save(str(audio), son, modele.sr)
            res.update(audio=audio, moteur="Chatterbox (voix clonée)")
    except ImportError:
        pass
    if not res["audio"]:
        audio = f.with_suffix(".mp3")
        if connecteurs.elevenlabs(texte, audio):
            res.update(audio=audio, moteur="ElevenLabs")
    base.journal("Voix off", res["moteur"], post=post["id"])
    return res


def plans_de_coupe(post: dict) -> list[dict]:
    """Liste de plans à tourner ou à chercher, avec liens Pexels si la clé est branchée."""
    sujet = post.get("sujet") or post["titre"]
    plans = [{"plan": f"Gros plan mains : {sujet}", "recherche": "shooting range hands close up"},
             {"plan": "Cible vue de face, impacts visibles", "recherche": "paper target close up"},
             {"plan": "Plan large du stand, lumière naturelle", "recherche": "indoor shooting range"},
             {"plan": "Préparation du sac et du matériel", "recherche": "packing range bag"}]
    for p in plans:
        p["liens"] = connecteurs.pexels(p["recherche"]) or []
    base.journal("Plans de coupe", f"{len(plans)} plans", post=post["id"])
    return plans
