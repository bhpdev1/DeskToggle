# DeskToggle (Desktop-Icons-Toggle) — Rebranding, DA & Showcase Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> [!IMPORTANT]
> **Statut 2026-10-10 : exécuté, avec corrections.** Ce qui a changé par rapport au plan ci-dessous :
> - DA propre **« Lights Off »** au lieu de la palette FolderTags : encre `#0D0E10`, texte `#ECEBE6`, accent unique `#78B4FF` (bleu des tuiles de l'icône réelle), la touche de clavier comme élément signature. Ce qui reste commun avec FolderTags : Segoe UI Variable et la mise en page identité à gauche, démo à droite. Tokens dans `assets/brand/brand.css`.
> - Logo tiré de la géométrie réelle de l'icône (cadre + 2×2 tuiles, moitié allumée, moitié éteinte).
> - Captures : **vraies** captures (menu, fenêtre du raccourci) mises en scène dans `assets/brand/frames/*.html`, plutôt que de faux mockups en anglais.
> - Un seul `README.md` bilingue dans `desktop-icons-toggle-public` (`README.en.md` et `screenshots/` retirés). Pas d'option « compiler depuis les sources » ni de noms d'API internes.
> - Renommage limité à la marque : l'exe, le namespace et la clé `Run` restent `DesktopIconsToggle`.
> - Pas de `palette.json`. Le README du dépôt privé n'a pas été modifié.
> - Rendu : `.\scripts\render-brand.ps1`.

**Goal:** Aligner `Desktop-Icons-Toggle` sur la même Direction Artistique (DA), identité visuelle, logos, images Hero/Social Preview, captures d'écran et structure de README (Showcase bilingue FR/EN) que `FolderTags`, `LaunchRadar` et `Solv360`.

**Architecture:**
1. **Identité de marque & Naming** : Nom validé définitivement : **DeskToggle** (symétrique à `FolderTags`, clair et direct). Création du logo vectoriel (`logo.svg`) et exports PNG haute résolution (`logo-256.png`, `logo-512.png`).
2. **Génération d'assets via Headless Edge** : Script `scripts/render-brand.ps1` et templates HTML/CSS (`assets/brand/hero.html`, `mockups/*.html`) pour générer `assets/hero.png` (1280x640 @ 2x), `social-preview.png` et les mockups de captures d'écran.
3. **README Showcase Bilingue & Harmonisé** : Réécriture complète de `README.md` et `README.fr.md` calquée sur le standard `FolderTags` / `Solv360` (badges flat-square, pitch exécutif, tableau comparatif/fonctionnalités, grille visuelle 2x2, architecture technique NativeAOT / Win32 sans divulgation de code sensible).
4. **Synchronisation du Dépôt Public** : Réplication propre des assets et README vers `desktop-icons-toggle-public`.

**Tech Stack:**
- HTML5 / CSS3 / SVG (Templates de marque, glassmorphism Mica, typographie Segoe UI Variable)
- PowerShell + Microsoft Edge Headless (`--headless=new --screenshot=...`)
- Markdown GFM (Badges Shields.io flat-square, GitHub alerts, tables)
- .NET 10 NativeAOT / Win32 Shell API (spécifications techniques mentionnées dans le README)

## Global Constraints

- **Aucune divulgation de code sensible** : Positionnement en "Dépôt Showcase" propre, mettant en valeur les performances et l'expérience utilisateur sans exposer la logique interne propriétaire.
- **Cohérence visuelle stricte** : Même ratio d'image hero (1280x640), même palette sombre / graphite / bleu cobalt, mêmes badges Shields.io `style=flat-square`.
- **Rendu déterministe** : Tous les assets graphiques dérivés (PNG) sont compilés automatiquement via headless browser depuis les sources SVG/HTML.
- **Bilinguisme intégral** : En-tête avec navigation d'ancres `English` • `Français`, Pitch exécutif et documentation complète dans les deux langues.

---

### Task 1: Naming & Charte Graphique (Brand Identity)

**Files:**
- Create: `assets/brand/logo.svg`
- Create: `assets/brand/palette.json`

**Interfaces:**
- Produces: Identité vectorielle principale (icône écran + bascule minimale d'icônes / focus zen) avec accents Cobalt `#388AF0` / Cyan `#00D2FF` sur fond graphite sombre `#07080D`.

- [x] **Step 1: Validation du nommage**
  Nom sélectionné et validé : **DeskToggle** (court, technique, parfaitement assorti à `FolderTags`).
  - Baseline FR : *Un raccourci pour masquer vos icônes et la barre des tâches. Concentration instantanée, zéro surcharge.*
  - Baseline EN : *One keystroke to hide desktop icons and taskbar. Instant focus, zero bloat.*

- [ ] **Step 2: Créer le logo vectoriel `assets/brand/logo.svg`**
  Concevoir un SVG 512x512 moderne :
  - Silhouette d'écran ou grille de bureau aux coins adoucis (radius 28px).
  - Motifs d'icônes stylisées à gauche basculant vers un espace pur et dégagé à droite.
  - Curseur / Switch discret avec halo néon bleu cobalt (`#388AF0`).
  - Dégradé subtil et bordure fine `rgba(255, 255, 255, 0.12)`.

- [ ] **Step 3: Vérifier le rendu visuel du SVG**
  Ouvrir et inspecter le SVG dans un navigateur ou générer une prévisualisation temporaire.

---

### Task 2: Script de Rendu Automatisé des Assets (`render-brand.ps1`)

**Files:**
- Create: `scripts/render-brand.ps1`

**Interfaces:**
- Consumes: `assets/brand/logo.svg`, `assets/brand/hero.html`, `assets/brand/mockups/*.html`
- Produces: `assets/brand/logo-256.png`, `assets/brand/logo-512.png`, `assets/hero.png`, `assets/brand/social-preview.png`, `assets/screenshots/*.png`

- [ ] **Step 1: Écrire le script PowerShell de capture headless Edge**
  Créer `scripts/render-brand.ps1` calqué sur l'implémentation de `FolderTags` :
  - Détection automatique de Microsoft Edge ou Google Chrome.
  - Fonction `RenderUrl` avec support du facteur d'échelle DPI (scale 1x et 2x Retina) et transparence de fond (`--default-background-color=00000000`).
  - Tableau de tâches de rendu (logos, hero, mockups UI).

- [ ] **Step 2: Valider l'exécution unitaire sur `logo.svg`**
  Lancer `.\scripts\render-brand.ps1 -Only logo` et vérifier la création de `logo-256.png` et `logo-512.png`.

---

### Task 3: Templates HTML des Mockups UI & Hero Banner

**Files:**
- Create: `assets/brand/mockups/desktop-toggle.html`
- Create: `assets/brand/mockups/tray-menu.html`
- Create: `assets/brand/mockups/hotkey-dialog.html`
- Create: `assets/brand/mockups/taskbar-focus.html`
- Create: `assets/brand/hero.html`

**Interfaces:**
- Consumes: `assets/brand/logo.svg`
- Produces: Pages web autonomes pixel-perfect reproduisant les écrans réels de l'application sous Windows 11 (thème sombre, police Segoe UI Variable, effets Mica et Lucide icons).

- [ ] **Step 1: Créer le mockup `desktop-toggle.html`**
  Présenter un avant/après élégant : un bureau Windows 11 avec icônes versus le même bureau instantanément épuré (Zen / Clean view) avec indicateur de raccourci `Ctrl+Alt+D`.

- [ ] **Step 2: Créer le mockup `tray-menu.html`**
  Reproduire fidèlement le menu contextuel sombre WinUI 3 avec ses icônes Lucide colorées :
  - *Afficher / Masquer les icônes*
  - *Masquer aussi la barre des tâches* (avec case cochée)
  - *Raccourci : Ctrl + Alt + D*
  - *Démarrage automatique*
  - *Quitter*

- [ ] **Step 3: Créer le mockup `hotkey-dialog.html`**
  Reproduire la modale de capture de raccourci avec détection de touches et layout système moderne.

- [ ] **Step 4: Créer le template de bannière `assets/brand/hero.html`**
  Format 1280x640 :
  - Arrière-plan sombre avec wallpaper abstrait et flou directionnel Mica.
  - Colonne gauche : Logo 84x84, Titre (`DeskToggle`), baseline percutante en gras, sous-titre explicatif, chips technologiques (`NativeAOT`, `0 ms Idle CPU`, `Crash-Safe`, `Win32 Hook`).
  - Colonne droite : Composition dynamique en perspective avec ombre portée douce intégrant les mockups UI (le menu tray et le widget de bascule).

- [ ] **Step 5: Exécuter le rendu complet**
  Lancer `.\scripts\render-brand.ps1` pour compiler tous les PNG dans `assets/` et `assets/screenshots/`.

---

### Task 4: Rédaction du README Showcase Standardisé

**Files:**
- Modify: `README.md`
- Modify: `README.fr.md`

**Interfaces:**
- Consumes: `assets/brand/logo-256.png`, `assets/hero.png`, `assets/screenshots/*.png`
- Produces: Documentation produit de niveau Showcase GitHub, bilingue, concise et valorisante.

- [ ] **Step 1: Rédiger l'en-tête centré et les badges flat-square**
  ```html
  <div align="center">
    <img src="assets/brand/logo-256.png" alt="DeskToggle logo" width="112" height="112">
    # DeskToggle
    **One keystroke to hide desktop icons and taskbar. Instant focus, zero bloat.**<br>
    <em>Un raccourci pour masquer vos icônes et votre barre des tâches. Concentration instantanée, zéro surcharge.</em>
    <!-- Badges flat-square : Windows 11 | 10, NativeAOT, Idle CPU: 0ms, RAM: ~3MB, License MIT -->
    <img src="assets/hero.png" width="100%">
    <a href="#pitch">Pitch & Vision</a> • <a href="#english">English</a> • <a href="#français">Français</a>
  </div>
  ```

- [ ] **Step 2: Ajouter la notice Showcase Repository**
  Encadré `> [!NOTE]` expliquant la nature de la vitrine publique :
  - Dépôt vitrine présentant la documentation, l'architecture et les versions binaires.
  - Mettre en valeur la sécurité, l'absence de télémétrie et la compilation NativeAOT sans runtime lourd.

- [ ] **Step 3: Rédiger la section Executive Pitch**
  Expliquer le cas d'usage professionnel :
  - Problème : bureaux encombrés lors de partages d'écran, streaming, réunions Teams/Meet ou sessions de deep work ; menu contextuel Windows lent et fastidieux.
  - Solution : bascule globale instantanée (hotkey ou tray icon discret), masquage optionnel de la barre des tâches, restauration automatique infaillible en cas de crash.

- [ ] **Step 4: Rédiger le tableau des fonctionnalités & la grille visuelle**
  - Tableau à 2 colonnes (Feature / Value Delivered).
  - Grille HTML 2x2 sous `<div align="center">` intégrant les captures de `assets/screenshots/`.

- [ ] **Step 5: Rédiger les sections Techniques, Installation & Déploiement**
  - Option A : Exécutable autonome portable pré-compilé (`DeskToggle.exe`, 1.5 Mo).
  - Option B : Compilation source avec le SDK .NET 10 NativeAOT (`dotnet publish -c Release`).
  - Architecture technique synthétique : boucle de messages Win32 `SetWindowLongPtr`, injection message `WM_COMMAND` vers `SHELLDLL_DefView`, gestion de l'état `ABM_SETSTATE` pour la barre des tâches sans altération du système.

- [ ] **Step 6: Synchroniser `README.fr.md` ou fusionner la section française**
  Garantir la parité exacte du contenu en langue française comme sur FolderTags/Solv360.

---

### Task 5: Synchronisation avec le Dépôt Public (`desktop-icons-toggle-public`)

**Files:**
- Modify/Sync: `c:\Dev\projects\desktop-icons-toggle-public\`

**Interfaces:**
- Consumes: Assets générés et README finalisés de `desktop-icons-toggle`
- Produces: Dépôt public prêt à être publié avec historique propre et sans fuite de données internes.

- [ ] **Step 1: Copier l'arborescence `assets/` dans `desktop-icons-toggle-public/assets/`**
- [ ] **Step 2: Synchroniser le `README.md` et `README.en.md` dans `desktop-icons-toggle-public`**
- [ ] **Step 3: Vérification de sécurité anti-fuite**
  Vérifier `git status` dans le dossier public pour confirmer qu'aucun fichier source C#, fichier binaire lourd non désiré ou log temporaire n'est présent.

---

## Self-Review Checklist

1. **Couverture de la demande utilisateur :**
   - [x] Même DA que FolderTags, LaunchRadar, Solv360 (logo vectoriel, hero banner 1280x640, mockups soignés).
   - [x] Propositions de changement de nom incluses.
   - [x] Format Showcase GitHub (sans excès d'informations internes sensibles mais percutant).
   - [x] Aucun lancement de commande destructive ou prématurée ("lance rien juste le plan détaillé").
2. **Qualité technique :**
   - Utilisation du moteur éprouvé `render-brand.ps1` via headless Edge pour un rendu graphique reproductible.
   - Respect strict des conventions Markdown et des tokens de style existants.
