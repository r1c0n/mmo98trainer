# Developer guide

For installation and everyday use, see [README.md](README.md).

## Build

Use the .NET 10 SDK and a BepInEx 6 Mono installation for MMO98.

Build against your installed game's assemblies, replacing the example path with your game folder:

```powershell
dotnet build .\mmo98trainer.csproj -c Release '-p:GameManagedPath=C:\Path\To\MMO98\MMO98_Data\Managed'
```

Alternatively, sync local reference copies and use the default build:

```powershell
.\scripts\Sync-GameReferences.ps1 -GameDirectory 'C:\Path\To\MMO98'
dotnet build .\mmo98trainer.csproj -c Release
```

The seven references are Assembly-CSharp, R3, Unity.InputSystem, SourceGeneration, ObservableCollections, UnityEngine.UI, and Unity.TextMeshPro. Sync them after a game update. The `libs` directory is ignored by Git.

Install only `bin/Release/netstandard2.1/mmo98trainer.dll` under `MMO98/BepInEx/plugins/mmo98trainer` while the game is closed. Keep reference assemblies and test DLLs out of the installed trainer package.

## Verification

Build and run the numeric, installed game API, and compiled plugin metadata checks:

```powershell
.\scripts\Verify.ps1 -GameDirectory 'C:\Path\To\MMO98'
```

Run numeric checks without an installed game:

```powershell
dotnet run --project .\tests\Verification\Verification.csproj -c Release
```

For graphical runtime checks, close MMO98 and keep Steam available:

```powershell
.\scripts\Runtime-Smoke.ps1 -GameDirectory 'C:\Path\To\MMO98' -Graphics
```

The runtime harness uses disposable state and real game catalogs without loading a profile or resuming a game timer. It temporarily suppresses scene-specific UI, audio, and IRC presentation while testing game commands. It restores the previous trainer and config, removes its test DLL and temporary Steam app ID file, and checks save-file hashes. Logs remain under `obj/runtime-smoke`, named by mode.

Additional modes:

| Option | Purpose |
| --- | --- |
| No option | Run headless batch checks; skip rendered hit-target checks |
| `-Baseline` | Compare startup and shutdown with the installed trainer |
| `-UIPreview` | Capture four tab screenshots under `obj/runtime-smoke/ui-preview` |
| `-UIProbe` | Export native UI assets and control sizes to `obj/runtime-smoke/NativeUI-probe.csv` |

See [COMPATIBILITY.md](COMPATIBILITY.md) for the known headless shutdown limitation. Real-profile saving/reloading and gameplay notifications require a manual play session.

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), for example `docs(readme): clarify installation`.
