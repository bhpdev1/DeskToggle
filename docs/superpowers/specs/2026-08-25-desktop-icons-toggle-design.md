# DesktopIconsToggle — Design

**Date :** 2026-08-25
**Statut :** implémenté. Révisé après la V2 (raccourci clavier, barre des tâches,
retrait du hook souris).

## 1. Problème

Masquer/afficher les icônes du bureau Windows sans passer par le menu contextuel
(clic droit → Affichage → « Afficher les éléments du Bureau »), et optionnellement
la barre des tâches avec. L'utilitaire doit démarrer avec la session et rester
invisible et quasi gratuit en ressources.

## 2. Objectifs

- Toggle des icônes du bureau via un **raccourci clavier global configurable**,
  `Win+Maj+D` par défaut, avec chaîne de repli (§5.3).
- Toggle via une **icône de zone de notification** présente uniquement quand le
  bureau est au premier plan.
- Option **« masquer aussi la barre des tâches »**, étendant la bascule.
- Démarrage automatique à l'ouverture de session, activable/désactivable depuis
  l'application.
- Empreinte cible : exe ≈ 1,5 Mo, RSS ≈ 5 Mo, 0 % CPU au repos.

**Mesuré après la V2 (2026-08-25) :** exe 1 463 808 octets (1,40 Mo) ; jeu de
travail au repos 10,27 Mo mais **mémoire privée 3,01 Mo** ; 0 ms de CPU sur
20 secondes au repos. Les trois cibles sont tenues si l'on retient la mémoire
privée, qui est ce que l'application coûte réellement — le jeu de travail compte
en plus des pages partagées (image de l'exe, DLL système) déjà présentes pour
tout le système. Le retrait du hook souris de la V1 a fait passer l'exe de
1 863 168 à 1 463 808 octets, soit −21 %.

## 3. Non-objectifs

- Pas de gestion par moniteur. `ABM_SETSTATE` est global et Windows n'expose
  aucune API d'auto-masquage par écran ; la seule autre voie serait de masquer
  directement les fenêtres `Shell_SecondaryTrayWnd`, qu'Explorer restaure de
  lui-même et qui ne recalcule pas la zone de travail.
- Pas d'installeur ; l'exe est autonome et se copie à la main.
- Pas de masquage sélectif d'icônes : Windows ne l'expose pas, il faudrait
  piloter la ListView d'Explorer par messages inter-processus.

*(La V1 listait « pas de fenêtre de configuration » et « pas de raccourci clavier
global » comme non-objectifs. La V2 a fait des deux des objectifs.)*

## 4. Choix techniques

| Décision | Choix | Raison |
|---|---|---|
| Langage / runtime | C# .NET 10, `PublishAot=true`, `win-x64` | Exe natif autonome, pas de runtime à installer, démarrage instantané |
| UI | P/Invoke Win32 brut | WinForms n'est pas supporté en NativeAOT ; évite ~10 Mo d'exe et de RAM |
| Fenêtre hôte | Top-level cachée, `WS_EX_TOOLWINDOW` | Voir la correction en §5 : une fenêtre message-only ne reçoit pas les diffusions et ne peut pas porter `RegisterHotKey` |
| Déclencheur clavier | `RegisterHotKey` + `MOD_NOREPEAT` | Windows ne nous réveille qu'à la combinaison ; aucun hook, aucun coût continu |
| Détection bureau | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` | Événementiel : aucun polling, aucun réveil périodique |
| Barre des tâches | `SHAppBarMessage` / `ABM_SETSTATE` / `ABS_AUTOHIDE` | C'est Explorer qui révèle la barre au bord de l'écran ; l'app ne surveille rien, donc aucune latence à implémenter |
| Réglages | `HKCU\Software\DesktopIconsToggle` | Même mécanisme que l'autostart, aucun fichier à gérer |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Aucun droit administrateur requis |

**Prérequis de build :** SDK .NET 10, MSVC 14.51, Windows SDK 10.0.26100.

## 5. Architecture

Un seul processus, un seul thread UI portant une fenêtre cachée et sa boucle de
messages. Tous les composants publient vers ce thread ; aucun verrouillage n'est
nécessaire.

**Correction post-revue V1.** Cette section imposait initialement une fenêtre
*message-only* (`HWND_MESSAGE`). C'était incompatible avec trois exigences des
sections 5.4 et 7 : une fenêtre message-only n'a pas de z-order, n'est pas
énumérable et **ne reçoit aucun message de diffusion**. Elle ne peut donc ni
recevoir `WM_ENDSESSION`, ni recevoir `TaskbarCreated`, ni servir de cible à
`SetForegroundWindow`. La V2 a ajouté une quatrième raison : `RegisterHotKey`
ne fonctionne pas sur une fenêtre message-only. La fenêtre est donc **top-level,
cachée** : parent `IntPtr.Zero`, style étendu `WS_EX_TOOLWINDOW`, jamais affichée
(ni `WS_VISIBLE`, ni `ShowWindow`).

```
DesktopIconsToggle/
├── DesktopIconsToggle.csproj
├── Program.cs           point d'entrée, câblage, récupération après crash, arrêt propre
├── AppWindow.cs         fenêtre top-level cachée, WndProc, boucle de messages
├── Native/Win32.cs      P/Invoke : user32, shell32, gdi32, kernel32
├── IconToggler.cs       localisation de SHELLDLL_DefView, toggle, lecture d'état
├── DesktopWatcher.cs    WinEvent hook, exemptions shell et processus propre
├── HotKey.cs            RegisterHotKey, enregistrement transactionnel
├── HotkeyCapture.cs     fenêtre modale de choix de combinaison
├── HotkeyFormatter.cs   combinaison → libellé français (sans Win32, testable)
├── TaskbarHider.cs      SHAppBarMessage, auto-masquage, état d'origine
├── TaskbarPolicy.cs     règle de propriété de l'auto-masquage (sans Win32, testable)
├── ToggleThrottle.cs    anti-rebond des bascules (sans Win32, testable)
├── Settings.cs          persistance registre, marqueurs de récupération
├── TrayIcon.cs          Shell_NotifyIcon, menu contextuel, TaskbarCreated
├── Autostart.cs         IRegistryValueStore, RegistryValueStore, clé Run
└── app.ico
```

### 5.1 `IconToggler`

Responsabilité : parler à Explorer. Ne connaît ni le clavier ni le tray.

- `FindDefView()` — `Progman` → `SHELLDLL_DefView`. Si absent (fond d'écran animé,
  multi-écran), énumérer les `WorkerW` de niveau supérieur.
- `AreIconsVisible()` — `IsWindowVisible` sur l'enfant `SysListView32`.
  **L'état est lu depuis les fenêtres, jamais depuis la valeur de registre
  `HideIcons`**, qui n'est pas mise à jour de façon fiable par le message de
  toggle. La méthode **échoue ouvert** : elle répond « visibles » quand le bureau
  est illisible, pour qu'un état indéterminé ne provoque jamais un masquage.
- `Toggle()` — `PostMessage(defView, WM_COMMAND, 0x7402, 0)`.
- `EnsureVisible()` — **renvoie `false` quand le bureau est injoignable**, afin que
  l'appelant conserve son marqueur de récupération au lieu de l'effacer. Sinon,
  toggle uniquement si les icônes sont masquées.

### 5.2 `DesktopWatcher`

`SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)`. Le delegate
est conservé dans un **champ d'instance** pour empêcher son ramassage par le GC.
Le bureau est actif si la classe de la fenêtre au premier plan vaut `Progman` ou
`WorkerW`. `Changed` n'est émis que sur transition réelle ; `Start()` publie en
plus l'état initial et renvoie `false` si le hook n'a pas pu s'installer.

Deux exemptions, toutes deux nécessaires :

- **Fenêtres du shell** (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`,
  `NotifyIconOverflowWindow`, `TopLevelWindowForOverflowXamlIsland`) : cliquer
  notre icône active la barre des tâches, ce qui supprimerait l'icône sous le
  curseur avant l'arrivée du callback de clic.
