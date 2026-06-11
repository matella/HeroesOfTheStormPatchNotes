# Nexus Codex — refonte du site HotS Patch Notes

**Date :** 2026-06-11 · **Statut :** spec approuvée (mockup validé en session), à implémenter en session dédiée.

## Vision
Ne plus penser « blog de patch notes » mais **codex de jeu**. Trois piliers :

1. **Hero-first.** L'entrée du site est l'écran de sélection de héros : la grille des ~90 portraits,
   cerclés de la couleur de leur univers, filtrable rôle/univers + recherche instantanée.
2. **L'historique par héros** (le différenciateur). Chaque fiche héros affiche sa timeline de
   changements sur 12 ans (312 patches Nexus, 2014→2026) : badges BUFF/NERF/REWORK, un item par
   patch, lien vers le patch complet. Personne d'autre ne montre ça.
3. **Les patch notes sont des diffs.** Valeurs en monospace, `96 → 110 (+15%)` avec delta calculé
   et coloré, badges par type de patch (Live/PTR/Hotfix/Balance), sections par héros repliables.

## Référence visuelle (mockup validé)
Fond `#0e1016`, surfaces `#16181f`, hairlines `#1a1d2a/#232636`, texte `#e8eaf2` / muted `#8d93a8`
/ hint `#5d6275`. Accent principal violet `#7F77DD`. Couleurs d'univers (anneaux de portrait,
bordure gauche de fiche, kickers) : Warcraft `#EF9F27`, StarCraft `#378ADD`, Diablo `#E24B4A`,
Overwatch `#D85A30`, Nexus `#AFA9EC`. Badges : BUFF fond `#173404` texte `#C0DD97` ; NERF fond
`#501313` texte `#F7C1C1` ; REWORK fond `#26215C` texte `#CECBF6`. Jauges de stats fines (5px) :
santé teal `#1D9E75`, dégâts coral `#D85A30`, difficulté violet. Typo : Inter (déjà chargé) ;
valeurs chiffrées en mono (`--font-mono` existant). Flat — pas de glow/gradients ; la couleur
d'univers est le seul « éclat ».

**Maquette de référence :** ouvrir  dans un
navigateur — les 7 écrans cibles avec les tokens exacts.

## Matière première (vérifiée)
- Le repo `nexus-patch-notes/nexus-patch-notes.github.io` contient `images/heroes/*.png`
  (portraits), `images/battlegrounds/*.png` (15 cartes), `images/icons/`.
- Chaque patch HTML contient des blocs `div.section-block.heroes-section` : icône héros, nom,
  ancre `heroes/<slug>.html#patch<date>`, contenu des changements + sous-section bug fixes.
- Le modèle `PatchSection` (PatchId, Order, SectionType, EntityName, HeroId, Content) existe
  déjà en base — vide aujourd'hui. C'est le pont patches↔héros à remplir.

