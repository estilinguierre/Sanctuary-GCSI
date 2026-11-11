"use strict";
// Interface de Sentinelle. Tout le texte venant du modèle passe par textContent : jamais de HTML interprété.

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
if (params.get("jeton")) {
  try { sessionStorage.setItem("jeton", params.get("jeton")); } catch {}
  history.replaceState(null, "", location.pathname);
}
let jeton = "";
try { jeton = sessionStorage.getItem("jeton") || ""; } catch {}

const session = "hud-" + Math.random().toString(36).slice(2, 10);
let mode = "veille";          // veille | ecoute | reflexion | parole | panne
let niveauMicro = 0;
let ecouteLocale = false;
let dernierEvenement = null;

async function api(chemin, options = {}) {
  const r = await fetch(chemin, { ...options, headers: { "x-jeton": jeton, ...(options.headers || {}) } });
  const corps = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(corps.detail || `Erreur ${r.status}`);
  return corps;
}

function passer(nouveau, libelle) {
  mode = nouveau;
  $("voyant").dataset.etat = nouveau === "panne" ? "panne" : nouveau === "veille" ? "pret" : "actif";
  $("etat").textContent = libelle;
}

// ---------- fil de conversation ----------
function ligne(classe, texte) {
  const li = document.createElement("li");
  li.className = classe;
  li.textContent = texte;
  $("fil").append(li);
  li.scrollIntoView({ block: "end" });
  return li;
}

function afficherReponse(r) {
  const li = ligne("lui", r.reponse || "(réponse vide)");
  if (r.outils.length) {
    const boite = document.createElement("div");
    boite.className = "outils";
    for (const o of r.outils) {
      const d = document.createElement("details");
      d.dataset.erreur = o.erreur;
      const s = document.createElement("summary");
      s.textContent = o.nom;
      const pre = document.createElement("pre");
      pre.textContent = JSON.stringify(o.args) + "\n→ " + o.resultat;
      d.append(s, pre);
      boite.append(d);
    }
    li.append(boite);
  }
  const source = document.createElement("div");
  source.className = "source";
  source.textContent = `${r.fournisseur === "ollama" ? "local" : r.fournisseur} · ${r.modele}`;
  li.append(source);
}

async function envoyer(texte) {
  texte = texte.trim();
  if (!texte || mode === "reflexion") return;
  ligne("moi", texte);
  passer("reflexion", "Réflexion…");
  try {
    const r = await api("/api/message", {
      method: "POST", headers: { "content-type": "application/json" },
      body: JSON.stringify({ texte, session }),
    });
    afficherReponse(r);
    dire(r.reponse);
  } catch (e) {
    ligne("erreur", e.message);
    passer("veille", "Prêt");
  }
  rafraichir();
}

$("saisie").addEventListener("submit", (e) => {
  e.preventDefault();
  const champ = $("texte");
  envoyer(champ.value);
  champ.value = "";
});

// ---------- parole (synthèse du navigateur, locale) ----------
function dire(texte) {
  const actif = $("son").getAttribute("aria-pressed") === "true";
  if (!actif || !("speechSynthesis" in window) || !texte) return passer("veille", "Prêt");
  speechSynthesis.cancel();
  const u = new SpeechSynthesisUtterance(texte);
  u.lang = "fr-FR";
  const voix = speechSynthesis.getVoices().find((v) => v.lang.startsWith("fr"));
  if (voix) u.voice = voix;
  u.onstart = () => passer("parole", "Parole…");
  u.onend = u.onerror = () => passer("veille", "Prêt");
  speechSynthesis.speak(u);
  setTimeout(() => { if (mode === "reflexion") passer("veille", "Prêt"); }, 1500);  // aucune voix installée
}
$("son").addEventListener("click", () => {
  const b = $("son");
  const actif = b.getAttribute("aria-pressed") !== "true";
  b.setAttribute("aria-pressed", actif);
  if (!actif && "speechSynthesis" in window) speechSynthesis.cancel();
});

