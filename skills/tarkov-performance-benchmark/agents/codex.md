# Codex Agent Notes

Use these notes together with `../SKILL.md`.

## Local Work

- Work from the skill directory when running bundled scripts.
- Use PowerShell scripts in this skill's `scripts/` folder; do not introduce Python for normal usage.
- Keep all EFT interactions read-only.
- Do not edit `Graphics.ini`, `PostFx.ini`, or other game files.
- Keep persistent player data only in `%LOCALAPPDATA%\TarkovSkills\benchmark.json`.

## Suggested Command Flow

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-eft-settings.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\collect-system-info.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-presentmon.ps1 -DurationSec 120 -RequestElevation
```

Use `-ExecutionPolicy Bypass` only for the current process invocation. Do not change the user's system execution policy.

## User Guidance

- Start the capture only after the player is in the raid.
- Read map and game version from logs. Do not ask for the map unless parsing failed.
- Ask after capture for BSG servers versus Local, weather, and time of day only.
- BSG servers versus Local is mandatory; weather and time may be `unknown` when the player cannot tell.
- After appending `benchmark.json`, offer the Performance form: `https://forms.gle/D692T2Umd5ktD5wj8`.
- When a PowerShell script fails, preserve the sanitized error (no user paths) and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.
- Do not recommend setting changes from this skill; produce clean benchmark JSON data.