- **Fenêtres de notre propre processus**, par comparaison d'identifiant de
  processus : `TrayIcon` appelle `SetForegroundWindow` avant d'ouvrir son menu, et
  la fenêtre de capture est une vraie fenêtre. Une exemption par handle unique ne
  couvrait que la première et laissait la seconde rouvrir le défaut.

### 5.3 `HotKey` et `HotkeyCapture`

`HotKey` enregistre la combinaison sur la fenêtre top-level, avec `MOD_NOREPEAT`
pour qu'un maintien ne répète pas. `Register` est **transactionnel** : il
désenregistre l'actuelle, tente la nouvelle, et **remet l'ancienne en cas d'échec**
en renvoyant `false`. Le bit `MOD_NOREPEAT` n'est pas conservé dans les propriétés
publiques, qui reflètent le choix de l'utilisateur et servent à l'affichage et à la
persistance.

`HotkeyCapture` est une fenêtre modale avec sa propre boucle de messages, qui
continue de distribuer aux autres fenêtres pour que l'app reste vivante. Règles :

- Au moins un modificateur est exigé.
- **Échap seul annule** ; Échap avec modificateur est un choix valide, ce qui
  laisse `Win+Échap` disponible.
- **Alt+F4 annule** au lieu d'être capturé : `RegisterHotKey` l'accorderait, et
  l'app posséderait alors « fermer la fenêtre » pour tout le système.
