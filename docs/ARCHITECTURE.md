# Architecture

This is a condensed tour of the design, derived from the fuller,
authoritative record in French:
[`docs/superpowers/specs/2026-08-25-desktop-icons-toggle-design.md`](superpowers/specs/2026-08-25-desktop-icons-toggle-design.md).
Read that document for the full rationale, the error-handling table, and
the manual test checklist; this page exists to orient a reader (or a
contributor) who needs the shape of the system without wading through
the spec's revision history.

## Overview

DesktopIconsToggle is a single process, single thread. One hidden
top-level window owns a classic Win32 message loop (`GetMessage` /
`DispatchMessage`); every component — the hotkey, the tray icon, the
desktop-foreground watcher, the taskbar auto-hide logic — publishes into
that one loop. Nothing here is multi-threaded, so nothing here needs a
lock.

## Why a hidden top-level window, not a message-only window

This is the single most useful thing to understand before modifying this
code, because the mistake was made once, in the first version, and had
to be corrected.

A *message-only* window (parented to `HWND_MESSAGE`) looks tempting for
something that's never meant to be seen: it costs nothing extra and
never appears in Alt+Tab. But a message-only window has no z-order, is
not enumerable by other processes, and — critically — **receives no
broadcast messages**. That rules it out on four separate counts this
application actually depends on:

- It cannot receive `WM_ENDSESSION`, so a system shutdown or logoff
  would never trigger the clean-shutdown path, and icons would stay
  hidden until the next launch's crash recovery caught it.
- It cannot receive `TaskbarCreated`, so if Explorer restarts, the tray
  icon would never be re-added.
- `SetForegroundWindow` cannot succeed on it, which the tray icon needs
  before opening its context menu (Windows requires the calling window
  to be foreground-able for `TrackPopupMenu` to behave correctly).
- `RegisterHotKey` does not work on a message-only window at all.

The window is therefore **top-level and hidden**: parent `IntPtr.Zero`,
extended style `WS_EX_TOOLWINDOW` (so it never gets a taskbar button or
an Alt+Tab entry), and never shown — no `WS_VISIBLE`, no `ShowWindow`
call, ever. It behaves like a message-only window in every way a user
could notice, while still being a first-class citizen of the desktop's
window manager where that citizenship is actually required.

## Components

