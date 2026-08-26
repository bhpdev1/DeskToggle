# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-08-26

### Added

- Tray menu icons, rasterised from [Lucide](https://lucide.dev), shown in
  colour with the enabled/disabled state of an option conveyed by colour
  (full colour when active, dimmed alpha when inactive) rather than by a
  checkmark alone.
- The hotkey capture dialog was rebuilt: it now uses the system message
  font (read via `SPI_GETNONCLIENTMETRICS` instead of a hard-coded font),
  scales correctly per-monitor DPI, follows the current Windows light/dark
  theme colours, draws rounded corners, and adds an explicit confirmation
  step before a new combination is committed.
- Toggle throttling: a toggle arriving less than 300 ms after the previous
  one is ignored, preventing flicker from a held key and preventing two
  toggles from reading the same stale state.
- The global hotkey registration is retried when the tray menu is opened,
  so a combination that failed to register at startup (for example because
  another application held it at the time) can recover without a restart.

### Changed

- None.

### Fixed

- None.

## [2.0.0] - 2026-08-25

### Added

- Configurable global hotkey (default `Ctrl+Alt+D`, with a fallback chain
  tried only until the user chooses a combination themselves), replacing
  the middle-double-click trigger.
- Taskbar auto-hide option, extending the toggle to the taskbar via
  Windows' own `ABM_SETSTATE` auto-hide mechanism.
- Settings persistence under `HKCU\Software\DesktopIconsToggle`.
- Crash-recovery markers: icons and taskbar state are repaired on the next
  launch if the previous process was killed instead of exited cleanly.

### Removed

- The middle-double-click trigger and its global mouse hook, which woke
  the process on every mouse event system-wide regardless of whether the
  desktop was in focus.

## [1.0.0] - 2026-08-25

### Added

- First version: a notification-area (tray) icon and a middle-double-click
  gesture to toggle desktop icons.
