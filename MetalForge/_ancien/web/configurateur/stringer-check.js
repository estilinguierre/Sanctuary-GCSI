/* Contrôles géométriques des limons tôle (utilisé par engine.test.js). */
'use strict';
function segInt(a, b, c, d) { const o = (p, q, r) => (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]); return o(a, b, c) * o(a, b, d) < -1e-6 && o(c, d, a) * o(c, d, b) < -1e-6; }
/** Étendue verticale [bas, haut] du contour à l'abscisse x (intersections avec la verticale). */
function span(P, x) {
  const ys = [];
  for (let i = 0; i < P.length; i++) { const a = P[i], b = P[(i + 1) % P.length]; if ((a[0] - x) * (b[0] - x) <= 0 && Math.abs(b[0] - a[0]) > 1e-9) ys.push(a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0])); }
  return ys.length ? [Math.min(...ys), Math.max(...ys)] : null;
}
function distPtSeg(p, a, b) { const dx = b[0] - a[0], dy = b[1] - a[1], L = dx * dx + dy * dy, t = L ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L)) : 0; return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy); }
/** Retourne la liste des défauts d'un développé de limon. */
function checkStringerFlat(f, D, tv, dTop) {
  const P = f.outline, err = [];
  for (let i = 0; i < P.length; i++) for (let j = i + 2; j < P.length; j++) { if (i === 0 && j === P.length - 1) continue; if (segInt(P[i], P[(i + 1) % P.length], P[j], P[(j + 1) % P.length])) err.push('auto-intersection ' + i + '/' + j); }
  if (Math.min(...P.map(q => q[1])) < -0.01) err.push('sous le sol');
  const x0 = Math.min(...P.map(q => q[0])), x1 = Math.max(...P.map(q => q[0]));
  // épaisseur perpendiculaire : chaque point du bord haut est à D du bord bas (hors coupes d'extrémité)
  const top = [], bot = [];
  for (let k = 0; k <= 200; k++) { const x = x0 + (x1 - x0) * (0.02 + 0.96 * k / 200), s = span(P, x); if (s) { top.push([x, s[1]]); bot.push([x, s[0]]); } }
  let dmin = 1e9, dmax = 0;
  top.forEach((p, i) => {
    if (p[1] < D * 1.5 || i < 8 || i > top.length - 9) return; // zones de coupe (sol, chevêtre)
    let d = 1e9; for (let j = 0; j + 1 < bot.length; j++) d = Math.min(d, distPtSeg(p, bot[j], bot[j + 1]));
    dmin = Math.min(dmin, d); dmax = Math.max(dmax, d);
  });
  // zone trop mince = défaut ; une surépaisseur n'apparaît qu'aux onglets de la rive basse (géométrie exacte d'un décalage parallèle)
  if (dmin < 1e8 && dmin < D - 1) err.push('zone mince ' + dmin.toFixed(1) + ' mm au lieu de ' + D);
  // chaque marche est entièrement prise dans le limon, et le dessus du limon dépasse le nez de dTop
  (f.lines || []).filter(l => l.label).forEach(l => {
    const y = l.a[1];
    for (let k = 0; k <= 10; k++) {
      const x = l.a[0] + (l.b[0] - l.a[0]) * k / 10, s = span(P, Math.min(Math.max(x, x0 + 0.5), x1 - 0.5)); if (!s) { err.push(l.label + ' hors limon'); return; }
      if (s[1] < y + Math.min(dTop, 10) - 0.5) { err.push(l.label + ' : dessus du limon sous la marche (' + (s[1] - y).toFixed(0) + ')'); return; }
      if (s[0] > y - tv + 0.5) { err.push(l.label + ' : marche non portée (bas du limon à ' + (s[0] - (y - tv)).toFixed(0) + ' mm au-dessus du dessous de marche)'); return; }
    }
  });
  // la rive haute passe à dTop au-dessus de chaque nez réel
  (f.noses || []).forEach(nz => { const sp = span(P, Math.min(Math.max(nz[0], x0 + 0.2), x1 - 0.2)); if (sp && Math.abs(sp[1] - (nz[1] + dTop)) > 1) err.push('rive haute à ' + (sp[1] - nz[1]).toFixed(1) + ' mm du nez (attendu ' + dTop + ') en x=' + nz[0]); });
  return err;
}
/** Raccord de deux tronçons consécutifs à l'angle : mêmes hauteurs de rive haute et basse (tolérance 1 mm). */
function checkJoint(fa, fb) {
  if ((fa.meta && fa.meta.stepEnd) || (fb.meta && fb.meta.stepStart)) return []; // arrêt volontaire sur poteau d'angle
  const A = fa.outline, B = fb.outline, xa = Math.max(...A.map(q => q[0])), sa = span(A, xa - 0.01), sb = span(B, Math.min(...B.map(q => q[0])) + 0.01);
  if (!sa || !sb) return ['raccord introuvable'];
  const e = []; if (Math.abs(sa[1] - sb[1]) > 1) e.push('ressaut rive haute ' + (sb[1] - sa[1]).toFixed(1) + ' mm'); if (Math.abs(sa[0] - sb[0]) > 1) e.push('ressaut rive basse ' + (sb[0] - sa[0]).toFixed(1) + ' mm');
  return e;
}
module.exports = { checkStringerFlat, checkJoint, span };
