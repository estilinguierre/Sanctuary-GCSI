/*
 * MetalForge — moteur de configuration atomique.
 * Un seul moteur, partagé par le configurateur web (navigateur, téléphone) et par le complément SolidWorks
 * (exécuté en C# par l'interpréteur Jint). Unités : mm, kg, degrés.
 *
 *   ATOMES      profilé, tôle, verre, câble, grillage, platine, pince, gond…
 *   ASSEMBLAGES travée, remplissage, cadre, marche, palier, limon, ligne de foulée, crinoline…
 *   OUVRAGES    garde-corps, clôture, portails, 7 types d'escaliers, mezzanine, passerelle, auvent,
 *               pergola, portique, échelle, pliage, virole, tronc de cône, piquage, trémie.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.MetalForgeEngine = factory();
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  var VERSION = '1.0.0';
  var DEG = Math.PI / 180;
  var RHO = { acier: 7850, inox: 7900, alu: 2700, verre: 2500, bois: 550, grillage: 7850, filet: 7900, beton: 2400, polycarbonate: 1200, composite: 1500 };

  // ------------------------------------------------------------------ vecteurs [x, y, z]
  function V(x, y, z) { return [x, y, z]; }
  function add(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
  function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
  function mul(a, k) { return [a[0] * k, a[1] * k, a[2] * k]; }
  function len(a) { return Math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]); }
  function dist(a, b) { return len(sub(a, b)); }
  function norm(a) { var l = len(a); return l < 1e-12 ? [0, 0, 0] : mul(a, 1 / l); }
  function lerp(a, b, t) { return add(a, mul(sub(b, a), t)); }
  // plan (x, z)
  function p2(x, z) { return [x, z]; }
  function padd(a, b) { return [a[0] + b[0], a[1] + b[1]]; }
  function psub(a, b) { return [a[0] - b[0], a[1] - b[1]]; }
  function pmul(a, k) { return [a[0] * k, a[1] * k]; }
  function plen(a) { return Math.hypot(a[0], a[1]); }
  function pdist(a, b) { return plen(psub(a, b)); }
  function pnorm(a) { var l = plen(a); return l < 1e-12 ? [0, 0] : [a[0] / l, a[1] / l]; }
  function plerp(a, b, t) { return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; }
  function pcross(a, b) { return a[0] * b[1] - a[1] * b[0]; }
  function round(v, d) { var k = Math.pow(10, d || 0); return Math.round(v * k) / k; }
  function f0(v) { return (Math.round(v)).toString(); }
  function f1(v) { return (Math.round(v * 10) / 10).toFixed(1); }
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function num(v, d) { var x = typeof v === 'number' ? v : parseFloat(String(v).replace(',', '.')); return isFinite(x) ? x : d; }

  // ------------------------------------------------------------------ catalogue de profilés
  // Masses tabulées (EN 10056, EN 10055, DIN 1026, EN 10365) ; les autres sections sont calculées.
  var TAB = {
    UPN: { 80: [80, 45, 6, 8, 8.64], 100: [100, 50, 6, 8.5, 10.6], 120: [120, 55, 7, 9, 13.4], 140: [140, 60, 7, 10, 16.0], 160: [160, 65, 7.5, 10.5, 18.8], 180: [180, 70, 8, 11, 22.0], 200: [200, 75, 8.5, 11.5, 25.3], 220: [220, 80, 9, 12.5, 29.4], 240: [240, 85, 9.5, 13, 33.2] },
    IPE: { 80: [80, 46, 3.8, 5.2, 6.0], 100: [100, 55, 4.1, 5.7, 8.1], 120: [120, 64, 4.4, 6.3, 10.4], 140: [140, 73, 4.7, 6.9, 12.9], 160: [160, 82, 5, 7.4, 15.8], 180: [180, 91, 5.3, 8, 18.8], 200: [200, 100, 5.6, 8.5, 22.4], 220: [220, 110, 5.9, 9.2, 26.2], 240: [240, 120, 6.2, 9.8, 30.7], 270: [270, 135, 6.6, 10.2, 36.1], 300: [300, 150, 7.1, 10.7, 42.2], 330: [330, 160, 7.5, 11.5, 49.1], 360: [360, 170, 8, 12.7, 57.1] },
    HEA: { 100: [96, 100, 5, 8, 16.7], 120: [114, 120, 5, 8, 19.9], 140: [133, 140, 5.5, 8.5, 24.7], 160: [152, 160, 6, 9, 30.4], 180: [171, 180, 6, 9.5, 35.5], 200: [190, 200, 6.5, 10, 42.3], 220: [210, 220, 7, 11, 50.5], 240: [230, 240, 7.5, 12, 60.3] },
    HEB: { 100: [100, 100, 6, 10, 20.4], 120: [120, 120, 6.5, 11, 26.7], 140: [140, 140, 7, 12, 33.7], 160: [160, 160, 8, 13, 42.6], 180: [180, 180, 8.5, 14, 51.2], 200: [200, 200, 9, 15, 61.3] }
  };
  var LTAB = { '20x3': 0.882, '25x3': 1.12, '30x3': 1.36, '30x4': 1.78, '35x4': 2.09, '40x4': 2.42, '40x5': 2.97, '45x5': 3.38, '50x5': 3.77, '50x6': 4.47, '60x6': 5.42, '70x7': 7.38, '80x8': 9.63, '100x10': 15.0 };
  var TTAB = { '30x4': 1.77, '35x4.5': 2.33, '40x5': 2.96, '50x6': 4.44, '60x7': 6.23 };

  function rects(r) { // section composée de rectangles [x, y, w, h] : aire, Iy (axe horizontal), Iz, modules
    var A = 0, cx = 0, cy = 0, i;
    for (i = 0; i < r.length; i++) { var a = r[i][2] * r[i][3]; A += a; cx += a * (r[i][0] + r[i][2] / 2); cy += a * (r[i][1] + r[i][3] / 2); }
    cx /= A; cy /= A;
    var Iy = 0, Iz = 0, vy = 0, vz = 0;
    for (i = 0; i < r.length; i++) {
      var q = r[i], ar = q[2] * q[3];
      Iy += q[2] * Math.pow(q[3], 3) / 12 + ar * Math.pow(q[1] + q[3] / 2 - cy, 2);
      Iz += q[3] * Math.pow(q[2], 3) / 12 + ar * Math.pow(q[0] + q[2] / 2 - cx, 2);
      vy = Math.max(vy, Math.abs(q[1] - cy), Math.abs(q[1] + q[3] - cy));
      vz = Math.max(vz, Math.abs(q[0] - cx), Math.abs(q[0] + q[2] - cx));
    }
    return { A: A, Iy: Iy, Iz: Iz, Wy: Iy / vy, Wz: Iz / vz };
  }

  var _pcache = {};
  /** Décode un identifiant : TC40x40x2, TR60x40x2, TRD42.4x2, PL40x8, CP12, RP12, CAB6, L40x40x4, T40x40x5, UPN120, IPE200, HEA140, HEB120. */
  function profile(id) {
    if (!id) throw new Error('Profilé manquant');
    var key = String(id).replace(/\s+/g, '').replace(/,/g, '.').toUpperCase();
    if (_pcache[key]) return _pcache[key];
    var m, p = null;
    if ((m = /^(UPN|IPE|HEA|HEB)(\d+)$/.exec(key)) && TAB[m[1]][m[2]]) {
      var t = TAB[m[1]][m[2]];
      p = { fam: m[1], H: t[0], B: t[1], T: t[2], Tf: t[3], kgm: t[4] };
      p.sec = m[1] === 'UPN' ? rects([[0, 0, p.B, p.Tf], [0, p.Tf, p.T, p.H - 2 * p.Tf], [0, p.H - p.Tf, p.B, p.Tf]])
                             : rects([[0, 0, p.B, p.Tf], [(p.B - p.T) / 2, p.Tf, p.T, p.H - 2 * p.Tf], [0, p.H - p.Tf, p.B, p.Tf]]);
      p.perim = 2 * p.H + 4 * p.B - 2 * p.T; p.sh = p.H; p.vw = p.B; p.wa = p.B;
      p.label = m[1] + ' ' + m[2];
    } else if ((m = /^TRD(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)$/.exec(key))) {
      var D = +m[1], e = +m[2];
      p = { fam: 'TRD', B: D, H: D, T: e, round: true, A: Math.PI * (D - e) * e, perim: Math.PI * D, sh: D, vw: D, wa: D, label: 'Tube Ø' + D + 'x' + e };
      var I = Math.PI * (Math.pow(D, 4) - Math.pow(D - 2 * e, 4)) / 64;
      p.sec = { A: p.A, Iy: I, Iz: I, Wy: I / (D / 2), Wz: I / (D / 2), Wpl: (Math.pow(D, 3) - Math.pow(D - 2 * e, 3)) / 6 };
    } else if ((m = /^TC(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)(?:X(\d+(?:\.\d+)?))?$/.exec(key)) || (m = /^TR(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)$/.exec(key))) {
      var sq = key.indexOf('TC') === 0, H = +m[1], B = m[3] ? +m[2] : +m[1], th = m[3] ? +m[3] : +m[2];
      if (!sq) { H = +m[1]; B = +m[2]; th = +m[3]; }
      p = { fam: sq ? 'TC' : 'TR', H: H, B: B, T: th, A: 2 * th * (B + H - 8 * th) + 3 * Math.PI * th * th, perim: 2 * (B + H), sh: H, vw: B, wa: B };
      p.label = (sq ? 'Tube carré ' : 'Tube rect. ') + H + 'x' + B + 'x' + th;
      var iy = (B * Math.pow(H, 3) - (B - 2 * th) * Math.pow(H - 2 * th, 3)) / 12, iz = (H * Math.pow(B, 3) - (H - 2 * th) * Math.pow(B - 2 * th, 3)) / 12;
      p.sec = { A: p.A, Iy: iy, Iz: iz, Wy: iy / (H / 2), Wz: iz / (B / 2),
                Wpl: Math.min((B * H * H - (B - 2 * th) * Math.pow(H - 2 * th, 2)) / 4, (H * B * B - (H - 2 * th) * Math.pow(B - 2 * th, 2)) / 4) };
    } else if ((m = /^PL(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)$/.exec(key))) {
      var b = Math.max(+m[1], +m[2]), tt = Math.min(+m[1], +m[2]);
      p = { fam: 'PL', B: b, H: tt, T: tt, A: b * tt, perim: 2 * (b + tt), sh: b, vw: b, wa: tt, label: 'Plat ' + b + 'x' + tt,
            sec: { A: b * tt, Iy: tt * b * b * b / 12, Iz: b * tt * tt * tt / 12, Wy: tt * b * b / 6, Wz: b * tt * tt / 6, Wpl: b * tt * tt / 4 } };
    } else if ((m = /^(CP|RP|CAB)(\d+(?:\.\d+)?)$/.exec(key))) {
      var a = +m[2], rnd = m[1] !== 'CP';
      p = { fam: m[1], B: a, H: a, T: a, round: rnd, A: rnd ? Math.PI * a * a / 4 : a * a, perim: rnd ? Math.PI * a : 4 * a, sh: a, vw: a, wa: a,
            label: m[1] === 'CP' ? 'Carré plein ' + a : m[1] === 'RP' ? 'Rond plein Ø' + a : 'Câble inox Ø' + a };
      var ii = rnd ? Math.PI * Math.pow(a, 4) / 64 : Math.pow(a, 4) / 12;
      p.sec = { A: p.A, Iy: ii, Iz: ii, Wy: ii / (a / 2), Wz: ii / (a / 2), Wpl: rnd ? a * a * a / 6 : a * a * a / 4 };
      if (m[1] === 'CAB') { p.rho = RHO.inox; p.kgm = 0.0039 * a * a; } // câble inox 7x19 : ≈ 3,9·d² g/m
    } else if ((m = /^(L|T)(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)$/.exec(key))) {
      var A1 = +m[2], A2 = +m[3], ts = +m[4], isL = m[1] === 'L';
      p = { fam: m[1], B: A1, H: A2, T: ts, perim: 2 * (A1 + A2), sh: A2, vw: A1, wa: A1, label: (isL ? 'Cornière ' : 'Fer T ') + A1 + 'x' + A2 + 'x' + ts };
      p.sec = isL ? rects([[0, 0, A1, ts], [0, ts, ts, A2 - ts]]) : rects([[0, 0, A1, ts], [(A1 - ts) / 2, ts, ts, A2 - ts]]);
      p.A = p.sec.A;
      var tk = (A1 === A2 ? A1 + 'x' + ts : null);
      if (tk && isL && LTAB[tk]) p.kgm = LTAB[tk];
      if (tk && !isL && TTAB[tk]) p.kgm = TTAB[tk];
    }
    if (!p && (m = /^(MCDR|MCBO|MCMO|MCB)(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)$/.exec(key))) {
      // mains courantes pleines : demi-ronde, bombée, moulurée (acier), bois
      var Bm = +m[2], Hm = +m[3], kf = { MCDR: Math.PI / 4, MCBO: 0.85, MCMO: 0.72, MCB: 1 }[m[1]];
      p = { fam: m[1], B: Bm, H: Hm, T: Hm, A: Bm * Hm * kf, perim: m[1] === 'MCDR' ? Bm + Math.PI * Math.sqrt((Bm * Bm / 4 + Hm * Hm) / 2) : 2 * (Bm + Hm), sh: Hm, vw: Bm, wa: Bm,
            label: { MCDR: 'Main courante demi-ronde ', MCBO: 'Main courante bombée ', MCMO: 'Main courante moulurée ', MCB: 'Main courante bois ' }[m[1]] + Bm + 'x' + Hm };
      var rr = rects([[0, 0, Bm, Hm]]); p.sec = { A: p.A, Iy: rr.Iy * kf, Iz: rr.Iz * kf, Wy: rr.Wy * kf, Wz: rr.Wz * kf };
      if (m[1] === 'MCB') { p.rho = RHO.bois; p.material = 'bois'; }
      p.shape = m[1];
    } else if (!p && (m = /^(MCCU|UV|OV|OB|TRDI)(\d+(?:\.\d+)?)X(\d+(?:\.\d+)?)(?:X(\d+(?:\.\d+)?))?$/.exec(key))) {
      var fm = m[1], b1 = +m[2], h1 = m[4] ? +m[3] : +m[2], t1 = m[4] ? +m[4] : +m[3], A1;
      if (fm === 'TRDI') { b1 = +m[2]; h1 = b1; t1 = +m[3]; A1 = Math.PI * (b1 - t1) * t1; }
      else if (fm === 'OV') { var a2 = b1 / 2, c2 = h1 / 2; A1 = Math.PI * (3 * (a2 + c2) - Math.sqrt((3 * a2 + c2) * (a2 + 3 * c2))) * t1; }
      else if (fm === 'OB') A1 = (2 * (b1 - h1) + Math.PI * h1) * t1;
      else A1 = t1 * (b1 + 2 * (h1 - t1)); // MCCU (main courante creuse à gorge), UV (U pour verre)
      p = { fam: fm, B: b1, H: h1, T: t1, A: A1, round: fm === 'TRDI', perim: fm === 'TRDI' ? Math.PI * b1 : fm === 'OV' ? A1 / t1 : 2 * (b1 + h1), sh: h1, vw: b1, wa: b1, shape: fm,
            label: { MCCU: 'Main courante creuse ', UV: 'Profil U (lisse sur verre) ', OV: 'Tube ovale ', OB: 'Tube oblong ', TRDI: 'Tube inox Ø' }[fm] + (fm === 'TRDI' ? b1 + 'x' + t1 : b1 + 'x' + h1 + 'x' + t1) };
      var ro = fm === 'MCCU' || fm === 'UV' ? rects([[0, 0, b1, t1], [0, t1, t1, h1 - t1], [b1 - t1, t1, t1, h1 - t1]])
                                            : { A: A1, Iy: (b1 * Math.pow(h1, 3) - (b1 - 2 * t1) * Math.pow(h1 - 2 * t1, 3)) / 12 * 0.8, Iz: (h1 * Math.pow(b1, 3) - (h1 - 2 * t1) * Math.pow(b1 - 2 * t1, 3)) / 12 * 0.8 };
      if (!ro.Wy) { ro.Wy = ro.Iy / (h1 / 2); ro.Wz = ro.Iz / (b1 / 2); }
      p.sec = ro; if (fm === 'TRDI') { p.rho = RHO.inox; p.material = 'inox'; }
    } else if (!p && (m = /^TOR(\d+(?:\.\d+)?)$/.exec(key))) {
      var at = +m[1]; p = { fam: 'TOR', B: at, H: at, T: at, A: at * at, perim: 4 * at, sh: at, vw: at, wa: at, label: 'Carré torsadé ' + at, shape: 'TOR' };
      var it = Math.pow(at, 4) / 12; p.sec = { A: at * at, Iy: it, Iz: it, Wy: it / (at / 2), Wz: it / (at / 2), Wpl: at * at * at / 4 };
    }
    if (!p) throw new Error('Profilé inconnu : « ' + id + ' ». Exemples : TC40x40x2, TR60x40x2, TRD42.4x2, PL40x8, RP12, CAB6, L40x40x4, UPN120, IPE200, MCDR40x8.');
    p.id = key;
    if (!p.A) p.A = p.sec.A;
    if (!p.kgm) p.kgm = p.A * 1e-6 * (p.rho || RHO.acier);
    if (!p.sec.Wpl) p.sec.Wpl = Math.min(p.sec.Wy, p.sec.Wz);
    p.Wmin = Math.min(p.sec.Wy, p.sec.Wz);
    p.Imin = Math.min(p.sec.Iy, p.sec.Iz);
    p.i = Math.sqrt(p.Imin / p.A); // rayon de giration mini
    _pcache[key] = p;
    return p;
  }

  // ------------------------------------------------------------------ listes déroulantes complètes (groupées par famille)
  function ser(prefix, arr, fmt) { return arr.map(function (x) { return prefix + (fmt ? fmt(x) : x); }); }
  function pairs(prefix, map) { var o = []; Object.keys(map).forEach(function (w) { map[w].forEach(function (t) { o.push(prefix + w + 'x' + t); }); }); return o; }
  var GROUPS = {
    'Mains courantes moulurées': ser('MCDR', ['40x8', '40x10', '45x10', '50x10', '50x12', '60x12']).concat(ser('MCBO', ['40x10', '45x12', '50x15', '60x15']), ser('MCMO', ['40x30', '45x35', '50x40', '60x45']), ser('MCCU', ['40x20x3', '50x30x3', '60x30x4'])),
    'Mains courantes bois': ser('MCB', ['45x45', '50x40', '60x40', '70x45', '80x50', '90x21', '120x27']),
    'Tubes ovales et oblongs': ser('OV', ['50x25x1.5', '60x30x2', '80x40x2']).concat(ser('OB', ['40x20x1.5', '50x20x2', '60x30x2', '80x30x2'])),
    'Profils U (lisse sur verre)': ser('UV', ['30x30x3', '40x40x3', '50x40x4', '60x45x4']),
    'Tubes ronds acier': ser('TRD', ['12x1.5', '16x1.5', '20x1.5', '20x2', '21.3x2', '25x2', '26.9x2', '26.9x2.6', '30x2', '33.7x2', '33.7x2.6', '40x2', '42.4x2', '42.4x2.6', '48.3x2', '48.3x3', '50x2', '60.3x2', '60.3x3', '76.1x3', '88.9x3.2', '101.6x3.6', '114.3x3.6', '139.7x4', '168.3x4.5']),
    'Tubes ronds inox': ser('TRDI', ['25x1.5', '33.7x2', '42.4x2', '48.3x2', '50.8x1.5']),
    'Tubes carrés': ser('TC', ['15x15x1.5', '20x20x1.5', '20x20x2', '25x25x2', '30x30x2', '30x30x3', '35x35x2', '40x40x2', '40x40x3', '40x40x4', '50x50x2', '50x50x3', '50x50x4', '60x60x3', '60x60x4', '60x60x5', '70x70x3', '70x70x4', '80x80x3', '80x80x4', '80x80x5', '90x90x4', '100x100x3', '100x100x4', '100x100x5', '100x100x6', '120x120x4', '120x120x5', '120x120x6', '140x140x5', '150x150x5', '150x150x6', '160x160x6', '180x180x6', '200x200x6', '200x200x8']),
    'Tubes rectangulaires': ser('TR', ['30x15x1.5', '30x20x2', '40x20x2', '40x27x2', '40x30x2', '50x20x2', '50x25x2', '50x30x2', '60x20x2', '60x30x2', '60x40x2', '60x40x3', '70x30x2', '70x40x3', '80x20x2', '80x40x2', '80x40x3', '80x40x4', '80x60x3', '80x60x4', '100x20x2', '100x40x3', '100x50x3', '100x50x4', '100x60x4', '120x40x3', '120x60x4', '120x80x4', '120x80x5', '140x80x5', '150x50x4', '150x100x5', '160x80x5', '180x100x5', '200x100x5', '200x100x6']),
    'Plats': pairs('PL', { 20: [3, 4, 5, 6, 8, 10], 25: [3, 4, 5, 6, 8, 10], 30: [3, 4, 5, 6, 8, 10, 12], 35: [5, 6, 8, 10], 40: [4, 5, 6, 8, 10, 12, 15, 20], 45: [5, 8, 10], 50: [4, 5, 6, 8, 10, 12, 15, 20],
                               60: [5, 6, 8, 10, 12, 15, 20], 70: [8, 10], 80: [5, 6, 8, 10, 12, 15, 20], 90: [10], 100: [5, 6, 8, 10, 12, 15, 20], 120: [8, 10, 12, 15], 150: [8, 10, 12, 15], 160: [10],
                               180: [10, 12], 200: [8, 10, 12, 15], 220: [10, 12], 250: [10, 12, 15], 300: [10, 12, 15], 350: [12, 15], 400: [15, 20] }),
    'Ronds pleins': ser('RP', [6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 30, 32, 35, 40, 45, 50]),
    'Carrés pleins': ser('CP', [8, 10, 12, 14, 16, 18, 20, 22, 25, 30, 35, 40]),
    'Carrés torsadés': ser('TOR', [12, 14, 16, 18, 20]),
    'Câbles inox': ser('CAB', [3, 4, 5, 6, 8, 10]),
    'Cornières': ser('L', ['20x20x3', '25x25x3', '30x30x3', '30x30x4', '35x35x4', '40x40x4', '40x40x5', '45x45x5', '50x50x5', '50x50x6', '60x60x6', '70x70x7', '80x80x8', '100x100x10']),
    'Fers T': ser('T', ['30x30x4', '35x35x4.5', '40x40x5', '50x50x6', '60x60x7']),
    'UPN': Object.keys(TAB.UPN).map(function (k) { return 'UPN' + k; }), 'IPE': Object.keys(TAB.IPE).map(function (k) { return 'IPE' + k; }),
    'HEA': Object.keys(TAB.HEA).map(function (k) { return 'HEA' + k; }), 'HEB': Object.keys(TAB.HEB).map(function (k) { return 'HEB' + k; })
  };
  var LISTS = {
    mainCourante: ['Mains courantes moulurées', 'Mains courantes bois', 'Tubes ronds acier', 'Tubes ronds inox', 'Tubes ovales et oblongs', 'Tubes rectangulaires', 'Tubes carrés', 'Plats', 'Profils U (lisse sur verre)', 'Ronds pleins', 'Carrés pleins'],
    poteau: ['Tubes carrés', 'Tubes rectangulaires', 'Tubes ronds acier', 'Tubes ronds inox', 'Tubes ovales et oblongs', 'Plats', 'Carrés pleins', 'Ronds pleins', 'Cornières', 'Fers T'],
    lisse: ['Tubes rectangulaires', 'Tubes carrés', 'Tubes ronds acier', 'Tubes ronds inox', 'Tubes ovales et oblongs', 'Plats', 'Cornières', 'Fers T', 'Ronds pleins', 'Carrés pleins', 'Profils U (lisse sur verre)'],
    barreau: ['Carrés pleins', 'Carrés torsadés', 'Ronds pleins', 'Tubes carrés', 'Tubes ronds acier', 'Tubes ronds inox', 'Tubes rectangulaires', 'Tubes ovales et oblongs', 'Plats', 'Fers T', 'Câbles inox'],
    cable: ['Câbles inox', 'Ronds pleins'],
    cadre: ['Tubes rectangulaires', 'Tubes carrés', 'Tubes ronds acier', 'Cornières', 'Fers T', 'Plats', 'UPN'],
    pilier: ['Tubes carrés', 'Tubes rectangulaires', 'Tubes ronds acier', 'HEA', 'HEB', 'IPE'],
    limon: ['Plats', 'UPN', 'Tubes rectangulaires', 'IPE', 'HEA'],
    limonCentral: ['Tubes rectangulaires', 'Tubes carrés', 'HEA', 'HEB', 'IPE', 'UPN'],
    poutre: ['IPE', 'HEA', 'HEB', 'UPN', 'Tubes rectangulaires', 'Tubes carrés'],
    poteauStructure: ['Tubes carrés', 'HEA', 'HEB', 'IPE', 'Tubes ronds acier', 'Tubes rectangulaires'],
    corniere: ['Cornières', 'Fers T', 'Plats', 'UPN'],
    fut: ['Tubes ronds acier', 'Tubes carrés'],
    plat: ['Plats', 'Cornières', 'Fers T', 'Tubes rectangulaires', 'Mains courantes bois'],
    echelon: ['Tubes carrés', 'Ronds pleins', 'Carrés pleins', 'Carrés torsadés', 'Tubes ronds acier']
  };
  /** Candidats du dimensionnement automatique (une série cohérente, du plus léger au plus lourd). */
  var AUTO = {
    poteau: function (p) { return p.fam === 'TC' && p.B >= 30; }, mainCourante: function (p) { return p.fam === 'TRD' && p.B >= 33; },
    lisse: function (p) { return p.fam === 'TR'; }, cadre: function (p) { return p.fam === 'TR' && p.H >= 40; }, pilier: function (p) { return p.fam === 'TC' && p.B >= 80; },
    limon: function (p) { return p.fam === 'PL' && p.B >= 150; }, limonCentral: function (p) { return p.fam === 'TR' && p.H >= 100; }, poutre: function (p) { return p.fam === 'IPE'; },
    poteauStructure: function (p) { return p.fam === 'TC' && p.B >= 70; }, echelon: function (p) { return p.fam === 'TC' && p.B >= 20; }, barreau: function (p) { return p.fam === 'RP' && p.B >= 12; },
    corniere: function (p) { return p.fam === 'L'; }, fut: function (p) { return p.fam === 'TRD' && p.B >= 88; }
  };
  function autoCandidates(list) {
    var ids = []; (LISTS[list] || []).forEach(function (g) { ids = ids.concat(GROUPS[g]); });
    return ids.map(profile).filter(AUTO[list] || function () { return true; }).sort(function (a, b) { return a.kgm - b.kgm; }).map(function (p) { return p.id; });
  }
  function profileOptions(list) {
    return (LISTS[list] || Object.keys(GROUPS)).map(function (g) { return { g: g, o: GROUPS[g].map(function (id) { var p = profile(id); return [p.id, p.label + ' — ' + p.kgm.toFixed(2) + ' kg/m']; }) }; });
  }
  var PROFILES = {}; Object.keys(LISTS).forEach(function (k) { PROFILES[k] = [].concat.apply([], LISTS[k].map(function (g) { return GROUPS[g]; })); });

  // ------------------------------------------------------------------ modèle neutre
  /**
   * Model : sortie commune de tous les ouvrages.
   *  members  : barres { mark, role, profile, a:[x,y,z], b:[x,y,z], rot, cut }
   *  plates   : tôles / verres { mark, role, plane:'XY'|'XZ'|'YZ', outline:[[u,v]..], depth, offset, thickness, material }
   *             XY : u=x, v=y, extrusion selon Z centrée sur offset ; XZ : u=x, v=z, selon Y ; YZ : u=z, v=y, selon X
   *  surfaces : volumes de révolution / facettes (aperçu web) { kind:'cone'|'tube'|'mesh', ... }
   *  flats    : développés 2D { name, outline:[[x,y]..], lines:[{a,b,layer,label}], texts:[{at,text}] }
   */
  function Model(name) {
    this.name = name; this.members = []; this.plates = []; this.surfaces = []; this.flats = [];
    this.checks = []; this.values = []; this.notes = []; this.masonry = []; this._c = {};
  }
  Model.prototype.mark = function (prefix) { this._c[prefix] = (this._c[prefix] || 0) + 1; return prefix + this._c[prefix]; };
  Model.prototype.member = function (prefix, role, prof, a, b, opt) {
    if (dist(a, b) < 0.5) return null;
    profile(prof);
    var o = { mark: this.mark(prefix), role: role, profile: profile(prof).id, a: a.map(function (v) { return round(v, 3); }),
              b: b.map(function (v) { return round(v, 3); }), rot: (opt && opt.rot) || 0, cut: (opt && opt.cut) || 0 };
    this.members.push(o); return o;
  };
  /** Supprime les sommets intermédiaires alignés (une barre droite = un seul membre, pour le débit). */
  function simplify(pts) {
    var o = [pts[0]];
    for (var i = 1; i < pts.length; i++) {
      var p = pts[i]; if (dist(p, o[o.length - 1]) < 0.5) continue;
      if (o.length >= 2 && i < pts.length) {
        var a = sub(o[o.length - 1], o[o.length - 2]), b = sub(p, o[o.length - 1]), la = len(a), lb = len(b);
        var cx = a[1] * b[2] - a[2] * b[1], cy = a[2] * b[0] - a[0] * b[2], cz = a[0] * b[1] - a[1] * b[0];
        if (Math.sqrt(cx * cx + cy * cy + cz * cz) / (la * lb) < 2e-4 && (a[0] * b[0] + a[1] * b[1] + a[2] * b[2]) > 0) { o[o.length - 1] = p; continue; }
      }
      o.push(p);
    }
    return o;
  }
  Model.prototype.chain = function (prefix, role, prof, pts, opt) {
    var out = []; pts = simplify(pts);
    for (var i = 0; i + 1 < pts.length; i++) { var m = this.member(prefix, role, prof, pts[i], pts[i + 1], opt); if (m) out.push(m); }
    return out;
  };
  Model.prototype.plate = function (prefix, role, plane, outline, depth, offset, thickness, material, extra) {
    if (!outline || outline.length < 3 || depth <= 0) return null;
    var o = { mark: this.mark(prefix), role: role, plane: plane, outline: outline.map(function (q) { return [round(q[0], 3), round(q[1], 3)]; }),
              depth: round(depth, 3), offset: round(offset, 3), thickness: round(thickness || depth, 3), material: material || 'acier' };
    if (extra) for (var k in extra) o[k] = extra[k];
    this.plates.push(o); return o;
  };
  Model.prototype.surface = function (s) { this.surfaces.push(s); return s; };
  /** ATOME maçonnerie : prisme vertical (contour en plan [x, z], du niveau y0 sur la hauteur h). */
  Model.prototype.mason = function (role, material, poly, y0, h) {
    if (!poly || poly.length < 3 || !(h > 0)) return null;
    var o = { mark: this.mark('MA'), role: role, material: material, poly: poly.map(function (q) { return [round(q[0], 1), round(q[1], 1)]; }), y0: round(y0, 1), h: round(h, 1) };
    this.masonry.push(o); return o;
  };
  Model.prototype.flat = function (f) { this.flats.push(f); return f; };
  Model.prototype.check = function (level, rule, msg, ref) { this.checks.push({ level: level, rule: rule, msg: msg, ref: ref || '' }); };
  Model.prototype.ok = function (r, m, ref) { this.check('ok', r, m, ref); };
  Model.prototype.warn = function (r, m, ref) { this.check('warn', r, m, ref); };
  Model.prototype.fail = function (r, m, ref) { this.check('fail', r, m, ref); };
  Model.prototype.info = function (r, m, ref) { this.check('info', r, m, ref); };
  Model.prototype.rule = function (cond, r, okMsg, failMsg, ref, soft) { if (cond) this.ok(r, okMsg, ref); else if (soft) this.warn(r, failMsg, ref); else this.fail(r, failMsg, ref); };
  Model.prototype.value = function (k, v) { this.values.push([k, v]); };
  /** Insère un autre modèle (rotation autour de Y puis translation). */
  Model.prototype.merge = function (o, off, rotYdeg) {
    var c = Math.cos((rotYdeg || 0) * DEG), s = Math.sin((rotYdeg || 0) * DEG), self = this;
    function T(p) { return [p[0] * c + p[2] * s + off[0], p[1] + off[1], -p[0] * s + p[2] * c + off[2]]; }
    o.members.forEach(function (m) { var n = self.member(m.mark.replace(/\d+$/, ''), m.role, m.profile, T(m.a), T(m.b), { rot: m.rot, cut: m.cut }); });
    o.plates.forEach(function (p) {
      var q = JSON.parse(JSON.stringify(p)), rq = ((rotYdeg || 0) % 360 + 360) % 360;
      if (p.plane === 'XZ') q.outline = p.outline.map(function (u) { var w = T([u[0], 0, u[1]]); return [w[0], w[2]]; }), q.offset = p.offset + off[1];
      else if (rq === 0) { if (p.plane === 'XY') { q.outline = p.outline.map(function (u) { return [u[0] + off[0], u[1] + off[1]]; }); q.offset = p.offset + off[2]; }
                           else { q.outline = p.outline.map(function (u) { return [u[0] + off[2], u[1] + off[1]]; }); q.offset = p.offset + off[0]; } }
      else if (rq === 90 || rq === 270) { // XY <-> YZ
        var sg = rq === 90 ? 1 : -1;
        if (p.plane === 'XY') { q.plane = 'YZ'; q.outline = p.outline.map(function (u) { return [-sg * u[0] + off[2], u[1] + off[1]]; }); q.offset = sg * p.offset + off[0]; }
        else { q.plane = 'XY'; q.outline = p.outline.map(function (u) { return [sg * u[0] + off[0], u[1] + off[1]]; }); q.offset = -sg * p.offset + off[2]; }
      } else { self.notes.push('Tôle ' + p.mark + ' non tournée (angle non orthogonal)'); return; }
      q.mark = self.mark(p.mark.replace(/\d+$/, ''));
      self.plates.push(q);
    });
    o.surfaces.forEach(function (sf) { self.surfaces.push(sf); });
    o.masonry.forEach(function (ms) { self.mason(ms.role, ms.material, ms.poly.map(function (q) { var w = T([q[0], 0, q[1]]); return [w[0], w[2]]; }), ms.y0 + off[1], ms.h); });
    o.flats.forEach(function (f) { self.flats.push(f); });
    o.checks.forEach(function (ch) { self.checks.push(ch); });
  };

  // ------------------------------------------------------------------ masses, nomenclature
  function memberLen(m) { return dist(m.a, m.b) + (m.cut || 0); }
  function polyArea(o) { var a = 0; for (var i = 0; i < o.length; i++) { var p = o[i], q = o[(i + 1) % o.length]; a += p[0] * q[1] - q[0] * p[1]; } return Math.abs(a) / 2; }
  function plateMass(p) {
    var A = polyArea(p.outline) * (p.plane === 'XZ' ? 1 : 1), vol = A * p.depth;
    if (p.material === 'grillage' || p.material === 'filet') return polyArea(p.outline) * 1e-6 * (p.kgm2 || 4);
    var f = p.fillRatio == null ? 1 : p.fillRatio;
    return vol * 1e-9 * (RHO[p.material] || RHO.acier) * f;
  }
  function plateArea(p) { return polyArea(p.outline) * 1e-6; } // m² (une face)
  function surfaceMass(s) {
    if (s.kind === 'cone' || s.kind === 'tube') { var r1 = s.r1, r2 = s.r2 == null ? s.r1 : s.r2, h = s.h || dist(s.a, s.b);
      var slant = Math.sqrt(h * h + Math.pow(r1 - r2, 2)); return Math.PI * (r1 + r2) * slant * s.t * 1e-9 * RHO.acier; }
    if (s.kind === 'mesh') return (s.area || 0) * s.t * 1e-9 * RHO.acier;
    return 0;
  }
  function totals(m) {
    var mass = 0, paint = 0;
    m.members.forEach(function (x) { var p = profile(x.profile), L = memberLen(x); mass += p.kgm * L / 1000; paint += p.perim * L * 1e-6; });
    m.plates.forEach(function (x) { mass += plateMass(x); if (x.material === 'acier' || x.material === 'grillage') paint += 2 * plateArea(x); });
    m.surfaces.forEach(function (s) { mass += surfaceMass(s); });
    return { mass: mass, paint: paint };
  }
  function bom(m) {
    var g = {};
    m.members.forEach(function (x) {
      var L = Math.round(memberLen(x)), k = x.profile + '|' + L + '|' + x.role;
      (g[k] = g[k] || { kind: 'barre', role: x.role, profile: x.profile, label: profile(x.profile).label, length: L, qty: 0, marks: [], kg: 0 });
      g[k].qty++; g[k].marks.push(x.mark); g[k].kg += profile(x.profile).kgm * L / 1000;
    });
    m.plates.forEach(function (x) {
      var us = x.outline.map(function (q) { return q[0]; }), vs = x.outline.map(function (q) { return q[1]; });
      var dims = [Math.max.apply(null, us) - Math.min.apply(null, us), Math.max.apply(null, vs) - Math.min.apply(null, vs), x.depth].sort(function (a, b) { return b - a; });
      var th = x.thickness, L = Math.round(dims[0]), W = Math.round(dims[1] === th ? dims[2] : dims[1]);
      var k = x.role + '|' + x.material + '|' + th + '|' + L + '|' + W;
      (g[k] = g[k] || { kind: 'tole', role: x.role, material: x.material, label: matLabel(x) + ' ép. ' + th, length: L, width: W, qty: 0, marks: [], kg: 0, m2: 0 });
      g[k].qty++; g[k].marks.push(x.mark); g[k].kg += plateMass(x); g[k].m2 += plateArea(x);
    });
    m.surfaces.forEach(function (s, i) {
      var k = 'S|' + s.role;
      (g[k] = g[k] || { kind: 'surface', role: s.role, label: s.label || s.kind, length: 0, qty: 0, marks: [], kg: 0 });
      g[k].qty++; g[k].kg += surfaceMass(s);
    });
    return Object.keys(g).map(function (k) { return g[k]; }).sort(function (a, b) { return a.kind < b.kind ? -1 : a.kind > b.kind ? 1 : (a.profile || '') < (b.profile || '') ? -1 : 1; });
  }
  function matLabel(p) {
    return { acier: 'Tôle acier', inox: 'Tôle inox', alu: 'Tôle alu', verre: 'Verre feuilleté', bois: 'Bois', grillage: 'Grillage soudé', beton: 'Béton', polycarbonate: 'Polycarbonate' }[p.material] || p.material;
  }

  // ------------------------------------------------------------------ débit 1D (Best Fit Decreasing)
  function nest(m, opt) {
    opt = opt || {}; var stock = opt.stock || 6000, kerf = opt.kerf == null ? 3 : opt.kerf, trim = opt.trim == null ? 10 : opt.trim;
    var by = {};
    m.members.forEach(function (x) { (by[x.profile] = by[x.profile] || []).push({ mark: x.mark, L: memberLen(x) }); });
    return Object.keys(by).sort().map(function (pid) {
      var bars = [], over = [];
      by[pid].sort(function (a, b) { return b.L - a.L; }).forEach(function (pc) {
        if (pc.L + trim > stock) { over.push(pc); return; }
        var best = null, rest = 1e18;
        bars.forEach(function (b) { var need = pc.L + (b.cuts.length ? kerf : 0), r = stock - b.used - need; if (r >= -1e-9 && r < rest) { best = b; rest = r; } });
        if (!best) { best = { cuts: [], used: trim }; bars.push(best); }
        if (best.cuts.length) best.used += kerf;
        best.cuts.push({ mark: pc.mark, L: round(pc.L, 1), at: best.used }); best.used += pc.L;
      });
      var useful = bars.reduce(function (s, b) { return s + b.cuts.reduce(function (t, c) { return t + c.L; }, 0); }, 0);
      return { profile: pid, stock: stock, bars: bars.map(function (b) { return { cuts: b.cuts, rest: round(stock - b.used, 0) }; }), oversize: over,
               yield: bars.length ? useful / (bars.length * stock) : 0, kg: bars.length * stock / 1000 * profile(pid).kgm };
    });
  }

  // ------------------------------------------------------------------ chiffrage
  var COST = { acier: 1.9, inox: 7.5, alu: 6, toleAcier: 1.7, laser: 1.2, verreM2: 190, boisM2: 90, grillageM2: 18, polyM2: 60, rate: 55, cutMin: 4, weldMin: 10,
               finish: 'thermolaquage', paintM2: 18, powderM2: 25, galvaKg: 0.95, margin: 25, vat: 20 };
  function cost(m, nesting, c) {
    c = Object.assign({}, COST, c || {}); var lines = [], kgBars = 0;
    (nesting || nest(m)).forEach(function (n) { kgBars += n.kg; });
    lines.push(['Profilés (barres achetées, chutes incluses)', kgBars * c.acier]);
    var plates = { acier: 0, inox: 0, alu: 0, verre: 0, bois: 0, grillage: 0, polycarbonate: 0 };
    m.plates.forEach(function (p) { plates[p.material] = (plates[p.material] || 0) + (['verre', 'bois', 'grillage', 'polycarbonate'].indexOf(p.material) >= 0 ? plateArea(p) : plateMass(p)); });
    m.surfaces.forEach(function (s) { plates.acier += surfaceMass(s); });
    if (plates.acier) lines.push(['Tôle acier + découpe laser (' + f1(plates.acier) + ' kg)', plates.acier * (c.toleAcier + c.laser)]);
    if (plates.inox) lines.push(['Tôle inox (' + f1(plates.inox) + ' kg)', plates.inox * c.inox]);
    if (plates.alu) lines.push(['Tôle alu (' + f1(plates.alu) + ' kg)', plates.alu * c.alu]);
    if (plates.verre) lines.push(['Verre feuilleté (' + plates.verre.toFixed(2) + ' m²)', plates.verre * c.verreM2]);
    if (plates.bois) lines.push(['Bois (' + plates.bois.toFixed(2) + ' m²)', plates.bois * c.boisM2]);
    if (plates.grillage) lines.push(['Grillage (' + plates.grillage.toFixed(2) + ' m²)', plates.grillage * c.grillageM2]);
    if (plates.polycarbonate) lines.push(['Polycarbonate (' + plates.polycarbonate.toFixed(2) + ' m²)', plates.polycarbonate * c.polyM2]);
    var cuts = m.members.length * 2, welds = m.members.length * 2 + m.plates.filter(function (p) { return p.material === 'acier'; }).length;
    var h = 1.5 + (cuts * c.cutMin + welds * c.weldMin * 1.3) / 60;
    lines.push(['Fabrication (' + f1(h) + ' h, ' + cuts + ' coupes, ' + welds + ' soudures)', h * c.rate]);
    var t = totals(m);
    if (c.finish === 'peinture') lines.push(['Peinture (' + t.paint.toFixed(2) + ' m²)', t.paint * c.paintM2]);
    if (c.finish === 'thermolaquage') lines.push(['Thermolaquage (' + t.paint.toFixed(2) + ' m²)', t.paint * c.powderM2]);
    if (c.finish === 'galva') lines.push(['Galvanisation (' + f1(t.mass) + ' kg)', t.mass * c.galvaKg]);
    if (c.finish === 'galva+thermo') { lines.push(['Galvanisation', t.mass * c.galvaKg]); lines.push(['Thermolaquage', t.paint * c.powderM2]); }
    var sub = lines.reduce(function (s, l) { return s + l[1]; }, 0), ht = sub * (1 + c.margin / 100);
    return { lines: lines, sub: sub, ht: ht, ttc: ht * (1 + c.vat / 100), hours: h };
  }

  // ================================================================== ATOMES & ASSEMBLAGES DE PANNEAU
  /**
   * Repère local d'un panneau vertical : s le long du tracé (mm), y hauteur absolue, w décalage perpendiculaire.
   * A, B : points en plan [x, z]. Le plan est « principal » (XY ou YZ) si le segment est parallèle à X ou Z : tôles et verres possibles.
   */
  function segFrame(A, B) {
    var d = pnorm(psub(B, A)), n = [-d[1], d[0]], L = pdist(A, B);
    var plane = Math.abs(d[1]) < 1e-9 ? 'XY' : Math.abs(d[0]) < 1e-9 ? 'YZ' : null;
    return {
      A: A, d: d, n: n, L: L, plane: plane,
      at: function (s, y, w) { w = w || 0; return [A[0] + d[0] * s + n[0] * w, y, A[1] + d[1] * s + n[1] * w]; },
      uv: function (s, y) { return plane === 'XY' ? [A[0] + d[0] * s, y] : [A[1] + d[1] * s, y]; },
      off: function (w) { w = w || 0; return plane === 'XY' ? A[1] + n[1] * w : A[0] + n[0] * w; }
    };
  }
  function glassThickness(comp) { // 44.2 → 4+4 mm + 2 x 0,38 PVB
    var m = /^(\d)(\d)\.(\d)$/.exec(String(comp || '44.2')); if (!m) return 8.76;
    return +m[1] + +m[2] + +m[3] * 0.38;
  }
  function samples(s0, s1, n) { var r = []; for (var i = 0; i <= n; i++) r.push(s0 + (s1 - s0) * i / n); return r; }

  /** Intervalles de s où le panneau a au moins la hauteur h au-dessus du bas. */
  function intervals(pn, h) {
    var res = [], cur = null, S = samples(pn.s0, pn.s1, 48);
    S.forEach(function (s) {
      var ok = pn.top(s) - pn.bot(s) >= h - 1e-6;
      if (ok && !cur) cur = [s, s]; else if (ok) cur[1] = s; else if (cur) { res.push(cur); cur = null; }
    });
    if (cur) res.push(cur);
    return res.filter(function (iv) { return iv[1] - iv[0] > 1; });
  }
  function panelPlate(m, pn, prefix, role, thick, material, extra, inset) {
    inset = inset || 0;
    if (!pn.f.plane) return false;
    var S = samples(pn.s0 + inset, pn.s1 - inset, pn.curved ? 24 : 1), o = [];
    S.forEach(function (s) { o.push(pn.f.uv(s, pn.bot(s) + inset)); });
    S.slice().reverse().forEach(function (s) { o.push(pn.f.uv(s, pn.top(s) - inset)); });
    m.plate(prefix, role, pn.f.plane, o, thick, pn.f.off(pn.w || 0), thick, material, extra);
    return true;
  }
  function verticals(m, pn, prof, gap, prefix, role, over) {
    var p = profile(prof), W = pn.s1 - pn.s0;
    var n = Math.max(0, Math.ceil((W - gap) / (p.vw + gap) - 1e-9)), g = (W - n * p.vw) / (n + 1);
    for (var k = 1; k <= n; k++) {
      var s = pn.s0 + k * g + (k - 0.5) * p.vw;
      m.member(prefix, role, prof, pn.f.at(s, pn.bot(s), pn.w), pn.f.at(s, pn.top(s) + (over || 0), pn.w));
    }
    return { gap: g, count: n };
  }
  function horizontals(m, pn, prof, gap, prefix, role, rot) {
    var p = profile(prof), Hmax = 0;
    samples(pn.s0, pn.s1, 24).forEach(function (s) { Hmax = Math.max(Hmax, pn.top(s) - pn.bot(s)); });
    var ih = p.sh, n = Math.max(0, Math.ceil((Hmax - gap) / (ih + gap) - 1e-9)), g = (Hmax - n * ih) / (n + 1), lowest = null;
    for (var k = 1; k <= n; k++) {
      var off = k * g + (k - 0.5) * ih;
      intervals(pn, off + ih / 2).forEach(function (iv) {
        m.member(prefix, role, prof, pn.f.at(iv[0], pn.bot(iv[0]) + off, pn.w), pn.f.at(iv[1], pn.bot(iv[1]) + off, pn.w), { rot: rot || 0 });
      });
      if (lowest === null) lowest = off;
    }
    return { gap: g, count: n, lowest: lowest };
  }

  var INFILLS = [
    ['barreaux', 'Barreaudage vertical'], ['lisses', 'Lisses horizontales'], ['cables', 'Câbles inox'], ['lames', 'Lames / claustra'],
    ['croisillons', 'Croisillons (X)'], ['tole', 'Tôle pleine'], ['perforee', 'Tôle perforée'], ['decoupe', 'Tôle découpée laser (motif)'],
    ['verre', 'Verre feuilleté'], ['grillage', 'Grillage / treillis soudé'], ['mixte', 'Mixte : soubassement + partie haute'],
    ['industriel', 'Industriel : sous-lisse + plinthe'], ['barreaux-decales', 'Barreaudage rythmé (2 + 1)'], ['barreaux-lances', 'Barreaux à lances / pointes'],
    ['metal-deploye', 'Métal déployé'], ['tole-ondulee', 'Tôle ondulée / nervurée'], ['lames-bois', 'Lames bois / composite'], ['verre-opale', 'Verre opale / sérigraphié'],
    ['polycarbonate', 'Polycarbonate'], ['panneau-composite', 'Panneau composite aluminium'], ['filet-inox', 'Filet / maille inox'], ['aucun', 'Aucun']
  ];

  /**
   * ATOME « remplissage » : remplit un panneau (travée, vantail…) selon spec.type.
   * Retourne { gap, horizLowest (hauteur du 1er élément horizontal au-dessus du bas), climb (bool), desc }.
   */
  function infill(m, pn, spec) {
    var t = spec.type || 'barreaux', r = { gap: 0, horizLowest: null, climb: false, desc: '' };
    var plateFallback = function () { m.notes.push('Panneau non orthogonal : ' + t + ' remplacé par des barreaux.'); return infill(m, pn, Object.assign({}, spec, { type: 'barreaux' })); };
    switch (t) {
      case 'aucun': r.gap = pn.top(pn.s0) - pn.bot(pn.s0); break;
      case 'barreaux-lances': { var vl = verticals(m, pn, spec.bar || 'CP16', spec.gap || 110, 'B', 'Barreau à lance', spec.over || 120); r.gap = vl.gap; r.desc = vl.count + ' barreaux à lance'; r.lances = true; break; }
      case 'barreaux-decales': {
        var bp = profile(spec.bar || 'TC20x20x1.5'), Wd = pn.s1 - pn.s0, pair = 2 * bp.vw + 40, gmax = spec.gap || 110;
        var nu = Math.max(1, Math.ceil((Wd - gmax) / (pair + gmax) - 1e-9)), G = (Wd - nu * pair) / (nu + 1);
        for (var u = 0; u < nu; u++) { var x0 = pn.s0 + G + u * (pair + G); [x0 + bp.vw / 2, x0 + bp.vw * 1.5 + 40].forEach(function (x) { m.member('B', 'Barreau', bp.id, pn.f.at(x, pn.bot(x), pn.w), pn.f.at(x, pn.top(x), pn.w)); }); }
        r.gap = Math.max(G, 40); r.desc = nu + ' paires de barreaux'; break;
      }
      case 'metal-deploye': case 'filet-inox': {
        var ksp = t === 'metal-deploye' ? 9.5 : 1.4;
        if (!panelPlate(m, pn, 'G', t === 'metal-deploye' ? 'Métal déployé' : 'Filet inox', t === 'metal-deploye' ? 4 : 3, t === 'metal-deploye' ? 'grillage' : 'filet', { kgm2: ksp, mesh: spec.mesh || 40 }, 0)) return plateFallback();
        r.gap = spec.mesh || 40; r.desc = t === 'metal-deploye' ? 'métal déployé' : 'filet inox'; break;
      }
      case 'tole-ondulee': case 'panneau-composite': case 'polycarbonate': case 'verre-opale': {
        var def = { 'tole-ondulee': ['Tôle ondulée', 0.75, 'acier', 1.25], 'panneau-composite': ['Panneau composite alu', 4, 'composite', 1], polycarbonate: ['Polycarbonate', 8, 'polycarbonate', 0.25], 'verre-opale': ['Verre opale', glassThickness(spec.glass), 'verre', 1] }[t];
        if (!panelPlate(m, pn, 'T', def[0], def[1], def[2], { fillRatio: def[3], opaque: t === 'verre-opale' }, t === 'verre-opale' ? 10 : 0)) return plateFallback();
        r.gap = t === 'verre-opale' ? 10 : 0; r.glass = t === 'verre-opale'; r.desc = def[0]; break;
      }
      case 'lames-bois': { var lb2 = horizontals(m, pn, spec.slat && /^MCB/.test(String(spec.slat).toUpperCase()) ? spec.slat : 'MCB90x21', spec.gap || 15, 'LA', 'Lame bois'); r.gap = lb2.gap; r.horizLowest = lb2.lowest; r.climb = true; r.desc = lb2.count + ' lames bois'; break; }
      case 'barreaux': { var v = verticals(m, pn, spec.bar || 'TC20x20x1.5', spec.gap || 110, 'B', 'Barreau', spec.over || 0); r.gap = v.gap; r.desc = v.count + ' barreaux'; break; }
      case 'lisses': { var h = horizontals(m, pn, spec.rail || 'TR30x20x2', spec.gap || 110, 'LI', 'Lisse intermédiaire'); r.gap = h.gap; r.horizLowest = h.lowest; r.climb = true; r.desc = h.count + ' lisses'; break; }
      case 'lames': { var l = horizontals(m, pn, spec.slat || 'PL80x8', spec.gap || 20, 'LA', 'Lame', spec.slatRot || 0); r.gap = l.gap; r.horizLowest = l.lowest; r.climb = !(spec.slatRot > 30); r.desc = l.count + ' lames'; break; }
      case 'cables': {
        if (spec.cableDir === 'v') { var cv = verticals(m, pn, spec.cable || 'CAB6', spec.cableGap || 80, 'CA', 'Câble inox'); r.gap = cv.gap; r.desc = cv.count + ' câbles verticaux'; }
        else { var ch = horizontals(m, pn, spec.cable || 'CAB6', spec.cableGap || 80, 'CA', 'Câble inox'); r.gap = ch.gap; r.horizLowest = ch.lowest; r.climb = true; r.desc = ch.count + ' câbles horizontaux'; }
        r.cables = true; break;
      }
      case 'croisillons': {
        var W = pn.s1 - pn.s0, Hm = pn.top((pn.s0 + pn.s1) / 2) - pn.bot((pn.s0 + pn.s1) / 2), nx = Math.max(1, Math.round(W / Math.max(Hm, 1)));
        for (var i = 0; i < nx; i++) {
          var a = pn.s0 + W * i / nx, b = pn.s0 + W * (i + 1) / nx;
          m.member('X', 'Croisillon', spec.bar || 'PL30x8', pn.f.at(a, pn.bot(a), pn.w), pn.f.at(b, pn.top(b), pn.w));
          m.member('X', 'Croisillon', spec.bar || 'PL30x8', pn.f.at(a, pn.top(a), pn.w), pn.f.at(b, pn.bot(b), pn.w));
        }
        r.gap = Math.max(W / nx, Hm) / 2; r.climb = true; r.horizLowest = 0; r.desc = nx + ' croix'; break;
      }
      case 'tole': case 'perforee': case 'decoupe': {
        var ratio = t === 'tole' ? 1 : 1 - (spec.openRatio == null ? (t === 'perforee' ? 0.4 : 0.35) : spec.openRatio);
        if (!panelPlate(m, pn, 'T', t === 'tole' ? 'Tôle pleine' : t === 'perforee' ? 'Tôle perforée' : 'Tôle découpée', spec.thick || 3, spec.metal || 'acier',
                        { fillRatio: ratio, motif: t === 'decoupe' ? (spec.motif || 'Végétal') : undefined, hole: t === 'perforee' ? (spec.hole || 10) : undefined }, 0)) return plateFallback();
        r.gap = t === 'tole' ? 0 : t === 'perforee' ? (spec.hole || 10) : (spec.motifOpening || 100);
        r.desc = t === 'perforee' ? 'trous Ø' + (spec.hole || 10) : t === 'decoupe' ? 'motif ' + (spec.motif || 'Végétal') : 'pleine'; break;
      }
      case 'verre': {
        var e = glassThickness(spec.glass), j = spec.glassGap == null ? 10 : spec.glassGap;
        if (!panelPlate(m, pn, 'V', 'Verre feuilleté ' + (spec.glass || '44.2'), e, 'verre', null, j)) return plateFallback();
        if ((spec.glassFix || 'pinces') === 'pinces' && pn.f.plane) {
          [pn.s0 + 0.5, pn.s1 - 40.5].forEach(function (s) {
            [0.22, 0.78].forEach(function (k) {
              var y = pn.bot(s) + (pn.top(s) - pn.bot(s)) * k;
              m.plate('PV', 'Pince à verre', pn.f.plane, [pn.f.uv(s, y - 25), pn.f.uv(s + 40, y - 25), pn.f.uv(s + 40, y + 25), pn.f.uv(s, y + 25)], e + 16, pn.f.off(pn.w || 0), 6, 'inox');
            });
          });
        }
        r.gap = j; r.desc = 'verre ' + (spec.glass || '44.2') + ' (' + f1(e) + ' mm)'; r.glass = true; break;
      }
      case 'grillage': {
        var maille = spec.mesh || 50, fil = spec.wire || 4, kgm2 = 2 * 1000 / maille * Math.PI * fil * fil / 4 * 1e-6 * RHO.acier;
        if (!panelPlate(m, pn, 'G', 'Grillage ' + maille + 'x' + maille + ' fil Ø' + fil, fil * 2, 'grillage', { kgm2: kgm2, mesh: maille }, 0)) return plateFallback();
        r.gap = maille; r.desc = 'maille ' + maille; break;
      }
      case 'mixte': {
        var hs = spec.split || 600, rp = profile(spec.rail || 'TR40x20x2'), mid = function (s) { return pn.bot(s) + hs; };
        var lower = Object.assign({}, pn, { top: function (s) { return Math.min(pn.top(s), mid(s)); } });
        var upper = Object.assign({}, pn, { bot: function (s) { return mid(s) + rp.sh; } });
        m.member('LM', 'Lisse intermédiaire', spec.rail || 'TR40x20x2', pn.f.at(pn.s0, mid(pn.s0) + rp.sh / 2, pn.w), pn.f.at(pn.s1, mid(pn.s1) + rp.sh / 2, pn.w));
        var r1 = infill(m, lower, Object.assign({}, spec, { type: spec.lowerType || 'tole' }));
        var r2 = infill(m, upper, Object.assign({}, spec, { type: spec.upperType || 'barreaux' }));
        r.gap = Math.max(r1.gap, r2.gap); r.horizLowest = r1.horizLowest; r.climb = r1.climb; r.desc = (spec.lowerType || 'tole') + ' + ' + (spec.upperType || 'barreaux');
        r.cables = r1.cables || r2.cables; r.glass = r1.glass || r2.glass; break;
      }
      case 'industriel': {
        var tb = profile(spec.toe || 'PL100x5'), rl = profile(spec.rail || 'TR40x20x2');
        var tbAxis = function (s) { return pn.ground(s) + 10 + tb.sh / 2; };
        m.member('PLI', 'Plinthe', tb.id, pn.f.at(pn.s0, tbAxis(pn.s0), pn.w), pn.f.at(pn.s1, tbAxis(pn.s1), pn.w));
        var kl = function (s) { return (pn.ground(s) + 10 + tb.sh + pn.top(s)) / 2; };
        m.member('SL', 'Sous-lisse', rl.id, pn.f.at(pn.s0, kl(pn.s0), pn.w), pn.f.at(pn.s1, kl(pn.s1), pn.w));
        var s0 = pn.s0; r.gap = Math.max(pn.top(s0) - kl(s0) - rl.sh / 2, kl(s0) - rl.sh / 2 - (pn.ground(s0) + 10 + tb.sh)); r.toe = tb.sh; r.desc = 'sous-lisse + plinthe ' + tb.sh; break;
      }
      default: throw new Error('Remplissage inconnu : ' + t);
    }
    return r;
  }

  /** Paramètres de remplissage communs (schéma), préfixe de groupe g. */
  function infillParams(g, def) {
    return [
      { k: 'infill', l: 'Type de remplissage', t: 'sel', d: def || 'barreaux', o: INFILLS, g: g },
      { k: 'bar', l: 'Profil barreau / croisillon', t: 'prof', d: 'TC20x20x1.5', list: 'barreau', g: g, show: ['infill', 'barreaux', 'croisillons', 'mixte'] },
      { k: 'gap', l: 'Vide maxi entre éléments', t: 'num', d: 110, u: 'mm', g: g, show: ['infill', 'barreaux', 'lisses', 'lames', 'mixte'] },
      { k: 'rail', l: 'Profil lisse', t: 'prof', d: 'TR30x20x2', list: 'lisse', g: g, show: ['infill', 'lisses', 'mixte', 'industriel'] },
      { k: 'slat', l: 'Profil lame', t: 'prof', d: 'PL80x8', list: 'plat', g: g, show: ['infill', 'lames'] },
      { k: 'slatRot', l: 'Inclinaison des lames', t: 'num', d: 0, u: '°', g: g, show: ['infill', 'lames'] },
      { k: 'cable', l: 'Câble', t: 'prof', d: 'CAB6', list: 'cable', g: g, show: ['infill', 'cables'] },
      { k: 'cableDir', l: 'Sens des câbles', t: 'sel', d: 'h', o: [['h', 'Horizontaux'], ['v', 'Verticaux']], g: g, show: ['infill', 'cables'] },
      { k: 'cableGap', l: 'Entraxe des câbles', t: 'num', d: 80, u: 'mm', g: g, show: ['infill', 'cables'] },
      { k: 'thick', l: 'Épaisseur tôle', t: 'num', d: 3, u: 'mm', g: g, show: ['infill', 'tole', 'perforee', 'decoupe', 'mixte'] },
      { k: 'metal', l: 'Matière tôle', t: 'sel', d: 'acier', o: [['acier', 'Acier'], ['inox', 'Inox'], ['alu', 'Aluminium']], g: g, show: ['infill', 'tole', 'perforee', 'decoupe', 'mixte'] },
      { k: 'hole', l: 'Perforation Ø', t: 'num', d: 10, u: 'mm', g: g, show: ['infill', 'perforee'] },
      { k: 'openRatio', l: 'Taux de vide', t: 'num', d: 0.4, step: 0.05, g: g, show: ['infill', 'perforee', 'decoupe'] },
      { k: 'motif', l: 'Motif laser', t: 'sel', d: 'Végétal', o: [['Végétal', 'Végétal'], ['Géométrique', 'Géométrique'], ['Ondes', 'Ondes'], ['Bambou', 'Bambou'], ['Personnalisé', 'Personnalisé (DXF client)']], g: g, show: ['infill', 'decoupe'] },
      { k: 'motifOpening', l: 'Plus grande ouverture du motif', t: 'num', d: 100, u: 'mm', g: g, show: ['infill', 'decoupe'] },
      { k: 'glass', l: 'Composition verre', t: 'sel', d: '44.2', o: [['44.2', '44.2 (8,8 mm)'], ['55.2', '55.2 (10,8 mm)'], ['66.2', '66.2 (12,8 mm)'], ['88.2', '88.2 (16,8 mm)']], g: g, show: ['infill', 'verre'] },
      { k: 'glassFix', l: 'Fixation du verre', t: 'sel', d: 'pinces', o: [['pinces', 'Pinces inox'], ['parclose', 'Parcloses / cadre'], ['encastre', 'Encastré en pied (profil U)']], g: g, show: ['infill', 'verre'] },
      { k: 'mesh', l: 'Maille', t: 'num', d: 50, u: 'mm', g: g, show: ['infill', 'grillage'] },
      { k: 'wire', l: 'Fil Ø', t: 'num', d: 4, u: 'mm', g: g, show: ['infill', 'grillage'] },
      { k: 'split', l: 'Hauteur du soubassement', t: 'num', d: 600, u: 'mm', g: g, show: ['infill', 'mixte'] },
      { k: 'lowerType', l: 'Soubassement', t: 'sel', d: 'tole', o: INFILLS.filter(function (x) { return ['mixte', 'industriel', 'aucun'].indexOf(x[0]) < 0; }), g: g, show: ['infill', 'mixte'] },
      { k: 'upperType', l: 'Partie haute', t: 'sel', d: 'barreaux', o: INFILLS.filter(function (x) { return ['mixte', 'industriel', 'aucun'].indexOf(x[0]) < 0; }), g: g, show: ['infill', 'mixte'] },
      { k: 'toe', l: 'Plinthe', t: 'prof', d: 'PL100x5', list: 'plat', g: g, show: ['infill', 'industriel'] }
    ];
  }
  function infillSpec(p) {
    return { type: p.infill, bar: p.bar, gap: p.gap, rail: p.rail, slat: p.slat, slatRot: p.slatRot, cable: p.cable, cableDir: p.cableDir, cableGap: p.cableGap,
             thick: p.thick, metal: p.metal, hole: p.hole, openRatio: p.openRatio, motif: p.motif, motifOpening: p.motifOpening, glass: p.glass, glassFix: p.glassFix,
             mesh: p.mesh, wire: p.wire, split: p.split, lowerType: p.lowerType, upperType: p.upperType, toe: p.toe, over: p.over };
  }

  // ================================================================== MAÇONNERIE & CONTEXTE (rendu, implantation)
  var MASON = [['beton', 'Béton banché'], ['beton-brut', 'Béton brut de décoffrage'], ['parpaing', 'Parpaing (bloc béton) brut'], ['parpaing-enduit', 'Parpaing enduit'],
               ['brique', 'Brique monomur / terre cuite'], ['brique-rouge', 'Brique rouge apparente'], ['pierre', 'Pierre de taille'], ['moellon', 'Moellons / pierre sèche'],
               ['enduit-blanc', 'Enduit blanc'], ['enduit-ton-pierre', 'Enduit ton pierre'], ['bardage-bois', 'Bardage bois'], ['beton-cire', 'Béton ciré']];
  var SOLS = [['beton-lisse', 'Dalle béton lissée'], ['carrelage', 'Carrelage'], ['parquet', 'Parquet'], ['resine', 'Sol résine'], ['enrobe', 'Enrobé'], ['pave', 'Pavés'],
              ['gravier', 'Gravier'], ['gazon', 'Gazon'], ['terrasse-bois', 'Terrasse bois'], ['dalle-pierre', 'Dallage pierre']];
  function mBox(m, role, mat, x0, z0, x1, z1, y0, h) { return m.mason(role, mat, [[Math.min(x0, x1), Math.min(z0, z1)], [Math.max(x0, x1), Math.min(z0, z1)], [Math.max(x0, x1), Math.max(z0, z1)], [Math.min(x0, x1), Math.max(z0, z1)]], y0, h); }
  /** Bande de maçonnerie le long d'un segment de tracé, entre les décalages w0 et w1 (normale gauche). */
  function mStrip(m, role, mat, A, B, w0, w1, y0, h, ext) {
    var d = pnorm(psub(B, A)), n = [-d[1], d[0]], e = ext || 0, a = psub(A, pmul(d, e)), b = padd(B, pmul(d, e));
    return m.mason(role, mat, [padd(a, pmul(n, w0)), padd(b, pmul(n, w0)), padd(b, pmul(n, w1)), padd(a, pmul(n, w1))], y0, h);
  }
  /** Dalle rectangulaire percée d'une trémie rectangulaire (jusqu'à 4 rectangles). */
  function mSlab(m, role, mat, R, hole, y0, h) {
    if (!hole) return mBox(m, role, mat, R[0], R[1], R[2], R[3], y0, h);
    var hx0 = Math.max(R[0], hole[0]), hz0 = Math.max(R[1], hole[1]), hx1 = Math.min(R[2], hole[2]), hz1 = Math.min(R[3], hole[3]);
    if (hx0 > R[0]) mBox(m, role, mat, R[0], R[1], hx0, R[3], y0, h);
    if (hx1 < R[2]) mBox(m, role, mat, hx1, R[1], R[2], R[3], y0, h);
    if (hz0 > R[1]) mBox(m, role, mat, hx0, R[1], hx1, hz0, y0, h);
    if (hz1 < R[3]) mBox(m, role, mat, hx0, hz1, hx1, R[3], y0, h);
  }
  /** Mur plan (parallèle à X ou Z) avec ouvertures [{ s0, s1, y0, y1 }] le long du mur. */
  function mWall(m, role, mat, A, B, w0, w1, y0, h, openings) {
    var L = pdist(A, B), cuts = [0, L]; (openings || []).forEach(function (o) { cuts.push(clamp(o.s0, 0, L), clamp(o.s1, 0, L)); });
    cuts = cuts.filter(function (v, i, a) { return a.indexOf(v) === i; }).sort(function (a, b) { return a - b; });
    var d = pnorm(psub(B, A));
    for (var i = 0; i + 1 < cuts.length; i++) {
      var s0 = cuts[i], s1 = cuts[i + 1], o = (openings || []).filter(function (op) { return op.s0 <= s0 + 1e-6 && op.s1 >= s1 - 1e-6; })[0];
      var P0 = padd(A, pmul(d, s0)), P1 = padd(A, pmul(d, s1));
      if (!o) { mStrip(m, role, mat, P0, P1, w0, w1, y0, h); continue; }
      if (o.y0 > y0 + 1) mStrip(m, role, mat, P0, P1, w0, w1, y0, o.y0 - y0);
      if (o.y1 < y0 + h - 1) mStrip(m, role, mat, P0, P1, w0, w1, o.y1, y0 + h - o.y1);
    }
  }
  function masonParams(defs) {
    var base = [
      { k: 'masonMat', l: 'Matériau de maçonnerie', t: 'sel', d: defs.mat || 'beton', o: MASON, g: 'Maçonnerie & contexte' },
      { k: 'floorMat', l: 'Revêtement de sol', t: 'sel', d: defs.floor || 'carrelage', o: SOLS, g: 'Maçonnerie & contexte' }
    ];
    return (defs.extra || []).concat(base);
  }
  function masonValue(m) {
    var v = m.masonry.reduce(function (s, x) { return s + polyArea(x.poly) * x.h * 1e-9; }, 0);
    if (v > 0) m.value('Maçonnerie de contexte (indicatif)', v.toFixed(2) + ' m³');
  }

  // ================================================================== RÉFÉRENCES NORMATIVES (voir docs/NORMES.md)
  var N = {
    GC: 'NF P01-012:2024', CH: 'NF P06-111-2/A1', ISO3: 'NF EN ISO 14122-3', ISO4: 'NF EN ISO 14122-4', E85: 'NF E85-016',
    ERP: 'Arrêté 20/04/2017 (ERP)', LOG: 'Arrêté 24/12/2015 (logement)', EC3: 'NF EN 1993-1-1', EC1: 'NF EN 1991-1-1',
    EXE: 'NF EN 1090-2', PORT: 'NF EN 12604 / 12453', GAP: 'NF EN ISO 13854', GALVA: 'NF EN ISO 14713-2', DIN: 'DIN 6935'
  };
  var FY = { S235: 235, S275: 275, S355: 355 };
  var E_ACIER = 210000;
  function bendCheck(m, rule, Med, W, grade, ref) {
    var r = Med / W / (FY[grade] || 235), msg = 'σ = ' + f0(Med / W) + ' MPa / fy ' + (FY[grade] || 235) + ' → taux ' + f0(r * 100) + ' %';
    if (r <= 0.9) m.ok(rule, msg, ref || N.EC3); else if (r <= 1) m.warn(rule, msg + ' (marge faible)', ref || N.EC3); else m.fail(rule, msg, ref || N.EC3);
    return r;
  }
  function deflCheck(m, rule, d, span, lim, ref) {
    var L = span / lim; if (d <= L) m.ok(rule, 'flèche ' + f1(d) + ' mm ≤ L/' + lim + ' = ' + f1(L) + ' mm', ref || 'critère usuel');
    else m.fail(rule, 'flèche ' + f1(d) + ' mm > L/' + lim + ' = ' + f1(L) + ' mm', ref || 'critère usuel');
  }
  function bucklingCheck(m, rule, Ned, prof, Lf, grade) { // Eurocode 3, courbe c (α = 0,49)
    var p = profile(prof), fy = FY[grade] || 235, eps = Math.sqrt(235 / fy), lam = Lf / p.i / (93.9 * eps);
    var phi = 0.5 * (1 + 0.49 * (lam - 0.2) + lam * lam), chi = Math.min(1, 1 / (phi + Math.sqrt(Math.max(phi * phi - lam * lam, 0))));
    var r = Ned / (chi * p.A * fy), msg = 'N = ' + f1(Ned / 1000) + ' kN, λ̄ = ' + lam.toFixed(2) + ', χ = ' + chi.toFixed(2) + ' → taux ' + f0(r * 100) + ' %';
    if (r <= 0.9) m.ok(rule, msg, N.EC3); else if (r <= 1) m.warn(rule, msg, N.EC3); else m.fail(rule, msg, N.EC3);
  }
  var USAGE_RAIL = [['prive', 'Logement / privé (cat. A-B)'], ['public', 'Public / ERP (cat. C-D)'], ['industriel', 'Industriel (ISO 14122-3)']];
  var GRADES = [['S235', 'S235'], ['S275', 'S275'], ['S355', 'S355']];

  // ================================================================== ASSEMBLAGE « garde-corps le long d'un tracé »
  /**
   * pts : tracé en plan [[x, z]..] (axe des poteaux) ; gy : niveau du sol / nez à chaque sommet.
   * Retourne des statistiques pour les vérifications.
   */
  function railRun(m, pts, gy, o) {
    var post = profile(o.post), mc = profile(o.mc), lb = o.lb ? profile(o.lbProf) : null;
    var st = { gaps: [], spacing: 0, posts: 0, horizLowest: null, climb: false, length: 0, bottomGap: o.jeu, infill: [], toe: null, cables: false, glass: false };
    var side = o.side || 1, wPost = o.pose === 'anglaise' || o.pose === 'deport' || o.pose === 'sabot' ? side * (post.wa / 2 + (o.pose === 'deport' ? 60 : 8) + (o.wExtra || 0)) : 0;
    var noPosts = o.spec.type === 'verre' && o.spec.glassFix === 'encastre';
    // segments, longueurs cumulées, sommets « angle » (> 20°) qui reçoivent un poteau
    var segs = [], cum = [0];
    for (var i = 0; i + 1 < pts.length; i++) {
      var f = segFrame(pts[i], pts[i + 1]); if (f.L < 1e-6) continue;
      segs.push({ f: f, ga: gy[i], gb: gy[i + 1], c0: cum[cum.length - 1] }); cum.push(cum[cum.length - 1] + f.L);
    }
    segs.forEach(function (sg) { sg.ground = function (s) { return sg.ga + (sg.gb - sg.ga) * s / sg.f.L; }; });
    var total = cum[cum.length - 1]; st.length = total;
    var corners = [0];
    for (i = 1; i < segs.length; i++) { var c = segs[i - 1].f.d[0] * segs[i].f.d[0] + segs[i - 1].f.d[1] * segs[i].f.d[1]; if (c < Math.cos(20 * DEG)) corners.push(segs[i].c0); }
    corners.push(total);
    var stations = [];
    for (i = 0; i + 1 < corners.length; i++) {
      var Lr = corners[i + 1] - corners[i], n = Math.max(1, Math.ceil(Lr / o.emax - 1e-9));
      st.spacing = Math.max(st.spacing, Lr / n);
      for (var k = (i === 0 ? 0 : 1); k <= n; k++) stations.push(corners[i] + k * Lr / n);
    }
    function locate(S) { for (var j = 0; j < segs.length; j++) if (S <= segs[j].c0 + segs[j].f.L + 1e-6) return { sg: segs[j], s: clamp(S - segs[j].c0, 0, segs[j].f.L) }; var l = segs[segs.length - 1]; return { sg: l, s: l.f.L }; }
    var topRail = o.H - (o.mcPos === 'entre' ? 0 : mc.sh);
    if (!noPosts) stations.forEach(function (S) {
      var q = locate(S), g = q.sg.ground(q.s), base = o.pose === 'francaise' ? g + (o.plate ? o.plate[2] : 0) : g - o.drop;
      var w = wPost;
      m.member('P', 'Poteau', post.id, q.sg.f.at(q.s, base, w), q.sg.f.at(q.s, g + topRail, w));
      st.posts++;
      if (o.pose === 'francaise' && o.plate) {
        var cc = q.sg.f.at(q.s, g, w), a = o.plate[0] / 2, b = o.plate[1] / 2;
        m.plate('PL', 'Platine', 'XZ', [[cc[0] - a, cc[2] - b], [cc[0] + a, cc[2] - b], [cc[0] + a, cc[2] + b], [cc[0] - a, cc[2] + b]], o.plate[2], g + o.plate[2] / 2, o.plate[2]);
      } else if ((o.pose === 'anglaise' || o.pose === 'deport' || o.pose === 'sabot') && q.sg.f.plane && o.plate) {
        var ff = q.sg.f, s = q.s, hh = o.plate[0];
        m.plate('PL', 'Platine latérale', ff.plane, [ff.uv(s - o.plate[1] / 2, g - o.drop), ff.uv(s + o.plate[1] / 2, g - o.drop), ff.uv(s + o.plate[1] / 2, g - o.drop + hh), ff.uv(s - o.plate[1] / 2, g - o.drop + hh)],
                o.plate[2], ff.off(w - side * (post.wa / 2 + o.plate[2] / 2)), o.plate[2]);
      }
    });
    // main courante filante sur les sommets
    if (o.mcPos !== 'entre' || noPosts) {
      var mcPts = [];
      segs.forEach(function (sg, j) { if (j === 0) mcPts.push(sg.f.at(0, sg.ga + o.H - mc.sh / 2, wPost)); mcPts.push(sg.f.at(sg.f.L, sg.gb + o.H - mc.sh / 2, wPost)); });
      m.chain('MC', 'Main courante', mc.id, mcPts);
    }
    // travées : morceaux de segments entre deux poteaux
    for (var t = 0; t + 1 < stations.length; t++) {
      var S0 = stations[t], S1 = stations[t + 1];
      segs.forEach(function (sg) {
        var a0 = Math.max(S0, sg.c0), a1 = Math.min(S1, sg.c0 + sg.f.L); if (a1 - a0 < 1) return;
        var sa = a0 - sg.c0 + (Math.abs(a0 - S0) < 1e-6 && !noPosts ? post.vw / 2 : 0), sb = a1 - sg.c0 - (Math.abs(a1 - S1) < 1e-6 && !noPosts ? post.vw / 2 : 0);
        if (noPosts) { sa += 3; sb -= 3; }
        if (sb - sa < 1) return;
        if (o.mcPos === 'entre' && !noPosts) m.member('MC', 'Main courante', mc.id, sg.f.at(sa, sg.ground(sa) + o.H - mc.sh / 2, wPost), sg.f.at(sb, sg.ground(sb) + o.H - mc.sh / 2, wPost));
        var bot0 = o.jeu;
        if (lb && o.spec.type !== 'industriel' && !noPosts) {
          m.member('LB', 'Lisse basse', lb.id, sg.f.at(sa, sg.ground(sa) + o.jeu + lb.sh / 2, wPost), sg.f.at(sb, sg.ground(sb) + o.jeu + lb.sh / 2, wPost));
          bot0 = o.jeu + lb.sh;
        }
        if (noPosts) bot0 = 40;
        var pn = { f: sg.f, s0: sa, s1: sb, w: wPost, ground: sg.ground,
                   bot: function (s) { return sg.ground(s) + bot0; }, top: function (s) { return sg.ground(s) + topRail; } };
        var r = infill(m, pn, o.spec);
        st.gaps.push(r.gap); if (r.desc) st.infill.push(r.desc);
        if (r.horizLowest != null) { var hl = bot0 + r.horizLowest; st.horizLowest = st.horizLowest == null ? hl : Math.min(st.horizLowest, hl); }
        st.climb = st.climb || r.climb; st.toe = r.toe || st.toe; st.cables = st.cables || r.cables; st.glass = st.glass || r.glass;
      });
    }
    if (noPosts) segs.forEach(function (sg) { m.member('U', 'Profil U de pied', o.uProf || 'UPN100', sg.f.at(0, sg.ga + 50, 0), sg.f.at(sg.f.L, sg.gb + 50, 0), { rot: 90 }); });
    st.maxGap = st.gaps.length ? Math.max.apply(null, st.gaps) : 0;
    st.noPosts = noPosts;
    return st;
  }

  function railChecks(m, p, st, rampant) {
    var ind = p.usage === 'industriel' || p.infill === 'industriel';
    if (ind) {
      m.rule(p.H >= 1000 && p.H <= 1100, 'Hauteur lisse supérieure', f0(p.H) + ' mm (1000–1100)', f0(p.H) + ' mm hors 1000–1100 mm', N.ISO3);
      if (p.infill === 'industriel') {
        m.rule(st.toe >= 100, 'Plinthe', f0(st.toe) + ' mm ≥ 100 à 10 mm du sol', f0(st.toe) + ' mm < 100 mm', N.ISO3);
        m.rule(st.maxGap <= 500, 'Vides lisse / sous-lisse / plinthe', f0(st.maxGap) + ' mm ≤ 500', f0(st.maxGap) + ' mm > 500 mm', N.ISO3);
      } else m.rule(st.maxGap <= 500, 'Vide entre éléments', f0(st.maxGap) + ' mm ≤ 500', f0(st.maxGap) + ' mm > 500 mm', N.ISO3);
      if (p.usage !== 'industriel') m.fail('Usage', 'Remplissage industriel non admis en logement / ERP (vides > 110 mm)', N.GC);
    } else {
      var hMin = rampant ? 900 : (p.thickness >= 500 ? 800 : 1000), Hp = p.hProt || p.H;
      m.rule(Hp >= hMin, 'Hauteur de protection', f0(Hp) + ' mm ≥ ' + hMin + (rampant ? ' (rampant)' : ''), f0(Hp) + ' mm < ' + hMin + ' mm', N.GC);
      m.rule(st.maxGap <= 110.0001, 'Gabarit T1 (Ø 110)', 'vide ' + f1(st.maxGap) + ' mm ≤ 110', 'vide ' + f1(st.maxGap) + ' mm > 110 mm', N.GC);
      m.rule(st.bottomGap <= 110, 'Vide en partie basse', f0(st.bottomGap) + ' mm ≤ 110', f0(st.bottomGap) + ' mm > 110 mm', N.GC);
      if (st.horizLowest != null && st.horizLowest <= 600)
        m.fail('Zone basse non escaladable (0–600 mm)', 'élément horizontal à ' + f0(st.horizLowest) + ' mm : point d\'appui interdit. Barreaudage vertical, verre ou tôle en partie basse.', N.GC);
      else m.ok('Zone basse non escaladable (0–600 mm)', st.climb ? 'éléments horizontaux au-dessus de 600 mm' : 'pas de point d\'appui', N.GC);
      if (st.cables) m.warn('Câbles tendus', 'Règles spécifiques des câbles tendus (NF P01-012:2024) : flèche sous charge, tension, écartement à vérifier.', N.GC);
      if (st.glass) m.info('Verre', 'Verre feuilleté obligatoire ; épaisseur et fixation à justifier (essais / avis technique du système).', N.GC);
    }
    if (!st.noPosts) m.rule(st.spacing <= 1500, 'Entraxe poteaux', f0(st.spacing) + ' mm ≤ 1500', f0(st.spacing) + ' mm > 1500 : justifier par le calcul', ind ? N.ISO3 : N.GC, true);
    // résistance : poussée horizontale q à hauteur H
    var q = p.load > 0 ? p.load : (p.usage === 'prive' ? 0.6 : 1.0), e = st.spacing, H = p.H;
    m.info('Poussée horizontale', f1(q) + ' kN/m à ' + f0(H) + ' mm (' + (p.load > 0 ? 'imposée' : p.usage === 'prive' ? 'catégorie A-B' : 'catégorie C-D') + '), γQ = 1,5', N.CH);
    if (!st.noPosts) {
      var post = profile(p.post), Med = 1.5 * q * e * H;
      m.value('Moment ELU pied de poteau', f1(Med / 1e6) + ' kN·m');
      bendCheck(m, 'Résistance poteau (console)', Med, post.sec.Wpl, p.grade);
      bendCheck(m, 'Résistance main courante', 1.5 * q * e * e / 10, profile(p.mc).sec.Wpl, p.grade);
      var d = q * e * Math.pow(H, 3) / (3 * E_ACIER * post.Imin);
      m.value('Flèche tête de poteau (ELS)', f1(d) + ' mm');
      if (d > H / 50) m.warn('Déformation poteau', f1(d) + ' mm > H/50 : garde-corps souple'); else m.ok('Déformation poteau', f1(d) + ' mm (indicatif ≤ H/50)');
      if (p.pose === 'francaise') { var lev = Math.max(20, (p.plateW || 100) - 30); m.info('Chevilles de platine', '≈ ' + f1(Med / lev / 2 / 1000) + ' kN de traction par cheville (2 tendues, bras ' + f0(lev) + ' mm) : à valider avec le fabricant (ETE).'); }
    }
    m.info('Exécution', 'Garde-corps non porteur : NF EN 1090 non exigée selon la FFB, sauf prescription du marché.', N.EXE);
  }

  function railParams(defH) {
    return [
      { k: 'usage', l: 'Usage', t: 'sel', d: 'prive', o: USAGE_RAIL, g: 'Usage' },
      { k: 'H', l: 'Hauteur (dessus main courante)', t: 'num', d: defH || 1000, u: 'mm', g: 'Dimensions' },
      { k: 'thickness', l: 'Épaisseur du dispositif (≥ 500 → 800 mm admis)', t: 'num', d: 0, u: 'mm', g: 'Dimensions' },
      { k: 'emax', l: 'Entraxe maxi des poteaux', t: 'num', d: 1500, u: 'mm', g: 'Poteaux' },
      { k: 'post', l: 'Profil poteau', t: 'prof', d: '__auto', auto: 'poteau', list: 'poteau', g: 'Poteaux' },
      { k: 'pose', l: 'Pose', t: 'sel', d: 'francaise', o: [['francaise', 'À la française (platine sur dalle)'], ['anglaise', 'À l\'anglaise (platine en nez de dalle)'], ['deport', 'À l\'anglaise avec déport'], ['sabot', 'Sabot / patte en applique'],
        ['scellement', 'Scellement chimique / béton'], ['encastre', 'Encastré dans réservation']], g: 'Fixation' },
      { k: 'drop', l: 'Descente / scellement', t: 'num', d: 200, u: 'mm', g: 'Fixation', show: ['pose', 'anglaise', 'deport', 'scellement'] },
      { k: 'plateL', l: 'Platine longueur', t: 'num', d: 100, u: 'mm', g: 'Fixation', show: ['pose', 'francaise', 'anglaise', 'deport'] },
      { k: 'plateW', l: 'Platine largeur', t: 'num', d: 100, u: 'mm', g: 'Fixation', show: ['pose', 'francaise', 'anglaise', 'deport'] },
      { k: 'plateT', l: 'Platine épaisseur', t: 'num', d: 8, u: 'mm', g: 'Fixation', show: ['pose', 'francaise', 'anglaise', 'deport'] },
      { k: 'mc', l: 'Profil main courante', t: 'prof', d: 'TR40x20x2', auto: 'main courante', list: 'mainCourante', g: 'Main courante' },
      { k: 'mcPos', l: 'Position', t: 'sel', d: 'dessus', o: [['dessus', 'Filante sur poteaux'], ['entre', 'Entre poteaux']], g: 'Main courante' },
      { k: 'lb', l: 'Lisse basse', t: 'bool', d: true, g: 'Lisse basse' },
      { k: 'lbProf', l: 'Profil lisse basse', t: 'prof', d: 'TR40x20x2', list: 'lisse', g: 'Lisse basse', show: ['lb', true] },
      { k: 'jeu', l: 'Jeu en partie basse', t: 'num', d: 60, u: 'mm', g: 'Lisse basse' }
    ].concat(infillParams('Remplissage'), [
      { k: 'load', l: 'Poussée imposée (0 = selon usage)', t: 'num', d: 0, u: 'kN/m', step: 0.1, g: 'Résistance' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ]);
  }
  function railOpts(p) {
    return { H: p.H, post: p.post, emax: p.emax, mc: p.mc, mcPos: p.mcPos, lb: p.lb, lbProf: p.lbProf, jeu: p.jeu, pose: p.pose, drop: p.drop,
             plate: [p.plateL, p.plateW, p.plateT], spec: infillSpec(p), side: 1 };
  }

  var FAMILIES = {};
  function family(def) { FAMILIES[def.id] = def; return def; }

  family({
    id: 'garde-corps', cat: 'Garde-corps & clôtures', label: 'Garde-corps', icon: 'railing',
    params: [
      { k: 'trace', l: 'Tracé', t: 'sel', d: 'droit', o: [['droit', 'Droit'], ['L', 'En L (angle 90°)'], ['U', 'En U'], ['rampant', 'Rampant (escalier)']], g: 'Implantation' },
      { k: 'L1', l: 'Longueur 1', t: 'num', d: 3000, u: 'mm', g: 'Implantation' },
      { k: 'L2', l: 'Longueur 2', t: 'num', d: 1500, u: 'mm', g: 'Implantation', show: ['trace', 'L', 'U'] },
      { k: 'L3', l: 'Longueur 3', t: 'num', d: 1500, u: 'mm', g: 'Implantation', show: ['trace', 'U'] },
      { k: 'turn', l: 'Sens du retour', t: 'sel', d: 'gauche', o: [['gauche', 'Gauche'], ['droite', 'Droite']], g: 'Implantation', show: ['trace', 'L', 'U'] },
      { k: 'slope', l: 'Pente du rampant', t: 'num', d: 33, u: '°', g: 'Implantation', show: ['trace', 'rampant'] }
    ].concat(railParams(1000), masonParams({ mat: 'beton', floor: 'carrelage', extra: [
      { k: 'support', l: 'Support maçonné', t: 'sel', d: 'dalle', o: [['dalle', 'Dalle / plancher'], ['balcon', 'Balcon + façade'], ['muret', 'Muret / allège'], ['acrotere', 'Toiture-terrasse + acrotère'], ['escalier', 'Escalier béton (rampant)'], ['aucun', 'Aucun']], g: 'Maçonnerie & contexte' },
      { k: 'slabT', l: 'Épaisseur de dalle', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'dalle', 'balcon', 'muret', 'acrotere'] },
      { k: 'slabD', l: 'Profondeur de dalle (côté intérieur)', t: 'num', d: 1500, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'dalle', 'balcon', 'muret', 'acrotere'] },
      { k: 'nez', l: 'Recul des poteaux / nez de dalle', t: 'num', d: 100, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'dalle', 'balcon'] },
      { k: 'muretH', l: 'Hauteur muret / acrotère', t: 'num', d: 600, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'muret', 'acrotere'] },
      { k: 'muretT', l: 'Épaisseur muret / acrotère', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'muret', 'acrotere'] },
      { k: 'facadeH', l: 'Hauteur de façade', t: 'num', d: 2700, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'balcon'] },
      { k: 'stepH', l: 'Hauteur de marche béton', t: 'num', d: 175, u: 'mm', g: 'Maçonnerie & contexte', show: ['support', 'escalier'] }
    ] })),
    build: function (p) {
      var m = new Model('Garde-corps ' + p.trace + ' H' + p.H), sg = p.turn === 'droite' ? 1 : -1, pts, gy;
      if (p.trace === 'L') pts = [[0, 0], [p.L1, 0], [p.L1, sg * p.L2]];
      else if (p.trace === 'U') pts = [[0, 0], [p.L1, 0], [p.L1, sg * p.L2], [p.L1 - p.L3, sg * p.L2]];
      else pts = [[0, 0], [p.L1, 0]];
      var raised = p.support === 'muret' || p.support === 'acrotere', base = raised ? p.muretH : 0;
      gy = pts.map(function (q) { return (p.trace === 'rampant' ? q[0] * Math.tan(p.slope * DEG) : 0) + base; });
      var side = (p.trace === 'L' || p.trace === 'U') && p.turn === 'droite' ? -1 : 1, ro = railOpts(p); ro.side = side;
      var st = railRun(m, pts, gy, ro);
      // maçonnerie : le vide est du côté +side·n, l'intérieur du côté opposé
      var ang = p.pose === 'anglaise' || p.pose === 'deport' || p.pose === 'sabot', edge = raised ? p.muretT / 2 : ang ? 0 : p.nez;
      if (p.support !== 'aucun' && p.support !== 'escalier') {
        // dalle : polygone fermé = tracé décalé côté vide (nez) + côté(s) de fermeture vers le bâtiment
        var o = offsetPoly(pts, side * edge), poly, close;
        if (pts.length === 2) { var d0 = pnorm(psub(pts[1], pts[0])), nn = [-d0[1], d0[0]], back = pmul(nn, -side * p.slabD);
          poly = [o[0], o[1], padd(o[1], back), padd(o[0], back)]; close = [[poly[2], poly[3]]]; }
        else if (pts.length === 3) { var cr = [o[0][0], o[2][1]]; poly = [o[0], o[1], o[2], cr]; close = [[o[2], cr], [cr, o[0]]]; }
        else { var c2 = [o[0][0], o[3][1]]; poly = [o[0], o[1], o[2], o[3], c2]; close = [[c2, o[0]]]; }
        m.mason('Dalle', 'beton', poly, -p.slabT, p.slabT - 20);
        m.mason('Revêtement', p.floorMat, poly, -20, 20);
        if (p.support === 'balcon') {
          close.forEach(function (cl, ci) {
            var Lc = pdist(cl[0], cl[1]);
            mWall(m, 'Façade', p.masonMat, cl[0], cl[1], 0, side * 220, -20, p.facadeH + 20, ci === 0 && Lc > 1600 ? [{ s0: Lc / 2 - 700, s1: Lc / 2 + 700, y0: -20, y1: 2150 }] : []);
          });
          m.info('Façade', 'Porte-fenêtre 1400 x 2150 représentée pour l\'implantation.');
        }
      }
      if (raised) for (var i = 0; i + 1 < pts.length; i++) mStrip(m, p.support === 'muret' ? 'Muret' : 'Acrotère', p.masonMat, pts[i], pts[i + 1], side * edge, side * (edge - p.muretT), 0, p.muretH, i === 0 ? 0 : p.muretT / 2);
      if (p.support === 'escalier' && p.trace === 'rampant') {
        var tg = Math.tan(p.slope * DEG), hh = p.stepH, gg = hh / tg, nS = Math.floor(p.L1 / gg);
        for (var k = 0; k < nS; k++) mBox(m, 'Marche béton', p.masonMat, (k + 1) * gg - gg, 60 * side, (k + 1) * gg, -1100 * side, 0, (k + 1) * hh - (k === nS - 1 ? 0 : 0));
        m.value('Escalier béton', nS + ' marches de ' + f0(hh) + ' x ' + f0(gg) + ' mm');
      }
      masonValue(m);
      m.value('Longueur développée', f0(st.length) + ' mm'); m.value('Poteaux', st.posts); m.value('Entraxe', f0(st.spacing) + ' mm');
      m.value('Vide maxi', f1(st.maxGap) + ' mm'); m.value('Remplissage', st.infill[0] || '');
      if (raised) {
        p.hProt = p.muretH >= 600 ? p.muretH + p.H : p.H; p.thickness = Math.max(p.thickness, p.muretT);
        m.info('Muret / acrotère', p.muretH >= 600 ? 'Arase à ' + f0(p.muretH) + ' mm : hauteur de protection comptée depuis le sol fini (muret + garde-corps).'
                                                : 'Arase à moins de 600 mm : point d\'appui ; hauteur de protection comptée depuis l\'arase (interprétation à confirmer).', N.GC);
      }
      railChecks(m, p, st, p.trace === 'rampant');
      return m;
    }
  });

  family({
    id: 'cloture', cat: 'Garde-corps & clôtures', label: 'Clôture', icon: 'fence',
    params: [
      { k: 'L', l: 'Longueur', t: 'num', d: 10000, u: 'mm', g: 'Implantation' },
      { k: 'slopePct', l: 'Pente du terrain', t: 'num', d: 0, u: '%', g: 'Implantation' },
      { k: 'stepMode', l: 'Sur terrain en pente', t: 'sel', d: 'rampant', o: [['rampant', 'Panneaux rampants'], ['redans', 'Panneaux en redans (horizontaux)']], g: 'Implantation' },
      { k: 'H', l: 'Hauteur des panneaux', t: 'num', d: 1500, u: 'mm', g: 'Dimensions' },
      { k: 'jeu', l: 'Jeu au sol', t: 'num', d: 80, u: 'mm', g: 'Dimensions' },
      { k: 'emax', l: 'Entraxe maxi des poteaux', t: 'num', d: 2500, u: 'mm', g: 'Poteaux' },
      { k: 'post', l: 'Profil poteau', t: 'prof', d: 'TC60x60x3', list: 'poteau', g: 'Poteaux' },
      { k: 'postOver', l: 'Dépassement des poteaux', t: 'num', d: 50, u: 'mm', g: 'Poteaux' },
      { k: 'drop', l: 'Scellement', t: 'num', d: 500, u: 'mm', g: 'Poteaux' },
      { k: 'railP', l: 'Lisses haute et basse', t: 'prof', d: 'TR40x20x2', list: 'lisse', g: 'Cadre' },
      { k: 'over', l: 'Dépassement des barreaux (pointes)', t: 'num', d: 0, u: 'mm', g: 'Remplissage' }
    ].concat(infillParams('Remplissage', 'barreaux'), masonParams({ mat: 'parpaing-enduit', floor: 'gazon', extra: [
      { k: 'wallBase', l: 'Soubassement', t: 'sel', d: 'muret', o: [['aucun', 'Aucun (poteaux scellés au sol)'], ['muret', 'Muret de soubassement']], g: 'Maçonnerie & contexte' },
      { k: 'muretH', l: 'Hauteur du muret', t: 'num', d: 600, u: 'mm', g: 'Maçonnerie & contexte', show: ['wallBase', 'muret'] },
      { k: 'muretT', l: 'Épaisseur du muret', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['wallBase', 'muret'] },
      { k: 'cap', l: 'Chaperon béton', t: 'bool', d: true, g: 'Maçonnerie & contexte', show: ['wallBase', 'muret'] },
      { k: 'postMode', l: 'Poteaux', t: 'sel', d: 'acier', o: [['acier', 'Poteaux acier'], ['piliers', 'Piliers maçonnés']], g: 'Maçonnerie & contexte' },
      { k: 'pillar', l: 'Section des piliers', t: 'num', d: 300, u: 'mm', g: 'Maçonnerie & contexte', show: ['postMode', 'piliers'] }
    ] })),
    build: function (p) {
      var m = new Model('Clôture L' + p.L + ' H' + p.H), post = profile(p.post), rl = profile(p.railP), n = Math.max(1, Math.ceil(p.L / p.emax - 1e-9)), e = p.L / n;
      var slope = p.slopePct / 100, f = segFrame([0, 0], [p.L, 0]), mur = p.wallBase === 'muret', capH = mur && p.cap ? 60 : 0, pil = p.postMode === 'piliers';
      var gAt = function (x) { return x * slope; }, half = pil ? p.pillar / 2 : post.vw / 2;
      var spanBase = function (k) { return p.stepMode === 'redans' || mur ? Math.max(gAt(k * e), gAt((k + 1) * e)) : null; };
      var lift = mur ? p.muretH + capH : 0;
      for (var k = 0; k <= n; k++) {
        var x = k * e, b0 = Math.max(k > 0 ? (spanBase(k - 1) != null ? spanBase(k - 1) : gAt(x)) : gAt(x), k < n ? (spanBase(k) != null ? spanBase(k) : gAt(x)) : gAt(x));
        var top = b0 + lift + p.jeu + p.H + p.postOver;
        if (pil) {
          mBox(m, 'Pilier maçonné', p.masonMat, x - half, -half, x + half, half, gAt(x) - 100, top - gAt(x) + 100);
          mBox(m, 'Chapeau de pilier', 'beton', x - half - 30, -half - 30, x + half + 30, half + 30, top, 60);
        } else m.member('P', 'Poteau', post.id, f.at(x, (mur ? b0 + capH : gAt(x)) - p.drop, 0), f.at(x, top, 0));
      }
      var st = { gaps: [] };
      for (k = 0; k < n; k++) {
        var sa = k * e + half, sb = (k + 1) * e - half, base = spanBase(k);
        var g = function (s) { return (base != null ? base : gAt(s)) + lift; };
        var yb = function (s) { return g(s) + p.jeu + rl.sh / 2; }, yt = function (s) { return g(s) + p.jeu + p.H - rl.sh / 2; };
        m.member('LB', 'Lisse basse', rl.id, f.at(sa, yb(sa), 0), f.at(sb, yb(sb), 0));
        m.member('LH', 'Lisse haute', rl.id, f.at(sa, yt(sa), 0), f.at(sb, yt(sb), 0));
        var pn = { f: f, s0: sa, s1: sb, w: 0, ground: g, bot: function (s) { return yb(s) + rl.sh / 2; }, top: function (s) { return yt(s) - rl.sh / 2; } };
        var sp = infillSpec(p); if (p.over > 0 && sp.type === 'barreaux') { sp.over = p.over + rl.sh; }
        st.gaps.push(infill(m, pn, sp).gap);
        // maçonnerie de la travée : muret (redans), chaperon, sol
        var gl = Math.min(gAt(k * e), gAt((k + 1) * e));
        if (mur) {
          mBox(m, 'Muret', p.masonMat, k * e, -p.muretT / 2, (k + 1) * e, p.muretT / 2, gl - 100, base - gl + p.muretH + 100);
          if (p.cap) mBox(m, 'Chaperon', 'beton', k * e, -p.muretT / 2 - 30, (k + 1) * e, p.muretT / 2 + 30, base + p.muretH, capH);
        }
        mBox(m, 'Sol', p.floorMat, k * e, -1500, (k + 1) * e, 1500, gl - 120, 120);
      }
      var gmax = Math.max.apply(null, st.gaps);
      m.value('Panneaux', n); m.value('Entraxe', f0(e) + ' mm'); m.value('Vide maxi', f1(gmax) + ' mm');
      if (mur) m.value('Muret', f0(p.muretH) + ' x ' + f0(p.muretT) + ' mm (' + f0(p.L * p.muretH / 1e6 / (0.5 * 0.2)) + ' blocs 20x50 env.)');
      masonValue(m);
      m.info('Statut', 'Clôture : pas d\'exigence NF P01-012, sauf si elle protège d\'une chute (> 1 m) — utiliser alors « Garde-corps ».', N.GC);
      if (gmax > 110) m.warn('Vides', f0(gmax) + ' mm > 110 : passage possible d\'un enfant / animal'); else m.ok('Vides', f0(gmax) + ' mm ≤ 110');
      if (!pil) { if (p.drop < p.H / 3) m.warn('Scellement', f0(p.drop) + ' mm : usuellement ≥ H/3 pour la reprise du vent'); else m.ok('Scellement', f0(p.drop) + ' mm ≥ H/3'); }
      else m.info('Piliers maçonnés', 'Panneaux fixés par pattes à sceller ou chevilles chimiques dans les piliers (chaînage armé recommandé).');
      return m;
    }
  });

  // ------------------------------------------------------------------ portails
  var TOPS = [['droit', 'Droit'], ['cintre', 'Cintré (bombé)'], ['double-cintre', 'Double cintre (un arc par vantail)'], ['gendarme', 'Chapeau de gendarme'], ['gendarme-inverse', 'Chapeau de gendarme inversé'], ['concave', 'Concave (en creux)'], ['pointe', 'En pointe (fronton)'], ['biais', 'En biais']];
  function topShape(kind, W, H, f) {
    return function (s) {
      var u = clamp(s / W, 0, 1);
      switch (kind) {
        case 'cintre': { var R = (W * W / 4 + f * f) / (2 * f || 1), dx = s - W / 2; return f > 0 ? H + f - (R - Math.sqrt(Math.max(R * R - dx * dx, 0))) : H; }
        case 'gendarme': return H + f * (1 - Math.cos(2 * Math.PI * u)) / 2;
        case 'gendarme-inverse': return H + f - f * (1 - Math.cos(2 * Math.PI * u)) / 2;
        case 'pointe': return H + f * (1 - Math.abs(2 * u - 1));
        case 'double-cintre': { var Wh = W / 2, sx = s < Wh ? s : s - Wh, R3 = (Wh * Wh / 4 + f * f) / (2 * f || 1), d3 = sx - Wh / 2; return f > 0 ? H + f - (R3 - Math.sqrt(Math.max(R3 * R3 - d3 * d3, 0))) : H; }
        case 'concave': { var R2 = (W * W / 4 + f * f) / (2 * f || 1), d2 = s - W / 2; return f > 0 ? H + (R2 - Math.sqrt(Math.max(R2 * R2 - d2 * d2, 0))) : H; }
        case 'biais': return H + f * u;
        default: return H;
      }
    };
  }
  family({
    id: 'portail', cat: 'Portails & portillons', label: 'Portail / portillon', icon: 'gate',
    params: [
      { k: 'type', l: 'Type', t: 'sel', d: 'battant2', o: [['battant2', 'Battant 2 vantaux'], ['battant1', 'Battant 1 vantail'], ['portillon', 'Portillon'], ['coulissant', 'Coulissant sur rail'], ['autoportant', 'Coulissant autoportant']], g: 'Ouverture' },
      { k: 'W', l: 'Largeur de passage', t: 'num', d: 3500, u: 'mm', g: 'Ouverture' },
      { k: 'H', l: 'Hauteur aux montants', t: 'num', d: 1600, u: 'mm', g: 'Ouverture' },
      { k: 'jeu', l: 'Jeu au sol', t: 'num', d: 50, u: 'mm', g: 'Ouverture' },
      { k: 'hinge', l: 'Jeu côté gonds / pilier', t: 'num', d: 20, u: 'mm', g: 'Ouverture' },
      { k: 'meet', l: 'Jeu entre vantaux', t: 'num', d: 10, u: 'mm', g: 'Ouverture', show: ['type', 'battant2'] },
      { k: 'tail', l: 'Queue autoportant (% passage)', t: 'num', d: 45, u: '%', g: 'Ouverture', show: ['type', 'autoportant'] },
      { k: 'top', l: 'Forme du haut', t: 'sel', d: 'droit', o: TOPS, g: 'Forme' },
      { k: 'rise', l: 'Flèche / surélévation', t: 'num', d: 250, u: 'mm', g: 'Forme', show: ['top', 'cintre', 'double-cintre', 'gendarme', 'gendarme-inverse', 'concave', 'pointe', 'biais'] },
      { k: 'frame', l: 'Profil cadre', t: 'prof', d: 'TR60x40x2', list: 'cadre', g: 'Cadre' },
      { k: 'rails', l: 'Traverses intermédiaires', t: 'num', d: 1, g: 'Cadre' },
      { k: 'railP', l: 'Profil traverse', t: 'prof', d: 'TR40x20x2', list: 'lisse', g: 'Cadre' },
      { k: 'brace', l: 'Écharpe (diagonale)', t: 'bool', d: false, g: 'Cadre' },
      { k: 'over', l: 'Dépassement barreaux (pointes)', t: 'num', d: 0, u: 'mm', g: 'Remplissage' }
    ].concat(infillParams('Remplissage', 'barreaux'), [
      { k: 'pillars', l: 'Piliers', t: 'sel', d: 'maconnes', o: [['maconnes', 'Piliers maçonnés'], ['acier', 'Piliers acier'], ['aucun', 'Aucun (fixation sur existant)']], g: 'Piliers' },
      { k: 'postP', l: 'Profil pilier', t: 'prof', d: 'TC100x100x4', list: 'pilier', g: 'Piliers', show: ['pillars', 'acier'] },
      { k: 'embed', l: 'Scellement pilier', t: 'num', d: 500, u: 'mm', g: 'Piliers', show: ['pillars', 'acier'] },
      { k: 'pillarSize', l: 'Section des piliers maçonnés', t: 'num', d: 400, u: 'mm', g: 'Piliers', show: ['pillars', 'maconnes'] },
      { k: 'pillarCap', l: 'Chapeaux de piliers', t: 'bool', d: true, g: 'Piliers', show: ['pillars', 'maconnes'] },
      { k: 'motor', l: 'Motorisé', t: 'bool', d: false, g: 'Quincaillerie & sécurité' },
      { k: 'lock', l: 'Serrure', t: 'bool', d: true, g: 'Quincaillerie & sécurité' }
    ], masonParams({ mat: 'parpaing-enduit', floor: 'pave', extra: [
      { k: 'wingL', l: 'Murs de clôture latéraux (longueur, 0 = aucun)', t: 'num', d: 2000, u: 'mm', g: 'Maçonnerie & contexte' },
      { k: 'wingH', l: 'Hauteur des murs latéraux', t: 'num', d: 1200, u: 'mm', g: 'Maçonnerie & contexte' },
      { k: 'wingT', l: 'Épaisseur des murs', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte' }
    ] })),
    build: function (p) {
      var TYPES = { battant2: 'Portail battant 2 vantaux', battant1: 'Portail battant', portillon: 'Portillon', coulissant: 'Portail coulissant', autoportant: 'Portail autoportant' };
      var m = new Model(TYPES[p.type] + ' ' + p.W + 'x' + p.H + (p.top !== 'droit' ? ' — ' + TOPS.filter(function (t) { return t[0] === p.top; })[0][1].toLowerCase() : ''));
      var fr = profile(p.frame), a = fr.vw, f = segFrame([0, 0], [1, 0]), y0 = p.jeu, leaves = [], tailL = 0;
      var auto = p.type === 'autoportant', slide = p.type === 'coulissant' || auto;
      if (p.type === 'battant2') { var w = (p.W - 2 * p.hinge - p.meet) / 2; leaves.push({ x0: p.hinge, x1: p.hinge + w, hingeLeft: true }); leaves.push({ x0: p.hinge + w + p.meet, x1: p.W - p.hinge, hingeLeft: false }); }
      else if (slide) { tailL = auto ? p.W * p.tail / 100 : 0; leaves.push({ x0: p.hinge - tailL - (auto ? 0 : 0), x1: p.W + 60, hingeLeft: true, tail: tailL }); }
      else leaves.push({ x0: p.hinge, x1: p.W - p.hinge, hingeLeft: true });
      var gx0 = slide ? 0 : p.hinge, gW = slide ? p.W : p.W - 2 * p.hinge;
      var shape = topShape(p.top, gW, y0 + p.H, p.rise), topAt = function (x) { return shape(x - gx0); };
      var curved = p.top !== 'droit';
      var gapMax = 0;
      leaves.forEach(function (lf, li) {
        var mass0 = m.members.length;
        var x0 = lf.x0, x1 = lf.x1, T = function (x) { return slide && x < 0 ? y0 + p.H : topAt(clamp(x, gx0, gx0 + gW)); };
        var ptsTop = samples(x1 - a / 2, x0 + a / 2, curved ? 10 : 1).map(function (x) { return f.at(x, T(x) - a / 2, 0); });
        var loop = [f.at(x0 + a / 2, y0 + a / 2, 0), f.at(x1 - a / 2, y0 + a / 2, 0)].concat(ptsTop, [f.at(x0 + a / 2, y0 + a / 2, 0)]);
        m.chain('CA', curved ? 'Cadre (traverse haute cintrée)' : 'Cadre', fr.id, loop, { cut: curved ? 0 : a });
        var s0 = x0 + a, s1 = x1 - a;
        if (lf.tail) {
          var xs = x0 + lf.tail;
          m.member('CA', 'Montant de queue', fr.id, f.at(xs, y0 + a, 0), f.at(xs, T(xs) - a, 0));
          m.member('E', 'Écharpe de queue', p.railP, f.at(x0 + a, y0 + a, 0), f.at(xs - a / 2, T(xs) - a, 0));
          s0 = xs + a / 2;
        }
        var bot = function () { return y0 + a; }, top = function (s) { return T(s) - a; };
        var minTop = Math.min.apply(null, samples(s0, s1, 16).map(top));
        if (p.infill !== 'mixte') for (var k = 1; k <= p.rails; k++) {
          var y = bot() + k * (minTop - bot()) / (p.rails + 1);
          m.member('T', 'Traverse', p.railP, f.at(s0, y, 0), f.at(s1, y, 0));
        }
        if (p.brace && !lf.tail) {
          var hl = lf.hingeLeft; m.member('E', 'Écharpe', p.railP, f.at(hl ? s0 : s1, bot(), 0), f.at(hl ? s1 : s0, minTop, 0));
        }
        var sp = infillSpec(p); if (p.over > 0 && sp.type === 'barreaux') sp.over = p.over + a;
        var r = infill(m, { f: f, s0: s0, s1: s1, w: 0, ground: function () { return 0; }, bot: bot, top: top, curved: curved }, sp);
        gapMax = Math.max(gapMax, r.gap);
        // quincaillerie
        var hx = lf.hingeLeft ? x0 - 12 : x1 + 12;
        if (!slide) [y0 + 150, T(hx) - 200].forEach(function (y) { m.member('GO', 'Gond', 'RP20', f.at(hx, y, -40), f.at(hx, y, 40)); });
        if (p.lock && (p.type !== 'battant2' || !lf.hingeLeft)) {
          var lx = lf.hingeLeft ? x1 - a : x0 + a, ly = y0 + Math.min(1050, p.H / 2);
          m.plate('SE', 'Boîtier serrure', 'XY', [[lx - 50, ly - 100], [lx + 50, ly - 100], [lx + 50, ly + 100], [lx - 50, ly + 100]], 30, 0, 2);
        }
        var leafMass = m.members.slice(mass0).reduce(function (s, x) { return s + profile(x.profile).kgm * memberLen(x) / 1000; }, 0);
        m.value('Vantail ' + (li + 1), f0(x1 - x0) + ' mm, ' + f1(leafMass) + ' kg');
        if (!slide) {
          var hs = Math.max(T(hx) - 200 - (y0 + 150), 100), Hk = leafMass * 9.81 * (x1 - x0) / 2 / hs;
          m.value('Vantail ' + (li + 1) + ' : effort horizontal par gond', f1(Hk / 1000) + ' kN');
          if (x1 - x0 > 2500) m.warn('Largeur vantail', f0(x1 - x0) + ' mm > 2500 : affaissement, prévoir écharpe ou roulette');
          if (leafMass > 200) m.warn('Masse vantail', f0(leafMass) + ' kg : vérifier gonds et motorisation');
        }
      });
      if (p.type === 'battant2') m.member('BU', 'Butée centrale', 'RP20', f.at(p.W / 2, -60, 0), f.at(p.W / 2, 40, 0));
      if (p.type === 'coulissant') m.member('R', 'Rail au sol', 'RP20', f.at(-p.W - 100, 10, 0), f.at(p.W + 200, 10, 0));
      if (auto) {
        m.member('R', 'Rail autoportant', 'UPN80', f.at(leaves[0].x0, y0 - 45, 0), f.at(leaves[0].x1, y0 - 45, 0), { rot: 180 });
        [leaves[0].x0 + 200, leaves[0].x0 + tailL - 200].forEach(function (x) { m.plate('CH', 'Chariot à galets', 'XY', [[x - 150, -40], [x + 150, -40], [x + 150, y0 - 90], [x - 150, y0 - 90]], 120, -80, 120); });
        m.rule(p.tail >= 40, 'Queue autoportant', f0(p.tail) + ' % du passage', f0(p.tail) + ' % < 40 % : basculement possible', 'règle de l\'art', true);
      }
      var topY = y0 + p.H + Math.max(0, p.rise) + 100, ps = p.pillarSize, zw = slide ? -(fr.wa / 2 + Math.max(ps, p.wingT) / 2 + 30) : 0;
      if (p.pillars === 'maconnes') {
        [[-ps, 0], [p.W, p.W + ps]].forEach(function (xx) {
          mBox(m, 'Pilier maçonné', p.masonMat, xx[0], zw - ps / 2, xx[1], zw + ps / 2, -100, topY + 100);
          if (p.pillarCap) mBox(m, 'Chapeau de pilier', 'beton', xx[0] - 30, zw - ps / 2 - 30, xx[1] + 30, zw + ps / 2 + 30, topY, 70);
        });
        m.info('Piliers maçonnés', 'Gonds à sceller ou platines chevillées (chevilles chimiques) ; pilier en blocs à bancher armés conseillé pour un portail de plus de 100 kg.');
      }
      if (p.wingL > 0) {
        var wx = p.pillars === 'maconnes' ? ps : 0, wz = slide ? zw : 0;
        mBox(m, 'Mur de clôture', p.masonMat, -wx - p.wingL, wz - p.wingT / 2, -wx, wz + p.wingT / 2, -100, p.wingH + 100);
        mBox(m, 'Mur de clôture', p.masonMat, p.W + wx, wz - p.wingT / 2, p.W + wx + p.wingL, wz + p.wingT / 2, -100, p.wingH + 100);
        [[-wx - p.wingL, -wx], [p.W + wx, p.W + wx + p.wingL]].forEach(function (xx) { mBox(m, 'Chaperon', 'beton', xx[0], wz - p.wingT / 2 - 30, xx[1], wz + p.wingT / 2 + 30, p.wingH, 50); });
      }
      var ext = (p.pillars === 'maconnes' ? ps : 0) + Math.max(p.wingL, 500);
      mBox(m, 'Allée / sol', p.floorMat, -ext, -3000, p.W + ext, 3000, -120, 120);
      masonValue(m);
      if (p.pillars === 'acier') {
        var pp = profile(p.postP), zp = slide ? -(fr.wa / 2 + pp.wa / 2 + 30) : 0;
        [-pp.vw / 2, p.W + pp.vw / 2].forEach(function (x) { m.member('PIL', 'Pilier', pp.id, f.at(x, -p.embed, zp), f.at(x, y0 + p.H + Math.max(0, p.rise) + 100, zp)); });
      }
      m.value('Vide maxi remplissage', f1(gapMax) + ' mm');
      if (gapMax > 110) m.warn('Vides', f0(gapMax) + ' mm > 110 (passage enfant / animal)'); else m.ok('Vides', f1(gapMax) + ' mm ≤ 110');
      if (p.motor) {
        (p.type === 'battant2' ? [p.hinge, p.meet] : [p.hinge]).forEach(function (g) {
          m.rule(g >= 25, 'Écrasement des doigts', 'jeu ' + f0(g) + ' mm ≥ 25', 'jeu ' + f0(g) + ' mm < 25 mm : bord sensible / profil de sécurité', N.GAP, true);
        });
        if (slide) m.warn('Cisaillement au pilier', 'Protéger la zone de passage du remplissage devant le pilier (grillage fin, tôle).', N.PORT);
        m.info('Motorisation', 'Efforts, détection d\'obstacle, notice : NF EN 12453 ; marquage CE du portail motorisé (NF EN 13241).', N.PORT);
      } else m.info('Motorisation', 'Portail manuel. Si motorisation ultérieure : NF EN 12453.', N.PORT);
      if (curved) m.info('Cintrage', 'Traverse haute à cintrer (rouleuse / cintreuse) : développé = longueur d\'arc.');
      return m;
    }
  });

  // ================================================================== ESCALIERS — moteur générique « ligne de foulée »
  function polyLen(P) { var L = 0; for (var i = 0; i + 1 < P.length; i++) L += pdist(P[i], P[i + 1]); return L; }
  function polyAt(P, t) {
    var acc = 0;
    for (var i = 0; i + 1 < P.length; i++) { var l = pdist(P[i], P[i + 1]); if (t <= acc + l + 1e-9) return plerp(P[i], P[i + 1], l > 0 ? (t - acc) / l : 0); acc += l; }
    return P[P.length - 1].slice();
  }
  function polyTangent(P, t) {
    var acc = 0;
    for (var i = 0; i + 1 < P.length; i++) { var l = pdist(P[i], P[i + 1]); if (t <= acc + l + 1e-9) return pnorm(psub(P[i + 1], P[i])); acc += l; }
    return pnorm(psub(P[P.length - 1], P[P.length - 2]));
  }
  /** Intersection d'une demi-droite (O, D) avec une polyligne : { pt, t (abscisse curviligne), k } la plus proche (k > 0). */
  function rayPoly(O, D, P) {
    var best = null, acc = 0;
    for (var i = 0; i + 1 < P.length; i++) {
      var A = P[i], B = P[i + 1], E = psub(B, A), den = pcross(D, E), l = plen(E);
      if (Math.abs(den) > 1e-12) {
        var AO = psub(A, O), k = pcross(AO, E) / den, u = pcross(AO, D) / den;
        if (k > 1e-6 && u >= -1e-9 && u <= 1 + 1e-9 && (!best || k < best.k)) best = { pt: padd(O, pmul(D, k)), t: acc + clamp(u, 0, 1) * l, k: k };
      }
      acc += l;
    }
    return best;
  }
  function polyVertsBetween(P, t0, t1) { // sommets strictement entre t0 < t < t1
    var out = [], acc = 0;
    for (var i = 1; i + 1 < P.length; i++) { acc += pdist(P[i - 1], P[i]); if (acc > t0 + 1e-6 && acc < t1 - 1e-6) out.push(P[i]); }
    return out;
  }
  function arcPts(c, r, a0, a1, n) { var o = []; for (var i = 0; i <= n; i++) { var a = a0 + (a1 - a0) * i / n; o.push([c[0] + r * Math.cos(a), c[1] + r * Math.sin(a)]); } return o; }

  /**
   * Bande d'escalier (sens « droite » ; « gauche » = symétrie en Z) :
   *  kind 'droit' (L1 = reculement), 'quart' (emprise L1 x L2), 'deux' (L1, L2 travers, L3 retour).
   * Retourne bords intérieur / extérieur, ligne de foulée et zones tournantes (paliers potentiels).
   */
  function stairBand(kind, W, L1, L2, L3, f) {
    var inner, outer, walk, corners = [];
    if (kind === 'droit') {
      inner = [[0, W], [L1, W]]; outer = [[0, 0], [L1, 0]]; walk = [[0, W - f], [L1, W - f]];
    } else if (kind === 'quart') {
      var cx = L1 - W;
      inner = [[0, W], [cx, W], [cx, L2]]; outer = [[0, 0], [L1, 0], [L1, L2]];
      walk = [[0, W - f]].concat(arcPts([cx, W], f, -Math.PI / 2, 0, 12), [[cx + f, L2]]);
      corners.push({ poly: [[cx, 0], [L1, 0], [L1, W], [cx, W]], pa: [cx, W - f], pb: [cx + f, W] });
    } else {
      var c1 = L1 - W, zt = L2 - W;
      inner = [[0, W], [c1, W], [c1, zt], [L1 - L3, zt]]; outer = [[0, 0], [L1, 0], [L1, L2], [L1 - L3, L2]];
      walk = [[0, W - f]].concat(arcPts([c1, W], f, -Math.PI / 2, 0, 12), arcPts([c1, zt], f, 0, Math.PI / 2, 12), [[L1 - L3, zt + f]]);
      corners.push({ poly: [[c1, 0], [L1, 0], [L1, W], [c1, W]], pa: [c1, W - f], pb: [c1 + f, W] },
                   { poly: [[c1, zt], [L1, zt], [L1, L2], [c1, L2]], pa: [c1 + f, zt], pb: [c1, zt + f] });
    }
    walk = walk.filter(function (q, i) { return i === 0 || pdist(q, walk[i - 1]) > 1e-6; });
    var acc = 0, cum = [0]; for (var i = 1; i < walk.length; i++) { acc += pdist(walk[i - 1], walk[i]); cum.push(acc); }
    corners.forEach(function (c) {
      c.a = cum[walk.findIndex(function (q) { return pdist(q, c.pa) < 1e-6; })];
      c.b = cum[walk.length - 1 - walk.slice().reverse().findIndex(function (q) { return pdist(q, c.pb) < 1e-6; })];
    });
    return { inner: inner, outer: outer, walk: walk, S: polyLen(walk), corners: corners, W: W };
  }

  function stairUsageChecks(m, p, h, g, W, ang, rails) {
    var ind = p.usage === 'industriel', b = 2 * h + g;
    if (ind) m.rule(b >= 600 && b <= 660, 'Module 2h + g', f0(b) + ' mm (600–660)', f0(b) + ' mm hors 600–660', N.ISO3);
    else if (b >= 600 && b <= 650) m.ok('Loi de Blondel', '2h+g = ' + f0(b) + ' mm (600–650)');
    else if (b >= 580 && b <= 660) m.warn('Loi de Blondel', '2h+g = ' + f0(b) + ' mm hors confort 600–650');
    else m.fail('Loi de Blondel', '2h+g = ' + f0(b) + ' mm');
    switch (p.usage) {
      case 'logement':
        m.rule(h <= 180, 'Hauteur de marche', f1(h) + ' mm ≤ 180', f1(h) + ' mm > 180', N.LOG);
        m.rule(g >= 240, 'Giron (ligne de foulée)', f0(g) + ' mm ≥ 240', f0(g) + ' mm < 240', N.LOG);
        m.rule(W >= 800, 'Emmarchement', f0(W) + ' mm ≥ 800', f0(W) + ' mm < 800', N.LOG);
        break;
      case 'collectif':
        m.rule(h <= 170, 'Hauteur de marche', f1(h) + ' mm ≤ 170', f1(h) + ' mm > 170', N.LOG);
        m.rule(g >= 280, 'Giron (ligne de foulée)', f0(g) + ' mm ≥ 280', f0(g) + ' mm < 280', N.LOG);
        m.rule(rails > 0, 'Main courante', 'présente', 'obligatoire en parties communes', N.LOG);
        break;
      case 'erp':
        m.rule(h <= 160, 'Hauteur de marche', f1(h) + ' mm ≤ 160', f1(h) + ' mm > 160', N.ERP);
        m.rule(g >= 280, 'Giron (ligne de foulée)', f0(g) + ' mm ≥ 280', f0(g) + ' mm < 280', N.ERP);
        m.rule(W >= 1200, 'Largeur', f0(W) + ' mm ≥ 1200', f0(W) + ' mm < 1200 entre mains courantes', N.ERP);
        m.rule(rails >= 2, 'Mains courantes', 'des deux côtés', 'obligatoires des deux côtés', N.ERP);
        m.rule(p.railH >= 800 && p.railH <= 1000, 'Hauteur main courante', f0(p.railH) + ' mm (800–1000)', f0(p.railH) + ' mm hors 800–1000', N.ERP);
        m.info('Prolongement', 'Prolonger les mains courantes d\'un giron en haut et en bas ; nez de marche et 1re / dernière contremarche contrastés.', N.ERP);
        break;
      case 'industriel':
        m.rule(ang >= 30 && ang <= 38, 'Inclinaison', f1(ang) + '° (30–38°)', f1(ang) + '° hors 30–38° (au-delà : échelle à marches)', N.ISO3);
        m.rule(p.nosing >= 10, 'Recouvrement', f0(p.nosing) + ' mm ≥ 10', f0(p.nosing) + ' mm < 10', N.ISO3);
        break;
    }
    if (p.usage !== 'industriel' && rails > 0) m.rule(p.railH >= 900 || p.usage === 'erp', 'Hauteur rampant', f0(p.railH) + ' mm ≥ 900 à l\'aplomb du nez', f0(p.railH) + ' mm < 900', N.GC);
    if (rails === 0) m.warn('Garde-corps', 'Aucun : obligatoire côté vide (rampant ≥ 900 mm).', N.GC);
  }

  var TREADS = [['larmee', 'Tôle larmée'], ['pliee', 'Tôle pliée (plis avant / arrière)'], ['perforee', 'Tôle perforée pliée'], ['caillebotis', 'Caillebotis'], ['caillebotis-presse', 'Caillebotis pressé (maille fine)'], ['bois', 'Bois massif'], ['bois-acier', 'Bois sur cassette acier'], ['beton', 'Cassette tôle + béton'], ['pierre', 'Pierre / béton préfabriqué'], ['verre', 'Verre feuilleté']];
  function treadMaterial(type) {
    return { larmee: ['acier', 6], pliee: ['acier', 4], perforee: ['acier', 4], caillebotis: ['grillage', 30], 'caillebotis-presse': ['grillage', 30], bois: ['bois', 40], 'bois-acier': ['bois', 30], beton: ['beton', 50], pierre: ['beton', 40], verre: ['verre', 25.5] }[type] || ['acier', 6];
  }
  function stairCommonParams(kindParams) {
    return [
      { k: 'usage', l: 'Usage', t: 'sel', d: 'logement', o: [['logement', 'Intérieur d\'un logement'], ['collectif', 'Logement collectif (parties communes)'], ['erp', 'ERP neuf (accessibilité)'], ['industriel', 'Industriel (ISO 14122-3)']], g: 'Usage' },
      { k: 'H', l: 'Hauteur à monter', t: 'num', d: 2700, u: 'mm', g: 'Géométrie' },
      { k: 'h', l: 'Hauteur de marche visée', t: 'num', d: 175, u: 'mm', g: 'Géométrie' },
      { k: 'W', l: 'Emmarchement', t: 'num', d: 900, u: 'mm', g: 'Géométrie' }
    ].concat(kindParams, [
      { k: 'tread', l: 'Type de marche', t: 'sel', d: 'pliee', o: TREADS, g: 'Marches' },
      { k: 'tt', l: 'Épaisseur (0 = selon type)', t: 'num', d: 0, u: 'mm', g: 'Marches' },
      { k: 'fold', l: 'Hauteur des plis', t: 'num', d: 40, u: 'mm', g: 'Marches', show: ['tread', 'pliee', 'beton', 'perforee'] },
      { k: 'nosing', l: 'Recouvrement (nez)', t: 'num', d: 30, u: 'mm', g: 'Marches' },
      { k: 'stringer', l: 'Structure', t: 'sel', d: 'lateraux', o: [['lateraux', '2 limons latéraux'], ['central', 'Limon central + consoles'], ['cremaillere', 'Crémaillères (droit)'], ['exterieur', 'Limon extérieur seul (mur côté intérieur)']], g: 'Limons' },
      { k: 'sp', l: 'Profil limon', t: 'prof', d: '__auto', auto: 'limon', list: 'limon', g: 'Limons' },
      { k: 'spc', l: 'Profil limon central', t: 'prof', d: '__auto', auto: 'limon', list: 'limonCentral', g: 'Limons', show: ['stringer', 'central'] },
      { k: 'dTop', l: 'Débord limon au-dessus du nez', t: 'num', d: 40, u: 'mm', g: 'Limons' },
      { k: 'landingFrame', l: 'Cadre de palier', t: 'prof', d: 'UPN120', list: 'poutre', g: 'Limons' },
      { k: 'rails', l: 'Garde-corps rampant', t: 'sel', d: 'ext', o: [['ext', 'Côté extérieur'], ['int', 'Côté intérieur'], ['both', 'Deux côtés'], ['none', 'Aucun']], g: 'Garde-corps' },
      { k: 'railH', l: 'Hauteur main courante (aplomb nez)', t: 'num', d: 900, u: 'mm', g: 'Garde-corps' },
      { k: 'post', l: 'Profil poteau', t: 'prof', d: 'TC40x40x4', list: 'poteau', g: 'Garde-corps' },
      { k: 'mc', l: 'Profil main courante', t: 'prof', d: 'TRD42.4x2', list: 'mainCourante', g: 'Garde-corps' },
      { k: 'emax', l: 'Entraxe maxi poteaux', t: 'num', d: 1200, u: 'mm', g: 'Garde-corps' }
    ], infillParams('Garde-corps', 'barreaux').filter(function (x) { return x.k !== 'infill'; }).map(function (x) { return x; }), [
      { k: 'infill', l: 'Remplissage du rampant', t: 'sel', d: 'barreaux', o: INFILLS.filter(function (x) { return ['barreaux', 'lisses', 'cables', 'verre', 'tole', 'perforee', 'aucun', 'industriel'].indexOf(x[0]) >= 0; }), g: 'Garde-corps' },
      { k: 'mcMural', l: 'Main courante murale', t: 'sel', d: 'aucun', o: [['aucun', 'Aucune'], ['ext', 'Côté extérieur'], ['int', 'Côté intérieur'], ['both', 'Deux côtés']], g: 'Garde-corps' },
      { k: 'mcMuralP', l: 'Profil main courante murale', t: 'prof', d: 'TRD42.4x2', list: 'mainCourante', g: 'Garde-corps', show: ['mcMural', 'ext', 'int', 'both'] },
      { k: 'qk', l: 'Charge d\'exploitation (0 = selon usage)', t: 'num', d: 0, u: 'kN/m²', step: 0.5, g: 'Résistance' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ], masonParams({ mat: 'enduit-blanc', floor: 'carrelage', extra: [
      { k: 'floorLow', l: 'Dalle basse (sol fini)', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'upper', l: 'Plancher haut avec trémie', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'slabT', l: 'Épaisseur du plancher haut', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['upper', true] },
      { k: 'walls', l: 'Murs d\'échiffre', t: 'sel', d: 'aucun', o: [['aucun', 'Aucun'], ['ext', 'Côté extérieur'], ['int', 'Côté intérieur'], ['both', 'Deux côtés (cage)']], g: 'Maçonnerie & contexte' },
      { k: 'wallT', l: 'Épaisseur des murs', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['walls', 'ext', 'int', 'both'] }
    ] }));
  }

  function polyPt(P, t) { if (t >= 0) return polyAt(P, t); return padd(P[0], pmul(pnorm(psub(P[1], P[0])), t)); }
  function clipY0(poly) { // Sutherland–Hodgman : conserve y ≥ 0 (coupe d'aplomb au sol)
    var out = [];
    for (var i = 0; i < poly.length; i++) {
      var a = poly[i], b = poly[(i + 1) % poly.length], ina = a[1] >= -1e-9, inb = b[1] >= -1e-9;
      if (ina) out.push(a);
      if (ina !== inb) { var k = a[1] / (a[1] - b[1]); out.push([a[0] + (b[0] - a[0]) * k, 0]); }
    }
    return out;
  }
  /**
   * ATOME « limon tôle » : contour exact par tronçon droit du bord (coupe au sol, coupe verticale au chevêtre,
   * ressaut aux paliers), modélisé en tôle et livré en développé avec traçage des marches.
   */
  function stringerPlates(m, p, band, L, nT, h, landIdx, inner, sp, tv) {
    var P = inner ? band.inner : band.outer, ts = L.map(function (l) { return (inner ? l.I : l.O).t; }), T = sp.T, D = sp.sh;
    var nose = [[ts[0] - (ts[1] - ts[0]), 0]];
    for (var k = 0; k <= nT; k++) {
      nose.push([ts[k], (k + 1) * h]);
      if (landIdx[k] && k + 2 <= nT) { var gN = ts[k + 2] - ts[k + 1], tf = ts[k + 1] - gN; if (tf > ts[k] + 1) nose.push([tf, (k + 1) * h]); }
    }
    function seg(t) { for (var i = 0; i + 1 < nose.length; i++) if (t <= nose[i + 1][0] + 1e-9) return i; return nose.length - 2; }
    function yN(t) { var i = seg(t), a = nose[i], b = nose[i + 1], d = b[0] - a[0]; return d > 1e-9 ? a[1] + (b[1] - a[1]) * (t - a[0]) / d : b[1]; }
    function slope(t) { var i = seg(t), a = nose[i], b = nose[i + 1], d = b[0] - a[0]; return d > 1e-9 ? (b[1] - a[1]) / d : 0; }
    var top = function (t) { return yN(t) + p.dTop; }, bot = function (t, s2) { return top(t) - D * Math.sqrt(1 + s2 * s2); };
    var cum = [0]; for (var i = 1; i < P.length; i++) cum.push(cum[i - 1] + pdist(P[i - 1], P[i]));
    var tS = nose[0][0], tE = ts[nT], name = inner ? 'intérieur' : 'extérieur', pieces = [];
    for (i = 0; i + 1 < P.length; i++) {
      var a = i === 0 ? Math.min(tS, 0) : cum[i], b = cum[i + 1]; a = Math.max(a, tS); b = Math.min(b, tE); if (b - a < 1) continue;
      pieces.push({ i: i, a: a, b: b });
    }
    pieces.forEach(function (pc, pi) {
      var d = pnorm(psub(P[pc.i + 1], P[pc.i])), n = [-d[1], d[0]], out = inner ? n : pmul(n, -1);
      var plane = Math.abs(d[1]) < 1e-9 ? 'XY' : Math.abs(d[0]) < 1e-9 ? 'YZ' : null; if (!plane) return;
      var bps = [pc.a, pc.b].concat(nose.map(function (q) { return q[0]; }).filter(function (t) { return t > pc.a + 1e-6 && t < pc.b - 1e-6; })).sort(function (x, y) { return x - y; });
      var lower = bps.map(function (t, j) { var s1 = slope(Math.max(pc.a, t - 1)), s2 = slope(Math.min(pc.b, t + 1)); return [t, Math.min(bot(t, s1), bot(t, s2))]; });
      var poly = clipY0(lower.concat(bps.slice().reverse().map(function (t) { return [t, top(t)]; })));
      if (poly.length < 3) return;
      var U = function (t) { var q = polyPt(P, t); return plane === 'XY' ? q[0] : q[1]; };
      var off = plane === 'XY' ? P[pc.i][1] + out[1] * T / 2 : P[pc.i][0] + out[0] * T / 2;
      m.plate('LIM', 'Limon tôle', plane, poly.map(function (q) { return [U(q[0]), q[1]]; }), T, off, T);
      var sL = function (t) { return t - pc.a; }, lines = [], table = [];
      for (var k2 = 0; k2 < nT; k2++) {
        if (ts[k2] < pc.a - 1e-6 || ts[k2] >= pc.b - 1e-6) continue;
        var y = (k2 + 1) * h, x0 = sL(ts[k2]) - (landIdx[k2] ? 0 : p.nosing), x1 = sL(Math.min(ts[k2 + 1], pc.b));
        lines.push({ a: [x0, y], b: [x1, y], layer: 'TRACAGE', label: landIdx[k2] ? 'Palier' : 'M' + (k2 + 1) });
        lines.push({ a: [x0, y - tv], b: [x1, y - tv], layer: 'TRACAGE' });
        table.push([landIdx[k2] ? 'Palier' : 'M' + (k2 + 1), round(x0, 1), round(y, 1), round(x1 - x0, 1)]);
      }
      var xs = poly.map(function (q) { return sL(q[0]); }), dev = Math.max.apply(null, xs) - Math.min.apply(null, xs);
      m.flat({ name: 'Limon ' + name + (pieces.length > 1 ? ' — tronçon ' + (pi + 1) + '/' + pieces.length : '') + ' · ' + sp.label + ' (débit ' + f0(dev) + ' mm)',
               outline: poly.map(function (q) { return [sL(q[0]), q[1]]; }), lines: lines, table: table, cols: ['Marche', 'Nez à (mm)', 'Dessus à (mm)', 'Profondeur'],
               texts: [{ at: [0, -40], text: 'Niveau 0 = sol fini ; traits = dessus et dessous de marche' }] });
    });
  }

  /**
   * ASSEMBLAGE « volée » générique : construit marches, paliers, limons et garde-corps sur une bande.
   * mode : 'palier' | 'balance' ; kb : marches balancées de part et d'autre des zones tournantes.
   */
  function buildStair(m, p, band, mode, kb) {
    var n = Math.max(2, Math.round(p.H / p.h)), h = p.H / n, nT = n - 1, S = band.S;
    var tm = treadMaterial(p.tread), tt = p.tt > 0 ? p.tt : tm[1];
    var land = mode === 'palier' ? band.corners.slice() : [];
    if (mode === 'palier' && land.length === 2 && land[1].a - land[0].b < 0.6 * (S / nT) * 2) { // demi-tournant : palier unique
      land = [{ a: land[0].a, b: land[1].b, poly: [land[0].poly[0], land[0].poly[1], land[1].poly[2], land[1].poly[3]] }];
    }
    // abscisses des lignes de nez (nT + 1 lignes)
    var lines = [0], nLand = land.length, stTreads = nT - nLand;
    if (stTreads < 1) throw new Error('Pas assez de marches pour les paliers : augmenter la hauteur ou réduire les paliers.');
    if (mode === 'palier' && nLand) {
      var portions = [], cur = 0;
      land.forEach(function (L) { portions.push([cur, L.a]); cur = L.b; }); portions.push([cur, S]);
      var tot = portions.reduce(function (s2, q) { return s2 + (q[1] - q[0]); }, 0), ks = portions.map(function (q) { return (q[1] - q[0]) / tot * stTreads; });
      var ki = ks.map(Math.floor), rem = stTreads - ki.reduce(function (a, b) { return a + b; }, 0);
      ks.map(function (v, i) { return [v - ki[i], i]; }).sort(function (a, b) { return b[0] - a[0]; }).slice(0, rem).forEach(function (x) { ki[x[1]]++; });
      var goings = [];
      portions.forEach(function (q, i) {
        for (var j = 1; j <= ki[i]; j++) lines.push(q[0] + (q[1] - q[0]) * j / ki[i]);
        if (ki[i] > 0) goings.push((q[1] - q[0]) / ki[i]);
        if (i < land.length) lines.push(land[i].b); // le palier est une « marche » de la ligne en L.a (fin de volée) à L.b
      });
      lines = lines.filter(function (v, i) { return i === 0 || v - lines[i - 1] > 1e-6; });
      if (lines[lines.length - 1] < S - 1e-6) lines.push(S);
      var g = goings.reduce(function (a, b) { return a + b; }, 0) / goings.length;
      if (goings.length > 1 && Math.max.apply(null, goings) - Math.min.apply(null, goings) > 5)
        m.warn('Régularité du giron', 'giron variable entre volées : ' + goings.map(f0).join(' / ') + ' mm — ajuster les longueurs');
    } else { g = S / nT; for (var j = 1; j <= nT; j++) lines.push(j * g); }
    var landIdx = {}; // index de marche → palier
    land.forEach(function (L) { for (var k = 0; k + 1 < lines.length; k++) if (Math.abs(lines[k + 1] - L.b) < 1e-3) { landIdx[k] = L; break; } });

    // zones de balancement
    var zones = [];
    if (mode === 'balance') band.corners.forEach(function (c) {
      var A = Math.max(0, c.a - kb * g), B = Math.min(S, c.b + kb * g);
      if (zones.length && A <= zones[zones.length - 1].B) zones[zones.length - 1].B = B; else zones.push({ A: A, B: B });
    });
    function perp(s) {
      var W0 = polyAt(band.walk, s), tg = polyTangent(band.walk, s), nn = [-tg[1], tg[0]];
      var I = rayPoly(W0, nn, band.inner), O = rayPoly(W0, pmul(nn, -1), band.outer);
      return { W: W0, t: tg, I: I, O: O };
    }
    zones.forEach(function (z) { z.tA = perp(z.A).I.t; z.tB = perp(z.B).I.t; });
    var L = lines.map(function (s) {
      var z = zones.filter(function (zz) { return s > zz.A + 1e-6 && s < zz.B - 1e-6; })[0], r = perp(s);
      if (z) {
        var ti = z.tA + (s - z.A) / (z.B - z.A) * (z.tB - z.tA), Ip = polyAt(band.inner, ti), dir = pnorm(psub(r.W, Ip));
        var O = rayPoly(Ip, dir, band.outer);
        r = { W: r.W, t: r.t, I: { pt: Ip, t: ti }, O: O || r.O };
      }
      if (!r.I || !r.O) throw new Error('Géométrie impossible (ligne de foulée hors de la bande) : vérifier largeur et longueurs.');
      return r;
    });

    // marches et paliers
    var innerGoings = [];
    for (var k = 0; k < nT; k++) {
      var a = L[k], b = L[k + 1], top = (k + 1) * h, isL = !!landIdx[k];
      var back = pmul(a.t, -p.nosing);
      var poly = [padd(a.I.pt, back), padd(a.O.pt, back)].concat(polyVertsBetween(band.outer, a.O.t, b.O.t), [b.O.pt, b.I.pt], polyVertsBetween(band.inner, a.I.t, b.I.t).reverse());
      if (isL) poly = landIdx[k].poly;
      var th = isL ? Math.max(tt, 6) : tt;
      m.plate(isL ? 'PA' : 'M', isL ? 'Palier' : 'Marche', 'XZ', poly, th, top - th / 2, th, isL ? (p.tread === 'bois' ? 'bois' : tm[0] === 'grillage' ? 'grillage' : 'acier') : tm[0],
              tm[0] === 'grillage' ? { kgm2: 25 } : null);
      if ((p.tread === 'pliee' || p.tread === 'perforee') && !isL) m.plate('M', 'Pli de marche', 'XZ', [padd(a.I.pt, back), padd(a.O.pt, back), padd(a.O.pt, padd(back, pmul(a.t, tt))), padd(a.I.pt, padd(back, pmul(a.t, tt)))], p.fold, top - p.fold / 2, tt);
      if (isL) {
        var lp = landIdx[k].poly.concat([landIdx[k].poly[0]]).map(function (q) { return [q[0], top - th - profile(p.landingFrame).sh / 2, q[1]]; });
        m.chain('CP', 'Cadre de palier', p.landingFrame, lp, { cut: profile(p.landingFrame).vw });
        var oc = landIdx[k].poly[1]; m.member('PP', 'Poteau de palier', 'TC80x80x4', [oc[0], 0, oc[1]], [oc[0], top - th - profile(p.landingFrame).sh, oc[1]]);
      } else innerGoings.push(pdist(a.I.pt, b.I.pt));
    }

    // limons : chaînes par volée, interrompues aux paliers
    var sp = profile(p.stringer === 'central' ? p.spc : p.sp), dv = p.dTop - sp.sh / 2;
    function edgeChains(getPt) {
      var chains = [], cur = null;
      for (var k = 0; k <= nT; k++) {
        var E = getPt(L[k]), y = (k + 1) * h + dv;
        if (landIdx[k - 1]) { // sortie de palier : départ au niveau du palier
          var E2 = getPt(L[Math.min(k + 1, nT)]), back2 = psub(E, psub(E2, E));
          cur = [[back2[0], k * h + dv, back2[1]]]; chains.push(cur);
        }
        if (!cur) { // départ posé au sol : l'axe descend à sp.sh/2 le long de la ligne de nez prolongée
          var E1 = getPt(L[1]), ge = pdist(E1, E), dd = (h + dv - sp.sh / 2) / (h / Math.max(ge, 1)), st0 = psub(E, pmul(pnorm(psub(E1, E)), dd));
          cur = [[st0[0], sp.sh / 2, st0[1]]]; chains.push(cur); m.info('Limon profilé', 'Coupes d\'about (sol et chevêtre) à ajuster à l\'assemblage ; préférer un limon tôle (plat) pour un débit laser exact.'); }
        cur.push([E[0], y, E[1]]);
        if (landIdx[k]) cur = null;
      }
      return chains;
    }
    var mid = function (l) { return plerp(l.I.pt, l.O.pt, 0.5); };
    var edges = p.stringer === 'central' ? [mid] : p.stringer === 'exterieur' ? [function (l) { return l.O.pt; }] : [function (l) { return l.I.pt; }, function (l) { return l.O.pt; }];
    if (p.stringer === 'cremaillere' && band.corners.length === 0) {
      [0.2, 0.8].forEach(function (fz) {
        var z = band.W * fz, o = [], D = sp.sh;
        o.push([lines[0] - g, 0]);
        for (var k = 0; k < nT; k++) { o.push([lines[k] - p.nosing, k * h + h - tt]); o.push([lines[k + 1], k * h + h - tt]); }
        o.push([lines[nT], p.H]); var ang = Math.atan2(p.H, lines[nT] - lines[0] + g), dx = D / Math.sin(ang);
        o.push([lines[nT], p.H - D / Math.cos(ang)]); o.push([lines[0] - g + dx, 0]);
        m.plate('CR', 'Crémaillère', 'XY', o, sp.T || 12, z, sp.T || 12);
        if (fz === 0.2) m.flat({ name: 'Crémaillère (x2) · ' + sp.label, outline: o.map(function (q) { return [q[0] - (lines[0] - g), q[1]]; }), lines: [], texts: [] });
      });
    } else if (sp.fam === 'PL' && (p.stringer === 'lateraux' || p.stringer === 'exterieur')) {
      var tvis = (p.tread === 'pliee' || p.tread === 'beton' || p.tread === 'perforee') ? Math.max(p.fold, tt) : tt, dmin = p.dTop + tvis + 100;
      m.rule(sp.sh >= dmin, 'Hauteur du limon', f0(sp.sh) + ' mm ≥ ' + f0(dmin) + ' (débord + marche + fixation)', f0(sp.sh) + ' mm < ' + f0(dmin) + ' mm : pas la place de fixer les marches', 'règle de construction');
      m.rule(sp.T >= 8, 'Épaisseur du limon', f0(sp.T) + ' mm ≥ 8', f0(sp.T) + ' mm < 8 mm : soudure et rigidité insuffisantes', 'règle de construction');
      if (p.stringer === 'lateraux') stringerPlates(m, p, band, L, nT, h, landIdx, true, sp, tvis);
      stringerPlates(m, p, band, L, nT, h, landIdx, false, sp, tvis);
    } else edges.forEach(function (fn, ei) {
      var off = p.stringer === 'central' ? 0 : (ei === 0 && edges.length === 2 ? 1 : -1) * sp.wa / 2;
      edgeChains(function (l) {
        var q = fn(l); if (!off) return q;
        var side = pnorm(psub(l.I.pt, l.O.pt)); // vers l'intérieur
        return padd(q, pmul(side, (fn === edges[0] && edges.length === 2) ? sp.wa / 2 : -sp.wa / 2));
      }).forEach(function (ch) { m.chain('LIM', p.stringer === 'central' ? 'Limon central' : 'Limon', sp.id, ch); });
    });
    if (p.stringer === 'central') for (k = 0; k < nT; k++) if (!landIdx[k]) {
      var ii = plerp(L[k].I.pt, L[k + 1].I.pt, 0.5), oo = plerp(L[k].O.pt, L[k + 1].O.pt, 0.5), y2 = (k + 1) * h - tt - 20;
      m.member('CO', 'Console', 'TR60x40x3', [plerp(ii, oo, 0.06)[0], y2, plerp(ii, oo, 0.06)[1]], [plerp(ii, oo, 0.94)[0], y2, plerp(ii, oo, 0.94)[1]]);
    }

    // garde-corps rampants le long des bords
    var railSides = p.rails === 'both' ? ['int', 'ext'] : p.rails === 'none' ? [] : [p.rails];
    railSides.forEach(function (sd) {
      var pts = [], gy = [];
      for (var k = 0; k <= nT; k++) {
        var E = sd === 'int' ? L[k].I : L[k].O;
        pts.push(E.pt); gy.push((k + 1) * h);
        if (landIdx[k]) { var Pl = sd === 'int' ? band.inner : band.outer, nx = sd === 'int' ? L[k + 1].I : L[k + 1].O;
          polyVertsBetween(Pl, E.t, nx.t).forEach(function (v) { pts.push(v); gy.push((k + 1) * h); }); }
      }
      // dédoublonne
      var P2 = [], G2 = []; pts.forEach(function (q, i) { if (!P2.length || pdist(q, P2[P2.length - 1]) > 1) { P2.push(q); G2.push(gy[i]); } });
      var off = (p.stringer === 'central' ? 0 : sp.wa) + 4;
      var st = railRun(m, P2, G2, { H: p.railH, post: p.post, emax: p.emax, mc: p.mc, mcPos: 'dessus', lb: true, lbProf: 'TR40x20x2', jeu: 60,
                                     pose: 'anglaise', drop: Math.min(150, sp.sh * 0.6), plate: null, spec: infillSpec(p), side: sd === 'int' ? 1 : -1, wExtra: off });
      m.value('Garde-corps ' + (sd === 'int' ? 'intérieur' : 'extérieur'), st.posts + ' poteaux, vide ' + f1(st.maxGap) + ' mm');
      if (st.maxGap > (p.usage === 'industriel' ? 500 : 110)) m.fail('Vides du garde-corps rampant', f1(st.maxGap) + ' mm', N.GC);
      if (st.horizLowest != null && st.horizLowest <= 600 && p.usage !== 'industriel') m.fail('Zone basse du rampant', 'éléments horizontaux escaladables', N.GC);
    });

    var ang = Math.atan2(h, g) / DEG;
    m.value('Hauteurs / marches', n + ' / ' + nT); m.value('Hauteur de marche h', f1(h) + ' mm'); m.value('Giron (ligne de foulée)', f1(g) + ' mm');
    m.value('2h + g', f0(2 * h + g) + ' mm'); m.value('Inclinaison', f1(ang) + '°'); m.value('Ligne de foulée', f0(S) + ' mm');
    if (innerGoings.length && band.corners.length) {
      var gi = Math.min.apply(null, innerGoings); m.value('Giron mini côté intérieur', f0(gi) + ' mm');
      if (mode === 'balance') m.rule(gi >= 100, 'Giron intérieur des marches balancées', f0(gi) + ' mm ≥ 100 (usuel)', f0(gi) + ' mm < 100 : augmenter le nombre de marches balancées', 'règle de l\'art', true);
    }
    stairUsageChecks(m, p, h, g, band.W, ang, railSides.length);
    // résistance limons : volée droite la plus longue
    var qk = p.qk > 0 ? p.qk : (p.usage === 'logement' || p.usage === 'collectif' ? 2.5 : 4.0);
    var run = 0; for (k = 0; k < nT; k++) if (!landIdx[k]) run += g;
    var flight = band.corners.length ? run / (band.corners.length + 1) : run + g, ls = flight / Math.cos(ang * DEG);
    var tot = totals(m), nStr = p.stringer === 'central' ? 1 : p.stringer === 'exterieur' ? 1.5 : 2;
    var gk = tot.mass * 9.81 / Math.max(run, 1) / nStr, qkl = qk * 1e-3 * band.W / nStr, pEd = 1.35 * gk + 1.5 * qkl;
    if (p.stringer !== 'cremaillere') {
      bendCheck(m, 'Résistance limon (volée ' + f0(flight) + ' mm)', pEd * flight * flight / 8, sp.sec.Wy, p.grade);
      deflCheck(m, 'Flèche limon', 5 * (gk + qkl) * Math.pow(Math.cos(ang * DEG), 2) * Math.pow(ls, 4) / (384 * E_ACIER * sp.sec.Iy), ls, 300);
    }
    if (p.tread === 'verre') m.info('Marche verre', 'Verre feuilleté trempé (ex. 3 x 8 mm) : résistance et glissance à justifier (avis technique / essais).');
    if (p.tread === 'larmee' || p.tread === 'pliee' || p.tread === 'perforee') {
      var ts = p.tread !== 'larmee' ? rects([[0, 0, g + p.nosing, tt], [0, tt, tt, p.fold - tt], [g + p.nosing - tt, tt, tt, p.fold - tt]]) : rects([[0, 0, g + p.nosing, tt]]);
      var span = band.W - (p.stringer === 'central' ? band.W * 0.5 : 0);
      bendCheck(m, 'Résistance marche', Math.max(1.5 * qk * 1e-3 * (g + p.nosing) * span * span / 8, 1.5 * 2000 * span / 4), ts.Wy, p.grade);
      deflCheck(m, 'Flèche marche (2 kN au centre)', 2000 * Math.pow(span, 3) / (48 * E_ACIER * ts.Iy), span, 250);
      if (p.tread === 'larmee' && m.checks.some(function (c) { return c.level === 'fail' && /marche/.test(c.rule); })) m.info('Conseil', 'Tôle larmée plane trop souple : choisir « Tôle pliée » ou ajouter une cornière de raidissage sous le nez.');
      if (p.tread === 'pliee' || p.tread === 'perforee') m.flat(treadFlat(g + p.nosing, band.W - 4, p.fold, tt));
    }
    m.info('Charges', 'qk = ' + f1(qk) + ' kN/m², Qk = 2 kN ; γG = 1,35, γQ = 1,5 (à confirmer : NF P06-111-2)', N.EC1);
    m.info('Exécution', 'Escalier = ouvrage porteur : NF EN 1090-2, EXC2 par défaut.', N.EXE);
    return { n: n, h: h, g: g, lines: lines, L: L, nT: nT, landIdx: landIdx };
  }

  /** Symétrie Z (sens « gauche »). */
  function mirrorZ(m) {
    m.members.forEach(function (x) { x.a[2] = -x.a[2]; x.b[2] = -x.b[2]; });
    m.plates.forEach(function (p) {
      if (p.plane === 'XZ') p.outline = p.outline.map(function (q) { return [q[0], -q[1]]; });
      else if (p.plane === 'XY') p.offset = -p.offset;
      else p.outline = p.outline.map(function (q) { return [-q[0], q[1]]; });
    });
    m.surfaces.forEach(function (s) { ['a', 'b', 'base'].forEach(function (k) { if (s[k]) s[k][2] = -s[k][2]; }); });
    m.masonry.forEach(function (x) { x.poly = x.poly.map(function (q) { return [q[0], -q[1]]; }); });
  }

  function straightRunFor(p) { // reculement de la ligne de foulée
    var n = Math.max(2, Math.round(p.H / p.h)), h = p.H / n;
    return (p.runMode === 'reculement' && p.run > 0) ? p.run : (n - 1) * (p.blondel - 2 * h);
  }
  function headroomStraight(m, p, h, g, run) {
    if (!(p.opening > 0)) return;
    var xEdge = run - p.nosing - p.opening, y = h / g * (xEdge + p.nosing) + h, hr = p.H - p.slab - y;
    if (xEdge < -p.nosing - g) { m.ok('Échappée', 'trémie plus longue que l\'escalier'); return; }
    m.value('Échappée', f0(hr) + ' mm');
    if (hr >= 2000) m.ok('Échappée', f0(hr) + ' mm ≥ 2000'); else if (hr >= 1900) m.warn('Échappée', f0(hr) + ' mm : 2000 conseillé'); else m.fail('Échappée', f0(hr) + ' mm < 1900 : allonger la trémie');
  }
  function stringerFlat(m, p, r) { // limon tôle droit : contour de découpe + traçage des marches
    var sp = profile(p.sp); if (sp.fam !== 'PL') return;
    var tan = r.h / r.g, cos = Math.cos(Math.atan(tan)), xA = -r.g - p.nosing, top = p.dTop, bot = top - sp.sh / cos, xE = r.lines[r.nT] - p.nosing;
    var xy = function (y, off) { return (y - off) / tan + xA; }, yx = function (x, off) { return tan * (x - xA) + off; };
    var o = [[xy(0, bot), 0], [xE, yx(xE, bot)], [xE, yx(xE, top)], [xy(0, top), 0]];
    var lines = [];
    for (var k = 0; k < r.nT; k++) { var x = r.lines[k] - p.nosing, y = (k + 1) * r.h; lines.push({ a: [x, y], b: [x + r.g + p.nosing, y], layer: 'TRACAGE', label: 'M' + (k + 1) }); }
    m.flat({ name: 'Limon tôle ' + sp.label + ' (x2, symétriques)', outline: o, lines: lines, texts: [{ at: [o[0][0], -60], text: r.nT + ' marches h=' + f1(r.h) + ' g=' + f1(r.g) }] });
  }

  var KIND_PARAMS = {
    droit: [
      { k: 'runMode', l: 'Dimensionnement', t: 'sel', d: 'blondel', o: [['blondel', 'Par la loi de Blondel'], ['reculement', 'Reculement imposé']], g: 'Géométrie' },
      { k: 'blondel', l: 'Module 2h + g visé', t: 'num', d: 630, u: 'mm', g: 'Géométrie', show: ['runMode', 'blondel'] },
      { k: 'run', l: 'Reculement', t: 'num', d: 3800, u: 'mm', g: 'Géométrie', show: ['runMode', 'reculement'] },
      { k: 'opening', l: 'Longueur de trémie (0 = non vérifiée)', t: 'num', d: 3400, u: 'mm', g: 'Trémie' },
      { k: 'slab', l: 'Épaisseur plancher', t: 'num', d: 200, u: 'mm', g: 'Trémie' }
    ],
    quart: [
      { k: 'L1', l: 'Emprise volée 1 (hors tout)', t: 'num', d: 2600, u: 'mm', g: 'Géométrie' },
      { k: 'L2', l: 'Emprise volée 2 (hors tout)', t: 'num', d: 2200, u: 'mm', g: 'Géométrie' },
      { k: 'turn', l: 'Sens', t: 'sel', d: 'gauche', o: [['gauche', 'Tournant à gauche'], ['droite', 'Tournant à droite']], g: 'Géométrie' },
      { k: 'mode', l: 'Partie tournante', t: 'sel', d: 'balance', o: [['balance', 'Marches balancées'], ['palier', 'Palier']], g: 'Géométrie' },
      { k: 'kb', l: 'Marches balancées de part et d\'autre', t: 'num', d: 3, g: 'Géométrie', show: ['mode', 'balance'] },
      { k: 'walkOff', l: 'Ligne de foulée depuis l\'intérieur', t: 'num', d: 500, u: 'mm', g: 'Géométrie' }
    ],
    deux: [
      { k: 'L1', l: 'Emprise volée 1', t: 'num', d: 1900, u: 'mm', g: 'Géométrie' },
      { k: 'L2', l: 'Largeur de cage (travers)', t: 'num', d: 2100, u: 'mm', g: 'Géométrie' },
      { k: 'L3', l: 'Emprise volée 3', t: 'num', d: 1800, u: 'mm', g: 'Géométrie' },
      { k: 'turn', l: 'Sens', t: 'sel', d: 'gauche', o: [['gauche', 'Tournant à gauche'], ['droite', 'Tournant à droite']], g: 'Géométrie' },
      { k: 'mode', l: 'Parties tournantes', t: 'sel', d: 'balance', o: [['balance', 'Marches balancées'], ['palier', 'Paliers']], g: 'Géométrie' },
      { k: 'kb', l: 'Marches balancées de part et d\'autre', t: 'num', d: 2, g: 'Géométrie', show: ['mode', 'balance'] },
      { k: 'walkOff', l: 'Ligne de foulée depuis l\'intérieur', t: 'num', d: 500, u: 'mm', g: 'Géométrie' }
    ],
    demi: [
      { k: 'L1', l: 'Emprise des volées', t: 'num', d: 2790, u: 'mm', g: 'Géométrie' },
      { k: 'jour', l: 'Jour entre volées', t: 'num', d: 100, u: 'mm', g: 'Géométrie' },
      { k: 'turn', l: 'Sens', t: 'sel', d: 'gauche', o: [['gauche', 'Tournant à gauche'], ['droite', 'Tournant à droite']], g: 'Géométrie' },
      { k: 'mode', l: 'Demi-tour', t: 'sel', d: 'palier', o: [['palier', 'Palier intermédiaire'], ['balance', 'Marches balancées']], g: 'Géométrie' },
      { k: 'kb', l: 'Marches balancées de part et d\'autre', t: 'num', d: 2, g: 'Géométrie', show: ['mode', 'balance'] },
      { k: 'walkOff', l: 'Ligne de foulée depuis l\'intérieur', t: 'num', d: 450, u: 'mm', g: 'Géométrie' }
    ]
  };

  function offsetPoly(P, d) { // décalage d'une polyligne plane (normale gauche × d)
    return P.map(function (q, i) {
      var a = i > 0 ? pnorm(psub(q, P[i - 1])) : pnorm(psub(P[1], q)), b = i + 1 < P.length ? pnorm(psub(P[i + 1], q)) : a;
      var n1 = [-a[1], a[0]], n2 = [-b[1], b[0]], nn = pnorm(padd(n1, n2)), k = 1 / Math.max(0.3, nn[0] * n1[0] + nn[1] * n1[1]);
      return padd(q, pmul(nn, d * k));
    });
  }
  /** Contexte maçonné d'un escalier : sols, plancher haut avec trémie, murs d'échiffre, main courante murale. */
  function stairMasonry(m, p, band, r, kind) {
    var all = band.inner.concat(band.outer), x0 = Math.min.apply(null, all.map(function (q) { return q[0]; })), x1 = Math.max.apply(null, all.map(function (q) { return q[0]; }));
    var z0 = Math.min.apply(null, all.map(function (q) { return q[1]; })), z1 = Math.max.apply(null, all.map(function (q) { return q[1]; })), M = 1200;
    var sp = profile(p.stringer === 'central' ? p.spc : p.sp), st = sp.fam === 'PL' ? sp.T : sp.wa, slabT = kind === 'droit' ? (p.slab || 200) : p.slabT, x0r = x0 - r.g - p.nosing;
    if (p.floorLow) { mBox(m, 'Dalle basse', 'beton', x0r - M, z0 - M, x1 + M, z1 + M, -220, 200); mBox(m, 'Revêtement', p.floorMat, x0r - M, z0 - M, x1 + M, z1 + M, -20, 20); }
    if (p.upper) {
      var S = r.lines[r.nT], hole = kind === 'droit' ? [p.opening > 0 ? S - p.opening : x0r - 50, z0 - st - 150, S, z1 + st + 150] : [x0 - st - 150, z0 - st - 150, x1 + st + 150, z1 + st + 150];
      if (kind === 'quart') hole[3] = z1; if (kind !== 'droit' && kind !== 'quart') hole[0] = x0;
      mSlab(m, 'Plancher haut', 'beton', [x0r - M, z0 - M, x1 + M, z1 + M], hole, p.H - slabT, slabT - 20);
      mSlab(m, 'Revêtement', p.floorMat, [x0r - M, z0 - M, x1 + M, z1 + M], hole, p.H - 20, 20);
      m.value('Trémie', f0(hole[2] - hole[0]) + ' x ' + f0(hole[3] - hole[1]) + ' mm');
    }
    var top = p.upper ? p.H - slabT : p.H + 1200, bot = p.floorLow ? -220 : 0, sides = p.walls === 'both' ? ['int', 'ext'] : p.walls === 'aucun' ? [] : [p.walls];
    sides.forEach(function (sd) {
      var P = sd === 'int' ? band.inner : band.outer, sg = sd === 'int' ? 1 : -1;
      for (var i = 0; i + 1 < P.length; i++) mStrip(m, 'Mur d\'échiffre', p.masonMat, P[i], P[i + 1], sg * (st + 10), sg * (st + 10 + p.wallT), bot, top - bot, i === 0 ? 0 : p.wallT);
    });
    var mur = p.mcMural === 'both' ? ['int', 'ext'] : p.mcMural === 'aucun' ? [] : [p.mcMural];
    mur.forEach(function (sd) {
      var pts = [], gy = [], sg = sd === 'int' ? 1 : -1, mp = profile(p.mcMuralP);
      for (var k = 0; k <= r.nT; k++) { var E = sd === 'int' ? r.L[k].I : r.L[k].O; if (!pts.length || pdist(E.pt, pts[pts.length - 1]) > 1) { pts.push(E.pt); gy.push((k + 1) * r.h); } }
      var off = offsetPoly(pts, sg * (st + 10 - 70 - mp.wa / 2)), line = off.map(function (q, i) { return [q[0], gy[i] + 900 - mp.sh / 2, q[1]]; });
      m.chain('MCM', 'Main courante murale', mp.id, line);
      var Ltot = polyLen(off), ns = Math.max(2, Math.ceil(Ltot / 1000) + 1), wallPts = offsetPoly(pts, sg * (st + 10));
      for (var j = 0; j < ns; j++) {
        var t = Ltot * j / (ns - 1), a = polyAt(off, t), b = polyAt(wallPts, t * polyLen(wallPts) / Ltot), y = 0;
        var accu = 0, yy = gy[0]; for (var q = 0; q + 1 < off.length; q++) { var l = pdist(off[q], off[q + 1]); if (t <= accu + l) { yy = gy[q] + (gy[q + 1] - gy[q]) * (t - accu) / Math.max(l, 1e-9); break; } accu += l; yy = gy[q + 1]; }
        m.member('SMC', 'Support de main courante', 'RP12', [a[0], yy + 900 - mp.sh - 30, a[1]], [b[0], yy + 900 - mp.sh - 30, b[1]]);
      }
      m.rule(p.walls === 'both' || p.walls === sd, 'Main courante murale (' + (sd === 'int' ? 'intérieur' : 'extérieur') + ')', 'fixée sur mur d\'échiffre', 'pas de mur de ce côté : prévoir un support (cloison, limon)', 'cohérence', true);
    });
    masonValue(m);
  }

  function simpleStairMasonry(m, p, hx0, hx1, hz0, hz1) {
    var M = 1200;
    if (p.floorLow) { mBox(m, 'Dalle basse', 'beton', hx0 - M, hz0 - M, hx1 + M, hz1 + M, -220, 200); mBox(m, 'Revêtement', p.floorMat, hx0 - M, hz0 - M, hx1 + M, hz1 + M, -20, 20); }
    if (p.upper) { mSlab(m, 'Plancher haut', 'beton', [hx0 - M, hz0 - M, hx1 + M, hz1 + M], [hx0, hz0, hx1, hz1], p.H - p.slabT, p.slabT - 20); mSlab(m, 'Revêtement', p.floorMat, [hx0 - M, hz0 - M, hx1 + M, hz1 + M], [hx0, hz0, hx1, hz1], p.H - 20, 20); }
    masonValue(m);
  }

  function stairFamily(id, label, kind) {
    family({
      id: id, cat: 'Escaliers', label: label, icon: 'stair',
      params: (function () { var ps = stairCommonParams(KIND_PARAMS[kind]); if (kind === 'demi') ps.filter(function (q) { return q.k === 'H'; })[0].d = 2880; return ps; })(),
      build: function (p) {
        var m = new Model(label + ' H' + p.H), W = p.W, f = kind === 'droit' ? W / 2 : Math.min(p.walkOff || 500, W / 2), band, r;
        if (kind === 'droit') band = stairBand('droit', W, straightRunFor(p), 0, 0, f);
        else if (kind === 'quart') band = stairBand('quart', W, p.L1, p.L2, 0, f);
        else if (kind === 'deux') band = stairBand('deux', W, p.L1, p.L2, p.L3, f);
        else band = stairBand('deux', W, p.L1, 2 * W + p.jour, p.L1, f);
        if (kind !== 'droit' && (p.L1 < W + 200 || (p.L2 && p.L2 < W + 200 && kind !== 'deux'))) throw new Error('Emprise trop courte par rapport à l\'emmarchement.');
        r = buildStair(m, p, band, kind === 'droit' ? 'none' : p.mode, p.kb || 2);
        stairMasonry(m, p, band, r, kind);
        if (kind === 'droit') headroomStraight(m, p, r.h, r.g, r.lines[r.nT]);
        else m.info('Échappée', 'À vérifier en 3D sous la trémie (géométrie tournante).');
        if (p.turn === 'droite' && kind !== 'droit') { /* construit à droite */ } else if (kind !== 'droit') mirrorZ(m);
        return m;
      }
    });
  }
  stairFamily('escalier-droit', 'Escalier droit', 'droit');
  stairFamily('escalier-quart', 'Escalier quart tournant', 'quart');
  stairFamily('escalier-deux-quarts', 'Escalier deux quarts tournants', 'deux');
  stairFamily('escalier-demi', 'Escalier demi-tournant', 'demi');

  // ------------------------------------------------------------------ hélicoïdal
  family({
    id: 'escalier-helicoidal', cat: 'Escaliers', label: 'Escalier hélicoïdal', icon: 'spiral',
    params: [
      { k: 'usage', l: 'Usage', t: 'sel', d: 'logement', o: [['logement', 'Intérieur d\'un logement'], ['collectif', 'Logement collectif'], ['erp', 'ERP'], ['industriel', 'Industriel']], g: 'Usage' },
      { k: 'H', l: 'Hauteur à monter', t: 'num', d: 2800, u: 'mm', g: 'Géométrie' },
      { k: 'h', l: 'Hauteur de marche visée', t: 'num', d: 180, u: 'mm', g: 'Géométrie' },
      { k: 'D', l: 'Diamètre extérieur', t: 'num', d: 1800, u: 'mm', g: 'Géométrie' },
      { k: 'fut', l: 'Fût central', t: 'prof', d: 'TRD139.7x4', list: 'fut', g: 'Géométrie' },
      { k: 'rot', l: 'Rotation totale (0 = auto)', t: 'num', d: 0, u: '°', g: 'Géométrie' },
      { k: 'walkR', l: 'Rayon de la ligne de foulée (0 = 2/3)', t: 'num', d: 0, u: 'mm', g: 'Géométrie' },
      { k: 'turn', l: 'Sens de montée', t: 'sel', d: 'droite', o: [['droite', 'Sens horaire'], ['gauche', 'Sens trigonométrique']], g: 'Géométrie' },
      { k: 'landing', l: 'Palier d\'arrivée (quart)', t: 'bool', d: true, g: 'Géométrie' },
      { k: 'tread', l: 'Type de marche', t: 'sel', d: 'larmee', o: TREADS, g: 'Marches' },
      { k: 'tt', l: 'Épaisseur (0 = selon type)', t: 'num', d: 0, u: 'mm', g: 'Marches' },
      { k: 'overlap', l: 'Recouvrement angulaire', t: 'num', d: 3, u: '°', g: 'Marches' },
      { k: 'console', l: 'Console sous marche', t: 'prof', d: 'TR60x40x3', list: 'lisse', g: 'Structure' },
      { k: 'outerStr', l: 'Limon extérieur hélicoïdal', t: 'bool', d: false, g: 'Structure' },
      { k: 'railH', l: 'Hauteur main courante', t: 'num', d: 900, u: 'mm', g: 'Garde-corps' },
      { k: 'mc', l: 'Main courante (cintrée)', t: 'prof', d: 'TRD42.4x2', list: 'mainCourante', g: 'Garde-corps' },
      { k: 'bar', l: 'Barreaux', t: 'prof', d: 'TC20x20x1.5', list: 'barreau', g: 'Garde-corps' },
      { k: 'gap', l: 'Vide maxi entre barreaux', t: 'num', d: 110, u: 'mm', g: 'Garde-corps' }
    ].concat(masonParams({ mat: 'enduit-blanc', floor: 'parquet', extra: [
      { k: 'floorLow', l: 'Dalle basse (sol fini)', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'upper', l: 'Plancher haut avec trémie', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'slabT', l: 'Épaisseur du plancher haut', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['upper', true] },
      { k: 'cage', l: 'Cage maçonnée (4 murs)', t: 'bool', d: false, g: 'Maçonnerie & contexte' }] })),
    build: function (p) {
      var m = new Model('Escalier hélicoïdal Ø' + p.D + ' H' + p.H), n = Math.max(3, Math.round(p.H / p.h)), h = p.H / n, nT = n - 1;
      var fut = profile(p.fut), R = p.D / 2, ri = fut.B / 2, rf = p.walkR > 0 ? p.walkR : ri + (R - ri) * 2 / 3;
      var tm = treadMaterial(p.tread), tt = p.tt > 0 ? p.tt : tm[1];
      var dth = p.rot > 0 ? p.rot * DEG / nT : (630 - 2 * h) / rf; // giron sur ligne de foulée = 2h+g = 630
      var g = rf * dth, tot = dth * nT;
      m.member('F', 'Fût central', fut.id, [0, 0, 0], [0, p.H + p.railH, 0]);
      m.plate('PF', 'Platine de pied', 'XZ', [[-fut.B, -fut.B], [fut.B, -fut.B], [fut.B, fut.B], [-fut.B, fut.B]], 12, 6, 12);
      var outerPts = [], rail = [];
      for (var k = 0; k < nT; k++) {
        var a0 = k * dth - p.overlap * DEG, a1 = (k + 1) * dth, top = (k + 1) * h, poly = [];
        arcPts([0, 0], ri, a0, a1, 2).forEach(function (q) { poly.push(q); });
        arcPts([0, 0], R, a1, a0, 6).forEach(function (q) { poly.push(q); });
        m.plate('M', 'Marche', 'XZ', poly, tt, top - tt / 2, tt, tm[0], tm[0] === 'grillage' ? { kgm2: 25 } : null);
        var am = (a0 + a1) / 2; m.member('CO', 'Console', p.console, [ri * Math.cos(am), top - tt - 25, ri * Math.sin(am)], [0.92 * R * Math.cos(am), top - tt - 25, 0.92 * R * Math.sin(am)]);
        outerPts.push([(R - 20) * Math.cos(k * dth), top, (R - 20) * Math.sin(k * dth)]);
      }
      if (p.landing) {
        var a0 = nT * dth, poly2 = [[0, 0]].concat(arcPts([0, 0], R, a0, a0 + Math.PI / 2, 8));
        m.plate('PA', 'Palier d\'arrivée', 'XZ', poly2, Math.max(tt, 6), p.H - Math.max(tt, 6) / 2, Math.max(tt, 6), tm[0], tm[0] === 'grillage' ? { kgm2: 25 } : null);
      }
      outerPts.push([(R - 20) * Math.cos(nT * dth), p.H, (R - 20) * Math.sin(nT * dth)]);
      if (p.outerStr) m.chain('LIM', 'Limon hélicoïdal', 'PL200x8', outerPts.map(function (q) { return [q[0] * (R + 4) / (R - 20), q[1] - 80, q[2] * (R + 4) / (R - 20)]; }));
      // garde-corps : main courante hélicoïdale + barreaux
      var mcp = profile(p.mc), bar = profile(p.bar), arcStep = (R - 30) * dth, nb = Math.max(1, Math.ceil(arcStep / (bar.vw + p.gap) - 1e-9));
      var gapReal = arcStep / nb - bar.vw, rr = R - 30;
      for (k = 0; k <= nT; k++) rail.push([rr * Math.cos(k * dth), (k + 1) * h + p.railH - mcp.sh / 2, rr * Math.sin(k * dth)]);
      rail[rail.length - 1][1] = p.H + p.railH - mcp.sh / 2;
      m.chain('MC', 'Main courante hélicoïdale', mcp.id, rail);
      for (k = 0; k < nT; k++) for (var j = 0; j < nb; j++) {
        var a = (k + (j + 0.5) / nb) * dth, y0 = (k + 1) * h, ytop = y0 + p.railH - mcp.sh + (j + 0.5) / nb * h;
        m.member('B', 'Barreau', bar.id, [rr * Math.cos(a), y0, rr * Math.sin(a)], [rr * Math.cos(a), ytop, rr * Math.sin(a)]);
      }
      var Rm = R + 1200, hs = R + 150;
      if (p.floorLow) { mBox(m, 'Dalle basse', 'beton', -Rm, -Rm, Rm, Rm, -220, 200); mBox(m, 'Revêtement', p.floorMat, -Rm, -Rm, Rm, Rm, -20, 20); }
      if (p.upper) { mSlab(m, 'Plancher haut', 'beton', [-Rm, -Rm, Rm, Rm], [-hs, -hs, hs, hs], p.H - p.slabT, p.slabT - 20); mSlab(m, 'Revêtement', p.floorMat, [-Rm, -Rm, Rm, Rm], [-hs, -hs, hs, hs], p.H - 20, 20); m.value('Trémie', f0(2 * hs) + ' x ' + f0(2 * hs) + ' mm'); }
      if (p.cage) { var c0 = R + 60, c1 = c0 + 200, top2 = p.upper ? p.H - p.slabT : p.H + 1200;
        mBox(m, 'Mur de cage', p.masonMat, -c1, -c1, c1, -c0, -220, top2 + 220); mBox(m, 'Mur de cage', p.masonMat, -c1, c0, c1, c1, -220, top2 + 220);
        mBox(m, 'Mur de cage', p.masonMat, -c1, -c0, -c0, c0, -220, top2 + 220); mBox(m, 'Mur de cage', p.masonMat, c0, -c0, c1, c0, -220, top2 + 220); }
      masonValue(m);
      if (p.turn === 'gauche') mirrorZ(m);
      var headroom = (2 * Math.PI / dth) * h - tt;
      m.value('Hauteurs / marches', n + ' / ' + nT); m.value('Hauteur de marche', f1(h) + ' mm'); m.value('Angle par marche', f1(dth / DEG) + '°');
      m.value('Giron sur ligne de foulée (r = ' + f0(rf) + ')', f0(g) + ' mm'); m.value('Rotation totale', f0(tot / DEG) + '°'); m.value('Échappée après 1 tour', f0(headroom) + ' mm');
      m.value('Giron au bord extérieur', f0(R * dth) + ' mm'); m.value('Giron à 150 mm du fût', f0((ri + 150) * dth) + ' mm');
      stairUsageChecks(m, { usage: p.usage, nosing: 30, railH: p.railH }, h, g, R - ri, Math.atan2(h, g) / DEG, 1);
      m.rule(headroom >= 2000, 'Échappée (une révolution)', f0(headroom) + ' mm ≥ 2000', f0(headroom) + ' mm < 2000 : réduire l\'angle par marche ou augmenter h', 'usage', headroom >= 1900);
      m.rule(gapReal <= p.gap + 0.1 && gapReal <= 110, 'Vides garde-corps', f1(gapReal) + ' mm ≤ 110', f1(gapReal) + ' mm > 110', N.GC);
      if (p.usage === 'erp') m.warn('ERP', 'Escalier hélicoïdal en ERP : admis sous conditions (ligne de foulée, giron, mains courantes) — à valider avec le contrôleur technique.', N.ERP);
      m.info('Cintrage', 'Main courante et limon hélicoïdaux : cintrage au rayon ' + f0(rr) + ' mm, pas ' + f0(2 * Math.PI / dth * h) + ' mm.');
      m.info('Exécution', 'Ouvrage porteur : NF EN 1090-2, EXC2 par défaut.', N.EXE);
      return m;
    }
  });

  // ------------------------------------------------------------------ échelle de meunier & pas japonais
  family({
    id: 'escalier-meunier', cat: 'Escaliers', label: 'Échelle de meunier', icon: 'ladder',
    params: [
      { k: 'H', l: 'Hauteur à monter', t: 'num', d: 2600, u: 'mm', g: 'Géométrie' },
      { k: 'h', l: 'Hauteur de marche visée', t: 'num', d: 220, u: 'mm', g: 'Géométrie' },
      { k: 'angle', l: 'Inclinaison', t: 'num', d: 65, u: '°', g: 'Géométrie' },
      { k: 'W', l: 'Largeur entre limons', t: 'num', d: 600, u: 'mm', g: 'Géométrie' },
      { k: 'depth', l: 'Profondeur de marche', t: 'num', d: 150, u: 'mm', g: 'Marches' },
      { k: 'tread', l: 'Type de marche', t: 'sel', d: 'larmee', o: TREADS, g: 'Marches' },
      { k: 'sp', l: 'Limons', t: 'prof', d: 'PL160x10', list: 'limon', g: 'Limons' },
      { k: 'mc', l: 'Mains courantes (2 côtés)', t: 'prof', d: 'TRD33.7x2', list: 'mainCourante', g: 'Garde-corps' }
    ].concat(masonParams({ mat: 'enduit-blanc', floor: 'parquet', extra: [
      { k: 'floorLow', l: 'Dalle basse (sol fini)', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'upper', l: 'Plancher haut avec trémie', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'slabT', l: 'Épaisseur du plancher haut', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['upper', true] }] })),
    build: function (p) {
      var m = new Model('Échelle de meunier H' + p.H), n = Math.max(2, Math.round(p.H / p.h)), h = p.H / n, g = h / Math.tan(p.angle * DEG), sp = profile(p.sp);
      var tm = treadMaterial(p.tread), L = n * g;
      [-1, 1].forEach(function (sd) {
        var z = sd * (p.W / 2 + sp.wa / 2);
        m.member('LIM', 'Limon', sp.id, [-sp.sh / 2 / Math.sin(p.angle * DEG), 0, z], [L, p.H + 100, z]);
        m.member('MC', 'Main courante', p.mc, [0, 900, sd * (p.W / 2 + sp.wa + 40)], [L, p.H + 900, sd * (p.W / 2 + sp.wa + 40)]);
      });
      for (var k = 1; k < n; k++) {
        var x = k * g, y = k * h;
        m.plate('M', 'Marche', 'XZ', [[x - p.depth / 2, -p.W / 2], [x + p.depth / 2, -p.W / 2], [x + p.depth / 2, p.W / 2], [x - p.depth / 2, p.W / 2]], tm[1], y - tm[1] / 2, tm[1], tm[0], tm[0] === 'grillage' ? { kgm2: 25 } : null);
      }
      m.value('Marches', n - 1); m.value('Hauteur de marche', f1(h) + ' mm'); m.value('Giron', f0(g) + ' mm'); m.value('Encombrement au sol', f0(L) + ' mm');
      m.rule(p.angle >= 45 && p.angle <= 75, 'Inclinaison', f0(p.angle) + '° (45–75° usuel)', f0(p.angle) + '° hors 45–75°', 'usage', true);
      m.rule(p.depth >= 120, 'Profondeur de marche', f0(p.depth) + ' mm ≥ 120 (usuel)', f0(p.depth) + ' mm', 'usage', true);
      m.info('Usage', 'Accès occasionnel (combles, mezzanine privée) : à descendre face aux marches. Non admis comme escalier principal / ERP.');
      simpleStairMasonry(m, p, -300, L + 50, -p.W / 2 - 150, p.W / 2 + 150);
      return m;
    }
  });
  family({
    id: 'escalier-japonais', cat: 'Escaliers', label: 'Escalier à pas japonais', icon: 'stair',
    params: [
      { k: 'H', l: 'Hauteur à monter', t: 'num', d: 2600, u: 'mm', g: 'Géométrie' },
      { k: 'h', l: 'Hauteur de pas visée', t: 'num', d: 200, u: 'mm', g: 'Géométrie' },
      { k: 'angle', l: 'Inclinaison', t: 'num', d: 60, u: '°', g: 'Géométrie' },
      { k: 'W', l: 'Largeur totale', t: 'num', d: 600, u: 'mm', g: 'Géométrie' },
      { k: 'depth', l: 'Profondeur de pas', t: 'num', d: 230, u: 'mm', g: 'Marches' },
      { k: 'tread', l: 'Type de marche', t: 'sel', d: 'larmee', o: TREADS, g: 'Marches' },
      { k: 'spc', l: 'Limon central', t: 'prof', d: 'TR120x60x4', list: 'limonCentral', g: 'Limons' },
      { k: 'mc', l: 'Mains courantes', t: 'prof', d: 'TRD33.7x2', list: 'mainCourante', g: 'Garde-corps' }
    ].concat(masonParams({ mat: 'enduit-blanc', floor: 'parquet', extra: [
      { k: 'floorLow', l: 'Dalle basse (sol fini)', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'upper', l: 'Plancher haut avec trémie', t: 'bool', d: true, g: 'Maçonnerie & contexte' },
      { k: 'slabT', l: 'Épaisseur du plancher haut', t: 'num', d: 200, u: 'mm', g: 'Maçonnerie & contexte', show: ['upper', true] }] })),
    build: function (p) {
      var m = new Model('Escalier à pas japonais H' + p.H), n = Math.max(2, Math.round(p.H / p.h)), h = p.H / n, g = h / Math.tan(p.angle * DEG), sp = profile(p.spc), tm = treadMaterial(p.tread);
      m.member('LIM', 'Limon central', sp.id, [0, 0, 0], [n * g, p.H - 40, 0]);
      for (var k = 1; k < n; k++) {
        var x = k * g, y = k * h, sd = k % 2 ? -1 : 1;
        m.plate('M', 'Pas', 'XZ', [[x - p.depth / 2, 0], [x + p.depth / 2, 0], [x + p.depth / 2, sd * p.W / 2], [x - p.depth / 2, sd * p.W / 2]], tm[1], y - tm[1] / 2, tm[1], tm[0], tm[0] === 'grillage' ? { kgm2: 25 } : null);
        m.member('CO', 'Console', 'TR40x20x2', [x, y - tm[1] - 10, 0], [x, y - tm[1] - 10, sd * (p.W / 2 - 30)]);
      }
      [-1, 1].forEach(function (sd) { m.member('MC', 'Main courante', p.mc, [0, 900, sd * (p.W / 2 + 50)], [n * g, p.H + 900, sd * (p.W / 2 + 50)]); });
      m.value('Pas', n - 1); m.value('Hauteur entre pas', f1(h) + ' mm'); m.value('Hauteur entre pas d\'un même pied', f0(2 * h) + ' mm'); m.value('Giron', f0(g) + ' mm');
      m.rule(p.angle >= 50 && p.angle <= 70, 'Inclinaison', f0(p.angle) + '° (50–70° usuel)', f0(p.angle) + '° hors 50–70°', 'usage', true);
      m.info('Usage', 'Gain de place pour accès occasionnel ; non admis en ERP ni en parties communes.');
      simpleStairMasonry(m, p, -300, n * g + 50, -p.W / 2 - 150, p.W / 2 + 150);
      return m;
    }
  });

  // ================================================================== STRUCTURES & DIVERS
  var FLOOR_LOADS = [['logement', 'Logement (1,5 kN/m²)'], ['bureau', 'Bureaux (2,5 kN/m²)'], ['erp', 'ERP / commerce (5,0 kN/m²)'], ['industriel', 'Industriel / atelier (5,0 kN/m²)'], ['stockage', 'Stockage (7,5 kN/m²)']];
  var QK = { logement: 1.5, bureau: 2.5, erp: 5.0, industriel: 5.0, stockage: 7.5 };
  var DECKS = [['caillebotis', 'Caillebotis 30x3 (≈ 25 kg/m²)'], ['caillebotis-presse', 'Caillebotis pressé 30x2'], ['larmee', 'Tôle larmée 5/7'], ['tole-perforee', 'Tôle perforée 4 mm'], ['bois', 'Panneau bois CTBX 22'], ['composite', 'Lames composite 25'], ['bac', 'Bac acier + dalle béton 80'], ['collaborant', 'Plancher collaborant 120']];
  function deck(m, poly, top, type) {
    if (type === 'caillebotis') m.plate('PT', 'Caillebotis', 'XZ', poly, 30, top - 15, 30, 'grillage', { kgm2: 25 });
    else if (type === 'bois') m.plate('PT', 'Platelage bois', 'XZ', poly, 22, top - 11, 22, 'bois');
    else if (type === 'caillebotis-presse') m.plate('PT', 'Caillebotis pressé', 'XZ', poly, 30, top - 15, 30, 'grillage', { kgm2: 21 });
    else if (type === 'tole-perforee') m.plate('PT', 'Tôle perforée', 'XZ', poly, 4, top - 2, 4, 'acier', { fillRatio: 0.6 });
    else if (type === 'composite') m.plate('PT', 'Lames composite', 'XZ', poly, 25, top - 12.5, 25, 'composite');
    else if (type === 'collaborant') { m.plate('PT', 'Dalle collaborante', 'XZ', poly, 120, top - 60, 120, 'beton'); }
    else if (type === 'bac') { m.plate('PT', 'Dalle béton', 'XZ', poly, 80, top - 40, 80, 'beton'); m.plate('PT', 'Bac acier', 'XZ', poly, 1, top - 80.5, 1, 'acier'); }
    else m.plate('PT', 'Tôle larmée', 'XZ', poly, 6, top - 3, 6, 'acier');
    return { caillebotis: 30, 'caillebotis-presse': 30, bois: 22, composite: 25, bac: 81, collaborant: 120, 'tole-perforee': 4, larmee: 6 }[type] || 6;
  }
  function deckKg(type) { return { caillebotis: 25, 'caillebotis-presse': 21, bois: 12, composite: 30, bac: 205, collaborant: 250, 'tole-perforee': 19, larmee: 47 }[type] || 47; }
  function beamChecks(m, label, prof, span, qLine, gLine, grade, lim) { // qLine, gLine en N/mm
    var p = profile(prof), Med = (1.35 * gLine + 1.5 * qLine) * span * span / 8, d = 5 * (gLine + qLine) * Math.pow(span, 4) / (384 * E_ACIER * p.sec.Iy);
    bendCheck(m, 'Résistance ' + label + ' (' + p.label + ', L = ' + f0(span) + ')', Med, p.sec.Wy, grade);
    deflCheck(m, 'Flèche ' + label, d, span, lim || 300);
    return (1.35 * gLine + 1.5 * qLine) * span / 2; // réaction ELU par appui (N)
  }

  family({
    id: 'mezzanine', cat: 'Structures & divers', label: 'Mezzanine / plateforme', icon: 'platform',
    params: [
      { k: 'use', l: 'Exploitation', t: 'sel', d: 'bureau', o: FLOOR_LOADS, g: 'Usage' },
      { k: 'qk', l: 'Charge imposée (0 = selon exploitation)', t: 'num', d: 0, u: 'kN/m²', step: 0.5, g: 'Usage' },
      { k: 'L', l: 'Longueur (X)', t: 'num', d: 6000, u: 'mm', g: 'Dimensions' },
      { k: 'P', l: 'Profondeur (Z)', t: 'num', d: 4000, u: 'mm', g: 'Dimensions' },
      { k: 'H', l: 'Niveau fini du plancher', t: 'num', d: 2800, u: 'mm', g: 'Dimensions' },
      { k: 'wall', l: 'Adossée à un mur (côté arrière)', t: 'bool', d: false, g: 'Dimensions' },
      { k: 'colP', l: 'Poteaux', t: 'prof', d: '__auto', auto: 'Flambement', list: 'poteauStructure', g: 'Ossature' },
      { k: 'colMax', l: 'Entraxe maxi poteaux', t: 'num', d: 3500, u: 'mm', g: 'Ossature' },
      { k: 'beam', l: 'Poutres principales', t: 'prof', d: '__auto', auto: 'poutre principale', list: 'poutre', g: 'Ossature' },
      { k: 'joist', l: 'Solives', t: 'prof', d: '__auto', auto: 'solive', list: 'poutre', g: 'Ossature' },
      { k: 'joistMax', l: 'Entraxe maxi solives', t: 'num', d: 800, u: 'mm', g: 'Ossature' },
      { k: 'deck', l: 'Platelage', t: 'sel', d: 'caillebotis', o: DECKS, g: 'Plancher' },
      { k: 'gcFront', l: 'Garde-corps avant', t: 'bool', d: true, g: 'Garde-corps' },
      { k: 'gcSides', l: 'Garde-corps latéraux', t: 'bool', d: true, g: 'Garde-corps' },
      { k: 'gcType', l: 'Type de garde-corps', t: 'sel', d: 'industriel', o: [['industriel', 'Industriel (lisse, sous-lisse, plinthe)'], ['barreaux', 'Barreaudage'], ['verre', 'Verre'], ['tole', 'Tôle']], g: 'Garde-corps' },
      { k: 'access', l: 'Accès', t: 'sel', d: 'droit', o: [['aucun', 'Aucun'], ['droit', 'Escalier droit intégré'], ['echelle', 'Échelle à crinoline']], g: 'Accès' },
      { k: 'accessX', l: 'Position de l\'ouverture (X)', t: 'num', d: 4500, u: 'mm', g: 'Accès' },
      { k: 'accessW', l: 'Largeur de l\'ouverture', t: 'num', d: 900, u: 'mm', g: 'Accès' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ].concat(masonParams({ mat: 'parpaing-enduit', floor: 'beton-lisse', extra: [] })),
    build: function (p) {
      var m = new Model('Mezzanine ' + p.L + 'x' + p.P + ' à ' + p.H), qk = p.qk > 0 ? p.qk : QK[p.use];
      var bm = profile(p.beam), js = profile(p.joist), col = profile(p.colP);
      var nx = Math.max(1, Math.ceil(p.L / p.colMax - 1e-9)), ex = p.L / nx, lines = p.wall ? [0] : [0, p.P];
      var dt = { caillebotis: 30, 'caillebotis-presse': 30, bois: 22, composite: 25, bac: 81, collaborant: 120, 'tole-perforee': 4, larmee: 6 }[p.deck], yTop = p.H - dt, yB = yTop - bm.sh / 2;
      lines.forEach(function (z) {
        for (var i = 0; i <= nx; i++) {
          m.member('PO', 'Poteau', col.id, [i * ex, 12, z], [i * ex, yTop - bm.sh, z]);
          m.plate('PL', 'Platine de pied', 'XZ', [[i * ex - col.B / 2 - 50, z - col.H / 2 - 50], [i * ex + col.B / 2 + 50, z - col.H / 2 - 50], [i * ex + col.B / 2 + 50, z + col.H / 2 + 50], [i * ex - col.B / 2 - 50, z + col.H / 2 + 50]], 12, 6, 12);
        }
        m.member('PP', 'Poutre principale', bm.id, [0, yB, z], [p.L, yB, z]);
      });
      if (p.wall) { m.member('PP', 'Lisse murale', 'UPN200', [0, yTop - 100, p.P], [p.L, yTop - 100, p.P], { rot: 90 }); m.info('Mur', 'Lisse murale chevillée : vérifier la capacité du mur support.'); }
      var ns = Math.max(1, Math.ceil(p.L / p.joistMax - 1e-9)), es = p.L / ns;
      for (var j = 0; j <= ns; j++) { var x = clamp(j * es, bm.B / 2, p.L - bm.B / 2); m.member('SO', 'Solive', js.id, [x, yTop - js.sh / 2, bm.B / 2], [x, yTop - js.sh / 2, p.P - (p.wall ? 0 : bm.B / 2)]); }
      deck(m, [[0, 0], [p.L, 0], [p.L, p.P], [0, p.P]], p.H, p.deck);
      // garde-corps (ouverture d'accès en façade)
      var gp = { H: p.gcType === 'industriel' ? 1100 : 1000, post: 'TC50x50x4', emax: 1500, mc: 'TRD42.4x2', mcPos: 'dessus', lb: p.gcType !== 'industriel', lbProf: 'TR40x20x2', jeu: 60,
                 pose: 'francaise', drop: 0, plate: [120, 120, 10], spec: { type: p.gcType, bar: 'TC20x20x2', gap: 110, thick: 3, glass: '55.2', glassFix: 'pinces', rail: 'TR40x20x2', toe: 'PL100x5' }, side: -1 };
      var a0 = p.accessX, a1 = p.accessX + p.accessW, paths = [];
      if (p.gcFront) {
        var front = p.access === 'aucun' ? [[[0, 0], [p.L, 0]]] : [[[0, 0], [Math.max(0, a0), 0]], [[Math.min(p.L, a1), 0], [p.L, 0]]];
        if (p.gcSides && front[0][0][0] === 0) front[0].unshift([0, p.P]);
        if (p.gcSides) front[front.length - 1].push([p.L, p.P]);
        paths = front.filter(function (pt) { return polyLen(pt) > 200; });
      } else if (p.gcSides) paths = [[[0, p.P], [0, 0]], [[p.L, 0], [p.L, p.P]]];
      paths.forEach(function (pt) { var st = railRun(m, pt, pt.map(function () { return p.H; }), gp);
        if (p.gcType === 'industriel') { m.rule(st.maxGap <= 500, 'Garde-corps : vides', f0(st.maxGap) + ' mm ≤ 500', f0(st.maxGap) + ' mm', N.ISO3); }
        else m.rule(st.maxGap <= 110, 'Garde-corps : vides', f0(st.maxGap) + ' mm ≤ 110', f0(st.maxGap) + ' mm', N.GC); });
      if (p.access === 'droit') {
        var sd = FAMILIES['escalier-droit'], sp = defaults('escalier-droit');
        sp.H = p.H; sp.W = p.accessW - 40; sp.usage = p.use === 'industriel' || p.use === 'stockage' ? 'industriel' : p.use === 'erp' ? 'erp' : 'collectif';
        sp.opening = 0; sp.rails = 'both'; sp.floorLow = false; sp.upper = false; sp.walls = 'aucun'; sp.h = sp.usage === 'industriel' ? 200 : sp.usage === 'erp' ? 155 : 165;
        if (sp.usage === 'industriel') { sp.blondel = 640; sp.infill = 'industriel'; sp.railH = 1000; }
        var sm = solve('escalier-droit', coerce('escalier-droit', sp)).m, run = straightRunFor(sp);
        m.merge(sm, [a1 - 20, 0, -run], -90);
        m.value('Escalier d\'accès', 'intégré (' + sm.values.filter(function (v) { return /marches/.test(v[0]); }).map(function (v) { return v[1]; })[0] + ')');
      } else if (p.access === 'echelle') {
        var lm = solve('echelle', coerce('echelle', { climb: p.H, context: 'aucun' })).m;
        m.merge(lm, [a0 + 50, 0, -250], 0);
      }
      // vérifications
      var g0 = deckKg(p.deck) * 9.81 * 1e-6; // N/mm²
      var rJ = beamChecks(m, 'solive', js.id, p.P - (p.wall ? 0 : bm.B), qk * 1e-3 * es, g0 * es + js.kgm * 9.81e-3, p.grade, 300);
      var trib = p.wall ? p.P / 2 : p.P / 2;
      var rB = beamChecks(m, 'poutre principale', bm.id, ex, qk * 1e-3 * trib, g0 * trib + bm.kgm * 9.81e-3 + js.kgm * 9.81e-3 * trib / es, p.grade, 300);
      bucklingCheck(m, 'Flambement poteau (Lf = ' + f0(p.H) + ')', 2 * rB, col.id, p.H, p.grade);
      m.value('Charge d\'exploitation', f1(qk) + ' kN/m²'); m.value('Trame poteaux', nx + ' x ' + f0(ex) + ' mm'); m.value('Solives', (ns + 1) + ' à ' + f0(es) + ' mm');
      m.value('Surface', f1(p.L * p.P / 1e6) + ' m²');
      mBox(m, 'Dalle', 'beton', -1500, -5500, p.L + 1500, p.P + 1500, -220, 200); mBox(m, 'Revêtement', p.floorMat, -1500, -5500, p.L + 1500, p.P + 1500, -20, 20);
      if (p.wall) mBox(m, 'Mur d\'adossement', p.masonMat, -1000, p.P + 60, p.L + 1000, p.P + 260, -220, p.H + 1700);
      masonValue(m);
      m.info('Charges', 'qk = ' + f1(qk) + ' kN/m² (valeurs à confirmer avec NF P06-111-2) ; contreventement, assemblages et fondations à justifier.', N.EC1);
      m.info('Exécution', 'Ouvrage porteur : NF EN 1090-2, EXC2 par défaut.', N.EXE);
      return m;
    }
  });

  family({
    id: 'passerelle', cat: 'Structures & divers', label: 'Passerelle', icon: 'bridge',
    params: [
      { k: 'L', l: 'Portée', t: 'num', d: 6000, u: 'mm', g: 'Dimensions' },
      { k: 'W', l: 'Largeur utile', t: 'num', d: 1000, u: 'mm', g: 'Dimensions' },
      { k: 'H', l: 'Hauteur des appuis (0 = posée)', t: 'num', d: 0, u: 'mm', g: 'Dimensions' },
      { k: 'beam', l: 'Poutres de rive', t: 'prof', d: '__auto', auto: 'poutre de rive', list: 'poutre', g: 'Ossature' },
      { k: 'cross', l: 'Traverses', t: 'prof', d: 'L50x50x5', list: 'corniere', g: 'Ossature' },
      { k: 'crossMax', l: 'Entraxe maxi traverses', t: 'num', d: 1000, u: 'mm', g: 'Ossature' },
      { k: 'deck', l: 'Platelage', t: 'sel', d: 'caillebotis', o: DECKS, g: 'Plancher' },
      { k: 'qk', l: 'Charge d\'exploitation', t: 'num', d: 5, u: 'kN/m²', step: 0.5, g: 'Résistance' },
      { k: 'gcType', l: 'Garde-corps', t: 'sel', d: 'industriel', o: [['industriel', 'Industriel'], ['barreaux', 'Barreaudage'], ['verre', 'Verre'], ['aucun', 'Aucun']], g: 'Garde-corps' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ].concat(masonParams({ mat: 'beton', floor: 'gazon', extra: [
      { k: 'supports', l: 'Appuis', t: 'sel', d: 'culees', o: [['culees', 'Culées maçonnées'], ['poteaux', 'Poteaux acier']], g: 'Maçonnerie & contexte' }] })),
    build: function (p) {
      var m = new Model('Passerelle L' + p.L + ' x ' + p.W), bm = profile(p.beam), y = p.H + 0, top = y + bm.sh;
      [-bm.B / 2, p.W + bm.B / 2].forEach(function (z, i) { m.member('PR', 'Poutre de rive', bm.id, [0, y + bm.sh / 2, z], [p.L, y + bm.sh / 2, z], { rot: i ? 180 : 0 }); });
      var n = Math.max(1, Math.ceil(p.L / p.crossMax - 1e-9));
      for (var i = 0; i <= n; i++) { var x = clamp(i * p.L / n, 30, p.L - 30); m.member('TV', 'Traverse', p.cross, [x, top - 60, 0], [x, top - 60, p.W]); }
      var dt = deck(m, [[0, 0], [p.L, 0], [p.L, p.W], [0, p.W]], top, p.deck);
      if (p.H > 0 && p.supports === 'poteaux') [[0, -bm.B / 2], [0, p.W + bm.B / 2], [p.L, -bm.B / 2], [p.L, p.W + bm.B / 2]].forEach(function (q) { m.member('PO', 'Poteau d\'appui', 'TC120x120x5', [q[0], 0, q[1]], [q[0], y, q[1]]); });
      if (p.gcType !== 'aucun') [[[0, -bm.B], [p.L, -bm.B]], [[0, p.W + bm.B], [p.L, p.W + bm.B]]].forEach(function (pt, k) {
        var st = railRun(m, pt, [top, top], { H: p.gcType === 'industriel' ? 1100 : 1000, post: 'TC50x50x4', emax: 1500, mc: 'TRD42.4x2', mcPos: 'dessus', lb: p.gcType !== 'industriel', lbProf: 'TR40x20x2', jeu: 40,
          pose: 'anglaise', drop: bm.sh - 20, plate: null, spec: { type: p.gcType, bar: 'TC20x20x2', gap: 110, glass: '55.2', glassFix: 'pinces', rail: 'TR40x20x2', toe: 'PL100x5' }, side: k ? 1 : -1 });
        if (k === 0) m.value('Garde-corps', st.posts + ' poteaux par côté');
      });
      var g0 = deckKg(p.deck) * 9.81e-6 * p.W / 2 + bm.kgm * 9.81e-3 + 15 * 9.81e-3;
      beamChecks(m, 'poutre de rive', bm.id, p.L, p.qk * 1e-3 * p.W / 2, g0, p.grade, 300);
      m.info('Vibrations', 'Passerelle piétonne : vérifier la fréquence propre (> 3 Hz usuel) et le contreventement horizontal.');
      var zA = -bm.B - 400, zB = p.W + bm.B + 400;
      if (p.supports === 'culees') { mBox(m, 'Culée', p.masonMat, -900, zA, 150, zB, -800, p.H + 800); mBox(m, 'Culée', p.masonMat, p.L - 150, zA, p.L + 900, zB, -800, p.H + 800); }
      mBox(m, 'Sol', p.floorMat, -3500, zA - 1500, -900, zB + 1500, p.H - 220 > -800 ? p.H - 220 : -220, 200);
      mBox(m, 'Sol', p.floorMat, p.L + 900, zA - 1500, p.L + 3500, zB + 1500, p.H - 220 > -800 ? p.H - 220 : -220, 200);
      masonValue(m);
      m.info('Exécution', 'Ouvrage porteur : NF EN 1090-2, EXC2 par défaut.', N.EXE);
      return m;
    }
  });

  family({
    id: 'auvent', cat: 'Structures & divers', label: 'Auvent / marquise', icon: 'canopy',
    params: [
      { k: 'L', l: 'Largeur', t: 'num', d: 3000, u: 'mm', g: 'Dimensions' },
      { k: 'A', l: 'Avancée', t: 'num', d: 1200, u: 'mm', g: 'Dimensions' },
      { k: 'Hf', l: 'Hauteur de fixation', t: 'num', d: 2600, u: 'mm', g: 'Dimensions' },
      { k: 'pente', l: 'Pente', t: 'num', d: 5, u: '°', g: 'Dimensions' },
      { k: 'cons', l: 'Consoles', t: 'prof', d: '__auto', auto: 'console', list: 'cadre', g: 'Ossature' },
      { k: 'emax', l: 'Entraxe maxi consoles', t: 'num', d: 1500, u: 'mm', g: 'Ossature' },
      { k: 'rive', l: 'Rive avant', t: 'prof', d: 'TR80x40x3', list: 'cadre', g: 'Ossature' },
      { k: 'ties', l: 'Tirants', t: 'bool', d: true, g: 'Ossature' },
      { k: 'tie', l: 'Tirant', t: 'prof', d: '__auto', auto: 'Tirant', list: 'barreau', g: 'Ossature', show: ['ties', true] },
      { k: 'tieH', l: 'Hauteur d\'ancrage des tirants', t: 'num', d: 700, u: 'mm', g: 'Ossature', show: ['ties', true] },
      { k: 'cover', l: 'Couverture', t: 'sel', d: 'verre', o: [['verre', 'Verre feuilleté 66.2'], ['tole', 'Tôle acier 2 mm'], ['polycarbonate', 'Polycarbonate 16 mm']], g: 'Couverture' },
      { k: 'snow', l: 'Neige + entretien', t: 'num', d: 0.9, u: 'kN/m²', step: 0.1, g: 'Résistance' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ].concat(masonParams({ mat: 'enduit-ton-pierre', floor: 'dalle-pierre', extra: [
      { k: 'facade', l: 'Façade avec porte', t: 'bool', d: true, g: 'Maçonnerie & contexte' }] })),
    build: function (p) {
      var m = new Model('Auvent ' + p.L + 'x' + p.A), n = Math.max(1, Math.ceil(p.L / p.emax - 1e-9)), e = p.L / n, drop = p.A * Math.tan(p.pente * DEG), cp = profile(p.cons);
      for (var i = 0; i <= n; i++) {
        var x = clamp(i * e, cp.B / 2, p.L - cp.B / 2);
        m.member('CO', 'Console', cp.id, [x, p.Hf, 0], [x, p.Hf - drop, p.A]);
        m.plate('PM', 'Platine murale', 'XY', [[x - 70, p.Hf - 120], [x + 70, p.Hf - 120], [x + 70, p.Hf + 120], [x - 70, p.Hf + 120]], 10, -5, 10);
        if (p.ties) { m.member('TI', 'Tirant', p.tie, [x, p.Hf + p.tieH, 0], [x, p.Hf - drop + cp.sh / 2, p.A - 40]);
          m.plate('PM', 'Platine de tirant', 'XY', [[x - 50, p.Hf + p.tieH - 60], [x + 50, p.Hf + p.tieH - 60], [x + 50, p.Hf + p.tieH + 60], [x - 50, p.Hf + p.tieH + 60]], 10, -5, 10); }
      }
      m.member('RV', 'Rive avant', p.rive, [0, p.Hf - drop, p.A], [p.L, p.Hf - drop, p.A]);
      m.member('RV', 'Lisse murale', p.rive, [0, p.Hf, cp.B / 2 + 30], [p.L, p.Hf, cp.B / 2 + 30]);
      var ct = { verre: 12.76, tole: 2, polycarbonate: 16 }[p.cover], mat = p.cover === 'tole' ? 'acier' : p.cover;
      m.plate('CV', 'Couverture ' + p.cover, 'XZ', [[0, 0], [p.L, 0], [p.L, p.A], [0, p.A]], ct, p.Hf - drop / 2 + cp.sh / 2 + ct / 2, ct, mat);
      var gk = ({ verre: 0.32, tole: 0.16, polycarbonate: 0.03 }[p.cover]) * 1e-3 * e + cp.kgm * 9.81e-3, qk = p.snow * 1e-3 * e, A = p.A;
      if (p.ties) {
        var Med = (1.35 * gk + 1.5 * qk) * A * A / 8, beta = Math.atan2(p.tieH + drop, A), T = (1.35 * gk + 1.5 * qk) * A / 2 / Math.sin(beta);
        bendCheck(m, 'Résistance console (appuyée sur tirant)', Med, cp.sec.Wy, p.grade);
        var tp = profile(p.tie), rt = T / (tp.A * (FY[p.grade] || 235));
        m.rule(rt <= 1, 'Tirant', 'N = ' + f1(T / 1000) + ' kN → taux ' + f0(rt * 100) + ' % (section brute ; filetage à vérifier)', 'N = ' + f1(T / 1000) + ' kN → ' + f0(rt * 100) + ' %', N.EC3);
        m.value('Effort par tirant (ELU)', f1(T / 1000) + ' kN');
      } else {
        bendCheck(m, 'Résistance console (encastrée)', (1.35 * gk + 1.5 * qk) * A * A / 2, cp.sec.Wy, p.grade);
        m.value('Moment d\'encastrement (ELU)', f1((1.35 * gk + 1.5 * qk) * A * A / 2 / 1e6) + ' kN·m');
      }
      m.value('Consoles', (n + 1) + ' à ' + f0(e) + ' mm');
      if (p.facade) {
        var Lw = p.L + 1600, Hw = p.Hf + (p.ties ? p.tieH : 0) + 700;
        mWall(m, 'Façade', p.masonMat, [-800, -10], [p.L + 800, -10], 0, -220, -20, Hw + 20, [{ s0: Lw / 2 - 500, s1: Lw / 2 + 500, y0: -20, y1: 2200 }]);
        mBox(m, 'Seuil', 'pierre', p.L / 2 - 560, -10, p.L / 2 + 560, 60, -20, 40);
      }
      mBox(m, 'Terrasse', p.floorMat, -800, -10, p.L + 800, p.A + 1500, -200, 200);
      masonValue(m);
      m.info('Charges climatiques', 'Neige NF EN 1991-1-3 et vent NF EN 1991-1-4 selon la région et l\'altitude ; soulèvement au vent à vérifier.', N.EC1);
      if (p.cover === 'verre') m.info('Verre', 'Verre feuilleté obligatoire en toiture (sécurité en cas de bris).');
      return m;
    }
  });

  family({
    id: 'pergola', cat: 'Structures & divers', label: 'Pergola', icon: 'pergola',
    params: [
      { k: 'L', l: 'Longueur', t: 'num', d: 4000, u: 'mm', g: 'Dimensions' },
      { k: 'P', l: 'Profondeur', t: 'num', d: 3000, u: 'mm', g: 'Dimensions' },
      { k: 'H', l: 'Hauteur', t: 'num', d: 2500, u: 'mm', g: 'Dimensions' },
      { k: 'adossee', l: 'Adossée (côté arrière au mur)', t: 'bool', d: false, g: 'Dimensions' },
      { k: 'col', l: 'Poteaux', t: 'prof', d: 'TC100x100x4', list: 'pilier', g: 'Ossature' },
      { k: 'colMax', l: 'Entraxe maxi poteaux', t: 'num', d: 3500, u: 'mm', g: 'Ossature' },
      { k: 'beam', l: 'Poutres périphériques', t: 'prof', d: 'TR150x100x5', list: 'limonCentral', g: 'Ossature' },
      { k: 'slat', l: 'Chevrons / lames', t: 'prof', d: 'TR80x40x3', list: 'cadre', g: 'Toiture' },
      { k: 'slatGap', l: 'Entraxe des lames', t: 'num', d: 200, u: 'mm', g: 'Toiture' },
      { k: 'slatRot', l: 'Orientation des lames', t: 'num', d: 0, u: '°', g: 'Toiture' }
    ].concat(masonParams({ mat: 'enduit-ton-pierre', floor: 'terrasse-bois' })),
    build: function (p) {
      var m = new Model('Pergola ' + p.L + 'x' + p.P), bm = profile(p.beam), col = profile(p.col), yb = p.H - bm.sh / 2;
      var nx = Math.max(1, Math.ceil(p.L / p.colMax - 1e-9)), zs = p.adossee ? [0] : [0, p.P];
      zs.forEach(function (z) { for (var i = 0; i <= nx; i++) { m.member('PO', 'Poteau', col.id, [i * p.L / nx, 0, z], [i * p.L / nx, p.H - bm.sh, z]);
        m.plate('PL', 'Platine', 'XZ', [[i * p.L / nx - 100, z - 100], [i * p.L / nx + 100, z - 100], [i * p.L / nx + 100, z + 100], [i * p.L / nx - 100, z + 100]], 10, 5, 10); } });
      m.chain('PB', 'Poutre périphérique', bm.id, [[0, yb, p.P], [0, yb, 0], [p.L, yb, 0], [p.L, yb, p.P]].concat(p.adossee ? [] : [[0, yb, p.P]]), { cut: bm.vw });
      if (p.adossee) m.member('PB', 'Lisse murale', 'UPN120', [0, yb, p.P], [p.L, yb, p.P], { rot: 90 });
      var sl = profile(p.slat), n = Math.max(1, Math.round(p.L / p.slatGap));
      for (var j = 1; j < n; j++) m.member('LA', 'Lame', sl.id, [j * p.L / n, p.H + sl.sh / 2, 0], [j * p.L / n, p.H + sl.sh / 2, p.P], { rot: p.slatRot });
      m.value('Lames', n - 1); m.value('Poteaux', (nx + 1) * zs.length);
      mBox(m, 'Terrasse', p.floorMat, -600, -600, p.L + 600, p.P + 600, -150, 150);
      if (p.adossee) mBox(m, 'Mur', p.masonMat, -1000, p.P + 80, p.L + 1000, p.P + 280, -150, p.H + 900);
      masonValue(m);
      m.info('Charges', 'Pergola ouverte : vent et éventuelle toile / lames orientables à justifier (NF EN 1991-1-4).', N.EC1);
      return m;
    }
  });

  family({
    id: 'portique', cat: 'Structures & divers', label: 'Portique / abri', icon: 'frame',
    params: [
      { k: 'bays', l: 'Nombre de travées', t: 'num', d: 3, g: 'Dimensions' },
      { k: 'e', l: 'Entraxe des portiques', t: 'num', d: 4000, u: 'mm', g: 'Dimensions' },
      { k: 'P', l: 'Portée', t: 'num', d: 6000, u: 'mm', g: 'Dimensions' },
      { k: 'H', l: 'Hauteur sablière', t: 'num', d: 3000, u: 'mm', g: 'Dimensions' },
      { k: 'roof', l: 'Toiture', t: 'sel', d: '2pans', o: [['2pans', '2 pans'], ['mono', 'Monopente']], g: 'Dimensions' },
      { k: 'pente', l: 'Pente', t: 'num', d: 10, u: '°', g: 'Dimensions' },
      { k: 'col', l: 'Poteaux', t: 'prof', d: 'HEA160', list: 'poteauStructure', g: 'Ossature' },
      { k: 'raf', l: 'Traverses', t: 'prof', d: '__auto', auto: 'traverse', list: 'poutre', g: 'Ossature' },
      { k: 'purlin', l: 'Pannes', t: 'prof', d: 'UPN100', list: 'poutre', g: 'Ossature' },
      { k: 'purlinMax', l: 'Entraxe maxi pannes', t: 'num', d: 1500, u: 'mm', g: 'Ossature' },
      { k: 'brace', l: 'Contreventement (croix)', t: 'prof', d: 'RP16', list: 'barreau', g: 'Ossature' },
      { k: 'snow', l: 'Neige', t: 'num', d: 0.65, u: 'kN/m²', step: 0.05, g: 'Résistance' },
      { k: 'grade', l: 'Nuance', t: 'sel', d: 'S235', o: GRADES, g: 'Résistance' }
    ].concat(masonParams({ mat: 'beton', floor: 'beton-lisse', extra: [] })),
    build: function (p) {
      var m = new Model('Portique ' + p.bays + ' travées ' + p.P), t = Math.tan(p.pente * DEG), col = profile(p.col), raf = profile(p.raf);
      var ridge = p.roof === '2pans' ? p.H + p.P / 2 * t : null, hHigh = p.roof === 'mono' ? p.H + p.P * t : ridge;
      for (var i = 0; i <= p.bays; i++) {
        var x = i * p.e, h0 = p.H, h1 = p.roof === 'mono' ? p.H + p.P * t : p.H;
        m.member('PO', 'Poteau', col.id, [x, 0, 0], [x, h0, 0]); m.member('PO', 'Poteau', col.id, [x, 0, p.P], [x, h1, p.P]);
        if (p.roof === '2pans') { m.member('TR', 'Traverse', raf.id, [x, h0, 0], [x, ridge, p.P / 2]); m.member('TR', 'Traverse', raf.id, [x, ridge, p.P / 2], [x, h1, p.P]); }
        else m.member('TR', 'Traverse', raf.id, [x, h0, 0], [x, h1, p.P]);
      }
      var slopeL = p.roof === '2pans' ? p.P / 2 / Math.cos(p.pente * DEG) : p.P / Math.cos(p.pente * DEG), np = Math.max(1, Math.ceil(slopeL / p.purlinMax - 1e-9));
      var faces = p.roof === '2pans' ? [[[0, p.H], [p.P / 2, ridge]], [[p.P, p.H], [p.P / 2, ridge]]] : [[[0, p.H], [p.P, hHigh]]];
      faces.forEach(function (fc) { for (var k = 0; k <= np; k++) { var z = fc[0][0] + (fc[1][0] - fc[0][0]) * k / np, y = fc[0][1] + (fc[1][1] - fc[0][1]) * k / np + raf.sh / 2 + 50;
        m.member('PA', 'Panne', p.purlin, [0, y, z], [p.bays * p.e, y, z]); } });
      [0, p.bays - 1].forEach(function (b) { if (b < 0) return; var x0 = b * p.e, x1 = x0 + p.e;
        [0, p.P].forEach(function (z) { var hz = z === 0 ? p.H : (p.roof === 'mono' ? hHigh : p.H); m.member('CV', 'Croix de contreventement', p.brace, [x0, 200, z], [x1, hz - 200, z]); m.member('CV', 'Croix de contreventement', p.brace, [x1, 200, z], [x0, hz - 200, z]); }); });
      var span = p.roof === '2pans' ? p.P : p.P, q = p.snow * 1e-3 * p.e, gq = (0.15 * 1e-3 * p.e + raf.kgm * 9.81e-3);
      beamChecks(m, 'traverse (approche poutre simple)', raf.id, span, q, gq, p.grade, 200);
      m.value('Emprise', f0(p.bays * p.e) + ' x ' + f0(p.P) + ' mm'); m.value('Faîtage', f0(hHigh) + ' mm');
      mBox(m, 'Dalle', p.masonMat, -500, -500, p.bays * p.e + 500, p.P + 500, -220, 200); mBox(m, 'Revêtement', p.floorMat, -500, -500, p.bays * p.e + 500, p.P + 500, -20, 20);
      masonValue(m);
      m.warn('Note de calcul', 'Charpente : neige, vent, stabilité d\'ensemble, assemblages et fondations à justifier par un bureau d\'études (NF EN 1991, NF EN 1993).', N.EC3);
      m.info('Exécution', 'Ouvrage porteur : NF EN 1090-2, EXC2 par défaut.', N.EXE);
      return m;
    }
  });

  family({
    id: 'echelle', cat: 'Structures & divers', label: 'Échelle à crinoline', icon: 'ladder',
    params: [
      { k: 'use', l: 'Référentiel', t: 'sel', d: 'batiment', o: [['batiment', 'Bâtiment (NF E85-016)'], ['machine', 'Machine (NF EN ISO 14122-4)']], g: 'Usage' },
      { k: 'climb', l: 'Hauteur à franchir', t: 'num', d: 6000, u: 'mm', g: 'Dimensions' },
      { k: 'W', l: 'Largeur entre montants', t: 'num', d: 450, u: 'mm', g: 'Dimensions' },
      { k: 'pitch', l: 'Pas visé des échelons', t: 'num', d: 280, u: 'mm', g: 'Dimensions' },
      { k: 'ext', l: 'Dépassement des montants', t: 'num', d: 1100, u: 'mm', g: 'Dimensions' },
      { k: 'wall', l: 'Dégagement mur / échelon', t: 'num', d: 200, u: 'mm', g: 'Dimensions' },
      { k: 'stile', l: 'Montants', t: 'prof', d: 'PL60x10', list: 'plat', g: 'Échelle' },
      { k: 'rung', l: 'Échelons', t: 'prof', d: '__auto', auto: 'échelon', list: 'echelon', g: 'Échelle' },
      { k: 'cage', l: 'Crinoline', t: 'bool', d: true, g: 'Crinoline' },
      { k: 'cageStart', l: 'Départ 1er arceau', t: 'num', d: 2500, u: 'mm', g: 'Crinoline', show: ['cage', true] },
      { k: 'hoopMax', l: 'Espacement maxi arceaux', t: 'num', d: 1200, u: 'mm', g: 'Crinoline', show: ['cage', true] },
      { k: 'D', l: 'Diamètre intérieur', t: 'num', d: 700, u: 'mm', g: 'Crinoline', show: ['cage', true] },
      { k: 'barMax', l: 'Espacement maxi filants', t: 'num', d: 280, u: 'mm', g: 'Crinoline', show: ['cage', true] },
      { k: 'hoop', l: 'Arceaux / filants', t: 'prof', d: 'PL40x5', list: 'plat', g: 'Crinoline', show: ['cage', true] }
    ].concat(masonParams({ mat: 'beton', floor: 'beton-lisse', extra: [
      { k: 'context', l: 'Contexte', t: 'sel', d: 'facade', o: [['facade', 'Façade + toiture-terrasse + acrotère'], ['aucun', 'Aucun']], g: 'Maçonnerie & contexte' }] })),
    build: function (p) {
      var m = new Model('Échelle' + (p.cage ? ' à crinoline' : '') + ' H' + p.climb), st = profile(p.stile), xL = -st.wa / 2, xR = p.W + st.wa / 2, top = p.climb + p.ext;
      m.member('MO', 'Montant', st.id, [xL, 0, 0], [xL, top, 0], { rot: 90 }); m.member('MO', 'Montant', st.id, [xR, 0, 0], [xR, top, 0], { rot: 90 });
      var n = Math.max(1, Math.round(p.climb / p.pitch)), pitch = p.climb / n;
      for (var k = 1; k <= n; k++) m.member('E', 'Échelon', p.rung, [0, k * pitch, 0], [p.W, k * pitch, 0]);
      var nb = Math.max(2, Math.ceil(p.climb / 2000) + 1);
      for (k = 0; k < nb; k++) { var y = 300 + k * (p.climb - 300) / (nb - 1); [xL, xR].forEach(function (x) { m.member('PF', 'Patte murale', 'PL50x8', [x, y, 0], [x, y, -p.wall]); }); }
      var hs = 0, bs = 0, nh = 0;
      if (p.cage) {
        var R = p.D / 2, c = (xR - xL) / 2, cx = (xL + xR) / 2, zc = Math.sqrt(Math.max(R * R - c * c, 1)), a0 = Math.atan2(-zc, c), a1 = Math.atan2(-zc, -c) + 2 * Math.PI;
        var at = function (a, yy) { return [cx + R * Math.cos(a), yy, zc + R * Math.sin(a)]; }, start = Math.min(p.cageStart, top);
        nh = Math.max(2, Math.ceil((top - start) / p.hoopMax - 1e-9) + 1); hs = (top - start) / (nh - 1);
        for (var hI = 0; hI < nh; hI++) m.chain('A', 'Arceau', p.hoop, samples(a0, a1, 10).map(function (a) { return at(a, start + hI * hs); }));
        var arc = R * (a1 - a0), nf = Math.max(1, Math.ceil(arc / p.barMax - 1e-9) - 1); bs = arc / (nf + 1);
        for (k = 1; k <= nf; k++) { var a = a0 + (a1 - a0) * k / (nf + 1); m.member('F', 'Filant', p.hoop, at(a, start), at(a, top)); }
      }
      var ref = p.use === 'batiment' ? N.E85 : N.ISO4;
      if (p.context === 'facade') {
        var zw = -p.wall;
        mBox(m, 'Façade', p.masonMat, -1200, zw - 220, p.W + 1200, zw, -220, p.climb + 300 + 220);
        mBox(m, 'Toiture-terrasse', 'beton', -1200, zw - 3500, p.W + 1200, zw - 220, p.climb - 250, 250);
        mBox(m, 'Sol', p.floorMat, -1500, zw, p.W + 1500, 2500, -220, 220);
        m.info('Acrotère', 'Acrotère de 300 mm représenté : prévoir une crosse / passage sur toiture et un garde-corps en rive si nécessaire.');
        masonValue(m);
      }
      m.value('Échelons', n + ' au pas de ' + f1(pitch) + ' mm'); if (p.cage) { m.value('Arceaux', nh + ' à ' + f0(hs) + ' mm'); m.value('Filants', 'espacés de ' + f0(bs) + ' mm'); }
      m.rule(pitch >= 225 && pitch <= 300, 'Pas des échelons', f0(pitch) + ' mm (225–300)', f0(pitch) + ' mm hors 225–300', ref);
      m.rule(p.W >= 400 && p.W <= 600, 'Largeur entre montants', f0(p.W) + ' mm (400–600)', f0(p.W) + ' mm hors 400–600', ref);
      m.rule(p.wall >= 200, 'Dégagement au mur', f0(p.wall) + ' mm ≥ 200', f0(p.wall) + ' mm < 200', ref);
      var maxF = p.use === 'batiment' ? 8000 : 10000;
      m.rule(p.climb <= maxF, 'Volée unique', f0(p.climb) + ' mm ≤ ' + maxF, f0(p.climb) + ' mm > ' + maxF + ' : volées décalées et paliers (≈ tous les 6 m)', ref);
      if (p.climb > 3000 && !p.cage) m.fail('Protection contre la chute', '> 3 m sans crinoline ni antichute (seuil à confirmer)', ref);
      if (p.cage) {
        m.rule(p.cageStart >= 2200 && p.cageStart <= 3000, 'Départ crinoline', f0(p.cageStart) + ' mm (2200–3000)', f0(p.cageStart) + ' mm hors 2200–3000', ref);
        m.rule(hs <= 1500, 'Espacement arceaux', f0(hs) + ' mm ≤ 1500', f0(hs) + ' mm > 1500', ref);
        m.rule(bs < 300, 'Espacement filants', f0(bs) + ' mm < 300', f0(bs) + ' mm ≥ 300', ref);
      }
      bendCheck(m, 'Résistance échelon (1,5 kN au centre)', 1.5 * 1500 * p.W / 4, profile(p.rung).sec.Wpl, 'S235');
      return m;
    }
  });

  // ================================================================== PLIAGE & CHAUDRONNERIE
  var MATS = {
    S235: { l: 'Acier S235', Rm: 430, rmin: 1.0, nat: 0.16, rho: 7850 }, S355: { l: 'Acier S355', Rm: 550, rmin: 1.5, nat: 0.16, rho: 7850 },
    DX51D: { l: 'Acier galva DX51D', Rm: 400, rmin: 1.0, nat: 0.16, rho: 7850 }, INOX304: { l: 'Inox 304L', Rm: 620, rmin: 1.0, nat: 0.22, rho: 7900 },
    ALU5754: { l: 'Alu 5754 H111', Rm: 230, rmin: 1.5, nat: 0.14, rho: 2700 }
  };
  var V_STD = [6, 8, 10, 12, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160];
  function autoV(t) { var v = t < 3 ? 8 * t : t < 10 ? 10 * t : 12 * t; for (var i = 0; i < V_STD.length; i++) if (V_STD[i] >= v - 1e-9) return V_STD[i]; return V_STD[V_STD.length - 1]; }
  /** DIN 6935 : k = 0,65 + 0,5·log10(r/s) (k ≤ 1) ; fibre neutre à r + k·s/2, soit K = k/2. */
  function kFactorDIN(Ri, t) { return Math.min(1, Math.max(0.2, 0.65 + 0.5 * Math.log10(Ri / t))) / 2; }
  /**
   * Développé d'un profil plié en cotes extérieures.
   * L : longueurs d'ailes (cotes extérieures aux arêtes fictives), A : angles de pli signés (degrés de déviation).
   */
  function bendCalc(L, A, t, Ri, K) {
    var oss = A.map(function (a) { var ph = Math.abs(a) * DEG; return ph > 170 * DEG ? Ri + t : Math.tan(ph / 2) * (Ri + t); });
    var ba = A.map(function (a) { return Math.abs(a) * DEG * (Ri + K * t); });
    var flat = L.map(function (l, i) { return l - (i > 0 ? oss[i - 1] : 0) - (i < A.length ? oss[i] : 0); });
    var dev = flat.reduce(function (s, x) { return s + x; }, 0) + ba.reduce(function (s, x) { return s + x; }, 0), pos = [], acc = 0;
    for (var i = 0; i < A.length; i++) { acc += flat[i]; pos.push(acc + ba[i] / 2); acc += ba[i]; }
    return { oss: oss, ba: ba, flat: flat, dev: dev, pos: pos, bd: oss.map(function (o, i) { return 2 * o - ba[i]; }) };
  }
  /** Contour épaissi (section) d'une tôle pliée, plan local (u, v). */
  function bentSection(flat, A, t, Ri) {
    var c = [[0, 0]], dir = 0, rm = Ri + t / 2;
    for (var i = 0; i < flat.length; i++) {
      var p0 = c[c.length - 1]; c.push([p0[0] + Math.cos(dir) * flat[i], p0[1] + Math.sin(dir) * flat[i]]);
      if (i < A.length) {
        var a = A[i] * DEG, sg = a >= 0 ? 1 : -1, p1 = c[c.length - 1], ctr = [p1[0] - Math.sin(dir) * rm * sg, p1[1] + Math.cos(dir) * rm * sg];
        for (var j = 1; j <= 8; j++) { var ang = dir - sg * Math.PI / 2 + a * j / 8; c.push([ctr[0] + Math.cos(ang) * rm, ctr[1] + Math.sin(ang) * rm]); }
        dir += a;
      }
    }
    var left = [], right = [];
    c.forEach(function (q, i) {
      var a0 = i > 0 ? pnorm(psub(q, c[i - 1])) : pnorm(psub(c[1], q)), a1 = i + 1 < c.length ? pnorm(psub(c[i + 1], q)) : a0;
      var tg = pnorm(padd(a0, a1)), nn = [-tg[1], tg[0]];
      left.push(padd(q, pmul(nn, t / 2))); right.push(padd(q, pmul(nn, -t / 2)));
    });
    return left.concat(right.reverse());
  }
  function treadFlat(depth, width, fold, t) {
    var r = bendCalc([fold, depth, fold], [90, 90], t, t, kFactorDIN(t, t));
    return { name: 'Marche tôle pliée — développé ' + f1(r.dev) + ' x ' + f0(width), outline: [[0, 0], [r.dev, 0], [r.dev, width], [0, width]],
             lines: r.pos.map(function (x, i) { return { a: [x, 0], b: [x, width], layer: 'PLIAGE', label: 'P' + (i + 1) + ' 90°' }; }), texts: [] };
  }
  var PRESETS = [['corniere', 'Cornière'], ['U', 'U'], ['Z', 'Z'], ['omega', 'Oméga / chapeau'], ['cassette', 'Cassette de bardage'], ['couvertine', 'Couvertine'], ['bavette', 'Bavette / appui'], ['ourlet', 'Tôle ourlée (180°)'], ['perso', 'Profil personnalisé']];
  function presetShape(p) {
    switch (p.preset) {
      case 'corniere': return [[p.a, p.b], [p.angle]];
      case 'U': return [[p.a, p.b, p.a], [90, 90]];
      case 'Z': return [[p.a, p.b, p.c], [90, -90]];
      case 'omega': return [[p.c, p.a, p.b, p.a, p.c], [-90, 90, 90, -90]];
      case 'cassette': return [[p.c, p.a, p.b, p.a, p.c], [90, 90, 90, 90]];
      case 'couvertine': return [[15, p.a, p.b, p.a, 15], [45, 90, 90, 45]];
      case 'bavette': return [[p.c, p.b, p.a, 12], [-90, 95, 45]];
      case 'ourlet': return [[p.a, p.c], [180]];
      default: {
        var v = String(p.custom || '40,90,100,90,40').split(/[;,\s]+/).filter(Boolean).map(Number), Ls = [], As = [];
        v.forEach(function (x, i) { if (i % 2 === 0) Ls.push(x); else As.push(x); });
        if (As.length >= Ls.length) As = As.slice(0, Ls.length - 1);
        return [Ls, As];
      }
    }
  }
  family({
    id: 'pliage', cat: 'Pliage & chaudronnerie', label: 'Pliage (profil plié)', icon: 'bend',
    params: [
      { k: 'preset', l: 'Profil', t: 'sel', d: 'U', o: PRESETS, g: 'Forme' },
      { k: 'a', l: 'Cote a (aile)', t: 'num', d: 40, u: 'mm', g: 'Forme', show: ['preset', 'corniere', 'U', 'Z', 'omega', 'cassette', 'couvertine', 'bavette', 'ourlet'] },
      { k: 'b', l: 'Cote b (âme / dessus)', t: 'num', d: 100, u: 'mm', g: 'Forme', show: ['preset', 'corniere', 'U', 'Z', 'omega', 'cassette', 'couvertine', 'bavette'] },
      { k: 'c', l: 'Cote c (retour / rebord)', t: 'num', d: 20, u: 'mm', g: 'Forme', show: ['preset', 'Z', 'omega', 'cassette', 'bavette', 'ourlet'] },
      { k: 'angle', l: 'Angle de pli', t: 'num', d: 90, u: '°', g: 'Forme', show: ['preset', 'corniere'] },
      { k: 'custom', l: 'Ailes et angles (ex. 40,90,100,-90,30)', t: 'text', d: '30,90,120,90,60,-45,20', g: 'Forme', show: ['preset', 'perso'] },
      { k: 'L', l: 'Longueur de pliage', t: 'num', d: 2000, u: 'mm', g: 'Tôle' },
      { k: 't', l: 'Épaisseur', t: 'num', d: 2, u: 'mm', step: 0.5, g: 'Tôle' },
      { k: 'mat', l: 'Matière', t: 'sel', d: 'S235', o: Object.keys(MATS).map(function (k) { return [k, MATS[k].l]; }), g: 'Tôle' },
      { k: 'Ri', l: 'Rayon intérieur (0 = épaisseur)', t: 'num', d: 0, u: 'mm', step: 0.5, g: 'Outillage' },
      { k: 'V', l: 'Ouverture de vé (0 = auto)', t: 'num', d: 0, u: 'mm', g: 'Outillage' },
      { k: 'K', l: 'Facteur K (0 = DIN 6935)', t: 'num', d: 0, step: 0.01, g: 'Outillage' },
      { k: 'press', l: 'Capacité presse', t: 'num', d: 100, u: 't', g: 'Outillage' }
    ],
    build: function (p) {
      var sh = presetShape(p), Ls = sh[0], As = sh[1], t = p.t, Ri = p.Ri > 0 ? p.Ri : t, mat = MATS[p.mat] || MATS.S235;
      var K = p.K > 0 ? p.K : kFactorDIN(Ri, t), V = p.V > 0 ? p.V : autoV(t), r = bendCalc(Ls, As, t, Ri, K);
      var m = new Model('Pliage ' + (PRESETS.filter(function (x) { return x[0] === p.preset; })[0] || ['', ''])[1] + ' ép. ' + t + ' — dév. ' + f1(r.dev));
      r.flat.forEach(function (fl, i) { if (fl <= 0) throw new Error('Aile ' + (i + 1) + ' trop courte pour l\'épaisseur et le rayon (longueur droite ' + f1(fl) + ' mm).'); });
      var sec = bentSection(r.flat, As, t, Ri);
      m.plate('TP', 'Tôle pliée', 'YZ', sec.map(function (q) { return [q[0], q[1]]; }), p.L, p.L / 2, t, p.mat === 'INOX304' ? 'inox' : p.mat === 'ALU5754' ? 'alu' : 'acier', { flatArea: r.dev * p.L });
      m.flat({ name: 'Développé ' + f1(r.dev) + ' x ' + f0(p.L) + ' — ' + mat.l + ' ép. ' + t, outline: [[0, 0], [r.dev, 0], [r.dev, p.L], [0, p.L]],
               lines: r.pos.map(function (x, i) { return { a: [x, 0], b: [x, p.L], layer: 'PLIAGE', label: 'P' + (i + 1) + ' ' + Math.abs(As[i]) + '° ' + (As[i] >= 0 ? '↑' : '↓') }; }),
               texts: [{ at: [0, -30], text: 'Ri ' + f1(Ri) + '  K ' + K.toFixed(3) + '  V ' + V } ] });
      m.value('Développé', f1(r.dev) + ' mm'); m.value('Facteur K', K.toFixed(3) + (p.K > 0 ? ' (imposé)' : ' (DIN 6935)')); m.value('Rayon intérieur', f1(Ri) + ' mm'); m.value('Vé', V + ' mm');
      As.forEach(function (a, i) { m.value('Pli ' + (i + 1), Math.abs(a) + '° ' + (a >= 0 ? 'haut' : 'bas') + ' à ' + f1(r.pos[i]) + ' mm — BA ' + f2(r.ba[i]) + ', BD ' + f2(r.bd[i])); });
      var Fm = 1.42 * mat.Rm * t * t / V, F = Fm * p.L / 1000;
      m.value('Effort de pliage', f0(Fm) + ' kN/m → ' + f1(F / 9.81) + ' t sur ' + f0(p.L) + ' mm');
      m.rule(F / 9.81 <= p.press, 'Capacité presse', f1(F / 9.81) + ' t ≤ ' + p.press + ' t', f1(F / 9.81) + ' t > ' + p.press + ' t : augmenter le vé ou fractionner', 'F = 1,42·Rm·t²/V');
      var bmin = 0.7 * V;
      Ls.forEach(function (l, i) { if (l < bmin) m.fail('Aile mini ' + (i + 1), f1(l) + ' mm < 0,7·V = ' + f1(bmin) + ' mm : l\'aile tombe dans le vé', 'règle usuelle'); });
      if (!Ls.some(function (l) { return l < bmin; })) m.ok('Ailes mini', 'toutes ≥ 0,7·V = ' + f1(bmin) + ' mm', 'règle usuelle');
      m.rule(Ri >= mat.rmin * t - 1e-9, 'Rayon mini matière', 'Ri ' + f1(Ri) + ' ≥ ' + f1(mat.rmin * t) + ' (' + mat.l + ')', 'Ri ' + f1(Ri) + ' < ' + f1(mat.rmin * t) + ' : risque de criques', 'valeurs usuelles', true);
      var nat = mat.nat * V; m.info('Rayon naturel', 'En frappe libre, vé ' + V + ' → Ri ≈ ' + f1(nat) + ' mm. ' + (Math.abs(nat - Ri) > t ? 'Écart avec Ri retenu : adapter le vé ou frapper.' : 'Cohérent avec Ri retenu.'));
      if (As.some(function (a) { return Math.abs(a) >= 170; })) m.info('Ourlet', 'Pli à 180° : pré-pli à 30–45° puis écrasement.');
      m.info('Méthode', 'Développé = Σ ailes − Σ BD, BD = 2·(Ri+t)·tan(φ/2) − φ·(Ri+K·t).', N.DIN);
      return m;
    }
  });
  function f2(v) { return (Math.round(v * 100) / 100).toFixed(2); }

  family({
    id: 'virole', cat: 'Pliage & chaudronnerie', label: 'Virole (cylindre roulé)', icon: 'cylinder',
    params: [
      { k: 'D', l: 'Diamètre', t: 'num', d: 600, u: 'mm', g: 'Forme' },
      { k: 'ref', l: 'Diamètre coté', t: 'sel', d: 'int', o: [['int', 'Intérieur'], ['ext', 'Extérieur'], ['moy', 'Moyen']], g: 'Forme' },
      { k: 'H', l: 'Hauteur (largeur de tôle)', t: 'num', d: 1000, u: 'mm', g: 'Forme' },
      { k: 't', l: 'Épaisseur', t: 'num', d: 5, u: 'mm', g: 'Tôle' },
      { k: 'gap', l: 'Jeu de soudure', t: 'num', d: 2, u: 'mm', g: 'Tôle' },
      { k: 'sheet', l: 'Longueur maxi de tôle', t: 'num', d: 3000, u: 'mm', g: 'Tôle' }
    ],
    build: function (p) {
      var Dm = p.ref === 'int' ? p.D + p.t : p.ref === 'ext' ? p.D - p.t : p.D, dev = Math.PI * Dm - p.gap, n = Math.ceil(dev / p.sheet);
      var m = new Model('Virole Ø' + p.D + ' (' + p.ref + ') x ' + p.H + ' ép. ' + p.t);
      m.surface({ kind: 'tube', role: 'Virole', label: 'Virole Ø' + f0(Dm) + ' moyen', a: [0, 0, 0], b: [0, p.H, 0], r1: Dm / 2, t: p.t });
      for (var i = 0; i < n; i++) m.flat({ name: 'Virole — tôle ' + (i + 1) + '/' + n + ' : ' + f1(dev / n) + ' x ' + f0(p.H), outline: [[0, 0], [dev / n, 0], [dev / n, p.H], [0, p.H]], lines: [], texts: [] });
      m.value('Diamètre moyen', f1(Dm) + ' mm'); m.value('Développé', f1(dev) + ' mm' + (n > 1 ? ' en ' + n + ' tôles' : '')); m.value('Masse', f1(Math.PI * Dm * p.H * p.t * 7.85e-6) + ' kg');
      m.info('Roulage', 'Prévoir les amorces (croquage) des rives avant roulage, ou ajouter 2 × ' + f0(1.5 * p.t + 20) + ' mm de chute de cintrage à couper après.');
      m.rule(Dm >= 20 * p.t, 'Rapport D / t', 'D/t = ' + f0(Dm / p.t) + ' ≥ 20', 'D/t = ' + f0(Dm / p.t) + ' < 20 : roulage à chaud ou rouleuse renforcée', 'règle usuelle', true);
      return m;
    }
  });

  family({
    id: 'tronc-cone', cat: 'Pliage & chaudronnerie', label: 'Tronc de cône (réduction)', icon: 'cone',
    params: [
      { k: 'D1', l: 'Grand diamètre (moyen)', t: 'num', d: 800, u: 'mm', g: 'Forme' },
      { k: 'D2', l: 'Petit diamètre (moyen)', t: 'num', d: 300, u: 'mm', g: 'Forme' },
      { k: 'H', l: 'Hauteur', t: 'num', d: 500, u: 'mm', g: 'Forme' },
      { k: 't', l: 'Épaisseur', t: 'num', d: 3, u: 'mm', g: 'Tôle' },
      { k: 'seg', l: 'Nombre de secteurs', t: 'num', d: 1, g: 'Tôle' }
    ],
    build: function (p) {
      if (p.D1 <= p.D2) throw new Error('Le grand diamètre doit être supérieur au petit.');
      var g = Math.sqrt(p.H * p.H + Math.pow((p.D1 - p.D2) / 2, 2)), R1 = g * p.D1 / (p.D1 - p.D2), R2 = R1 - g, th = 360 * (p.D1 / 2) / R1, ts = th / p.seg;
      var m = new Model('Tronc de cône Ø' + p.D1 + '/Ø' + p.D2 + ' H' + p.H);
      m.surface({ kind: 'cone', role: 'Tronc de cône', label: 'Tronc de cône', base: [0, 0, 0], h: p.H, r1: p.D1 / 2, r2: p.D2 / 2, t: p.t });
      var o = arcPts([0, 0], R1, 0, ts * DEG, 48).concat(arcPts([0, 0], R2, ts * DEG, 0, 48));
      for (var i = 0; i < p.seg; i++) m.flat({ name: 'Secteur ' + (i + 1) + '/' + p.seg + ' — R ' + f1(R1) + ' / r ' + f1(R2) + ', angle ' + f2(ts) + '°', outline: o,
        lines: [{ a: [0, 0], b: [R1 * Math.cos(ts * DEG / 2), R1 * Math.sin(ts * DEG / 2)], layer: 'TRACAGE', label: 'axe' }], texts: [] });
      m.value('Génératrice', f1(g) + ' mm'); m.value('Rayon de traçage extérieur R', f1(R1) + ' mm'); m.value('Rayon de traçage intérieur r', f1(R2) + ' mm');
      m.value('Angle du développé', f2(th) + '°' + (p.seg > 1 ? ' (' + p.seg + ' x ' + f2(ts) + '°)' : '')); m.value('Corde du grand arc', f1(2 * R1 * Math.sin(ts * DEG / 2)) + ' mm');
      m.value('Demi-angle au sommet', f1(Math.atan2((p.D1 - p.D2) / 2, p.H) / DEG) + '°');
      m.info('Traçage', 'Tracer depuis le centre O : arcs R et r, angle au centre ' + f2(ts) + '°. Diamètres moyens (cote fibre neutre).');
      return m;
    }
  });

  family({
    id: 'piquage', cat: 'Pliage & chaudronnerie', label: 'Piquage tube sur tube', icon: 'branch',
    params: [
      { k: 'D', l: 'Collecteur Ø extérieur', t: 'num', d: 168.3, u: 'mm', g: 'Tubes' },
      { k: 'd', l: 'Piquage Ø extérieur', t: 'num', d: 88.9, u: 'mm', g: 'Tubes' },
      { k: 'tb', l: 'Épaisseur piquage', t: 'num', d: 3.2, u: 'mm', g: 'Tubes' },
      { k: 'beta', l: 'Angle entre axes', t: 'num', d: 90, u: '°', g: 'Tubes' },
      { k: 'Lb', l: 'Longueur du piquage (axe collecteur → extrémité)', t: 'num', d: 250, u: 'mm', g: 'Tubes' }
    ],
    build: function (p) {
      if (p.d > p.D) throw new Error('Le piquage doit être plus petit ou égal au collecteur.');
      var R = p.D / 2, r = p.d / 2, b = p.beta * DEG, ri = r - p.tb, N2 = 96, C = 2 * Math.PI * r;
      var tAt = function (rr, ph) { return (Math.sqrt(Math.max(R * R - rr * rr * Math.sin(ph) * Math.sin(ph), 0)) - rr * Math.cos(ph) * Math.cos(b)) / Math.sin(b); };
      var cut = [], hole = [], tmin = 1e9, tmax = -1e9;
      for (var i = 0; i <= N2; i++) {
        var ph = 2 * Math.PI * i / N2, t = tAt(r, ph); tmin = Math.min(tmin, t); tmax = Math.max(tmax, t); cut.push([r * ph, p.Lb - t]);
        var ti = tAt(ri, ph); hole.push([R * Math.asin(clamp(ri * Math.sin(ph) / R, -1, 1)), ti * Math.cos(b) - ri * Math.cos(ph) * Math.sin(b)]);
      }
      var m = new Model('Piquage Ø' + p.d + ' sur Ø' + p.D + ' à ' + p.beta + '°');
      m.surface({ kind: 'tube', role: 'Collecteur', label: 'Collecteur Ø' + p.D, a: [-1.5 * p.D, 0, 0], b: [1.5 * p.D, 0, 0], r1: R, t: p.tb });
      m.surface({ kind: 'tube', role: 'Piquage', label: 'Piquage Ø' + p.d, a: [0, 0, 0], b: [p.Lb * Math.cos(b), p.Lb * Math.sin(b), 0], r1: r, t: p.tb, clipR: R });
      m.flat({ name: 'Gabarit de coupe du piquage (à enrouler sur Ø' + p.d + ')', outline: [[0, 0], [C, 0]].concat(cut.slice().reverse()),
               lines: [0, 0.25, 0.5, 0.75].map(function (k) { return { a: [C * k, 0], b: [C * k, p.Lb - tAt(r, 2 * Math.PI * k)], layer: 'TRACAGE', label: f0(k * 360) + '°' }; }), texts: [] });
      m.flat({ name: 'Trou dans le collecteur (développé sur Ø' + p.D + ')', outline: hole, lines: [], texts: [] });
      m.value('Génératrice la plus courte', f1(p.Lb - tmax) + ' mm'); m.value('Génératrice la plus longue', f1(p.Lb - tmin) + ' mm'); m.value('Circonférence du gabarit', f1(C) + ' mm');
      m.rule(p.beta >= 30 && p.beta <= 90, 'Angle', p.beta + '° (30–90°)', p.beta + '° : soudure difficile côté talon', 'règle usuelle', true);
      if (p.d / p.D > 0.8) m.warn('Rapport des diamètres', 'd/D = ' + f2(p.d / p.D) + ' > 0,8 : piquage « égal », préparer la soudure en selle');
      m.info('Soudure', 'Chanfrein variable le long de l\'intersection (talon / flanc / pointe) ; gabarit tracé sur le Ø extérieur.');
      return m;
    }
  });

  family({
    id: 'tremie', cat: 'Pliage & chaudronnerie', label: 'Trémie pyramidale', icon: 'hopper',
    params: [
      { k: 'A', l: 'Ouverture haute (X)', t: 'num', d: 1000, u: 'mm', g: 'Forme' },
      { k: 'B', l: 'Ouverture haute (Z)', t: 'num', d: 800, u: 'mm', g: 'Forme' },
      { k: 'a', l: 'Sortie basse (X)', t: 'num', d: 250, u: 'mm', g: 'Forme' },
      { k: 'b', l: 'Sortie basse (Z)', t: 'num', d: 250, u: 'mm', g: 'Forme' },
      { k: 'H', l: 'Hauteur', t: 'num', d: 700, u: 'mm', g: 'Forme' },
      { k: 't', l: 'Épaisseur', t: 'num', d: 4, u: 'mm', g: 'Tôle' }
    ],
    build: function (p) {
      var hs1 = Math.sqrt(p.H * p.H + Math.pow((p.B - p.b) / 2, 2)), hs2 = Math.sqrt(p.H * p.H + Math.pow((p.A - p.a) / 2, 2));
      var edge = Math.sqrt(p.H * p.H + Math.pow((p.A - p.a) / 2, 2) + Math.pow((p.B - p.b) / 2, 2));
      var m = new Model('Trémie ' + p.A + 'x' + p.B + ' → ' + p.a + 'x' + p.b + ' H' + p.H);
      var T = [[-p.A / 2, p.H, -p.B / 2], [p.A / 2, p.H, -p.B / 2], [p.A / 2, p.H, p.B / 2], [-p.A / 2, p.H, p.B / 2]], Bt = [[-p.a / 2, 0, -p.b / 2], [p.a / 2, 0, -p.b / 2], [p.a / 2, 0, p.b / 2], [-p.a / 2, 0, p.b / 2]];
      var area = 2 * (p.A + p.a) / 2 * hs1 + 2 * (p.B + p.b) / 2 * hs2;
      m.surface({ kind: 'mesh', role: 'Trémie', label: 'Trémie 4 faces', quads: [[T[0], T[1], Bt[1], Bt[0]], [T[1], T[2], Bt[2], Bt[1]], [T[2], T[3], Bt[3], Bt[2]], [T[3], T[0], Bt[0], Bt[3]]], area: area, t: p.t });
      var x0 = 0;
      [['Face avant / arrière (x2)', p.A, p.a, hs1], ['Face latérale (x2)', p.B, p.b, hs2]].forEach(function (fc) {
        var W1 = fc[1], w2 = fc[2], hh = fc[3];
        m.flat({ name: fc[0] + ' — trapèze ' + f0(W1) + ' / ' + f0(w2) + ' h ' + f1(hh), outline: [[x0, 0], [x0 + W1, 0], [x0 + (W1 + w2) / 2, hh], [x0 + (W1 - w2) / 2, hh]], lines: [], texts: [] });
        x0 += W1 + 80;
      });
      m.value('Hauteur vraie des faces X', f1(hs1) + ' mm'); m.value('Hauteur vraie des faces Z', f1(hs2) + ' mm'); m.value('Arête vraie', f1(edge) + ' mm');
      m.value('Pente des faces', f1(Math.atan2(p.H, (p.B - p.b) / 2) / DEG) + '° / ' + f1(Math.atan2(p.H, (p.A - p.a) / 2) / DEG) + '°');
      m.value('Surface de tôle', f2(area / 1e6) + ' m²');
      var slope = Math.min(Math.atan2(p.H, (p.B - p.b) / 2), Math.atan2(p.H, (p.A - p.a) / 2)) / DEG;
      m.rule(slope >= 45, 'Écoulement', 'pente mini ' + f0(slope) + '° ≥ 45° (vrac courant)', 'pente mini ' + f0(slope) + '° < 45° : risque de voûte selon le produit', 'règle usuelle', true);
      m.info('Fabrication', 'Développé par faces (4 tôles, soudure aux arêtes) ; en 1 ou 2 pièces, plier selon les arêtes vraies.');
      return m;
    }
  });

  // ================================================================== REGISTRE & API
  function defaults(id) {
    var f = FAMILIES[id]; if (!f) throw new Error('Ouvrage inconnu : ' + id);
    var o = {}; f.params.forEach(function (q) { o[q.k] = q.d; }); return o;
  }
  function coerce(id, input) {
    var f = FAMILIES[id], o = defaults(id);
    Object.keys(input || {}).forEach(function (k) { if (k in o) o[k] = input[k]; });
    f.params.forEach(function (q) {
      if (q.t === 'num') o[q.k] = num(o[q.k], q.d);
      else if (q.t === 'bool') o[q.k] = o[q.k] === true || o[q.k] === 'true' || o[q.k] === 1;
      else o[q.k] = o[q.k] == null ? q.d : String(o[q.k]);
    });
    return o;
  }
  var ROLE_ATOM = {
    'Poteau': 'Poteau', 'Platine': 'Fixation', 'Platine latérale': 'Fixation', 'Platine de pied': 'Fixation', 'Platine murale': 'Fixation', 'Platine de tirant': 'Fixation', 'Patte murale': 'Fixation',
    'Main courante': 'Main courante', 'Main courante hélicoïdale': 'Main courante', 'Lisse basse': 'Lisse', 'Lisse haute': 'Lisse', 'Lisse intermédiaire': 'Remplissage', 'Sous-lisse': 'Remplissage',
    'Barreau': 'Remplissage', 'Lame': 'Remplissage', 'Câble inox': 'Remplissage', 'Croisillon': 'Remplissage', 'Tôle pleine': 'Remplissage', 'Tôle perforée': 'Remplissage', 'Tôle découpée': 'Remplissage',
    'Plinthe': 'Remplissage', 'Pince à verre': 'Fixation', 'Marche': 'Marche', 'Pli de marche': 'Marche', 'Pas': 'Marche', 'Palier': 'Palier', 'Palier d\'arrivée': 'Palier', 'Cadre de palier': 'Palier',
    'Poteau de palier': 'Palier', 'Limon': 'Limon', 'Limon central': 'Limon', 'Limon hélicoïdal': 'Limon', 'Crémaillère': 'Limon', 'Console': 'Limon', 'Fût central': 'Limon',
    'Cadre': 'Cadre', 'Cadre (traverse haute cintrée)': 'Cadre', 'Montant de queue': 'Cadre', 'Traverse': 'Cadre', 'Écharpe': 'Cadre', 'Écharpe de queue': 'Cadre',
    'Gond': 'Quincaillerie', 'Boîtier serrure': 'Quincaillerie', 'Butée centrale': 'Quincaillerie', 'Rail au sol': 'Quincaillerie', 'Rail autoportant': 'Quincaillerie', 'Chariot à galets': 'Quincaillerie', 'Pilier': 'Pilier'
  };
  function atomOf(role) {
    if (ROLE_ATOM[role]) return ROLE_ATOM[role];
    if (/^Verre/.test(role)) return 'Remplissage'; if (/^Grillage/.test(role)) return 'Remplissage'; if (/Couverture/.test(role)) return 'Couverture';
    return role;
  }
  function composition(m) {
    var g = {};
    m.members.forEach(function (x) { var a = atomOf(x.role); (g[a] = g[a] || {}); g[a][x.role + ' — ' + profile(x.profile).label] = (g[a][x.role + ' — ' + profile(x.profile).label] || 0) + 1; });
    m.plates.forEach(function (x) { var a = atomOf(x.role); (g[a] = g[a] || {}); g[a][x.role] = (g[a][x.role] || 0) + 1; });
    m.surfaces.forEach(function (x) { (g['Chaudronnerie'] = g['Chaudronnerie'] || {}); g['Chaudronnerie'][x.label] = 1; });
    return Object.keys(g).map(function (a) { return { atom: a, items: Object.keys(g[a]).map(function (k) { return { name: k, qty: g[a][k] }; }) }; });
  }

  /**
   * Dimensionnement automatique : chaque profil réglé sur « Auto » prend le plus léger de sa série qui satisfait
   * les vérifications de résistance qui le concernent (recherche dichotomique, les autres profils étant fixés).
   */
  function solve(id, p) {
    var f = FAMILIES[id], chosen = [];
    var vis = function (q) { if (!q.show) return true; var v = p[q.show[0]]; return q.show.slice(1).some(function (x) { return x === v || String(x) === String(v); }); };
    f.params.forEach(function (q) { if (q.t === 'prof' && p[q.k] === '__auto' && !vis(q)) p[q.k] = autoCandidates(q.list)[Math.floor(autoCandidates(q.list).length / 2)]; });
    var autos = f.params.filter(function (q) { return q.t === 'prof' && p[q.k] === '__auto'; });
    var cands = {};
    autos.forEach(function (q) { cands[q.k] = autoCandidates(q.list); p[q.k] = cands[q.k][cands[q.k].length - 1]; });
    autos.forEach(function (q) {
      var c = cands[q.k], re = new RegExp(q.auto || '.', 'i');
      var okAt = function (i) { p[q.k] = c[i]; try { var mm = f.build(p); return !mm.checks.some(function (ch) { return ch.level === 'fail' && re.test(ch.rule); }); } catch (e) { return false; } };
      var lo = 0, hi = c.length - 1, best = -1;
      if (okAt(hi)) { best = hi; while (lo < hi) { var mid = (lo + hi) >> 1; if (okAt(mid)) { best = mid; hi = mid; } else lo = mid + 1; } best = lo; }
      p[q.k] = c[best >= 0 ? best : c.length - 1]; chosen.push({ k: q.k, label: q.l, id: p[q.k], found: best >= 0 });
    });
    var m = f.build(p);
    chosen.forEach(function (a) {
      m.value('Auto · ' + a.label, profile(a.id).label);
      if (!a.found) m.warn('Dimensionnement automatique', a.label + ' : aucun profil de la série ne suffit — choisir une autre famille de profil ou réduire les portées.');
    });
    return { m: m, p: p, auto: chosen };
  }

  /** Construit un ouvrage : résultat complet, sérialisable en JSON (lu par SolidWorks). */
  function build(id, input, opt) {
    var f = FAMILIES[id]; if (!f) throw new Error('Ouvrage inconnu : ' + id);
    var sv = solve(id, coerce(id, input)), p = sv.p, m = sv.m, t = totals(m), n = nest(m, opt && opt.nest), c = cost(m, n, opt && opt.cost);
    var fails = m.checks.filter(function (x) { return x.level === 'fail'; }).length;
    return {
      engine: 'MetalForge', version: VERSION, family: id, label: f.label, params: p, name: m.name, auto: sv.auto,
      members: m.members, plates: m.plates, surfaces: m.surfaces, flats: m.flats, masonry: m.masonry, checks: m.checks, values: m.values, notes: m.notes,
      mass: round(t.mass, 2), paint: round(t.paint, 3), compliant: fails === 0, bom: bom(m), nesting: n, cost: c, composition: composition(m)
    };
  }
  function schema() {
    return Object.keys(FAMILIES).map(function (k) {
      var f = FAMILIES[k];
      return { id: f.id, cat: f.cat, label: f.label, icon: f.icon, params: f.params.map(function (q) {
        if (q.t !== 'prof') return q;
        var o = Object.assign({}, q); o.groups = profileOptions(q.list); return o; }) };
    });
  }

  // ------------------------------------------------------------------ exports texte
  function toCsv(r) {
    var L = ['Repère;Désignation;Matière / profil;Longueur (mm);Largeur (mm);Qté;Masse (kg)'];
    r.bom.forEach(function (b) { var mk = b.marks.length > 4 ? b.marks[0] + '…' + b.marks[b.marks.length - 1] : b.marks.join(','); L.push([mk, b.role, b.label, b.length || '', b.width || '', b.qty, b.kg.toFixed(2).replace('.', ',')].join(';')); });
    L.push(';;;;;TOTAL;' + r.mass.toFixed(2).replace('.', ','));
    return L.join('\r\n');
  }
  function ascii(s) { return String(s).normalize ? String(s).normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^\x20-\x7e]/g, '_') : String(s); }
  /** DXF R12 (LINE, TEXT) des développés, disposés côte à côte. */
  function toDxf(r) {
    var o = ['0', 'SECTION', '2', 'HEADER', '9', '$ACADVER', '1', 'AC1009', '9', '$INSUNITS', '70', '4', '0', 'ENDSEC', '0', 'SECTION', '2', 'ENTITIES'];
    function line(a, b, layer) { o.push('0', 'LINE', '8', layer, '10', a[0].toFixed(3), '20', a[1].toFixed(3), '30', '0', '11', b[0].toFixed(3), '21', b[1].toFixed(3), '31', '0'); }
    function text(at, s, h, layer) { o.push('0', 'TEXT', '8', layer || 'TEXTE', '10', at[0].toFixed(3), '20', at[1].toFixed(3), '30', '0', '40', String(h || 12), '1', ascii(s)); }
    var x0 = 0;
    r.flats.forEach(function (f) {
      var xs = f.outline.map(function (q) { return q[0]; }), ys = f.outline.map(function (q) { return q[1]; }), mx = Math.min.apply(null, xs), my = Math.min.apply(null, ys), w = Math.max.apply(null, xs) - mx;
      var T = function (q) { return [q[0] - mx + x0, q[1] - my]; };
      for (var i = 0; i < f.outline.length; i++) line(T(f.outline[i]), T(f.outline[(i + 1) % f.outline.length]), 'DECOUPE');
      (f.lines || []).forEach(function (l) { line(T(l.a), T(l.b), l.layer || 'PLIAGE'); if (l.label) text(T([(l.a[0] + l.b[0]) / 2 + 4, (l.a[1] + l.b[1]) / 2]), l.label, 10, l.layer || 'PLIAGE'); });
      text([x0, -40], f.name, 14); (f.texts || []).forEach(function (t) { text(T(t.at), t.text, 10); });
      x0 += w + 200;
    });
    o.push('0', 'ENDSEC', '0', 'EOF');
    return o.join('\n') + '\n';
  }

  return {
    version: VERSION, families: FAMILIES, schema: schema, defaults: defaults, build: build, profile: profile, PROFILES: PROFILES,
    bendCalc: bendCalc, kFactorDIN: kFactorDIN, toCsv: toCsv, toDxf: toDxf, INFILLS: INFILLS,
    /** Point d'entrée JSON (C# / Jint) */
    buildJson: function (id, json) { return JSON.stringify(build(id, json ? JSON.parse(json) : {})); },
    schemaJson: function () { return JSON.stringify(schema()); },
    dxfJson: function (json) { return toDxf(JSON.parse(json)); },
    csvJson: function (json) { return toCsv(JSON.parse(json)); }
  };
});
