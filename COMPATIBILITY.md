# Compatibility verification

Verified October 8, 2026 against the locally installed game. This records the specific build checked, rather than promising compatibility with future updates.

| Component | Verified value |
| --- | --- |
| Game version | 2026.07.26.1 |
| Steam build | 24396256 |
| Unity runtime | 6000.3.14f1 |
| Installed BepInEx runtime | 6.0.0-be.755, Mono, x64 |
| Trainer | 1.1.0, netstandard2.1 |
| Compile-time BepInEx package | 6.0.0-be.788 |
| Assembly-CSharp SHA-256 | 33668A422C39BAD7CF4178D56A2098EF7082B588B2D43B2CBD9E4A77251DCAD0 |

## Findings from dnSpy MCP

- `GameController.UnlockAllUpgrades()`, `UnlockAllResearch()`, and `UnlockAllDatacenters()` are still private instance methods without parameters. The trainer caches these methods and reports unavailable actions instead of silently doing nothing.
- The game's datacenter helper can add a duplicate dictionary entry for a datacenter already under construction. The trainer finishes existing construction/unprovisioned entries before invoking the helper. The runtime test verifies this case and repeated invocation.
- `FinishDevelopment()` belongs to `SequelDevelopSimulation`, not `DatabaseState.SequelState`. The original trainer reflected over the state object, so the development button could not call the completion method. The trainer now tracks the registered simulation and calls its normal `OnUpdateSimulation(0)` completion path, which advances rounds, quality, next cost, and notifications.
- `PingSimulation.OnUpdateSimulation(float)` adds base ping and clamps to the minimum. Removing congestion/load penalties alone does not produce zero ping. The new prefix writes zero and skips the ping calculation without changing player count or datacenter capacity.
- `IncrementalSimulation.AdvanceTime(float)` advances database command timers before simulations and also maintains interval accumulators. The old per-simulation delta-time patches missed these paths. The trainer now changes `GameTimer.GameSpeedMultiplier`; the timer still divides elapsed time into steps no larger than its `maxStepTime`.
- `Database.Dispose()` clears state, commands, and reactive properties when leaving gameplay. Profile guards prevent actions and patches from accessing disposed state. Original game speed is restored when the trainer is disabled, destroyed, or leaves the active state, and its Harmony hooks are removed on destruction.
- BepInEx.Analyzers 1.0.8 hard-codes the BepInEx 5 base class name `BepInEx.BaseUnityPlugin`. Its BepInEx002 warning is suppressed specifically because this BepInEx 6 plugin inherits from `BepInEx.Unity.Mono.BaseUnityPlugin`. The verification harness checks the compiled inheritance and plugin attribute directly.
- The game uses uGUI, TextMeshPro, and the Input System. `InputListener.Register()` sorts handlers by ascending priority; its dispatcher stops after a consumed event and avoids game shortcuts while a TMP input is selected. The trainer registers a modal handler while visible and unregisters it when hidden, disabled, or destroyed.
- Native UI probing found `Latin-Main`/`English-Main` fonts and the game's window, tab, button, checkbox, slider, and close-icon sprites. The trainer waits for those assets before constructing its own controls, without cloning game controllers or changing their listeners. It shares the existing EventSystem and keeps its canvas under BepInEx's persistent manager.

## Results

- Release build: **0 warnings, 0 errors**.
- Numeric tests: **31 passed**, including en-US/de-DE decimal parsing, scientific notation, zero, negative/NaN/infinite input, overflow, and speed/scale boundaries.
- Numeric plus game API and compiled plugin metadata checks: **78 passed**.
- Graphical native runtime assertions: **74 passed**, including the original game-action checks and native canvas/assets, single EventSystem, main-menu control availability, modal input, hit targets, resource input/button callbacks, input presets, focus cleanup, toggle/slider bindings, keyboard navigation and slider adjustment, pause/resume buttons, screen fitting, wheel scrolling, retained tab positions, scaled dragging, saved position, close button, and UI/input-handler cleanup.
- The game-action checks cover five installed Harmony hooks, main-menu guards, actual zero ping without changing players, bugs, uptime, resource editing, invalid inputs, development registration/completion/unregistration, sequel quality, datacenter repair, construction-safe datacenter unlocks, upgrade/research unlocks, pause/resume, speed scaling/restoration, effect reset, disposal, singleton cleanup, and unpatching.
- Graphical runtime and four-tab screenshot preview exited with code **0**. Screenshots were visually inspected; preview balances are disposable sample data.
- Headless assertions: **71 passed**. Three rendered hit-target checks are skipped when the graphics device is null; batch shutdown still reports the native failure described below.
- Runtime startup reported **0 unavailable hooks/actions**.
- Test DLLs/config/plugin restoration completed; `.gnome` and `.bak` save-file hashes remained unchanged.

The native command test uses real installed assemblies and catalogs with disposable database state. It suppresses UI/audio/IRC presentation because no gameplay scene is loaded. UI tests dispatch synthetic pointer events and control callbacks; they do not replace a real-profile play session. Actual development notifications and saving/reloading a real profile remain untested.

## Native shutdown limitation

The headless process crashes during native shutdown with exit code **-1073740771** (`0xC000041D`) after assertions have completed. Player logs show the crash after physics cleanup and Input System shutdown. The same headless startup/shutdown comparison with the previously installed **1.0.0** trainer also crashes with that code.

This comparison indicates the shutdown issue predates these trainer changes; it does not identify the underlying cause. The normal graphical run exits cleanly on the same installation. The smoke script deliberately reports the native failure even when trainer assertions pass. Logs for each mode are saved under `obj/runtime-smoke`.
