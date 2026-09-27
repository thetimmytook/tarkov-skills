---
name: tarkov-config
description: Analyze Escape from Tarkov graphics configuration and Windows performance context without changing game files. Use for FPS expectations, stutter risks, visibility tradeoffs, or a saved performance goal; prefer the signed Tarkov Performance Toolkit and support transparent manual review when it is unavailable.
---

# Tarkov Config

Analyze settings and system readiness in read-only mode. Never edit EFT files or promise a specific FPS.

## Choose The Input Path

1. For a local agent, run `tarkov-skills.exe status`. If available, run `tarkov-skills.exe inspect` and consume its JSON.
2. In a web client, ask the user to open **Tarkov Performance Toolkit**, press **Collect report**, then **Copy JSON** and paste or attach the result.
3. Without Toolkit, explain each source before reading it. A local agent may directly read `Graphics.ini`, `PostFx.ini`, and `Game.ini` and use standard read-only Windows system queries. A web user may attach those files and a Windows System Information report. Do not download, generate, or execute script files.

The Toolkit is the only automated dependency. If it is missing, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`; do not offer unsigned PowerShell as a fallback.

## Goal

Read the active goal from `inspect` or `tarkov-skills.exe goal get`. When the user changes the target or quality tradeoff, confirm the values and save them with:

```text
tarkov-skills.exe goal set --goal <name> --target-fps <20-360> --quality <text> [--notes <text>]
```

In web/manual mode, keep the goal in the conversation when the local command cannot be run.

## Analysis

- Use only `Graphics.ini`, `PostFx.ini`, and graphically relevant `Game.ini` data. Ignore controls and sound.
- Consider CPU, GPU/VRAM, RAM, game-drive media, pagefile size/media, resolution, and driver version.
- Suggest only changes that are not already applied.
- If measured performance is at least 15% below a relevant expectation, switch from ordinary graphics tuning to diagnostics such as throttling, power, RAM configuration, storage, drivers, overlays, and local PvE load.
- Treat driver currency as a manual check against the GPU vendor's official page.

Use the readiness display and response conventions in `references/configuration-rules.md`. Apply thresholds from `references/measurement-rules.md`.
