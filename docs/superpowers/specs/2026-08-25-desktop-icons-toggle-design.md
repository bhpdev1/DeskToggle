# DesktopIconsToggle — Design

**Date :** 2026-08-25
**Statut :** validé, prêt pour plan d'implémentation

## 1. Problème

Masquer/afficher les icônes du bureau Windows sans passer par le menu contextuel
(clic droit → Affichage → « Afficher les éléments du Bureau »). L'utilitaire doit
démarrer avec la session et rester invisible et quasi gratuit en ressources.

## 2. Objectifs

- Toggle des icônes du bureau via **double-clic molette** sur le bureau.
- Toggle via une **icône de zone de notification** présente uniquement quand le
  bureau est au premier plan.
- Démarrage automatique à l'ouverture de session, activable/désactivable depuis
  l'application.
- Empreinte cible : exe ≈ 1,5 Mo, RSS ≈ 5 Mo, 0 % CPU au repos.

## 3. Non-objectifs

- Pas de fenêtre de configuration ni de fichier de paramètres.
- Pas de raccourci clavier global.
- Pas de gestion par moniteur (le toggle Windows est global par conception).
- Pas d'installeur ; l'exe est autonome et se copie à la main.

## 4. Choix techniques

| Décision | Choix | Raison |
|---|---|---|
| Langage / runtime | C# .NET 10, `PublishAot=true`, `win-x64` | Exe natif autonome, pas de runtime à installer, démarrage instantané |
| UI | P/Invoke Win32 brut, fenêtre message-only | WinForms n'est pas supporté en NativeAOT ; évite ~10 Mo d'exe et de RAM |
| Déclencheur souris | `WH_MOUSE_LL` global | Seul moyen fiable de voir un clic milieu sur le bureau |
| Détection bureau | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` | Événementiel : aucun polling, aucun réveil périodique |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Aucun droit administrateur requis |

**Prérequis de build :** SDK .NET 10, MSVC 14.51 (présent), Windows SDK
10.0.26100 (présent). Seul le SDK .NET manque sur la machine cible.

## 5. Architecture

Un seul processus, un seul thread UI portant une fenêtre message-only et sa
boucle de messages. Tous les composants publient vers ce thread ; aucun
verrouillage n'est nécessaire.

```
DesktopIconsToggle/
├── DesktopIconsToggle.csproj
├── Program.cs           point d'entrée, fenêtre message-only, message loop, arrêt propre
├── Native/Win32.cs      P/Invoke : user32, shell32, advapi32, kernel32
├── IconToggler.cs       localisation de SHELLDLL_DefView, toggle, lecture d'état
├── DesktopWatcher.cs    WinEvent hook, événement DesktopForegroundChanged
├── MouseHook.cs         WH_MOUSE_LL, détection du double-clic milieu
├── TrayIcon.cs          Shell_NotifyIcon, menu contextuel, TaskbarCreated
├── Autostart.cs         lecture/écriture de la clé Run
└── app.ico
```

### 5.1 `IconToggler`

Responsabilité : parler à Explorer. Ne connaît ni la souris ni le tray.

- `IntPtr FindDefView()` — `FindWindow("Progman", null)` puis
  `FindWindowEx(progman, 0, "SHELLDLL_DefView", null)`. Si absent (fond d'écran
  animé, second bureau), énumérer les fenêtres de niveau supérieur de classe
  `WorkerW` et chercher un enfant `SHELLDLL_DefView`.
- `bool AreIconsVisible()` — `IsWindowVisible` sur l'enfant `SysListView32` de
  la DefView. **L'état est lu depuis les fenêtres, jamais depuis la valeur de
  registre `HideIcons`**, qui n'est pas mise à jour de façon fiable par le
  message de toggle.
- `void Toggle()` — `PostMessage(defView, WM_COMMAND, 0x7402, 0)`.
- `void EnsureVisible()` — toggle uniquement si les icônes sont masquées.

Si aucune DefView n'est trouvée, les méthodes échouent silencieusement ; l'appli
ne plante pas.

### 5.2 `DesktopWatcher`

`SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, cb, 0, 0,
WINEVENT_OUTOFCONTEXT)`. À chaque changement, `GetClassName` de la fenêtre au
premier plan ; le bureau est actif si la classe vaut `Progman` ou `WorkerW`.
Émet `DesktopForegroundChanged(bool)` seulement quand la valeur change. L'état
initial est calculé au démarrage. Le delegate de callback est conservé dans un
champ statique pour empêcher son ramassage par le GC.

### 5.3 `MouseHook`

`SetWindowsHookEx(WH_MOUSE_LL, ...)` installé une fois pour toute la durée de
vie du processus.

Le callback sort immédiatement (`CallNextHookEx`) si `wParam != WM_MBUTTONDOWN`.
Aucune allocation sur ce chemin. Sur `WM_MBUTTONDOWN` :

1. Si `now - lastMiddleClick > GetDoubleClickTime() * 1.5` → mémoriser l'instant,
   sortir.
2. Sinon, deuxième clic : `WindowFromPoint(pt)`, puis remonter la chaîne des
   parents. Déclencher seulement si une classe de la chaîne appartient à
   { `Progman`, `WorkerW`, `SHELLDLL_DefView`, `SysListView32` }.
3. Poster un message applicatif à la fenêtre message-only ; ne jamais appeler
   `Toggle()` depuis le hook (un hook lent est désinstallé par Windows).
4. Réinitialiser `lastMiddleClick` pour qu'un triple clic ne compte pas double.

Le hook laisse toujours passer l'événement : le clic milieu sur le bureau n'a
aucune action native, rien n'est donc cassé.

**Compromis assumé :** le hook reste actif hors bureau. L'installer dynamiquement
ferait perdre le premier clic pendant que le bureau prend le focus. Le coût réel
est d'une comparaison d'entier par événement souris.

### 5.4 `TrayIcon`

`Shell_NotifyIcon` avec `NIM_ADD` / `NIM_DELETE` piloté par
`DesktopForegroundChanged`. `uCallbackMessage` pointe vers la fenêtre
message-only.

- Clic gauche (`WM_LBUTTONUP`) → toggle.
- Clic droit (`WM_RBUTTONUP`) → `SetForegroundWindow` puis `TrackPopupMenu` :
  - *Afficher / Masquer les icônes* (libellé calculé depuis `AreIconsVisible()`)
  - *Démarrage automatique* (coché selon `Autostart.IsEnabled()`)
  - séparateur
  - *Quitter*
- Sur `RegisterWindowMessage("TaskbarCreated")` : si le bureau est au premier
  plan, réajouter l'icône (redémarrage d'Explorer).
- L'infobulle indique l'état courant.

### 5.5 `Autostart`

Valeur `DesktopIconsToggle` sous
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, contenant le chemin de
l'exe entre guillemets. `IsEnabled()` compare au chemin courant ; si l'exe a été
déplacé, la valeur est réécrite au prochain basculement.

### 5.6 `Program`

1. Mutex nommé `Local\DesktopIconsToggle` ; sortie immédiate si déjà lancé.
2. Création de la fenêtre message-only (`HWND_MESSAGE`).
3. Initialisation de `IconToggler`, `DesktopWatcher`, `MouseHook`, `TrayIcon`.
4. `GetMessage` / `DispatchMessage`.
5. À l'arrêt : `IconToggler.EnsureVisible()`, `NIM_DELETE`, désinstallation des
   hooks.

## 6. Flux de données

```
double-clic molette ──► MouseHook ──► PostMessage(WM_APP_TOGGLE) ─┐
clic gauche tray ─────► TrayIcon ────────────────────────────────►├─► IconToggler.Toggle()
menu contextuel ──────► TrayIcon ────────────────────────────────┘

