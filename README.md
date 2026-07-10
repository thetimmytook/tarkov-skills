# Tarkov Skills

Read-only Escape from Tarkov performance toolkit: agent skills for config analysis, FPS/frametime measurement, repeatable benchmarks, and iterative tuning — plus a standalone benchmark app for players who just want to contribute performance statistics.

Everything is read-only toward the game: no config edits, no gameplay automation, no game process hooks, nothing anti-cheat-adjacent. FPS data comes from external capture tools (PresentMon, CapFrameX, FrameView) via exported CSV files.

## Skills

| Skill | Purpose |
|---|---|
| `skills/tarkov-config` | Read current EFT settings and system context, explain FPS/stability risks |
| `skills/tarkov-frametime` | Capture/parse FPS and frametime statistics (PresentMon or existing CSV) |
| `skills/tarkov-performance-benchmark` | Guide one repeatable benchmark run and produce a normalized `run.json` |
| `skills/tarkov-tuning` | Orchestrate the measure -> change -> measure tuning loop using the skills above |

## Install

### Claude Code (recommended)

```text
/plugin marketplace add thetimmytook/tarkov-skills
/plugin install tarkov-performance@tarkov-skills
```

The plugin ships all four skills together with the shared PowerShell scripts.

### Codex / manual

Download the repository archive from GitHub (Code -> Download ZIP, or a Release) and unpack it. `AGENTS.md` and the per-skill `agents/` notes drive agent behavior. Skills must stay inside the repository tree because they call shared scripts from the root `scripts/` folder.

### Benchmark app (no agent needed)

For contributing FPS statistics without installing any skill: download `TarkovBenchmarkApp.zip` from the GitHub Releases page, unpack, and run `Start-TarkovBenchmark.cmd`. It collects settings/system info, reads raid context from EFT logs, parses your FPS CSV, and builds an anonymized `run.json`.

## Local Data

All local state (goal memory, captures, runs, PresentMon binary) lives in `%LOCALAPPDATA%\TarkovSkills\`, so plugin or repository updates never touch your data.

## Requirements

- Windows 10/11 with PowerShell 5.1+
- Escape from Tarkov installed (for settings/log reading)
- PresentMon (optional, for automated capture) — elevation is required for capture; everything else runs without admin rights

## Repository Layout

- `skills/` — the four agent skills
- `scripts/` — shared PowerShell logic used by all skills and the app
- `app/` — standalone WinForms benchmark wizard
- `references/` — shared cross-skill rules (`measurement-rules.md`)
- `build/` — release packaging script
