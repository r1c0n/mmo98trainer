# MMO98 Trainer

Edit your resources, unlock upgrades, and change the pace of MMO98 from an in-game menu styled to match the game.

This was the **first-ever trainer made for MMO98**, created just a few days after the game's release. It was kept for private use until now.

## Installation

You need **MMO98**, **BepInEx 6 Mono (64-bit)**, and the trainer file, `mmo98trainer.dll`.

1. Open your MMO98 installation folder—the folder containing `MMO98.exe`.
2. If BepInEx is not already installed, follow the [official installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html) and use the **Unity.Mono-win-x64** build. Start the game once, then close it.
3. With the game closed, create a `mmo98trainer` folder inside `BepInEx/plugins` and copy `mmo98trainer.dll` into it:

   ```text
   MMO98/BepInEx/plugins/mmo98trainer/mmo98trainer.dll
   ```

4. Start MMO98. If the trainer covers the main menu, press **Insert** to hide it. Load a profile, then press **Insert** to open the trainer.

When updating, replace the existing trainer DLL. Keep only one copy inside the BepInEx plugins folder.

If you downloaded the source files and need to build the DLL yourself, see the [developer guide](DEVELOPMENT.md#build).

## Controls

| Key | Action |
| --- | --- |
| **Insert** | Open or close the trainer |
| **Escape** | Close the trainer |
| **F6** | Pause or resume the game |
| **F8** | Turn off trainer effects and restore normal speed |

Drag the blue title bar to move the window. Use the mouse wheel or scrollbar to see the rest of a tab. You can change the menu size and whether it opens at startup in **Settings**.

The game continues running while the menu is open. Use **F6** or the **Pause game** button when you want to pause.

## Features

- **Resources:** view and change money, players, fans, and data. **Add** increases the current amount; **Set** replaces it. Enter amounts such as `1000` or `1e6` for one million.
- **Progression:** unlock all upgrades, research, and datacenters; complete a free development cycle; maximize sequel quality; and repair damaged datacenters.
- **Simulation:** keep bugs and ping at zero, maintain 100% uptime, and adjust game speed from **0.1x to 50x**.
- **Settings:** adjust the menu scale, reset its position, choose whether it appears at startup, and check game and trainer versions.

Game actions become available after you load a profile. Effects start switched off, with game speed at 1x, on the first run.

## Saving and turning effects off

Resource edits, unlocks, sequel quality changes, and repairs are included in the game's normal saves.

Closing the menu leaves active effects running. **F8** switches off the continuous effects and returns speed to 1x. Resource edits and unlocks remain, and the game's pause state stays as you set it.

Your effect settings and menu preferences are remembered between sessions.

## Changing shortcuts

Close the game and open:

```text
BepInEx/config/com.mmo98.trainer.cfg
```

Edit the keys under `[Shortcuts]` and save the file. Supported keys are **Insert**, **Home**, and **F1–F12**. Use a different key for each action.

## Troubleshooting

- **The menu does not appear:** press Insert, check that BepInEx 6 Mono is installed, and confirm the DLL is in the folder shown above.
- **Buttons are disabled:** load a profile first. An action may also be unavailable if the game version has changed.
- **The menu is too large or misplaced:** use **Settings → Reset menu position and scale**.
- **An action reports an error:** check `BepInEx/LogOutput.log` for details.

Last checked with MMO98 **2026.07.26.1**. See [compatibility information](COMPATIBILITY.md) for the tested setup.

## Uninstalling

Close the game and remove `mmo98trainer.dll` from the plugins folder. You can also delete `BepInEx/config/com.mmo98.trainer.cfg` to remove its settings. Changes already saved to a profile remain.
