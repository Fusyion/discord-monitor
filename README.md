# Discord Mic Monitor

A tiny always-on-top Windows widget that shows whether you are **muted or unmuted in Discord** — a small rounded dark card with a mic icon and a status dot.

- ⚪ white mic, 🟢 green dot — unmuted
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

- **Drag** the card anywhere, on any monitor; position and scale are remembered.
- **Left-click** toggles Discord mute (also undeafens if you were deafened).
- **Right-click** → version info, Scale (50–200%), Re-authorize if the connection ever gets stuck, or Exit.
- It reconnects automatically if Discord restarts.

If something misbehaves, check `%AppData%\DiscordMicMonitor\error.log`.

### Start with Windows (optional)

Press `Win+R`, run `shell:startup`, and drop a shortcut to `DiscordMicMonitor.exe` there.
