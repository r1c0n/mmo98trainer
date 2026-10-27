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

## Native shutdown limitation

The headless process crashes during native shutdown with exit code **-1073740771** (`0xC000041D`) after assertions have completed. Player logs show the crash after physics cleanup and Input System shutdown. The same headless startup/shutdown comparison with the previously installed **1.0.0** trainer also crashes with that code.

This comparison indicates the shutdown issue predates these trainer changes; it does not identify the underlying cause. The normal graphical run exits cleanly on the same installation. The smoke script deliberately reports the native failure even when trainer assertions pass. Logs for each mode are saved under `obj/runtime-smoke`.
