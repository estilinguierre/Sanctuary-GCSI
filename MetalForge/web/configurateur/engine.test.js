/* Tests du moteur MetalForge (Node ≥ 16, aucune dépendance) : node engine.test.js */
'use strict';
const E = require('./engine.js');
let n = 0, failed = 0;
function test(name, fn) { n++; try { fn(); console.log('  ✔ ' + name); } catch (e) { failed++; console.log('  ✖ ' + name + '\n      ' + e.message); } }
function eq(a, b, msg) { if (a !== b) throw new Error((msg || '') + ' attendu ' + b + ', obtenu ' + a); }
function near(a, b, tol, msg) { if (Math.abs(a - b) > tol) throw new Error((msg || '') + ' attendu ≈' + b + ', obtenu ' + a); }
function ok(c, msg) { if (!c) throw new Error(msg || 'condition fausse'); }
const has = (r, rule, level) => r.checks.some(c => c.rule.indexOf(rule) === 0 && (!level || c.level === level));

console.log('MetalForge engine ' + E.version);

test('21 familles, toutes constructibles et conformes avec leurs valeurs par défaut', () => {
  const ids = E.schema().map(f => f.id);
  eq(ids.length, 21, 'familles');
  ids.forEach(id => {
    const r = E.build(id, {}), j = JSON.stringify(r);
    ok(!/NaN|Infinity/.test(j), id + ' contient NaN/Infinity');
    ok(r.members.length + r.plates.length + r.surfaces.length > 0, id + ' vide');
    ok(r.compliant, id + ' non conforme : ' + r.checks.filter(c => c.level === 'fail').map(c => c.rule).join(', '));
  });
});

test('Schéma sérialisable en JSON (lu par SolidWorks)', () => {
  const s = JSON.parse(E.schemaJson());
  ok(s.every(f => f.params.every(p => p.k && p.t && 'd' in p)), 'paramètres incomplets');
});

test('Profils : masses linéiques EN', () => {
  near(E.profile('TC40x40x2').kgm, 2.31, 0.02); near(E.profile('TR60x40x2').kgm, 2.93, 0.02);
  near(E.profile('IPE200').kgm, 22.4, 0.01); near(E.profile('PL40x8').kgm, 2.51, 0.01); near(E.profile('RP12').kgm, 0.888, 0.005);
  ok(/inconnu/.test((() => { try { E.profile('XYZ'); } catch (e) { return e.message; } })()), 'erreur attendue');
});

test('Garde-corps : vides ≤ 110 et zone basse 0–600 mm (NF P01-012:2024)', () => {
  const r = E.build('garde-corps', {});
  ok(has(r, 'Gabarit T1', 'ok')); ok(has(r, 'Zone basse', 'ok'));
  const l = E.build('garde-corps', { infill: 'lisses' }); ok(has(l, 'Zone basse', 'fail'), 'lisses basses escaladables');
  const c = E.build('garde-corps', { infill: 'cables' }); ok(has(c, 'Câbles tendus', 'warn'));
  ok(has(E.build('garde-corps', { H: 850 }), 'Hauteur de protection', 'fail'));
  ok(has(E.build('garde-corps', { H: 850, thickness: 500 }), 'Hauteur de protection', 'ok'));
});

test('Garde-corps : tous les remplissages construisent', () => {
  E.INFILLS.forEach(([t]) => { const r = E.build('garde-corps', { infill: t, usage: t === 'industriel' ? 'industriel' : 'prive', H: t === 'industriel' ? 1100 : 1000 }); ok(r.members.length > 0, t); });
});

test('Garde-corps en L et en U : poteaux d\'angle et main courante continue', () => {
  const r = E.build('garde-corps', { trace: 'U', L1: 3000, L2: 1500, L3: 1500 });
  eq(r.members.filter(m => m.role === 'Main courante').length, 3, 'main courante (1 barre par côté)');
  eq(r.members.filter(m => m.role === 'Poteau').length, 5, 'poteaux (2 travées + 1 + 1, angles partagés)');
});

test('Verre encastré : pas de poteaux, profil U de pied', () => {
  const r = E.build('garde-corps', { infill: 'verre', glassFix: 'encastre', lb: false });
  eq(r.members.filter(m => m.role === 'Poteau').length, 0); ok(r.members.some(m => m.role === 'Profil U de pied')); ok(r.plates.some(p => p.material === 'verre'));
});

test('Escalier droit : Blondel 630, 15 hauteurs, limons d\'une pièce, développé du limon', () => {
  const r = E.build('escalier-droit', {});
  const v = Object.fromEntries(r.values); eq(v['Hauteurs / marches'], '15 / 14'); eq(v['2h + g'], '630 mm');
  eq(r.members.filter(m => m.role === 'Limon').length, 2); ok(r.flats.some(f => /Limon/.test(f.name)));
  eq(r.plates.filter(p => p.role === 'Marche').length, 14);
});

test('Escalier quart tournant balancé : giron constant sur la ligne de foulée, giron intérieur mini', () => {
  const r = E.build('escalier-quart', {}), v = Object.fromEntries(r.values);
  ok(parseFloat(v['Giron mini côté intérieur']) >= 100, 'giron intérieur'); eq(r.plates.filter(p => p.role === 'Marche').length, 14);
  const g = E.build('escalier-quart', { turn: 'gauche' }), d = E.build('escalier-quart', { turn: 'droite' });
  ok(g.plates[0].outline.some(q => q[1] < 0) && d.plates.some(p => p.outline.some(q => q[1] > 1000)), 'symétrie gauche / droite');
});

