# Discord Mic Monitor

A tiny always-on-top Windows widget that shows whether you are **muted or unmuted in Discord** — a small rounded dark card with a mic icon and a status dot.

- ⚪ white mic, 🟢 green dot — unmuted
- 🟢 green outline/glow — actively speaking (voice transmitting)
- 🔴 red mic with slash, red dot — muted (or deafened)
- gray mic and dot — Discord not running / not connected

No installs, no dependencies, no Discord developer account. It's a single ~20 KB exe built with the C# compiler that ships with Windows.

## How it works

It connects to the Discord desktop client's local IPC pipe (`\\.\pipe\discord-ipc-0`) and subscribes to voice-settings changes, identifying as Discord's own **StreamKit Overlay** app. The one-time OAuth code is exchanged via `streamkit.discord.com` (Discord's own service), so there is no client secret or token for you to manage. Everything else is local.

On first run, Discord may pop up an authorization dialog for "StreamKit Overlay" — click **Authorize** once. The token is cached in `%AppData%\DiscordMicMonitor\config.txt`.

## Build

```
build.cmd
```

## Use

Run `DiscordMicMonitor.exe`.

- The card **only appears while you are in a voice channel or call**; the rest of the time it stays out of the way. A tray icon (which also mirrors your mute state) is always there for the menu.
- **Drag** the card anywhere, on any monitor; position and scale are remembered.
- **Left-click** toggles Discord mute (also undeafens if you were deafened).
- **Right-click** the card or tray icon → version info, Scale (50–200%), Start with Windows, Re-authorize if the connection ever gets stuck, or Exit.
- It reconnects automatically if Discord restarts.

If something misbehaves, check `%AppData%\DiscordMicMonitor\error.log`.

### Updating

The app checks GitHub for a newer release shortly after startup and asks before installing; you can also right-click → **Check for updates...**. Updates it downloads itself never trigger SmartScreen warnings (no Mark of the Web). It swaps its own exe in place and restarts, so position, scale, authorization and the autostart entry all survive.

To publish a release: bump `Program.Version`, commit, then tag and push — GitHub Actions builds the exe and attaches it to the release:

```
git tag v1.3.0
git push origin v1.3.0
```

### Start with Windows (optional)

Right-click the card or tray icon and tick **Start with Windows** (uses the standard `HKCU` Run registry key).