## Backend (HotsPatchNotes.Api)
- **B1 — Images.** Étendre le sync Nexus : télécharger `images/battlegrounds/*` et
  `images/heroes/*` (via l'API contents + raw, comme les patches) vers le volume image existant
  (`ImageDownloadService`) ; mapper sur `Battleground.ImageUrl` / `Hero.Icon` quand absent.
  Idempotent (skip si fichier présent).
- **B2 — PatchSections depuis Nexus.** Au parsing d'un patch Nexus, extraire chaque
  `heroes-section` → une `PatchSection` (SectionType="Hero", EntityName=nom, HeroId par lookup
  insensible casse/accents, Content = HTML du bloc). Idem battlegrounds-section → SectionType="Map".
  Backfill : re-parser les 310 patches déjà importés (commande/flag one-shot au sync).
- **B3 — Deltas & badges.** Service pur (unit-testé) qui analyse le Content d'une section :
  détecte les paires `X → Y` (ou « increased/reduced from X to Y ») → calcule le % ; classifie la
  section BUFF / NERF / MIXED / REWORK (mots-clés rework/redesign) / BUGFIX. Stocké sur la
  PatchSection (colonne `Classification` + migration) pour ne pas recalculer à chaque requête.
- **B4 — Endpoints.**
  - `GET /api/heroes/{id}/changes` → timeline paginée (patch date/type/nom, classification,
    contenu de section) triée desc.
  - `GET /api/battlegrounds/{id}/changes` → idem côté carte.
  - `GET /api/patches/{id}` enrichi des sections classifiées (pour l'affichage diff).

## Frontend (HotsPatchNotes.Web — 7 pages Razor + layout)
- **F1 — Design system.** Réécrire `app.css` avec les tokens ci-dessus ; conserver les noms de
  variables existants quand possible (migration douce des CSS scoped).
- **F2 — Layout.** Header « NEXUS CODEX » : nav (Héros · Patchs · Battlegrounds), recherche héros
  globale (autocomplete sur /api/heroes), lien admin discret.
- **F3 — Home = hero select.** Grille de portraits (image `Hero.Icon`, fallback initiales),
  anneau couleur univers, filtres rôle/univers (pills), recherche. Clic → fiche.
- **F4 — Fiche héros.** Bandeau teinté univers : portrait 64px, nom, rôle·univers ; jauges
  santé/dégâts/difficulté ; **timeline des changements** (B4) avec badges et diffs mono ;
  bouton « voir le patch complet ». Puis le **kit complet** (maquette 2b) : CAPACITÉS en grille
  2 col. (keycap Q/W/E/Trait + nom + description courte) ; TALENTS PAR NIVEAU en chips (heroics
  niv 10 accentués violet, niveaux secondaires repliés) ; FONCTIONNE AVEC / CONTRÉ PAR en
  mini-avatars (SynergiesJson/CounteredByJson) ; lore & astuces repliés. Multi-formes : un
  sous-onglet par forme (FormatFormName existant).
- **F5 — Patches.** Liste chronologique groupée PAR ANNÉE (jusqu'à l'alpha 2014), pills de
  filtre par type (Tous/Live/PTR/Hotfix/Alpha-Beta), chaque ligne : date mono + badge type + nom
  + résumé « N héros · M cartes » (compté depuis les PatchSections). Détail (F5b, maquette 4b) :
  **sommaire collant** à gauche (Général · héros avec un point de couleur par classification ·
  cartes · bug fixes) ; section héros dépliée structurée : STATS (diffs) → CAPACITÉS (keycap
  Q/W/E/R) → TALENTS par niveau (badges NOUVEAU/RETIRÉ/ajusté) → **commentaire développeurs** en
  citation (bordure violette) ; bug fixes groupés par catégorie, repliés au-delà de 2 lignes ;
  lien « voir la fiche & son historique » par héros. **Fallback robustesse** : si une section ne se
  découpe pas proprement (HTML irrégulier), rendre son HTML source stylé + diffs `X → Y` colorisés
  par regex — beau mais moins sémantique, jamais cassé.
- **F6 — Battlegrounds.** Grille de cartes AVEC images (B1). Détail (maquette validée) : bandeau
  image + nom + royaume/univers + badges (EN ROTATION, nb voies) ; colonne gauche = Objective +
  ObjectiveTiming, MercCamps + BossInfo, Tips (champs existants du modèle) ; colonne droite =
  timeline des changements de la carte (PatchSections type Map, badges + diffs).
- **F7 — About.** Crédits sources (heroespatchnotes, Nexus Patch Notes, BlueTracker) + Blizzard
  disclaimer.
- **F8 — Admin (4 pages existantes restylées, maquette 8).** Même langage visuel, version outil :
  header « NEXUS CODEX / admin » ; Dashboard = cartes métriques (héros / patchs / cartes+images /
  sections classifiées) + boutons Resync (tout / patchs / images) + journal des derniers syncs
  (badges SYNC OK / PARTIEL avec lien d'inspection des sections en fallback). AdminHeroes/Patches/
  Battlegrounds = tables denses mêmes tokens.

## Phases & acceptation
1. **P1 Backend data** (B1+B2+backfill) — accept : `PatchSection` non vide pour ≥250 patches ;
   15 battlegrounds avec image ; portraits héros résolus.
2. **P2 Classification** (B3+B4) — accept : `GET /api/heroes/{muradin}/changes` renvoie une
   timeline multi-années avec classifications plausibles (spot-check 5 héros).
3. **P3 Design system + layout + home** (F1-F3) — accept : grille filtrable avec vrais portraits.
4. **P4 Fiches + patches + battlegrounds** (F4-F7) — accept : parcours complet héros→timeline→
   patch→carte sans page « brute ».
Chaque phase : build conteneur + déploiement box + vérification live avant la suivante.

## Phase 5 (extension validée) — panneau patch dans HotS Overlay
Le stream-overlay (repo Hots-Overlay, Node :8086) consomme l'API Codex (même box, :5001) :
- **En match** : vignette ~320px croisant les héros de la partie en cours avec le dernier patch —
  n'affiche QUE les héros concernés (avatar + badge BUFF/NERF + résumé mono ultra-court, ex.
  « Q dégâts +15% ») + la carte si changée. Mention « N autres héros : aucun changement ».
- **Hors match** : une ligne compacte « PATCH <date> · 7 BUFFS · 5 NERFS · 1 REWORK » avec noms en
  rotation. Mêmes tokens visuels que le Codex.
- Dépend de P2 (classification + endpoints) ; côté overlay = un composant + un fetch. Résumé court
  par héros : nouvelle donnée `short_summary` calculée en B3 (la plus grosse variation de la
  section, formatée « <capacité> <delta%> »).

## Hors scope (plus tard)
Winrates/pick rates (pas de source de stats live fiable post-maintenance) · comparateur de héros ·
mode clair · i18n FR de l'UI (les contenus patch restent en anglais source).