**`Program`** — the entry point. Acquires a `Local\DesktopIconsToggle`
mutex (a second launch exits immediately), loads settings, runs crash
recovery for icons and then the taskbar (in that order — see
[Data flow](#data-flow) below for why the order matters), captures the
taskbar's original auto-hide state, wires up the window/tray/watcher/
hotkey, and runs the message loop. On any exit path, restores icons and
the taskbar, then clears recovery markers only for restores it could
confirm.

**`AppWindow`** — the hidden top-level window described above. Owns the
window procedure and the message loop; every other component reacts to
messages delivered through it (`WM_HOTKEY`, `WM_TIMER`, `WM_ENDSESSION`,
tray callback messages, `TaskbarCreated`).

**`Native/Win32.cs`** — every P/Invoke signature the app uses, across
`user32`, `shell32`, `gdi32`, and `kernel32`. Nothing here is unit-tested;
it's verified by using it and by the manual checklist in
[CONTRIBUTING.md](../CONTRIBUTING.md).

**`IconToggler`** — the only component that talks to Explorer's desktop
window. Finds `SHELLDLL_DefView` under `Progman` (falling back to
enumerating top-level `WorkerW` windows when it's not there directly —
see the note on that fallback in the main README's limitations),
posts the same `WM_COMMAND` the real "Show desktop icons" menu item
posts, and reads visibility straight from the `SysListView32` child's
`IsWindowVisible` state rather than from a registry value, because the
registry value is not reliably updated by the toggle message itself.
Fails open: an unreadable desktop is reported as "visible," so an
indeterminate state can never cause an unwanted hide.

**`DesktopWatcher`** — a `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, ...)`
hook that decides whether the desktop is currently focused (foreground
window class `Progman` or `WorkerW`), used to show or hide the tray icon.
Exempts the shell's own tray windows (clicking the tray icon momentarily
focuses the taskbar, which would otherwise hide the icon out from under
the cursor) and the app's own windows (by process ID, since both the
tray's `SetForegroundWindow` call and the hotkey capture dialog are real
windows that would otherwise re-trigger the default hide).

**`HotKey`** — wraps `RegisterHotKey` with `MOD_NOREPEAT` (so holding the
key doesn't repeat), and makes re-registration transactional: it
unregisters the current combination, tries the new one, and restores the
old one if the new one fails.

**`HotkeyCapture`** — the modal dialog for choosing a combination. Runs
its own nested message loop that still dispatches to other windows (so
the rest of the app stays alive while it's open). Enforces at least one
modifier, treats Escape-alone as cancel (but Escape+modifier as valid,
deliberately leaving `Win+Escape` available), refuses Alt+F4 outright,
and tests every candidate with a real, temporary `RegisterHotKey` call
before accepting it.

**`HotkeyFormatter`** — pure function from a modifier/virtual-key pair to
its French display label. No Win32 involved; fully unit-tested.

**`TaskbarHider`** — flips Windows' own taskbar auto-hide
(`SHAppBarMessage` / `ABM_SETSTATE` / `ABS_AUTOHIDE`). Does a
read-modify-write to preserve the `ABS_ALWAYSONTOP` bit, and confirms by
reading the state back rather than trusting `ABM_SETSTATE`'s return
value (which is always `TRUE`). Refuses to capture the "original" state
until `Shell_TrayWnd` exists, because the main launch path (the `Run`
key, at logon) can run before Explorer is ready, and `ABM_GETSTATE`
returns `0` both for "auto-hide is off" and for "the shell isn't
answering" — treating the second as the first would permanently disable
a setting the user actually wanted.

**`TaskbarPolicy`** — the ownership rule as pure, tested logic, isolated
from `TaskbarHider` on purpose: the effective state is
`hidden || originalAutoHide`, and the app only claims ownership when
`hidden && !originalAutoHide`. This is what guarantees a user who
already had auto-hide on keeps it, and is never marked as something the
app needs to undo.

**`ToggleThrottle`** — ignores any toggle request arriving less than
300 ms after the last one. Explorer takes roughly 150 ms to settle a
posted toggle message, so two toggles inside that window would both read
the same stale state and leave the crash-recovery marker describing the
opposite of reality; 300 ms leaves margin above that. A throttled
request does not reset the window, so holding a key down can't jam the
toggle indefinitely.

**`Settings`** — persistence under `HKCU\Software\DesktopIconsToggle` as
decimal `REG_SZ` values. `Load()` treats every value independently: a
missing, empty, or corrupt value falls back to its own default without
affecting any other value.

**`TrayIcon`** — `Shell_NotifyIcon`-driven tray presence, controlled by
`DesktopWatcher`, plus the context menu (rendered with `MenuIcons`).
Calls `SetForegroundWindow` before `TrackPopupMenu` and posts `WM_NULL`
afterward — both are needed for the menu to close correctly on an
outside click. Only records the icon as present if the shell actually
accepted the `NIM_ADD` call, so a failed removal can't leave a phantom
icon the process has lost the handle to.

**`Autostart`** — writes/removes the quoted executable path under
`HKCU\...\Run`, through the same `IRegistryValueStore` abstraction
`Settings` uses so both can be tested against an in-memory fake instead
of the real registry.

**`DialogLayout`, `Theme`, `SystemFont`, `IconPalette`, `MenuIcons`** —
the visual layer added for the capture dialog and tray menu: pure layout
math for the dialog at any DPI, light/dark theme detection, the shell's
own message font (read via `SPI_GETNONCLIENTMETRICS` rather than
hard-coded), contrast-correcting whatever accent colour the user has
configured, and rendering the embedded Lucide alpha masks into
DPI-correct, colour-and-alpha-cached bitmaps. Everything here that
doesn't have to touch Win32 directly (`DialogLayout`, `IconPalette`, the
theme-decision logic) is unit-tested against a fake; the GDI rendering
itself is not.

## Data flow

```
global hotkey ─────────► WM_HOTKEY ──┐
tray left-click ───────► TrayIcon ───┼──► ToggleAll() ──► IconToggler.Toggle()
context menu ───────────► TrayIcon ───┘                └─► TaskbarHider (if enabled)
                                                        └─► WM_TIMER (150 ms)
                                                             └─► reconciliation

foreground change ──► DesktopWatcher ──► TrayIcon.SetVisible(bool)
TaskbarCreated ─────────────────────► TrayIcon.Readd() + deferred capture retry
```

`ToggleAll` sets the `IconsHiddenByUs` marker *before* posting anything,
then a 150 ms `WM_TIMER` reconciles the marker, the taskbar, and the
tooltip against whatever state Explorer actually ended up in — Explorer
takes a moment to apply a posted toggle message, so reading the state
synchronously right after posting would be racy.

## The two crash-recovery markers

`IconsHiddenByUs` and `TaskbarHiddenByUs`, both persisted in `Settings`,
are the entire crash-recovery mechanism:

- Both are cleared only on a **confirmed** clean shutdown.
- If either is found set at startup, it means the previous process was
  killed rather than exited cleanly (`Quitter`, `WM_ENDSESSION`, and
  `ProcessExit` all clear their marker on success). Windows itself does
  not undo a hidden desktop or an auto-hidden taskbar just because the
  process that hid them is gone — without these markers, nothing would
  ever restore that state.
- `TaskbarHiddenByUs` recovery must run **before** `CaptureOriginalState`
  on startup. Capturing "original state" after a leftover auto-hide from
  a killed previous run would record the app's own residue as if it were
  the user's real preference, making it permanent.
- A restoration that cannot be confirmed (for example, Explorer is mid-
  restart when the app tries to verify the icons came back) **keeps**
  its marker instead of clearing it. An unnecessary repair on the next
  launch costs nothing; a missed repair can leave a user looking at a
  blank desktop with no obvious way back. See the main README's safety
  guarantees section for the one gap this still can't close (killed with
  icons hidden, and never relaunched at all).

## Tests

179 xUnit tests cover every component listed above that has no direct
Win32 dependency: `HotkeyFormatter`, `Settings`, `Autostart`,
`TaskbarPolicy`, `TaskbarHider.ApplyAutoHide`, `ToggleThrottle`,
`DialogLayout`, `Theme`, `SystemFont`, and `IconPalette`. The Win32 glue
itself — the window procedure, hotkey registration, tray lifecycle, GDI
rendering — is verified by hand; see the manual checklist in
[CONTRIBUTING.md](../CONTRIBUTING.md).