- La combinaison est **testée par un `RegisterHotKey` temporaire** avant d'être
  acceptée, donc aucun raccourci inopérant ne peut être enregistré.
- Une seule fenêtre de capture à la fois : une seconde orphelinerait la première,
  dont le bouton de fermeture agit sur un handle statique.
- `WM_QUIT` est **reposté** et non avalé, sinon l'app se bloquerait à la fermeture.

**Chaîne de repli.** Tant que l'utilisateur n'a rien choisi lui-même
(`HotkeyConfigured` faux), le démarrage tente `Win+Maj+D`, puis `Ctrl+Alt+D`, puis
`Ctrl+Alt+Maj+D`, et conserve la première accordée. Windows réserve certaines
combinaisons — `Win+Échap` (quitter la Loupe), `Win+D`, `Win+Alt+D` — et un défaut
fixe peut donc laisser l'application sans déclencheur clavier, ce qui s'est produit
en test. La combinaison retenue est persistée et affichée. Dès que l'utilisateur en
choisit une, `HotkeyConfigured` passe à vrai et **plus aucune substitution
silencieuse n'a lieu** : un échec est signalé, pas contourné.

**Limite connue :** une combinaison déjà enregistrée par un autre processus lui est
livrée à lui ; la fenêtre ne voit rien passer et ne peut pas le signaler. Le test
`RegisterHotKey` n'attrape qu'une course entre la vérification et l'enregistrement.

### 5.4 `TaskbarHider` et `TaskbarPolicy`

Masquer la barre = activer l'**auto-masquage de Windows**. Explorer gère la
révélation au bord de l'écran ; l'application n'implémente aucune détection.

`SetAutoHide` fait une lecture-modification-écriture pour **préserver le bit
`ABS_ALWAYSONTOP`**, et **confirme par relecture** — `ABM_SETSTATE` renvoie
toujours `TRUE`, donc la seule preuve est de relire l'état.

`CaptureOriginalState` **refuse de lire tant que `Shell_TrayWnd` n'existe pas**.
`ABM_GETSTATE` renvoie `0` aussi bien quand l'auto-masquage est désactivé que
quand le shell ne répond pas ; or le chemin de lancement principal est la clé
`Run` à l'ouverture de session, précisément quand le shell n'est pas prêt. Prendre
`0` pour argent comptant désactiverait définitivement le réglage d'un utilisateur
qui s'en sert. Tant que `HasOriginalState` est faux, l'application ne touche pas à
la barre ; la lecture est retentée sur `TaskbarCreated`.

`TaskbarPolicy.Decide(hidden, originalAutoHide)` isole la règle de propriété en
logique pure et testée : on écrit `hidden || originalAutoHide`, et on ne se déclare
propriétaire que si `hidden && !originalAutoHide`. Conséquence voulue : quelqu'un
qui utilisait déjà l'auto-masquage le conserve, et aucun marqueur n'est posé pour
lui.

### 5.5 `ToggleThrottle`