// ---------- écoute ----------
// Voie souveraine : le son est enregistré ici puis transcrit par Vosk sur le serveur local.
// Repli : la reconnaissance du navigateur (qui, selon le navigateur, envoie l'audio à son éditeur).
let arret = null;

function encoderWav(blocs, frequence) {
  const total = blocs.reduce((n, b) => n + b.length, 0);
  const pas = frequence / 16000, n = Math.floor(total / pas);
  const plat = new Float32Array(total);
  let p = 0;
  for (const b of blocs) { plat.set(b, p); p += b.length; }
  const vue = new DataView(new ArrayBuffer(44 + n * 2));
  const ascii = (o, s) => [...s].forEach((c, i) => vue.setUint8(o + i, c.charCodeAt(0)));
  ascii(0, "RIFF"); vue.setUint32(4, 36 + n * 2, true); ascii(8, "WAVEfmt ");
  vue.setUint32(16, 16, true); vue.setUint16(20, 1, true); vue.setUint16(22, 1, true);
  vue.setUint32(24, 16000, true); vue.setUint32(28, 32000, true); vue.setUint16(32, 2, true);
  vue.setUint16(34, 16, true); ascii(36, "data"); vue.setUint32(40, n * 2, true);
  for (let i = 0; i < n; i++) {
    const debut = Math.floor(i * pas), fin = Math.min(total, Math.floor((i + 1) * pas) || debut + 1);
    let somme = 0;
    for (let j = debut; j < fin; j++) somme += plat[j];
    const v = Math.max(-1, Math.min(1, somme / Math.max(1, fin - debut)));
    vue.setInt16(44 + i * 2, v * 32767, true);
  }
  return vue.buffer;
}

async function ecouterLocalement() {
  const flux = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true } });
  const audio = new AudioContext();
  const source = audio.createMediaStreamSource(flux);
  const capteur = audio.createScriptProcessor(4096, 1, 1);
  const blocs = [];
  let parle = false, silence = 0;
  const debut = performance.now();
  const finir = async () => {
    arret = null;
    capteur.disconnect(); source.disconnect();
    flux.getTracks().forEach((t) => t.stop());
    await audio.close();
    niveauMicro = 0;
    $("micro").setAttribute("aria-pressed", "false");
    if (!blocs.length) return passer("veille", "Prêt");
    passer("reflexion", "Transcription…");
    try {
      const r = await api("/api/ecoute", { method: "POST", body: encoderWav(blocs, audio.sampleRate) });
      mode = "veille";
      if (r.texte) envoyer(r.texte); else { ligne("info", "Je n'ai rien entendu."); passer("veille", "Prêt"); }
    } catch (e) { ligne("erreur", e.message); passer("veille", "Prêt"); }
  };
  capteur.onaudioprocess = (e) => {
    const bloc = new Float32Array(e.inputBuffer.getChannelData(0));
    blocs.push(bloc);
    const rms = Math.sqrt(bloc.reduce((s, v) => s + v * v, 0) / bloc.length);
    niveauMicro = Math.min(1, rms * 8);
    if (rms > 0.02) { parle = true; silence = 0; } else silence += bloc.length / audio.sampleRate;
    const ecoule = (performance.now() - debut) / 1000;
    if (arret && ((parle && silence > 1.2) || ecoule > 15)) finir();   // fin de phrase ou durée maximale
  };
  source.connect(capteur); capteur.connect(audio.destination);
  arret = finir;
}

function ecouterParNavigateur() {
  const Reco = window.SpeechRecognition || window.webkitSpeechRecognition;
  const reco = new Reco();
  reco.lang = "fr-FR";
  reco.onresult = (e) => { mode = "veille"; envoyer(e.results[0][0].transcript); };
  reco.onerror = (e) => ligne("erreur", "Écoute impossible : " + e.error);
  reco.onend = () => {
    arret = null;
    $("micro").setAttribute("aria-pressed", "false");
    if (mode === "ecoute") passer("veille", "Prêt");
  };
  reco.start();
  arret = () => reco.stop();
}

