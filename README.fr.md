# DesktopIconsToggle

[![build](https://github.com/YOUR-GITHUB-USERNAME/desktop-icons-toggle/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR-GITHUB-USERNAME/desktop-icons-toggle/actions/workflows/ci.yml)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

*[Read in English](README.md)*

Un petit utilitaire Windows autonome qui masque et affiche les icônes du
bureau via un raccourci clavier ou une icône de la zone de notification,
sans passer par le menu contextuel — et optionnellement fait de même avec
la barre des tâches.

> **L'interface de l'application est uniquement en français.** Il n'existe
> aucune couche de localisation. Ce document est en français ; la version
> anglaise ([README.md](README.md)) sert de porte d'entrée sur GitHub, mais
> décrit exactement la même application.

## Fonctionnalités

- **Raccourci clavier global**, `Ctrl+Alt+D` par défaut, configurable
  depuis le menu de la zone de notification via une fenêtre de capture.
- **Icône de zone de notification**, affichée uniquement quand le bureau
  est au premier plan. Clic gauche pour basculer, clic droit pour le menu.
- **Option de masquage automatique de la barre des tâches**, qui étend la
  même bascule à la barre en activant le **masquage automatique** propre à
  Windows — sans rien masquer ni détruire elle-même.
- **Démarrage automatique** à l'ouverture de session, via la clé `HKCU`
  `Run` — aucun droit administrateur requis, aucun installeur.
- **Résistant aux plantages** : les icônes et l'état de la barre sont
  restaurés à chaque sortie et réparés automatiquement au lancement
  suivant si le processus a été tué plutôt que fermé proprement.
- **Empreinte minimale** : un seul exécutable d'environ 1,5 Mo, aucun
  runtime à installer, environ 3 Mo de mémoire privée et 0 ms de CPU au
  repos.

## Captures d'écran

<!--
  Déposez les deux captures ci-dessous dans docs/images/ puis décommentez
  ce bloc. Un lien d'image cassé est pire que l'absence d'image, d'où le
  commentaire.

  1. docs/images/menu.png — le menu contextuel du tray, montrant les cinq
     entrées (bascule, « Masquer aussi la barre des tâches », le raccourci
     actuel, « Démarrage automatique », « Quitter ») avec leurs icônes
     Lucide colorées et leurs coches.
  2. docs/images/hotkey-dialog.png — la fenêtre de capture du raccourci, en
     cours de capture ou à son étape de confirmation, montrant la police
     système, les coins arrondis et les couleurs du thème.

  ![Menu du tray](docs/images/menu.png)
  ![Fenêtre de capture du raccourci](docs/images/hotkey-dialog.png)
-->

## Installation

1. Télécharger `DesktopIconsToggle.exe` (voir [Releases](../../releases),
   ou le compiler soi-même — voir plus bas).
2. Le copier où vous voulez, par exemple `C:\Tools\`.
3. Le lancer.
4. Clic droit sur l'icône de notification → cocher **Démarrage
   automatique** si vous voulez qu'il démarre avec Windows.

L'exécutable est autonome : aucune installation de runtime .NET n'est
nécessaire.

Windows vous avertira au premier lancement — voir
[Limitations](#limitations).

## Utilisation

Deux façons de basculer :

- **Raccourci clavier global**, `Ctrl+Alt+D` par défaut, configurable
  depuis le menu.
- **Icône de notification**, présente uniquement quand le bureau est au
  premier plan. Clic gauche pour basculer, clic droit pour le menu.

### Le menu

| Entrée | Effet |
|---|---|
| *Afficher / Masquer les icônes* | Bascule, comme le raccourci |
| *Masquer aussi la barre des tâches* | Étend la bascule à la barre des tâches |
| *Raccourci : …* | Ouvre la fenêtre de capture pour changer la combinaison |
| *Démarrage automatique* | Lance l'application à l'ouverture de session |
| *Quitter* | Restaure tout, puis ferme |

### Changer le raccourci

Menu → *Raccourci : …*. Appuyez sur la combinaison souhaitée ; au moins
une touche de modification est requise. **Échap seul et Alt+F4
annulent** — Échap accompagné d'un modificateur reste un choix valide, et
Alt+F4 est refusé parce que l'enregistrer confisquerait « fermer la
fenêtre » à tout le système.

Au tout premier lancement, tant que vous n'avez rien choisi vous-même,
l'application essaie `Ctrl+Alt+D`, puis `Ctrl+Alt+Maj+D`, puis
`Ctrl+Alt+Maj+F9`, et garde la première que Windows accorde. Le menu
affiche celle réellement obtenue.

La combinaison choisie est testée par un enregistrement réel avant
d'être acceptée, ce qui écarte les combinaisons que Windows refuse
d'emblée. Cela ne couvre **pas** le problème de la touche Windows décrit
dans [Limitations](#limitations).

### L'option barre des tâches

L'option n'efface pas la barre en la masquant ou en la détruisant : elle
active le **masquage automatique** propre à Windows. La barre revient
donc quand le pointeur atteint le bas de l'écran, et c'est l'explorateur
qui s'en charge — l'application ne surveille rien et n'ajoute aucune
latence.

Deux conséquences :

- Si vous utilisiez déjà le masquage automatique, il est préservé :
  l'application lit votre réglage au lancement et y revient exactement,
  pas à « désactivé ».
- Le réglage est **global** : il n'existe aucune API Windows pour masquer
  la barre sur un seul écran, donc l'option s'applique à tous.

## Sécurité

- Les icônes du bureau sont restaurées à chaque sortie : l'entrée de menu
  *Quitter*, une exception dans une procédure de fenêtre, `WM_ENDSESSION`
  (fermeture de session ou extinction), et `ProcessExit`.
- La barre des tâches est remise dans l'état d'auto-masquage observé au
  lancement, donc un utilisateur qui utilisait déjà ce réglage le
  conserve — l'application ne revendique jamais un réglage qu'elle n'a
  pas elle-même activé.
- Si le processus est tué, des marqueurs de registre notent que les
  icônes ou la barre ont été masquées par l'application. Le lancement
  suivant lit ces marqueurs et répare les deux. Une restauration qui ne
  peut pas être confirmée **conserve** son marqueur plutôt que de
  l'effacer, pour qu'un lancement futur retente au lieu d'abandonner
  silencieusement.
- Le seul cas qu'aucun programme ne peut couvrir : le processus est tué
  alors que les icônes sont masquées, et jamais relancé. La récupération
  est alors manuelle et tient en deux clics — clic droit sur le bureau,
  **Affichage**, **Afficher les éléments du Bureau** — le même menu que
  celui que l'application utilise elle-même, qu'elle ne désactive jamais.

## Limitations

À lire avant d'ouvrir une issue — plusieurs de ces points tiennent au
fonctionnement de Windows lui-même, pas à un bug de l'application.

- **L'interface est uniquement en français.** Il n'existe aucune couche
  de localisation.
- **L'exécutable n'est pas signé.** Windows SmartScreen affiche
  « Windows a protégé votre ordinateur » lorsqu'il est lancé après un
  téléchargement depuis Internet ; cliquez sur **Informations
  complémentaires** puis **Exécuter quand même**. Une copie via clé USB
  ou un partage réseau local ne porte pas de Mark of the Web et n'affiche
  aucun avertissement. La signature de code est le seul vrai remède, et
  ce projet ne la pratique pas (encore).
- **Les combinaisons avec la touche Windows peuvent s'enregistrer avec
  succès et pourtant ne jamais se déclencher.** L'explorateur traite les
  `Win+<touche>` par son propre hook clavier, en amont de la distribution
  des raccourcis enregistrés : le succès annoncé par `RegisterHotKey` ne
  prouve donc rien pour cette famille. Ceci a été mesuré, pas seulement
  théorisé : `Win+Maj+D` a été accordé par `RegisterHotKey` — un second
  enregistrement depuis un autre processus a été refusé avec
  `ERROR_HOTKEY_ALREADY_REGISTERED`, confirmant que le premier détenait
  bien l'enregistrement — et pourtant aucun message `WM_HOTKEY` n'est
  jamais arrivé. C'est pourquoi aucune combinaison de cette famille n'est
  proposée par défaut.
- **La fenêtre de capture ne peut pas détecter une combinaison déjà
  possédée par une autre application.** C'est cette application qui reçoit
  la frappe à sa place, donc la fenêtre ne voit rien passer et reste
  silencieuse.
- **Le masquage automatique de la barre des tâches est un réglage Windows
  global.** Il n'existe aucune API par écran, donc l'option s'applique à
  tous les moniteurs.
- **Une combinaison couramment utilisée par d'autres applications**
  (`Ctrl+Tab`, `Ctrl+W`, etc.) peut tout de même être choisie — la fenêtre
  avertit, mais laisse continuer — et ce choix confisque ce raccourci
  partout, pas seulement dans cette application.
- **Le repli `WorkerW` pour localiser le bureau n'a jamais été exercé en
  pratique.** Il ne se déclenche qu'avec des fonds d'écran animés ou
  certaines configurations multi-écrans.

## Compilation

**Prérequis :**

- SDK .NET 10
- MSVC et le Windows SDK, fournis par Visual Studio Build Tools
  (NativeAOT lie l'exécutable final avec `link.exe`)

```
dotnet test tests/DesktopIconsToggle.Tests
dotnet publish src/DesktopIconsToggle -c Release -r win-x64
```

L'exécutable est produit dans
`src/DesktopIconsToggle/bin/Release/net10.0-windows/win-x64/publish/DesktopIconsToggle.exe`.

Il cible `net10.0-windows`, `win-x64`, compilé avec `PublishAot=true` :
P/Invoke Win32 écrit à la main, ni WinForms ni WPF. Les compilations
Release retirent les symboles de débogage (`DebugType=none`) — un `.pdb`
NativeAOT pèse environ 10 Mo et porte le nom de toutes les classes et
méthodes, ce qui annulerait l'essentiel de ce que NativeAOT apporte face
à l'analyse du binaire.

Voir [CONTRIBUTING.md](CONTRIBUTING.md) (en anglais) pour les conventions
de code et les exigences de tests, et
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (en anglais) pour un tour de
la conception — la référence complète et faisant autorité reste la
spécification française, à
[`docs/superpowers/specs/2026-08-25-desktop-icons-toggle-design.md`](docs/superpowers/specs/2026-08-25-desktop-icons-toggle-design.md).

## Arborescence du projet

```
src/DesktopIconsToggle/
├── Program.cs           point d'entrée, câblage, récupération après crash, arrêt propre
├── AppWindow.cs          fenêtre top-level cachée, WndProc, boucle de messages
├── Native/Win32.cs       P/Invoke : user32, shell32, gdi32, kernel32
├── IconToggler.cs        localisation de SHELLDLL_DefView, toggle, lecture d'état
├── DesktopWatcher.cs     WinEvent hook, exemptions shell et processus propre
├── HotKey.cs             RegisterHotKey, enregistrement transactionnel
├── HotkeyCapture.cs      fenêtre modale de choix de combinaison
├── HotkeyFormatter.cs    combinaison → libellé français (sans Win32, testable)
├── TaskbarHider.cs       SHAppBarMessage, auto-masquage, état d'origine
├── TaskbarPolicy.cs      règle de propriété de l'auto-masquage (sans Win32, testable)
├── ToggleThrottle.cs     anti-rebond des bascules (sans Win32, testable)
├── Settings.cs           persistance registre, marqueurs de récupération
├── TrayIcon.cs           Shell_NotifyIcon, menu contextuel, TaskbarCreated
├── Autostart.cs          IRegistryValueStore, RegistryValueStore, clé Run
├── DialogLayout.cs       arithmétique pure de mise en page de la fenêtre de capture
├── Theme.cs              détection du thème clair/sombre
├── SystemFont.cs         lecture de la police système du shell
├── IconPalette.cs        colorisation des icônes à contraste garanti
├── MenuIcons.cs          rendu des masques alpha Lucide embarqués
├── LucideIcons.g.cs      généré : masques alpha Lucide embarqués
└── app.ico
tests/DesktopIconsToggle.Tests/   179 tests xUnit — voir CONTRIBUTING.md
tools/                             scripts de génération d'icônes (voir CONTRIBUTING.md)
docs/                              documents d'architecture et spécification française
```

## Licence

[MIT](LICENSE), Copyright (c) 2026 BH Enterprises.

Les icônes du menu du tray sont dérivées de [Lucide](https://lucide.dev)
(licence ISC) — voir [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Crédits

- Icônes : [Lucide](https://lucide.dev).
- Développé par BH Enterprises.
