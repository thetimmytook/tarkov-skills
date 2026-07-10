---
name: tarkov-config
description: Read-only Escape from Tarkov FPS configuration analysis. Use when an agent needs to read EFT settings from %APPDATA%\Battlestate Games\Escape from Tarkov\Settings, inspect graphics/PostFX/game configuration, collect basic Windows hardware and pagefile context, and produce practical conclusions for stability, 50-60 FPS expectations, stutters, visibility, and support reporting without modifying game files.
---

# Tarkov Config

Analyze current Escape from Tarkov configuration in read-only mode and explain likely performance/stability risks.

This skill is separate from `tarkov-performance-benchmark` and `tarkov-frametime`. Use `tarkov-tuning` to orchestrate iterative tuning decisions across config analysis and measured FPS/frametime results.

Core rules:

- Read from `%APPDATA%\Battlestate Games\Escape from Tarkov\Settings` by default.
- Do not edit EFT files or apply settings automatically.
- Do not promise perfect FPS or stutter-free gameplay.
- Prefer the user's active goal. Default to stable minimum FPS, reduced stutters, and practical visibility over pretty graphics only when no custom goal is saved.
- If the user changes the target FPS or asks for a different tradeoff such as lower FPS for better quality, save the new goal in local memory and follow it in later analysis.
- If settings are missing or key names differ by Tarkov version, report `unknown`, lower confidence, continue, and mention the TODO error-report/upload flow.
- Manual mode must work: if files are unavailable, ask for screenshots/settings text.

## Primary Script

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analyze-tarkov-fps-config.ps1
```

The script reads EFT settings, collects basic system/pagefile context, and writes a Markdown report. Use `-JsonOutputPath` to also save structured data.

To update the local goal memory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analyze-tarkov-fps-config.ps1 -Goal better-graphics -TargetFpsMin 45 -QualityPreference "higher quality, acceptable lower FPS" -SaveGoal
```

The active goal is stored in `%LOCALAPPDATA%\TarkovSkills\memory\current-goal.json` — outside the plugin/repository tree, so it survives updates.

## Workflow

1. Read current config:
   - `Graphics.ini`
   - `PostFx.ini`
   - `Game.ini`

2. Collect system context:
   - CPU
   - GPU
   - RAM amount and speed where available
   - OS
   - pagefile size and location

3. Produce conclusions:
   - expectation for the active saved goal
   - risky settings
   - stutter/pagefile/RAM warnings
   - visibility/PostFX notes
   - suggested next manual checks

4. If the system appears near a known-good baseline but performance misses the diagnostics threshold from `../../references/measurement-rules.md` (15% worse than expected), switch from graphics tuning to diagnostics:
   - manual/external thermal throttling check with tools such as HWiNFO or MSI Afterburner; this skill does not collect temperatures automatically
   - power plan
   - installed GPU driver version and whether it is current for AMD/NVIDIA
   - storage/pagefile
   - RAM XMP/EXPO
   - overlays/recording/background apps
   - PvE/local server load

## Commands To Support In Conversation

- `/baseline`: summarize system readiness and current settings.
- `/tune-more-fps`: recommend manual changes in priority order.
- `/troubleshoot-stutter`: focus on RAM/pagefile/storage/background/system risks.
- `/tune-better-graphics`: only after stable target FPS is reached.
- `/report-bsg`: prepare a concise support report.

## Response Style

For tuning:

```text
Diagnosis:
<short diagnosis>

Tarkov Readiness:
<component checks>

Recommended next changes:
1. <change>
2. <change>
3. <change>

Why:
<short explanation>

After testing:
Report FPS/stutters on <map> for <time or raid length>.
```

For idea validation:

```text
Verdict: accepted / risky / needs testing / rejected
Why: <1-2 short reasons>
Fix: <how to adjust it>
Skill note: <exact rule to save>
```
