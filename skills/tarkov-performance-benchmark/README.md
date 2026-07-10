# Tarkov Performance Benchmark

Instruction and overview for the `tarkov-performance-benchmark` skill.

This skill guides a user through a read-only Escape from Tarkov performance benchmark. It collects the current settings and system context, helps the user run one repeatable capture, parses the exported FPS CSV, and produces one normalized JSON record for the run.

## How To Use

For a local user, run `app\Start-TarkovBenchmark.cmd` from the repository root, or download the standalone `TarkovBenchmarkApp.zip` from GitHub Releases (no repository or agent needed).

The WinForms wizard will:

1. capture current EFT settings;
2. capture Windows hardware/system info (including pagefile);
3. read latest Tarkov logs to prefill map, server model, and game version when possible (the exact PvP/PvE mode always stays a manual choice);
4. ask for missing context such as weather, time of day, and activity;
5. let the user select an exported FPS CSV;
6. parse FPS and frametime metrics;
7. save an anonymized `run.json` under `%LOCALAPPDATA%\TarkovSkills\runs\<timestamp>\`;
8. TODO: offer to open an upload form for the benchmark contents.

Use the separate `tarkov-frametime` skill when the task is only FPS capture/stat parsing.

For an agent, load `SKILL.md`, then the relevant file in `agents/`, and use the same scripts directly when a GUI is not needed.

## Principles

- Do not modify Escape from Tarkov files.
- Use PowerShell scripts for local data collection and CSV parsing.
- Treat unknown user context as `unknown` instead of blocking the workflow.
- Do not optimize settings in this skill; use its JSON output as data for future analysis.
- TODO: on script failures, create an error report artifact and offer to open an error-report form. Form URL is not decided yet.

## Main Files

- `SKILL.md` - agent workflow.
- `scripts/read-eft-settings.ps1` - wrapper around the shared root settings reader.
- `scripts/collect-system-info.ps1` - wrapper around the shared root system info collector.
- `scripts/read-raid-context.ps1` - wrapper around the shared root log context reader.
- `scripts/parse-fps-csv.ps1` - wrapper around the shared root FPS CSV parser.
- `scripts/build-run-json.ps1` - wrapper around the shared root run builder (settings + system + FPS + context + game version).
- `references/benchmark-protocol.md` - user-facing test protocol.
- `references/fps-csv-formats.md` - CSV parsing hints.
- `agents/codex.md` and `agents/CLAUDE.md` - agent-specific notes.

Shared PowerShell utilities live in the repository root `scripts/` folder. The WinForms wizard lives in the repository root `app/` folder and is packaged for standalone distribution by `build/build-release.ps1`.