Toute bascule arrivant moins de 300 ms après la précédente est ignorée. Deux
raisons : un appui répété faisait clignoter le bureau et les fenêtres, l'explorateur
appliquant les messages en série ; et deux bascules postées à l'intérieur de la
fenêtre de latence lisaient toutes deux le même état périmé, laissant le marqueur de
récupération décrire l'inverse du résultat. Le seuil est choisi au-dessus du timer de
réconciliation de 150 ms, pour qu'aucune bascule ne soit acceptée tant que la
précédente n'est pas stabilisée. Un refus **ne repousse pas** la fenêtre, sinon
maintenir la touche bloquerait la fonction indéfiniment. L'horloge est
`Environment.TickCount64`, monotone et 64 bits.

### 5.6 `Settings`

Sous `HKCU\Software\DesktopIconsToggle`, valeurs `REG_SZ` décimales :
`HotkeyModifiers`, `HotkeyVirtualKey`, `HotkeyConfigured`, `HideTaskbar`,
`TaskbarHiddenByUs`, `IconsHiddenByUs`. `Load()` est **total** : toute valeur absente, vide ou illisible
retombe sur son défaut sans affecter les autres.

Les deux derniers sont des **marqueurs de récupération après crash**. L'application
les efface à chaque sortie propre, donc les trouver posés au démarrage signifie que
la session précédente a été tuée. Windows conserve l'état masqué des icônes comme
de la barre : sans ces marqueurs, rien ne le rétablirait jamais.

### 5.7 `TrayIcon`

`Shell_NotifyIcon` piloté par `DesktopWatcher`. `cbSize` vient de
`Marshal.SizeOf<NOTIFYICONDATAW>()` : la structure est volontairement tronquée à la
frontière V2, une taille codée en dur ferait rejeter l'appel. L'état `_visible`
n'est enregistré **que si le shell a accepté**, sinon un `NIM_DELETE` échoué
laisserait une icône fantôme que le processus ne peut plus adresser.

Menu : *Afficher/Masquer les icônes* — séparateur — *Masquer aussi la barre des
tâches* (cochable), *Raccourci : …*, *Démarrage automatique* (cochable) —
séparateur — *Quitter*. `SetForegroundWindow` avant `TrackPopupMenu` et `WM_NULL`
posté après, sans quoi le menu refuse de se fermer sur un clic à côté.

### 5.8 `Autostart`

Valeur `DesktopIconsToggle` sous la clé `Run`, contenant le chemin de l'exe entre
guillemets. `IsEnabled()` compare au chemin courant ; si l'exe a été déplacé, la
valeur est réécrite au prochain basculement. `RegistryValueStore` prend son
sous-chemin en paramètre et sert aussi bien la clé `Run` que celle des réglages.

### 5.9 `Program`

1. Mutex `Local\DesktopIconsToggle` ; sortie immédiate si déjà lancé.
2. `Settings.Load()`.
3. **Récupération des icônes** si `IconsHiddenByUs` est posé.
4. **Récupération de la barre** si `TaskbarHiddenByUs` est posé — impérativement
   **avant** `CaptureOriginalState()`, sinon notre propre résidu serait enregistré
   comme le réglage d'origine de l'utilisateur et deviendrait permanent.
5. `CaptureOriginalState()`.
6. Fenêtre, tray, watcher, puis `HotKey.Register` ; un échec est signalé par
   l'infobulle et laisse le tray comme seul déclencheur.
7. `GetMessage` / `DispatchMessage`.
8. À l'arrêt, sous garde de ré-entrance : `EnsureVisible()`, `RestoreOriginal()`,
   puis effacement des marqueurs **uniquement pour les restaurations confirmées**.

`ToggleAll` pose `IconsHiddenByUs` **avant** de poster quoi que ce soit, puis un
`WM_TIMER` à 150 ms réconcilie marqueur, barre et infobulle avec l'état
réellement obtenu. Explorer met un moment à appliquer un message posté : deux
bascules rapprochées liraient deux fois la même valeur périmée et laisseraient le
marqueur à l'opposé du résultat.

## 6. Flux de données

