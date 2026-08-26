# DesktopIconsToggle

[![build](https://github.com/YOUR-GITHUB-USERNAME/desktop-icons-toggle/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR-GITHUB-USERNAME/desktop-icons-toggle/actions/workflows/ci.yml)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

*[Lire en français](README.fr.md)*

A tiny, self-contained Windows utility that hides and shows your desktop
icons with a hotkey or a tray icon, instead of the right-click menu — and
optionally does the same to the taskbar.

> **The application's interface is French only.** There is no
> localisation layer. This README is in English so the project is
> approachable on GitHub, but every menu, dialog and tooltip you will
> actually see is in French. If that is a dealbreaker, stop here.

## Features

- **Global hotkey**, `Ctrl+Alt+D` by default, configurable from the tray
  menu via a capture dialog.
- **Tray (notification-area) icon**, shown only while the desktop is the
  foreground window. Left-click toggles, right-click opens the menu.
- **Optional taskbar auto-hide**, extending the same toggle to the
  taskbar by switching Windows' own auto-hide behaviour — not by hiding
  or destroying anything.
- **Starts with your session**, via the `HKCU` `Run` key — no
  administrator rights required, no installer.
- **Crash-safe**: icons and taskbar state are restored on every exit path
  and repaired automatically on the next launch if the process was
  killed instead of exited cleanly.
- **Minimal footprint**: a single ~1.5 MB executable, no runtime to
  install, ~3 MB of private memory and 0 ms of CPU while idle.

## Screenshots

<!--
  Drop the two screenshots below into docs/images/ and uncomment this
  block. A broken image link looks worse than no image, hence the comment.

  1. docs/images/menu.png — the tray right-click menu, showing the five
     entries (toggle, "Masquer aussi la barre des tâches", the current
     hotkey, "Démarrage automatique", "Quitter") with their coloured
     Lucide icons and check marks.
  2. docs/images/hotkey-dialog.png — the hotkey capture dialog, mid-capture
     or on its confirmation step, showing the system font, rounded
     corners and theme-matched colours.

  ![Tray menu](docs/images/menu.png)
  ![Hotkey capture dialog](docs/images/hotkey-dialog.png)
-->

## Installation

1. Download `DesktopIconsToggle.exe` (see [Releases](../../releases), or
   build it yourself — see below).
2. Copy it anywhere, for example `C:\Tools\`.
3. Run it.
4. Right-click the tray icon → check **Démarrage automatique** if you
   want it to start with Windows.

The executable is self-contained: no .NET runtime install is required.

Windows will warn you the first time — see [Limitations](#limitations).

## Usage

Two ways to toggle:

- **Global hotkey**, `Ctrl+Alt+D` by default, configurable from the menu.
- **Tray icon**, present only while the desktop is in the foreground.
  Left-click to toggle, right-click for the menu.

### The menu

| Entry (French) | Effect |
|---|---|
| *Afficher / Masquer les icônes* | Toggles, same as the hotkey |
| *Masquer aussi la barre des tâches* | Extends the toggle to the taskbar |
| *Raccourci : …* | Opens the capture dialog to change the combination |
| *Démarrage automatique* | Starts the app with the Windows session |
| *Quitter* | Restores everything, then exits |

### Changing the hotkey

Menu → *Raccourci : …*. Press the combination you want; at least one
modifier key is required. **Escape alone, and Alt+F4, cancel** — Escape
with a modifier is still a valid choice, and Alt+F4 is refused because
registering it would take "close window" away from the entire system.

On first run, until you have chosen a combination yourself, the app
tries `Ctrl+Alt+D`, then `Ctrl+Alt+Shift+D`, then `Ctrl+Alt+Shift+F9`,
and keeps the first one Windows grants. The menu shows whichever one
actually won.

The chosen combination is tested with a real registration before being
accepted, which rules out combinations Windows refuses outright. It does
**not** rule out the Windows-key problem described in
[Limitations](#limitations).

### The taskbar option

The option does not hide the taskbar by destroying or covering it: it
turns on Windows' own **auto-hide**. The taskbar reappears when the
pointer reaches the bottom of the screen, and Explorer handles that
reveal — the app implements no polling and adds no latency.

Two consequences:

- If you already had auto-hide enabled, it is preserved: the app reads
  your setting at launch and restores exactly that, not "off".
- The setting is **global** — there is no Windows API to auto-hide the
  taskbar on a single monitor, so the option applies to every display.

## Safety guarantees

- Desktop icons are restored on every exit path: the *Quitter* menu
  entry, an exception in a window procedure, `WM_ENDSESSION` (session
  logoff/shutdown), and `ProcessExit`.
- The taskbar is returned to the auto-hide state observed at launch, so
  a user who already used auto-hide keeps it — the app never claims
  ownership of a setting it didn't turn on.
- If the process is killed outright, registry markers record that icons
  or the taskbar were hidden by the app. The next launch reads those
  markers and repairs both. A restore that cannot be confirmed **keeps**
  its marker rather than clearing it, so a future launch retries instead
  of silently giving up.
- The one gap no program can close: the process is killed while icons
  are hidden, and never relaunched. Recovery is two clicks — right-click
  the desktop, **Affichage**, **Afficher les éléments du Bureau** — the
  same menu the app itself uses, which it never disables.

## Limitations

Read this before filing an issue — several of these are inherent to how
Windows works, not bugs in this app.

- **The interface is French only.** There is no localisation layer.
- **The executable is unsigned.** Windows SmartScreen shows "Windows
  protected your PC" when it's run after being downloaded from the
  internet; click **More info**, then **Run anyway**. Copying it via USB
  or a local network share carries no Mark of the Web and shows no such
  warning. Code signing is the only real fix, and this project doesn't
  do it (yet).
- **Windows-key combinations can register successfully yet never fire.**
  Explorer processes `Win`+key through its own keyboard hook, ahead of
  registered-hotkey dispatch, so `RegisterHotKey` reporting success
  proves nothing for that family. This was measured, not theorised:
  `Win+Shift+D` was granted by `RegisterHotKey` — a second registration
  attempt from another process was refused with
  `ERROR_HOTKEY_ALREADY_REGISTERED`, confirming the first one really
  held the registration — and yet no `WM_HOTKEY` message ever arrived.
  That is why no Windows-key combination is offered by default.
- **The capture dialog cannot detect a combination already owned by
  another application.** That application receives the keystroke
  instead, so the dialog never sees it and simply stays silent.
- **Taskbar auto-hide is a global Windows setting.** There is no
  per-monitor API, so the option applies to every display.
- **A combination that other applications commonly bind** (`Ctrl+Tab`,
  `Ctrl+W`, and similar) can still be chosen — the dialog warns you, but
  lets you proceed — and doing so takes that shortcut away everywhere,
  not just in this app.
- **The `WorkerW` fallback for locating the desktop has never been
  exercised in practice.** It only triggers with animated wallpapers or
  some multi-monitor configurations.

## Building from source

**Prerequisites:**

- .NET SDK 10
- MSVC and the Windows SDK, from Visual Studio Build Tools (NativeAOT
  links the final executable with `link.exe`)

```
dotnet test tests/DesktopIconsToggle.Tests
dotnet publish src/DesktopIconsToggle -c Release -r win-x64
```

The executable is produced at
`src/DesktopIconsToggle/bin/Release/net10.0-windows/win-x64/publish/DesktopIconsToggle.exe`.

It targets `net10.0-windows`, `win-x64`, built with `PublishAot=true`:
hand-written Win32 P/Invoke, no WinForms, no WPF. Release builds strip
debug symbols (`DebugType=none`) — a NativeAOT `.pdb` is roughly 10 MB
and carries every class and method name, which would undo most of the
benefit NativeAOT has over analysis of the binary.

See [CONTRIBUTING.md](CONTRIBUTING.md) for coding conventions and test
requirements, and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for a
tour of the design (the fuller, authoritative record is the French spec
at [`docs/superpowers/specs/2026-08-25-desktop-icons-toggle-design.md`](docs/superpowers/specs/2026-08-25-desktop-icons-toggle-design.md)).

## Project layout

```
src/DesktopIconsToggle/
├── Program.cs           entry point, wiring, crash recovery, clean shutdown
├── AppWindow.cs          hidden top-level window, WndProc, message loop
├── Native/Win32.cs       P/Invoke: user32, shell32, gdi32, kernel32
├── IconToggler.cs        finds SHELLDLL_DefView, toggles, reads state
├── DesktopWatcher.cs     WinEvent hook, shell and own-process exemptions
├── HotKey.cs             RegisterHotKey, transactional registration
├── HotkeyCapture.cs      modal dialog for choosing a combination
├── HotkeyFormatter.cs    combination → French label (no Win32, unit-testable)
├── TaskbarHider.cs       SHAppBarMessage, auto-hide, original-state capture
├── TaskbarPolicy.cs      auto-hide ownership rule (no Win32, unit-testable)
├── ToggleThrottle.cs     debounces toggles (no Win32, unit-testable)
├── Settings.cs           registry persistence, crash-recovery markers
├── TrayIcon.cs           Shell_NotifyIcon, context menu, TaskbarCreated
├── Autostart.cs          IRegistryValueStore, RegistryValueStore, Run key
├── DialogLayout.cs       pure layout arithmetic for the capture dialog
├── Theme.cs              light/dark theme detection
├── SystemFont.cs         reads the shell's own message font
├── IconPalette.cs        contrast-correct icon colouring
├── MenuIcons.cs          renders the embedded Lucide alpha masks
├── LucideIcons.g.cs      generated: embedded Lucide alpha masks
└── app.ico
tests/DesktopIconsToggle.Tests/   179 xUnit tests — see CONTRIBUTING.md
tools/                             icon generation scripts (see CONTRIBUTING.md)
docs/                              architecture docs and the French design spec
```

## Licence

[MIT](LICENSE), Copyright (c) 2026 BH Enterprises.

Tray menu icons are derived from [Lucide](https://lucide.dev) (ISC
licence) — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- Icons: [Lucide](https://lucide.dev).
- Built by BH Enterprises.
