---
name: tarkov-frametime
description: Collect read-only FPS and frametime statistics for Escape from Tarkov using PresentMon CSV capture or an existing CSV export. Use when an agent needs only FPS stats such as average FPS, 1% low, 0.1% low, p95/p99 frametime, capture duration, and sample count. Leave map, weather, mode, settings analysis, and gameplay context to benchmark/tuning skills that combine multiple data sources.
---

# Tarkov Frametime

Collect FPS/frametime statistics only. Keep this skill separate from map/context collection, settings analysis, and benchmark run assembly so it can be reused as a small measurement component.

Use `tarkov-tuning` when the measured result should drive the next config change.

Core rules:

- Use PresentMon as the preferred capture utility.
- Accept existing PresentMon/CapFrameX/FrameView CSV files as manual input.
- Stay read-only and do not touch EFT settings.
- Do not automate gameplay or interact with anti-cheat-adjacent game internals.
- Do not ask for map, weather, PvE/PvP, route, or settings context here; `tarkov-performance-benchmark` is responsible for combining these with FPS metrics.
- Output JSON metrics that other skills can combine with context later.

## Workflow

1. Check PresentMon:
   - Run `scripts/check-presentmon.ps1`.
   - If not found, tell the user to download PresentMon from `https://github.com/GameTechDev/PresentMon/releases`.
   - Ask them to place `PresentMon.exe` under `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\` (preferred; survives updates), or provide a custom path with `-PresentMonPath`.
   - If they already have a CSV exported from PresentMon, CapFrameX, or FrameView, use manual CSV mode. If they have no capture tool and no CSV, stop and guide them to install/export first.

2. Capture FPS stats when PresentMon is available:
   - Run `scripts/capture-presentmon.ps1 -DurationSec 120`.
   - PresentMon needs an elevated session for ETW capture. Explain this and ask the user to run the capture from an elevated console; everything else stays non-admin.
   - Captured CSVs are written to `%LOCALAPPDATA%\TarkovSkills\captures\` by default.
   - Ask the user to start the raid/gameplay scenario before capture.
   - Treat PresentMon capture as blocking by design because the script waits until the requested duration finishes.
   - If an agent orchestration environment supports subagents or separate threads, run the capture step there immediately instead of blocking the main agent.
   - Leave map/context collection to `tarkov-performance-benchmark`.

3. Parse CSV:
   - Run `scripts/parse-fps-csv.ps1 -Path <csv>`.
   - Return average FPS, 1% low, 0.1% low, p95/p99 frametime, duration, sample count, and method.

4. If PresentMon invocation is blocked:
   - switch to manual CSV mode only when the user can provide an existing export;
   - otherwise ask the user to install/use PresentMon, CapFrameX, or FrameView and export CSV;
   - parse the provided CSV.

## Blocking Capture Rule

PresentMon capture is blocking by design. For real captures of 90-180 seconds, delegate the capture step to a subagent/separate thread when available. The delegated worker should return only:

- CSV path
- parsed FPS JSON
- any PresentMon error message

Keep the main agent available for user interaction.