```
raccourci clavier ────► WM_HOTKEY ──┐
clic gauche tray ─────► TrayIcon ───┼──► ToggleAll() ──► IconToggler.Toggle()
menu contextuel ──────► TrayIcon ───┘                └─► TaskbarHider (si option)
                                                     └─► WM_TIMER 150 ms
                                                          └─► réconciliation

changement de premier plan ──► DesktopWatcher ──► TrayIcon.SetVisible(bool)
TaskbarCreated ──────────────► TrayIcon.Readd() + capture différée éventuelle
```

## 7. Gestion des erreurs

| Situation | Comportement |
|---|---|
| `SHELLDLL_DefView` introuvable | Le toggle ne fait rien ; l'infobulle affiche « bureau indisponible » |
| Explorer redémarre | `TaskbarCreated` réajoute l'icône ; les handles sont relocalisés à la demande |
| `RegisterHotKey` échoue | L'appli continue avec le seul tray ; l'infobulle le signale et le menu affiche « aucun » |
| `SHAppBarMessage` sans réponse | Aucune action sur la barre tant que l'état d'origine n'est pas connu |
| Écriture registre refusée | L'entrée de menu se décoche ; aucune exception remontée |
| Fermeture de session (`WM_ENDSESSION`, `wParam` vrai) | Même arrêt propre que *Quitter*, synchrone |
| Session précédente tuée | Marqueurs relus au démarrage ; icônes et barre restaurées |

**Filet de sécurité :** les icônes sont restaurées à toute sortie du processus, et
la barre des tâches remise dans l'état trouvé au lancement. Une restauration non
confirmée **laisse son marqueur posé** plutôt que de l'effacer : une réparation
inutile au lancement suivant est sans conséquence, une réparation manquée ne l'est
pas. Il doit être impossible de se retrouver avec un bureau vide et aucun moyen de
le rétablir.

## 8. Tests

66 tests unitaires (xUnit, projet séparé exclu de la publication AOT). Tout ce qui
peut être extrait de l'interopérabilité Win32 l'est, précisément pour être testé :

- `HotkeyFormatter` — libellés et ordre des modificateurs, accents compris.
- `Settings` — défauts, aller-retour, valeurs corrompues isolées les unes des autres.
- `Autostart` — via `IRegistryValueStore`, remplacé par un faux magasin en mémoire.
- `TaskbarPolicy` — les quatre cas de la table de vérité, dont celui qui porte la
  garantie : un utilisateur déjà en auto-masquage le conserve et n'est jamais
  revendiqué.
- `TaskbarHider.ApplyAutoHide` — préservation de `ABS_ALWAYSONTOP` dans les deux sens.

**Aucun test ne touche le vrai registre ni le vrai shell.**

Checklist manuelle, non automatisable :

1. Raccourci → les icônes disparaissent ; à nouveau → elles reviennent.
2. Icône de notification : apparaît sur le bureau, disparaît ailleurs.
3. Clic gauche sur l'icône → bascule. Clic droit → menu correct et cohérent.
4. *Masquer aussi la barre des tâches* : la barre s'auto-masque et revient au survol
   du bas de l'écran.
5. *Raccourci …* : la fenêtre s'affiche correctement, Échap seul annule, Alt+F4
   annule, une combinaison valide est acceptée et persiste après relance.
6. *Démarrage automatique* : la valeur de registre apparaît et disparaît.
7. Redémarrage d'Explorer → l'icône revient.
8. Masquer puis *Quitter* → tout est restauré avant la fermeture.
9. Masquer puis tuer le processus → tout est restauré au lancement suivant.
10. Deuxième lancement → sortie immédiate, une seule instance.
11. Deux bascules à moins de 150 ms → l'état final et l'infobulle concordent.
12. Si l'auto-masquage était déjà actif avant installation → il l'est toujours après
    un cycle masquer/restaurer/quitter.

## 9. Livrables

- `src/DesktopIconsToggle/bin/Release/net10.0-windows/win-x64/publish/DesktopIconsToggle.exe`,
  autonome, 1 463 808 octets.
- `README.md` : usage, raccourci, barre des tâches, désinstallation (y compris la
  suppression de `HKCU\Software\DesktopIconsToggle`).
