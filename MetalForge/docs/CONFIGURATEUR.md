# 🧩 Configurateur atomique MetalForge

Un **seul moteur** de briques paramétrables construit tous les ouvrages. Il est utilisé à deux endroits :

| Où | Comment | Pour quoi faire |
|---|---|---|
| 🌐 **Navigateur / téléphone** | `web/configurateur/index.html` (3D three.js) | chiffrer et montrer au client, vérifier la conformité, sortir les développés |
| 🛠️ **SolidWorks** | même page dans une fenêtre WebView2, commande **Configurateur** | **Générer** la pièce mécano-soudée (membres structurels + tôles) |
| ⚙️ **C# / tests** | `MetalForge.Core.Atomic.ConfiguratorEngine` exécute `engine.js` avec Jint | débit, devis, note de calcul, repli sans WebView2 |

Le même fichier `engine.js` sert dans les trois cas. Les tests C# vérifient que le modèle importé en C# a la même masse que celle calculée par le moteur JavaScript (écart ≤ 4 %, toutes familles).

![Escalier quart tournant balancé](images/configurateur-escalier-quart.png)

---

## ⚛️ Atomes → assemblages → ouvrages

```mermaid
flowchart LR
    subgraph ATOMES
        A1[Profilé<br/>123 sections + toute cote]
        A2[Tôle / verre / bois<br/>grillage / béton]
        A3[Câble inox]
        A4[Platine · pince<br/>gond · serrure · rail]
    end
    subgraph ASSEMBLAGES
        M1[Remplissage<br/>13 types]
        M2[Travée / garde-corps<br/>le long d'un tracé]
        M3[Cadre<br/>5 formes de chapeau]
        M4[Ligne de foulée<br/>marches balancées · paliers]
        M5[Limons<br/>latéraux · central · crémaillère]
        M6[Crinoline]
        M7[Pli / développé<br/>DIN 6935]
    end
    subgraph OUVRAGES
        O1[Garde-corps · clôture]
        O2[Portails]
        O3[7 types d'escaliers]
        O4[Mezzanine · passerelle<br/>auvent · pergola · portique · échelle]
        O5[Pliage · virole · cône<br/>piquage · trémie]
    end
    A1 --> M1 & M2 & M3 & M5 & M6
    A2 --> M1 & M7
    A3 --> M1
    A4 --> M2 & M3
    M1 --> M2 & M3
    M2 --> O1 & O3 & O4
    M3 --> O2
    M4 --> O3
    M5 --> O3
    M6 --> O4
    M7 --> O5
    O3 -. accès intégré .-> O4
```

Les assemblages se réutilisent entre ouvrages :
- le **remplissage** sert dans le garde-corps, la clôture, le portail, le rampant d'escalier et la mezzanine ;
- le **garde-corps le long d'un tracé** suit aussi bien un balcon en U que le limon d'un escalier balancé ;
- la **mezzanine** intègre un escalier droit ou une échelle complète, avec leurs propres vérifications.

---

## 📦 Éventail

### Garde-corps & clôtures
- **Tracé :** droit, en L, en U, rampant.
- **Pose :**
  - à la française (platine) ;
  - à l'anglaise (platine latérale) ;
  - à l'anglaise avec déport ;
  - scellement ;
  - verre encastré en pied (profil U, sans poteau).
- **Main courante :** filante sur les poteaux ou entre poteaux. Lisse basse en option.
- **Remplissages (13) :**
  - barreaudage vertical, lisses, câbles inox (horizontaux ou verticaux) ;
  - lames / claustra (inclinables), croisillons ;
  - tôle pleine, tôle perforée, tôle découpée laser (motifs) ;
  - verre feuilleté 44.2 à 88.2 (pinces, parcloses, encastré), grillage / treillis soudé ;
  - **mixte** (soubassement + partie haute, chacun au choix) ;
  - industriel (lisse + sous-lisse + plinthe), aucun.
- **Contrôles :**
  - NF P01-012:2024 : hauteur, gabarit Ø 110, zone basse 0–600 mm, câbles tendus ;
  - ISO 14122-3 en industriel ;
  - résistance des poteaux et de la main courante, effort par cheville.
- **Clôture :** terrain en pente avec panneaux rampants ou en redans, barreaux à pointes, scellement ≥ H/3.

### Portails & portillons
- **Types :** battant 1 ou 2 vantaux, portillon, coulissant sur rail, autoportant (queue + écharpe, chariots).
- **Formes du haut :** droit, cintré, chapeau de gendarme, concave, en biais (flèche réglable).
- **Cadre :** traverses intermédiaires, écharpe, barreaux traversants ou à pointes. Tous les remplissages ci-dessus.
- **Quincaillerie :** gonds, serrure, butée, rail, piliers.
- **Calculs et contrôles :**
  - efforts horizontaux par gond ;
  - sécurité motorisation : doigts 25 mm (ISO 13854), cisaillement au pilier, NF EN 12453.

### Escaliers (tous types)

