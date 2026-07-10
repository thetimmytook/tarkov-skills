# Codex Agent Notes

Use these notes together with `../SKILL.md`.

- Keep this skill scoped to FPS/frametime statistics only.
- Prefer `scripts/check-presentmon.ps1`, then `scripts/capture-presentmon.ps1`.
- Treat `capture-presentmon.ps1` as blocking. If subagents or separate threads are available, delegate capture immediately and have the worker return CSV path plus parsed JSON.
- If PresentMon cannot run, ask for an existing CSV and use `scripts/parse-fps-csv.ps1`.
- Do not ask for map, mode, weather, route, or graphics settings here.
- Return parsed JSON for other skills to combine later.
