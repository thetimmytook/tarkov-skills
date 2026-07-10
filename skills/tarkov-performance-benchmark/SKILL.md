---
name: tarkov-performance-benchmark
description: Collect read-only Escape from Tarkov graphics performance benchmark data. Use when an agent needs to guide a user through one repeatable benchmark run, read current EFT graphics/PostFX settings, collect Windows hardware info, infer map/mode/raid context from EFT logs when available, ask only for missing weather/time/activity context, parse exported FPS CSV files from PresentMon/CapFrameX/FrameView with PowerShell, and produce one normalized JSON run record without modifying game files.
---

# Tarkov Performance Benchmark

Guide the user through repeatable Escape from Tarkov performance data collection. Treat the skill as a benchmark recorder, not as an optimizer.

Use `tarkov-tuning` when benchmark results should drive iterative settings changes.

Core rules:

- Keep the workflow read-only toward the game. Do not edit EFT config files.
- Do not require Python or third-party packages.
- Prefer inline PowerShell commands or bundled `.ps1` scripts.
- For local non-agent usage, run `app/Start-TarkovBenchmark.cmd` from the repository root, or use the standalone release zip from GitHub Releases.
- If a field cannot be known from files or CSV, ask the user in plain language.
- Use `unknown` instead of blocking when the user is unsure.
- Do not optimize settings in this skill. A future analysis skill can use collected benchmark data for recommendations.
- TODO: after `run.json` is built, offer to open a benchmark upload form and send/paste the run contents. The actual form URL and anti-spam flow are not decided yet.
- TODO: when a bundled PowerShell script fails, create a small error report artifact and offer to open an error upload form. URL is TODO.

## Workflow

1. Establish the goal:
   - maximum FPS
   - fewer stutters
   - better visibility
   - balanced visibility/performance

2. Read current settings:
   - Run `scripts/read-eft-settings.ps1`.
   - It reads `%APPDATA%\Battlestate Games\Escape from Tarkov\Settings\Graphics.ini` and `PostFx.ini` when present.

3. Collect system info:
   - Run `scripts/collect-system-info.ps1`.
   - It collects CPU, GPU, RAM, OS, and display resolution where Windows exposes it.

4. Lead the benchmark:
   - Read `references/benchmark-protocol.md` when planning the user-facing test flow.
   - Ask the user to run one consistent scenario, usually 90-180 seconds.
   - Prefer the map where the user has the problem. If they want a worst-case stress test and have no specific map, use Streets.
   - Prefer reading map/mode/raid id/game version from logs with `scripts/read-raid-context.ps1`.
   - Logs cannot distinguish PvP from PvE on BSG servers (both report Online); always confirm the exact mode with the user.
   - Ask simple fallback questions after the run only when log context is missing or low-confidence:
     - Which map was it, if logs did not identify it?
     - Was it PvP online, PvE on BSG servers, PvE local, offline practice, hideout, or unknown, if logs did not identify it?
     - What was the weather: clear, rain, fog, snow, cloudy, or unknown?
     - Was it day, night, dawn/dusk, or unknown?
     - Did they stand still, walk a route, fight, or mixed/unknown?

5. Parse FPS capture:
   - Prefer using the separate `tarkov-frametime` skill for FPS capture/stat parsing.
   - Ask the user for the CSV path from PresentMon, CapFrameX, or FrameView when working manually.
   - Run `scripts/parse-fps-csv.ps1 -Path <csv>`.
   - If parsing fails, read `references/fps-csv-formats.md` and inspect the CSV headers.

6. Build a normalized run JSON:
   - Run `scripts/build-run-json.ps1` with the settings JSON, system JSON, FPS JSON, manual context parameters, and `-GameVersion` from the log context when available.
   - The script strips user/host names from paths, so the resulting `run.json` is safe to share.
   - Store or return the JSON as the run artifact.

7. Finish the run record:
   - Return or save the normalized JSON run.
   - Summarize missing fields and confidence.
   - TODO: offer to open a benchmark upload form and submit/copy the run contents.
   - Do not recommend graphics changes here; this skill's output is input data for later analysis.

## Quality Checks

Run confidence tiers are defined in `../../references/measurement-rules.md` and computed by `build-run-json.ps1`; do not restate the numbers here.

For Tarkov, do not over-trust a single run. Recommend repeated runs per profile when results are close (see the same file).
