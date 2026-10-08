# MMO98 Trainer

A BepInEx 6 Mono trainer for MMO98, updated to version 1.1.0. Compatibility was checked against game **2026.07.26.1** (Steam build **24396256**) on October 8, 2026 using dnSpy MCP and the installed game runtime. See [COMPATIBILITY.md](COMPATIBILITY.md) for evidence and test limits.

## Controls and features

| Shortcut | Action |
| --- | --- |
| Insert | Show/hide the trainer |
| F6 | Pause/resume the game |
| F8 | Disable all trainer effects and restore normal speed |

- **Resources:** live money, player, fan, and data totals; Add and Set buttons; 1,000/1,000,000 input presets. Accepts zero, local decimal notation, and scientific notation such as `1e6`. Invalid values and overflowing additions are rejected.
- **Progression:** unlock all upgrades/research/datacenters; complete a free development cycle; set every sequel quality category to 100%; repair degraded/critical datacenters. The development button also completes an already running cycle.
- **Simulation:** no bugs, actual zero ping, 100% uptime, speed from 0.1x to 50x, and speed presets. Speed uses the game's timer, so command timers and interval simulations advance together in the game's bounded steps.
- **Settings:** menu scale, automatic fitting on smaller screens, saved menu position, show-on-startup preference, game/trainer versions, and unavailable-feature diagnostics.

Effects and preferences persist in `BepInEx/config/com.mmo98.trainer.cfg`. First-run effects are off and speed is 1x. To change shortcuts, close the game and edit the config; supported keys are Insert, Home, and F1-F12. When shortcuts overlap, menu takes priority, followed by disable-effects, then pause.

Resource, unlock, quality, and repair actions change the loaded profile and are included in normal game saves. Hiding the menu leaves active effects enabled. F8 disables the continuous effects; it does not undo resource edits, unlocks, or the game's pause state. Game actions are disabled while no profile is loaded.

## Build and install

Use the .NET 10 SDK and an existing BepInEx 6 Mono installation for the game. This project is not an IL2CPP plugin.

Build directly against your installed game's assemblies:

```powershell
dotnet build .\mmo98trainer.csproj -c Release '-p:GameManagedPath=F:\SteamLibrary\steamapps\common\MMO98\MMO98_Data\Managed'
```

Alternatively, sync local reference copies, then use the default build:

```powershell
.\scripts\Sync-GameReferences.ps1 -GameDirectory 'F:\SteamLibrary\steamapps\common\MMO98'
dotnet build .\mmo98trainer.csproj -c Release
```

The five references are Assembly-CSharp, R3, Unity.InputSystem, SourceGeneration, and ObservableCollections. Sync them after a game update. Additional local reference files are ignored by Git. Build dependencies are pinned to the versions used for verification.

Close MMO98, then copy **only** `bin/Release/netstandard2.1/mmo98trainer.dll` to `MMO98/BepInEx/plugins/mmo98trainer/mmo98trainer.dll`. Replace the old trainer DLL and avoid leaving a second copy elsewhere under `plugins`. The game supplies its own assemblies; do not install the `libs` directory or test DLLs. A missing-reference build explains how to sync the required files.

## Verification

Run the build, numeric boundary tests, installed game API checks, and compiled plugin metadata checks:

```powershell
.\scripts\Verify.ps1 -GameDirectory 'F:\SteamLibrary\steamapps\common\MMO98'
```

Run just the numeric tests without an installed game:

```powershell
dotnet run --project .\tests\Verification\Verification.csproj -c Release
```

For the contained native runtime test, close MMO98 and keep Steam available:

```powershell
.\scripts\Runtime-Smoke.ps1 -GameDirectory 'F:\SteamLibrary\steamapps\common\MMO98'
```

The runtime harness starts the game hidden in headless mode, uses disposable state and real game catalogs, and never loads a profile or resumes a game timer. It temporarily suppresses scene-specific UI, audio, and IRC presentation while testing game commands. It restores the previous trainer and config, removes its test DLL and temporary Steam app ID file, and checks save-file hashes. Logs remain under `obj/runtime-smoke`. The script reports native shutdown failures separately from assertion results.

Use `-Baseline` to compare startup/shutdown with the previously installed trainer. On the verified installation, both the original 1.0.0 trainer and the updated trainer trigger a native headless shutdown crash after the test finishes. The 39 trainer assertions passed; the complete smoke command therefore still reports a shutdown failure. This has not been established as a regression from the update. Details are in [COMPATIBILITY.md](COMPATIBILITY.md).

To verify interactive behavior, open a profile and check menu dragging/scaling/scrolling, shortcuts, effect toggles, pause, development notifications, and normal saving/reloading. Those graphical and save-roundtrip checks are not covered by the disposable-state runtime harness.

Commit messages follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `fix(trainer): ...`, `feat(trainer): ...`, `test(trainer): ...`, and `docs(trainer): ...` at meaningful checkpoints.