| Type | Particularités |
|---|---|
| Droit | Blondel ou reculement imposé, échappée sous trémie, **développé du limon tôle** |
| Quart tournant | gauche / droite, **marches balancées** (nombre réglable) ou **palier** |
| Deux quarts tournants | volée intermédiaire, paliers ou balancement |
| Demi-tournant | palier intermédiaire unique ou balancement, jour réglable |
| Hélicoïdal | fût central, rotation auto, ligne de foulée, palier d'arrivée, échappée après une révolution |
| Échelle de meunier | 45–75°, mains courantes deux côtés |
| Pas japonais | pas alternés sur limon central |

Options communes :
- **Marches :** tôle larmée, **tôle pliée** (développé fourni), caillebotis, bois, cassette béton.
- **Limons :** deux limons latéraux, limon central avec consoles, crémaillères, limon extérieur seul.
- **Garde-corps :** rampant intérieur, extérieur ou les deux, avec tous les remplissages compatibles.
- **Règles par usage :**
  - logement : h ≤ 180, g ≥ 240 ;
  - logement collectif : h ≤ 170, g ≥ 280 ;
  - ERP neuf : h ≤ 160, g ≥ 280, largeur 1,20 m, mains courantes des deux côtés ;
  - industriel : 30–38°.
- **Contrôles toujours appliqués :** Blondel et giron intérieur des marches balancées.
- **Résistance :** limons et marches.

### Structures & divers
- **Mezzanine / plateforme :** trame de poteaux, poutres, solives, platelage, garde-corps, ouverture d'accès, **escalier droit ou échelle intégrés**. Vérifications : solives, poutres (flèche L/300), flambement des poteaux (Eurocode 3, courbe c).
- **Passerelle :** poutres de rive, traverses, platelage, garde-corps industriels, appuis.
- **Auvent / marquise :** consoles, tirants (effort calculé), couverture verre, tôle ou polycarbonate.
- **Pergola :** autoportante ou adossée, lames orientables.
- **Portique / abri :** travées, 2 pans ou monopente, pannes, croix de contreventement.
- **Échelle à crinoline :** NF E85-016 ou ISO 14122-4.

### Pliage & chaudronnerie
- **Pliage :**
  - profils : cornière, U, Z, oméga, cassette, couvertine, bavette, ourlet 180°, **profil personnalisé** (suite « aile, angle, aile… ») ;
  - développé DIN 6935 (facteur K automatique ou imposé) ;
  - position des plis, effort de presse F = 1,42·Rm·t²/V ;
  - aile mini 0,7·V, rayon mini matière, rayon naturel en frappe libre.
- **Virole :** diamètre intérieur, extérieur ou moyen ; découpe en plusieurs tôles ; rapport D/t.
- **Tronc de cône :** rayons de traçage, angle, corde ; découpe en n secteurs.
- **Piquage tube sur tube :** angle quelconque ; **gabarit de coupe** et **trou du collecteur** développés.
- **Trémie pyramidale :** faces en vraie grandeur, arête vraie, pente d'écoulement.

Tous les développés s'exportent en **DXF R12** (calques `DECOUPE`, `PLIAGE`, `TRACAGE`, `TEXTE`).

---

## 🛠️ Dans SolidWorks

1. Onglet **MetalForge → Configurateur** : la page s'ouvre dans SolidWorks.
2. Régler l'ouvrage, puis **Export → Générer dans SolidWorks**.
3. La pièce mécano-soudée est créée :
   - une esquisse 3D et un membre structurel par profilé ;
   - les tôles sur le plan de face, de dessus ou de droite, chacune en corps séparé.
4. **Enregistrer le DXF / la nomenclature** depuis la même page.

Si le runtime **Microsoft Edge WebView2** est absent, la commande ouvre la grille de propriétés classique. Elle utilise le même moteur et les mêmes familles, avec les onglets aperçu, débit, devis et note de calcul.

> ⚠️ Points à connaître :
> - L'aperçu 3D charge three.js depuis le CDN jsDelivr : il faut une connexion internet.
> - Les surfaces de révolution (virole, cône, piquage, trémie) ne sont pas générées en volume SolidWorks. Les créer en **Tôlerie** (pli lofté, bride de base) à partir du DXF fourni.
> - Les escaliers balancés produisent des limons en segments soudés, à reprendre en limon tôle découpé si besoin.

---

## ➕ Ajouter une brique ou un ouvrage

Dans `web/configurateur/engine.js` :

```js
family({
  id: 'mon-ouvrage', cat: 'Structures & divers', label: 'Mon ouvrage', icon: 'frame',
  params: [ { k: 'L', l: 'Longueur', t: 'num', d: 2000, u: 'mm', g: 'Dimensions' } ],
  build: function (p) {
    var m = new Model('Mon ouvrage ' + p.L);
    m.member('P', 'Poteau', 'TC60x60x3', [0, 0, 0], [0, 1000, 0]);   // atome profilé
    infill(m, panneau, { type: 'barreaux', gap: 110 });               // assemblage réutilisé
    m.rule(p.L <= 3000, 'Portée', 'OK', 'trop longue', 'référence');  // contrôle
    return m;
  }
});
```

L'interface web, la grille WinForms, l'export SolidWorks et les tests se mettent à jour automatiquement à partir du schéma `params`. Tests : `node web/configurateur/engine.test.js` puis `dotnet test`.
