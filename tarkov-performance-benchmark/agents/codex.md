# Codex Agent Notes

Use these notes together with `../SKILL.md`.

## Local Work

- Work from the skill directory when running bundled scripts.
- Use PowerShell scripts in `../scripts/`; do not introduce Python for normal usage.
- Keep all EFT interactions read-only.
- Do not edit `Graphics.ini`, `PostFx.ini`, or other game files.
- Prefer emitting or saving JSON artifacts for settings, system info, FPS parsing, run records, and comparisons.

## Suggested Command Flow

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\read-eft-settings.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\collect-system-info.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\parse-fps-csv.ps1 -Path "<capture.csv>"
```

Use `-ExecutionPolicy Bypass` only for the current process invocation. Do not change the user's system execution policy.

## User Guidance

- Lead the user one step at a time during a benchmark.
- Read map/mode/raid context from logs first when available, then ask only for missing or low-confidence map/mode fields.
- Ask for weather, time of day, route/activity, and notes after the capture.
- If the user does not know an answer, use `unknown`.
- TODO: after building `run.json`, offer to open a benchmark upload form. URL is TODO.
- TODO: when a PowerShell script fails, preserve the error and offer an error-report form. URL is TODO.
- Do not recommend setting changes from this skill; produce a clean benchmark JSON record.
