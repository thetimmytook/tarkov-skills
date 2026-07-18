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

The plugin ships all four self-contained skills. After install, just ask the agent in plain words, for example: "analyze my Tarkov settings", "capture a 2-minute FPS benchmark", or "help me tune Tarkov performance".

### Codex / manual

Download the repository archive from GitHub (Code -> Download ZIP, or a Release) and unpack it. `AGENTS.md` and the per-skill `agents/` notes drive agent behavior. Each skill keeps its executable dependencies in its own folder and can be installed or copied independently.

### Benchmark app (no agent needed)

For contributing FPS statistics without installing any skill: download `TarkovBenchmarkApp.zip` from the GitHub Releases page, unpack, and run `Start-TarkovBenchmark.cmd`. With Tarkov already running in a raid, the app captures 2 or 4 minutes with PresentMon, reads map context from EFT logs, and appends an anonymized run to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`. On first run the app shows where to download PresentMon (a free Intel tool) and lets the player install it either under the portable app's `tools\PresentMon\` folder or the shared `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\` folder used by skills. The Upload button copies only runs that were not submitted before.

## Local Data

Persistent local state (goal memory and benchmark data) lives in `%LOCALAPPDATA%\TarkovSkills\`, so plugin or repository updates never touch your data. PresentMon may live there as a shared tool or inside a standalone app's portable `tools\PresentMon\` folder.

## Requirements

- Windows 10/11 with PowerShell 5.1+
- Escape from Tarkov installed (for settings/log reading)
- PresentMon (optional, for automated FPS capture) — the tools first try without admin rights and show a Windows permission prompt only when it is actually required

## Repository Layout

- `skills/` — the four agent skills
- `scripts/` — PowerShell runtime for the standalone app
- `app/` — standalone WinForms benchmark wizard
- `references/` — app-level benchmark rules
- `build/` — release packaging script
