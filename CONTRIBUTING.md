# Contributing

Thanks for taking a look. This is a small, single-maintainer utility, but
the same conventions that got it this far should keep applying.

## Building

**Prerequisites:**

- .NET SDK 10
- MSVC and the Windows SDK, from Visual Studio Build Tools — NativeAOT
  links the final executable with `link.exe`, not the managed .NET
  linker

```
dotnet restore
dotnet build src/DesktopIconsToggle -c Release
```

## Testing

```
dotnet test tests/DesktopIconsToggle.Tests
```

There are 183 xUnit tests. They cover exactly the logic that has been
extracted out of the Win32 interop layer, on purpose, so it can be
tested without a real desktop, a real registry, or a real shell:

- `HotkeyFormatter` — combination → French label, including modifier
  order and accents.
- `Settings` — defaults, round-trips, and isolation between corrupted
  values (one bad value must not affect the others).
- `Autostart` — via `IRegistryValueStore`, backed by an in-memory fake
  in tests instead of the real `Run` key.
- `TaskbarPolicy` — the auto-hide ownership truth table, including the
  case that carries the app's core promise: a user already using
  auto-hide keeps it and is never claimed as "ours".
- `TaskbarHider.ApplyAutoHide` — `ABS_ALWAYSONTOP` is preserved in both
  directions.
- `ToggleThrottle` — the 300 ms debounce window and its edge cases.
- `DialogLayout` — the capture dialog's layout arithmetic, at multiple
  DPI scales.
- `Theme` — light/dark detection with a fake theme source.
- `SystemFont` — scaling and the DEFAULT_GUI_FONT fallback.
- `IconPalette` — contrast correction in both directions (light and dark
  backgrounds), and the alpha used for the inactive state.

**No test may touch the real registry or the real shell.** Anything that
calls into `Native/Win32.cs`, `Shell_NotifyIcon`, `SHAppBarMessage`, or
opens a live `HKCU` key belongs behind an interface (see
`IRegistryValueStore`, `IThemeSource`) with a fake substituted in tests,
or it is Win32 glue that is verified by hand instead (see the manual
checklist in the design spec, §8).

## Conventions

- **`TreatWarningsAsErrors` is on** in both csproj files. A warning is a
  build failure; fix it rather than suppress it.
- **Narrow exception filters, never bare `catch`.** Every catch clause
  should name the specific exception types a given Win32 or registry
  call can actually raise (see `Theme.cs`'s
  `catch (Exception ex) when (ex is UnauthorizedAccessException or
  System.Security.SecurityException)` for the pattern), so an unrelated
  bug doesn't get silently swallowed along with the expected failure.
- **Pull pure logic out of Win32 interop whenever you can**, specifically
  so it can be unit-tested. If you're adding a feature that touches
  Win32, ask whether the decision logic (a threshold, a truth table, a
  formatting rule, a layout calculation) can be its own type that takes
  plain data in and returns plain data out, with the interop reduced to
  a thin caller.
- **Fail open on desktop/shell detection.** `IconToggler.AreIconsVisible`
  reports "visible" when it cannot read the real state, so an
  indeterminate read can never trigger an unwanted hide. Keep that bias
  if you touch related code.
- **The window is a hidden top-level window, not a message-only one.**
  This was tried and reverted — see
  [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for why. Don't change it
  without re-reading that section.

## Regenerating the tray icons

The tray menu icons are rasterised from Lucide SVGs into embedded alpha
masks by `tools/make-lucide-icons.ps1`. To regenerate them:

1. Make sure Google Chrome is installed (the script renders each SVG via
   headless Chrome) and that the source SVGs are present in
   `tools/lucide/` (`eye-off.svg`, `panel-bottom.svg`, `keyboard.svg`,
   `zap.svg`, `x.svg`).
2. Run `tools/make-lucide-icons.ps1` from a PowerShell prompt at the repo
   root (or from `tools/`).
3. The script regenerates `src/DesktopIconsToggle/LucideIcons.g.cs` and a
   contact-sheet preview under `tools/lucide/` for visual review.
4. If you're adding a new icon, also add a File-name → enum-member entry
   in the `$icons` ordered map near the top of the script — the map order
   fixes the generated enum's values.
5. Keep the SVG sources under `tools/lucide/` even though the compiled
   executable only ever embeds the rasterised masks — see
   [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for why (Lucide's ISC
   licence, and reproducibility of the rasterisation step).

## Manual verification

The Win32 layer — the window procedure, hotkey registration, tray icon
lifecycle, the capture dialog's rendering, DPI handling, and taskbar
auto-hide — is **not** unit-tested and cannot realistically be, short of
driving a real Explorer session. Any change that touches
`AppWindow.cs`, `HotkeyCapture.cs`, `TrayIcon.cs`, `IconToggler.cs`,
`DesktopWatcher.cs`, `TaskbarHider.cs`, `MenuIcons.cs`, or `Native/Win32.cs`
needs a manual pass on a real Windows session before it can be
considered done. At minimum:

1. Hotkey toggles the icons; toggling again brings them back.
2. The tray icon appears when the desktop is focused, disappears
   otherwise.
3. Left-click toggles; right-click opens a correct, consistent menu.
4. *Masquer aussi la barre des tâches*: the taskbar auto-hides and
   reappears on hover at the bottom of the screen.
5. *Raccourci …*: the dialog renders correctly, Escape alone cancels,
   Alt+F4 cancels, a valid combination is accepted and survives a
   restart.
6. *Démarrage automatique*: the registry value appears and disappears
   with the checkbox.
7. Restarting Explorer brings the tray icon back.
8. Hide, then *Quitter*: everything is restored before the process
   exits.
9. Hide, then kill the process: everything is restored on the next
   launch.
10. A second launch exits immediately (single instance via the
    `Local\DesktopIconsToggle` mutex).
11. Two toggles under 150 ms apart: the final state and the tooltip
    agree.
12. If auto-hide was already on before installation, it's still on after
    a hide/restore/quit cycle.

Pull requests touching the Win32 layer should state which of these were
checked.
