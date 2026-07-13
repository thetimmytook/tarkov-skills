---
name: tarkov-performance-benchmark
description: Collect read-only Escape from Tarkov graphics benchmark data. Use when an agent needs to capture one fixed 2- or 4-minute FPS/frametime run with PresentMon, read current EFT settings and Windows hardware, infer map and game version from Tarkov logs, ask only for BSG servers versus Local plus weather and time of day, and append the result to one normalized benchmark.json file without modifying game files.
---

# Tarkov Performance Benchmark

Guide a player through a repeatable benchmark capture. Treat this skill as a recorder, not as an optimizer. Use `tarkov-tuning` when results should guide settings changes.

Core rules:

- Keep all game interactions read-only.
- Do not require Python.
- Use PresentMon for automated capture. It needs elevation for its short ETW capture process; explain the Windows permission prompt before starting it.
- Start capture only after Tarkov is running and the player is already in the raid.
- Capture for 120 seconds by default. Allow 240 seconds only when requested.
- Read the map and game version from logs. Do not ask the player to identify the map unless log parsing has failed and manual recovery is explicitly needed.
- Ask after capture for exactly: `BSG servers` or `Local`, weather, and time of day. Do not ask for a route, activity, PvP/PvE distinction, or a separate server-model field.
- Save all runs in `%LOCALAPPDATA%\TarkovSkills\benchmark.json`. Store `system` once at the root and a fresh settings snapshot inside every run.
- After a run is saved, build the upload payload with `scripts/get-benchmark-upload.ps1` — it returns only runs not submitted yet (tracked via `uploaded_run_count` inside `benchmark.json`) — then offer the Performance form: `https://forms.gle/D692T2Umd5ktD5wj8`.
- On script failure, create a short sanitized error report (no user paths or host names), save it under %LOCALAPPDATA%\TarkovSkills\reports\, and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.

## Workflow

1. Check readiness:
   - Run `scripts/check-presentmon.ps1`.
   - Check that `EscapeFromTarkov.exe` is running.
   - If PresentMon is absent, direct the player to the official release and `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\PresentMon.exe`.

2. Capture the run:
   - Read settings with `scripts/read-eft-settings.ps1` and system info with `scripts/collect-system-info.ps1 -IncludePagefile` at capture start.
   - Run `scripts/capture-presentmon.ps1 -DurationSec 120 -RequestElevation` or use the app.
   - Parse FPS and frametime output from the capture result.

3. Collect context after capture:
   - Run `scripts/read-raid-context.ps1` for map and game version.
   - Ask the player to choose `BSG servers` or `Local`; do not offer an unknown option for this field.
   - Ask weather and time of day. Allow `unknown` only when the player genuinely cannot tell.

4. Append the run:
   - Run `scripts/add-benchmark-run.ps1` with settings, system, FPS JSON, map, game version, execution, weather, and time.
   - The writer strips user and host names from paths before saving.

5. Finish:
   - Offer the benchmark folder or the upload: run `scripts/get-benchmark-upload.ps1`, give the player the payload, and after they submit it run the script again with `-MarkUploaded` so the same runs are not offered twice.
   - Do not recommend graphics changes in this skill.

## Quality Checks

Confidence tiers are defined in `references/measurement-rules.md` and computed by `add-benchmark-run.ps1`. Prefer repeated runs under comparable map, weather, time, settings, and capture duration.