const navigateurEcoute = "SpeechRecognition" in window || "webkitSpeechRecognition" in window;
$("micro").addEventListener("click", async () => {
  if (arret) return arret();
  if (mode !== "veille" && mode !== "parole") return;
  if ("speechSynthesis" in window) speechSynthesis.cancel();
  try {
    $("micro").setAttribute("aria-pressed", "true");
    passer("ecoute", "Écoute…");
    if (ecouteLocale && navigator.mediaDevices) await ecouterLocalement();
    else ecouterParNavigateur();
  } catch (e) {
    $("micro").setAttribute("aria-pressed", "false");
    ligne("erreur", "Micro inaccessible : " + e.message);
    passer("veille", "Prêt");
  }
});

// ---------- tableau de bord ----------
function jauge(nom, pct, valeur) {
  const div = document.createElement("div");
  div.className = "jauge";
  const dt = document.createElement("dt"); dt.textContent = nom;
  const barre = document.createElement("div"); barre.className = "barre";
  const i = document.createElement("i"); i.style.width = Math.max(0, Math.min(100, pct)) + "%";
  if (pct >= 85) i.dataset.haut = "";
  barre.append(i);
  const dd = document.createElement("dd"); dd.textContent = valeur;
  div.append(dt, barre, dd);
  return div;
}

function remplir(liste, elements, vide) {
  liste.replaceChildren(...(elements.length ? elements : [Object.assign(document.createElement("li"), { className: "vide", textContent: vide })]));
}

function duree(s) {
  const m = Math.floor(s / 60);
  return m >= 60 ? `${Math.floor(m / 60)} h ${String(m % 60).padStart(2, "0")}` : `${m} min ${String(s % 60).padStart(2, "0")} s`;
}

async function rafraichir() {
  let e;
  try { e = await api("/api/etat"); }
  catch (err) { if (mode === "veille" || mode === "panne") passer("panne", err.message); return; }
  if (mode === "panne") passer("veille", "Prêt");
  document.title = e.nom; $("nom").textContent = e.nom;
  ecouteLocale = e.ecoute_locale;
  const micro = $("micro");
  micro.disabled = !(ecouteLocale && navigator.mediaDevices) && !navigateurEcoute;
  micro.title = micro.disabled ? "Écoute indisponible : installez Vosk côté serveur (voir README)"
    : ecouteLocale ? "Parler (transcription locale)" : "Parler (reconnaissance du navigateur)";

  const m = e.machine;
  $("jauges").replaceChildren(
    jauge("Processeur", m.cpu_pct, m.cpu_pct.toFixed(0) + " %"),
    jauge("Mémoire", m.ram_pct, m.ram_pct.toFixed(0) + " %"),
    jauge("Disque", m.disque_pct, m.disque_pct.toFixed(0) + " %"),
    ...(m.temperature_c == null ? [] : [jauge("Température", m.temperature_c, m.temperature_c + " °C")]));

  for (const b of document.querySelectorAll(".cerveau button"))
    b.setAttribute("aria-pressed", b.dataset.fournisseur === e.choix);
  remplir($("fournisseurs"), Object.entries(e.fournisseurs).map(([nom, f]) => {
    const li = document.createElement("li");
    const g = document.createElement("span");
    g.textContent = (nom === "ollama" ? "Local" : nom === "claude" ? "Claude" : nom) + (nom === e.actif ? " · actif" : "");
    const petit = document.createElement("small"); petit.textContent = f.modele; g.append(petit);
    const p = document.createElement("span"); p.className = "pastille"; p.dataset.ok = f.disponible; p.textContent = f.detail;
    li.append(g, p);
    return li;
  }), "");

  remplir($("appareils"), e.appareils.map((a) => {
    const li = document.createElement("li");
    const g = document.createElement("span"); g.textContent = a.nom.replaceAll("_", " ");
    if (a.description) { const s = document.createElement("small"); s.textContent = a.description; g.append(s); }
    const actions = document.createElement("span"); actions.className = "actions";
    for (const action of a.actions) {
      const b = document.createElement("button"); b.type = "button"; b.textContent = action;
      b.addEventListener("click", async () => {
        try { await api(`/api/appareils/${encodeURIComponent(a.nom)}/${encodeURIComponent(action)}`, { method: "POST" });
              ligne("info", `${a.nom.replaceAll("_", " ")} : ${action}.`); }
        catch (err) { ligne("erreur", err.message); }
      });
      actions.append(b);
    }
    li.append(g, actions);
    return li;
  }), "Aucun appareil déclaré.");

  remplir($("minuteurs"), e.minuteurs.map((t) => {
    const li = document.createElement("li");
    const g = document.createElement("span"); g.textContent = t.libelle;
    const p = document.createElement("span"); p.className = "pastille"; p.textContent = duree(t.reste_s);
    li.append(g, p);
    return li;
  }), "Aucun minuteur en cours.");
}

