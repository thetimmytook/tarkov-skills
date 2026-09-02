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

Install **Tarkov Performance Benchmark** from Microsoft Store. With Tarkov running in a raid, the app performs a two-minute capture with its bundled PresentMon, reads map context from EFT logs, and stores benchmark history locally in its private Store data folder. Nothing is uploaded automatically; submission remains an explicit user action.

## Local Data

Skill goal memory and captures live in `%LOCALAPPDATA%\TarkovSkills\`, so plugin or repository updates never touch them. The Store benchmark application owns its benchmark history in package `LocalState`; Store updates preserve it, while uninstalling the application removes it.

## Future Raid Planner

Any future interactive map or raid-planning work follows the [raid planner data and asset policy](references/raid-planner-data-policy.md): use original or separately licensed schematic visuals and documented public data sources; do not redistribute EFT assets or publish hidden loot data.

## Requirements

- Windows 10/11 with PowerShell 5.1+
- Escape from Tarkov installed (for settings/log reading)
- PresentMon (optional, for automated FPS capture) — the tools first try without admin rights and show a Windows permission prompt only when it is actually required

## Repository Layout

- `skills/` — the four agent skills
- `scripts/` — shared PowerShell logic vendored into agent skills
- `apps/tarkov-performance-benchmark/` — C# WPF Microsoft Store application
- `references/` — app-level benchmark rules
- `build/` — release packaging script
