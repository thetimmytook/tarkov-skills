---
name: tarkov-performance-benchmark
description: Guide a repeatable read-only Escape from Tarkov performance benchmark with current settings, system context, raid logs, and FPS/frametime metrics. Use the signed Store applications for automated collection and produce a normalized run without modifying game files.
---

# Tarkov Performance Benchmark

Record a benchmark; do not optimize settings in this skill.

## Preferred Store Flow

For a local agent, use the signed Toolkit without opening its GUI:

1. Run `tarkov-skills.exe status` and require an active raid.
2. Ask before starting capture, then run `tarkov-skills.exe capture --duration 120` or `240` when requested.
3. Use the report's settings, system, map/log context, and performance metrics.
4. Ask only for missing `BSG servers` versus `Local`, weather, and time of day. Server execution is required; weather/time may be `unknown`.

Do not open the GUI when the local alias is callable. For a web client, ask the user to open **Tarkov Performance Toolkit**, select **Benchmark**, complete and save the run, press **Copy results**, and paste the JSON into the conversation. `Copy results` copies only the latest complete run and uploads nothing. The Toolkit Store page is `https://apps.microsoft.com/detail/9N3L7DZH0K64`. Without Toolkit, accept manually attached settings plus an existing capture export, but do not provide or execute unsigned scripts.

## Rules

- Start only when Tarkov is running and logs show an active raid.
- Default to 120 seconds; allow 240 seconds on request.
- Prefer map and game version from logs. Ask for the map only if log recovery fails.
- Do not ask for route, activity, PvP/PvE, or a separate server-model field.
- Discard interrupted, cancelled, game-crash, or raid-exit captures.
- Keep settings and system context with each run used for comparison.
- Do not include usernames, hostnames, local paths, IPs, serial numbers, or machine identifiers.
- Do not recommend graphics changes here. Use `tarkov-tuning` for that.

Apply confidence and repeatability rules from `references/measurement-rules.md`.