test('Escaliers : palier, deux quarts, demi-tournant', () => {
  ok(E.build('escalier-quart', { mode: 'palier' }).plates.some(p => p.role === 'Palier'));
  ok(E.build('escalier-deux-quarts', { mode: 'palier', L2: 2800, H: 3400 }).plates.filter(p => p.role === 'Palier').length === 2, 'deux paliers si volée intermédiaire');
  ok(E.build('escalier-deux-quarts', { mode: 'palier' }).plates.filter(p => p.role === 'Palier').length === 1, 'palier unique si volée intermédiaire trop courte');
  ok(E.build('escalier-demi', {}).plates.filter(p => p.role === 'Palier').length === 1, 'demi-tournant : palier unique');
});

test('Escalier industriel : 30–38°', () => {
  ok(has(E.build('escalier-droit', { usage: 'industriel', h: 210, blondel: 640, opening: 0 }), 'Inclinaison', 'fail'));
});

test('Escalier ERP : mains courantes des deux côtés', () => {
  ok(has(E.build('escalier-droit', { usage: 'erp', h: 160, W: 1200 }), 'Mains courantes', 'fail'));
  ok(has(E.build('escalier-droit', { usage: 'erp', h: 160, W: 1200, rails: 'both' }), 'Mains courantes', 'ok'));
});

test('Hélicoïdal : échappée après une révolution et vides du garde-corps', () => {
  const r = E.build('escalier-helicoidal', {}); ok(has(r, 'Échappée', 'ok')); ok(has(r, 'Vides garde-corps', 'ok'));
});

test('Portail : formes de chapeau, efforts aux gonds, motorisation', () => {
  ['droit', 'cintre', 'gendarme', 'concave', 'biais'].forEach(t => ok(E.build('portail', { top: t }).members.length > 10, t));
  const r = E.build('portail', { motor: true }); ok(has(r, 'Écrasement des doigts', 'warn'));
  ok(Object.fromEntries(r.values)['Vantail 1 : effort horizontal par gond']);
  ok(has(E.build('portail', { type: 'autoportant', motor: true }), 'Cisaillement au pilier'));
});

test('Pliage : développé DIN 6935 d\'une cornière 50x50x2, Ri 2', () => {
  const r = E.build('pliage', { preset: 'corniere', a: 50, b: 50, t: 2, Ri: 2 });
  near(parseFloat(Object.fromEntries(r.values)['Développé']), 96.2, 0.05);
  near(E.kFactorDIN(2, 2), 0.325, 1e-9); eq(r.flats[0].lines.length, 1);
});

test('Pliage : profil personnalisé et aile trop courte', () => {
  const r = E.build('pliage', { preset: 'perso', custom: '30,90,120,90,60,-45,20' }); eq(r.flats[0].lines.length, 3);
  ok(has(E.build('pliage', { preset: 'U', a: 8, t: 2 }), 'Aile mini 1', 'fail'));
});

test('Tronc de cône : rayons et angle du développé', () => {
  const r = E.build('tronc-cone', { D1: 800, D2: 300, H: 500 }), v = Object.fromEntries(r.values);
  const g = Math.hypot(500, 250), R1 = g * 800 / 500;
  near(parseFloat(v['Génératrice']), g, 0.06); near(parseFloat(v['Rayon de traçage extérieur R']), R1, 0.06);
  near(parseFloat(v['Angle du développé']), 360 * 400 / R1, 0.01);
});

test('Piquage 90° : génératrices extrêmes', () => {
  const r = E.build('piquage', { D: 168.3, d: 88.9, beta: 90, Lb: 250 }), v = Object.fromEntries(r.values);
  near(parseFloat(v['Génératrice la plus longue']), 250 - Math.sqrt(84.15 ** 2 - 44.45 ** 2), 0.06);
  near(parseFloat(v['Génératrice la plus courte']), 250 - 84.15, 0.06);
});

test('Mezzanine : escalier d\'accès intégré et contrôles de structure', () => {
  const r = E.build('mezzanine', {}); ok(r.plates.some(p => p.role === 'Marche')); ok(has(r, 'Résistance solive')); ok(has(r, 'Flambement poteau'));
  ok(has(E.build('mezzanine', { joist: 'IPE80', joistMax: 1500 }), 'Flèche solive', 'fail'), 'solive sous-dimensionnée');
});

test('Échelle : volée > 8 m refusée en bâtiment', () => {
  ok(has(E.build('echelle', { climb: 9000 }), 'Volée unique', 'fail'));
});

test('Exports : DXF R12 et CSV', () => {
  const r = E.build('pliage', {}), d = E.toDxf(r);
  ok(d.startsWith('0\nSECTION')); ok(d.trim().endsWith('EOF')); ok(!/[^\x00-\x7f]/.test(d), 'DXF ASCII');
  ok(E.toCsv(E.build('garde-corps', {})).split('\r\n').length > 3);
});

test('Débit : barres de 6 m respectées', () => {
  const r = E.build('cloture', {}); r.nesting.forEach(n => n.bars.forEach(b => ok(b.rest >= -0.5, 'barre dépassée')));
});

console.log((failed ? '✖ ' : '✔ ') + (n - failed) + ' / ' + n + ' tests');
process.exit(failed ? 1 : 0);
