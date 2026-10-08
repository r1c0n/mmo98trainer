# Changelog

## Unreleased

- Replaced Unity IMGUI with the game's native uGUI/TextMeshPro style, including its fonts, title bar, buttons, tabs, checkboxes, and sliders.
- Added native text fields, modal input handling, an Escape/close button, scrolling with retained tab positions, scaled dragging, and focus/lifecycle cleanup.
- Added graphical runtime interaction checks, native asset probing, and rendered tab previews. Graphical verification passes and exits cleanly.
- Added UnityEngine.UI and Unity.TextMeshPro to the seven local game references; reference DLLs remain ignored by Git.
- Kept the plugin and assembly version at 1.1.0.

## 1.1.0 - 2026-10-08

### Added

- Live resource totals, Add/Set controls, quick inputs, and numeric validation.
- Full uptime, datacenter repair, and maximum sequel quality.
- Speed presets, pause/resume, and an effect-reset shortcut.
- Persistent configuration, configurable shortcuts, scaled menu, saved position, and compatibility diagnostics.
- Numeric/API checks and a disposable-state native runtime harness.

### Fixed

- Development completion targets the active game simulation instead of the state object.
- Zero ping removes base/minimum ping while preserving player count.
- Game speed advances command timers and interval simulations together using the game's bounded steps.
- Main-menu/disposed-state access, silent action failures, and retained Harmony hooks/speed on plugin shutdown.
- Datacenter unlocks with an existing construction entry.
- Repeated scene-wide reference searches and GUI state leaking into other game UI.

### Build

- Pinned verified dependency versions and added an installed-game reference path.
- Game assemblies are excluded from copy-local output; install only the trainer DLL.
- Added reference-sync, verification, and runtime smoke scripts.

Native headless shutdown still crashes on the verified installation with both the original and updated trainer. See COMPATIBILITY.md for the observed limits.
