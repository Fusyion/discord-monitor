# Discord Mic Monitor

A tiny always-on-top Windows widget that shows whether you are **muted or unmuted in Discord**.

- 🟢 green mic — unmuted
- 🔴 red mic with slash — muted (or deafened)
- ⚪ gray — Discord not running / not connected

No installs, no dependencies, no Discord developer account. It's a single ~30 KB exe built with the C# compiler that ships with Windows.

## How it works

It connects to the Discord desktop client's local IPC pipe (`\\.\pipe\discord-ipc-0`) and subscribes to voice-settings changes, identifying as Discord's own **StreamKit Overlay** app. The one-time OAuth code is exchanged via `streamkit.discord.com` (Discord's own service), so there is no client secret or token for you to manage. Everything else is local.

On first run, Discord may pop up an authorization dialog for "StreamKit Overlay" — click **Authorize** once. The token is cached in `%AppData%\DiscordMicMonitor\config.txt`.

## Build

```
build.cmd
```

## Use

Run `DiscordMicMonitor.exe`.

- **Drag** the icon anywhere; position is remembered.
- **Left-click** toggles Discord mute (also undeafens if you were deafened).
- **Right-click** → Exit, or Re-authorize if the connection ever gets stuck.
- It reconnects automatically if Discord restarts.

### Start with Windows (optional)

Press `Win+R`, run `shell:startup`, and drop a shortcut to `DiscordMicMonitor.exe` there.