changement de premier plan ──► DesktopWatcher ──► TrayIcon.SetVisible(bool)
```

## 7. Gestion des erreurs

| Situation | Comportement |
|---|---|
| `SHELLDLL_DefView` introuvable | Le toggle ne fait rien ; l'infobulle affiche « Bureau indisponible » |
| Explorer redémarre | `TaskbarCreated` réajoute l'icône ; les handles sont relocalisés à la demande |
| `SetWindowsHookEx` échoue | L'appli continue avec le seul tray ; l'infobulle le signale |
| Écriture registre refusée | L'entrée de menu se décoche ; aucune exception remontée |
| Fermeture de session (`WM_ENDSESSION`) | Même arrêt propre que *Quitter* |

**Filet de sécurité :** les icônes sont toujours restaurées à la sortie du
processus. Il est impossible de se retrouver avec un bureau vide et aucun moyen
de le rétablir depuis l'application.

## 8. Tests

L'essentiel du code est de l'interopérabilité Win32 : la vérification est
manuelle et scriptée, il n'y a pas de couche métier à tester unitairement.

Tests unitaires (xUnit, projet séparé exclu de la publication AOT) :
- `MouseHook` : machine à états du double-clic, sur une horloge injectée —
  deux clics dans la fenêtre déclenchent, hors fenêtre non, un triple clic
  déclenche une seule fois.
- `Autostart` : lecture/écriture sur une racine de registre redirigée.

Checklist manuelle, à exécuter avant de considérer le travail terminé :
1. Double-clic molette sur zone vide du bureau → les icônes disparaissent ;
   à nouveau → elles réapparaissent.
2. Double-clic molette sur une icône → même résultat, aucun programme lancé.
3. Double-clic molette dans une autre application → aucun effet.
4. Le bureau passe au premier plan → l'icône apparaît dans la zone de
   notification ; une autre fenêtre est activée → elle disparaît.
5. Menu contextuel : chaque entrée agit, l'état coché reflète la réalité.
6. *Démarrage automatique* coché → la valeur de registre existe ; décoché →
   elle est supprimée.
7. Redémarrage d'Explorer via le Gestionnaire des tâches → l'icône revient.
8. *Quitter* alors que les icônes sont masquées → elles sont restaurées.
9. Deuxième lancement → sortie immédiate, une seule instance.
10. Gestionnaire des tâches : mémoire ≈ 5 Mo, CPU 0 % au repos.

## 9. Livrables

- `bin/Release/net10.0/win-x64/publish/DesktopIconsToggle.exe`, autonome.
- `README.md` : usage, raccourci, désinstallation (décocher le démarrage
  automatique puis supprimer l'exe).
