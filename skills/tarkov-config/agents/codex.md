# Codex Agent Notes

Use these notes together with `../SKILL.md`.

- Work read-only by default.
- Use `../scripts/analyze-tarkov-fps-config.ps1` for local config analysis.
- Do not edit files under `%APPDATA%\Battlestate Games\Escape from Tarkov\Settings`.
- If the script cannot identify a setting key, inspect the parsed files and add a safe alias later.
- If settings are missing or key names changed, continue with `unknown`, lower confidence, and mention the TODO error-report/upload flow.
- Keep conclusions practical: stable FPS, stutters, pagefile/RAM, and visibility.
- If the installed GPU driver version is available, report it as diagnostic context; automatic latest-driver checks against AMD/NVIDIA are a future enhancement.
- Do not mix this skill with benchmark FPS capture unless the user explicitly provides measured FPS data.
- If the user changes the target FPS or quality/performance tradeoff, run the analyzer with `-Goal`, `-TargetFpsMin`, `-QualityPreference`, and `-SaveGoal` so `%LOCALAPPDATA%\TarkovSkills\memory\current-goal.json` is updated.
