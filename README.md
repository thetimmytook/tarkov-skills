# Tarkov Skills

Read-only Escape from Tarkov performance toolkit: agent skills for config analysis, FPS/frametime measurement, repeatable benchmarks, and iterative tuning — plus a standalone benchmark app for players who just want to contribute performance statistics.

Everything is read-only toward the game: no config edits, no gameplay automation, no game process hooks, nothing anti-cheat-adjacent. FPS data comes from external capture tools (PresentMon, CapFrameX, FrameView) via exported CSV files.

## Skills

| Skill | Purpose |
|---|---|
| `skills/tarkov-config` | Read current EFT settings and system context, explain FPS/stability risks |
| `skills/tarkov-frametime` | Capture/parse FPS and frametime statistics (PresentMon or existing CSV) |
| `skills/tarkov-performance-benchmark` | Capture repeatable benchmark runs into one normalized `benchmark.json` |
| `skills/tarkov-tuning` | Orchestrate the measure -> change -> measure tuning loop using the skills above |

## Install

### Claude Code (recommended)

```text
/plugin marketplace add thetimmytook/tarkov-skills
/plugin install tarkov-performance@tarkov-skills
```

The plugin ships all four self-contained skills.

### Codex / manual

Download the repository archive from GitHub (Code -> Download ZIP, or a Release) and unpack it. `AGENTS.md` and the per-skill `agents/` notes drive agent behavior. Each skill keeps its executable dependencies in its own folder and can be installed or copied independently.

### Benchmark app (no agent needed)

For contributing FPS statistics without installing any skill: download `TarkovBenchmarkApp.zip` from the GitHub Releases page, unpack, and run `Start-TarkovBenchmark.cmd`. With Tarkov already running in a raid, the app captures 2 or 4 minutes with PresentMon, reads map context from EFT logs, and appends an anonymized run to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`.

## Local Data

All local state (goal memory, benchmark data, PresentMon binary) lives in `%LOCALAPPDATA%\TarkovSkills\`, so plugin or repository updates never touch your data.

## Requirements

- Windows 10/11 with PowerShell 5.1+
- Escape from Tarkov installed (for settings/log reading)
- PresentMon (optional, for automated capture) — elevation is required for capture; everything else runs without admin rights

## Repository Layout

- `skills/` — the four agent skills
- `scripts/` — PowerShell runtime for the standalone app
- `app/` — standalone WinForms benchmark wizard
- `references/` — app-level benchmark rules
- `build/` — release packaging script
