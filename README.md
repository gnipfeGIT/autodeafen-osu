# Preview https://youtu.be/ilrXSkWvlVs

# Auto Deafen osu!

A small Windows utility that toggles Discord deafen while you are still full-comboing an osu! map.

## How it works

- Reads live osu! data from the tosu/gosumemory-compatible endpoint at `http://127.0.0.1:24050/json`.
- Treats a run as FCing while gameplay is active, misses are `0`, and combo has not dropped during the run.
- Sends your configured Discord `Toggle Deafen` keybind once the combo threshold is reached. By default, that threshold is 75% of the current map's max combo.
- Sends the same keybind again after a miss/combo break or when gameplay stops, depending on your settings.

This app does not log into Discord or use Discord private APIs. It only presses the hotkey you configure, which keeps the behavior simple and under your control.

## Setup

1. Install and run `tosu`.
2. In Discord, create a keybind for **Toggle Deafen**. The default expected keybind is `Ctrl+Shift+D`.
3. Run this app, confirm the keybind and combo threshold, then press **Start Monitoring**.

Start with Discord undeafened, or tick **Discord is currently deafened** so the app's internal state matches reality.

If Live Status says telemetry is unavailable, open `http://127.0.0.1:24050/json` in a browser on the same PC. It should show a big JSON page. If it does not, tosu is not running, is using a different port, or is stuck and needs to be restarted.

If Discord ignores **Test Deafen Hotkey**, try the other **Hotkey method** values. They use different Windows input APIs. If all methods are ignored, Discord is filtering injected input on your setup and the app needs a direct Discord-control path instead of simulated keyboard input.

## Build

```powershell
dotnet build
```

The executable is produced under `bin\Debug\net9.0-windows\` for debug builds.

## Release

Create a single self-contained Windows executable:

```powershell
.\publish-release.ps1
```

The one-click release file is `dist\AutoDeafenOsu.exe`.
