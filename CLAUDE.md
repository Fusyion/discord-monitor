# Discord Mic Monitor

Single-file C# WinForms app: a floating always-on-top circle showing Discord mute/unmute state.

## Build & run

- Build: `build.cmd` (uses the built-in .NET Framework 4.x compiler at `%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` — so the code must stay **C# 5** compatible: no string interpolation, no null-conditional operators, no expression-bodied members).
- Run: `DiscordMicMonitor.exe`. Single-instance via named mutex.

## Architecture (all in DiscordMicMonitor.cs)

- `MonitorForm` — borderless topmost per-pixel-alpha layered window (WS_EX_LAYERED + UpdateLayeredWindow, drawn to an ARGB bitmap): rounded dark card, mic glyph, status dot, drop shadow. All drawing is in 68px design units scaled by `_scalePercent` (right-click Scale menu: 50–200%). Left-drag moves it, left-click toggles mute, right-click menu (grayed version header / Scale / Re-authorize / Exit). Version lives in `Program.Version` — bump it there. No hover tooltip by design. White mic + green dot = unmuted, red mic + slash = muted/deafened, gray = disconnected.
- `DiscordRpc` — background thread speaking Discord's IPC protocol over `\\.\pipe\discord-ipc-{0..9}` (frames: LE int32 opcode + int32 length + JSON). Auth uses Discord's own StreamKit Overlay client id (`207646673902501888`); the AUTHORIZE code is exchanged for an RPC token via `https://streamkit.discord.com/overlay/token` (no client secret needed). Then SUBSCRIBE `VOICE_SETTINGS_UPDATE` + `GET_VOICE_SETTINGS`. Reconnects every ~3s when the pipe drops.
- `Config` — `%AppData%\DiscordMicMonitor\config.txt`, `key=value` lines: cached `token`, window `x`/`y`.

JSON via `JavaScriptSerializer` (System.Web.Extensions) to avoid external packages.