for (const b of document.querySelectorAll(".cerveau button"))
  b.addEventListener("click", async () => {
    try { await api("/api/fournisseur", { method: "POST", headers: { "content-type": "application/json" },
                                            body: JSON.stringify({ nom: b.dataset.fournisseur }) }); }
    catch (e) { ligne("erreur", e.message); }
    rafraichir();
  });

async function annonces() {
  try {
    const nouveaux = await api("/api/evenements?depuis=" + (dernierEvenement || 0));
    for (const e of nouveaux) if (dernierEvenement !== null) { ligne("info", e.texte); if (mode === "veille") dire(e.texte); }
    dernierEvenement = nouveaux.length ? nouveaux[nouveaux.length - 1].id : (dernierEvenement || 0);
  } catch {}
}

// ---------- cadran : couronne de graduations qui traduit l'état ----------
const toile = $("cadran"), trace = toile.getContext("2d");
const calme = matchMedia("(prefers-reduced-motion: reduce)");
function dessiner(t) {
  const ratio = devicePixelRatio || 1, L = toile.clientWidth, H = toile.clientHeight;
  if (toile.width !== L * ratio) { toile.width = L * ratio; toile.height = H * ratio; }
  trace.setTransform(ratio, 0, 0, ratio, 0, 0);
  trace.clearRect(0, 0, L, H);
  const style = getComputedStyle(document.documentElement);
  const accent = style.getPropertyValue("--accent"), trait = style.getPropertyValue("--trait");
  const cx = L / 2, cy = H / 2, r = 52, N = 72, s = calme.matches ? 0 : t / 1000;
  for (let i = 0; i < N; i++) {
    const a = (i / N) * Math.PI * 2 - Math.PI / 2;
    let long = i % 6 === 0 ? 9 : 5, vif = false;
    if (mode === "ecoute") { long += niveauMicro * 14 * (0.5 + 0.5 * Math.sin(i * 1.7 + s * 9)); vif = true; }
    else if (mode === "reflexion") { vif = ((i - Math.floor(s * 30)) % N + N) % N < 14; }
    else if (mode === "parole") { long += 6 * (0.5 + 0.5 * Math.sin(i * 0.5 - s * 7)); vif = true; }
    else if (mode === "veille") { vif = ((i - Math.floor(s * 6)) % N + N) % N < 2; }
    trace.strokeStyle = mode === "panne" ? style.getPropertyValue("--alerte") : vif ? accent : trait;
    trace.lineWidth = i % 6 === 0 ? 2 : 1.2;
    trace.beginPath();
    trace.moveTo(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
    trace.lineTo(cx + Math.cos(a) * (r + long), cy + Math.sin(a) * (r + long));
    trace.stroke();
  }
  trace.strokeStyle = trait; trace.lineWidth = 1;
  trace.beginPath(); trace.arc(cx, cy, r - 8, 0, Math.PI * 2); trace.stroke();
  trace.fillStyle = mode === "veille" || mode === "panne" ? trait : accent;
  trace.beginPath(); trace.arc(cx, cy, 4, 0, Math.PI * 2); trace.fill();
  requestAnimationFrame(dessiner);
}

passer("veille", "Connexion…");
requestAnimationFrame(dessiner);
rafraichir().then(() => { if (mode === "veille") passer("veille", "Prêt"); });
setInterval(rafraichir, 4000);
setInterval(annonces, 2000);
