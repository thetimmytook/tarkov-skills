# Tarkov Skills

Read-only Escape from Tarkov performance tools: agent skills for config analysis, FPS/frametime measurement, repeatable benchmarks, and iterative tuning, plus desktop applications distributed through Microsoft Store and GitHub Releases.

Everything is read-only toward the game: no config edits, gameplay automation, process-memory reads, injection, or overlays. The desktop applications bundle a pinned PresentMon build for ETW capture.

## Install

### Microsoft Store (recommended)

Install [Tarkov Performance Toolkit](https://apps.microsoft.com/detail/9N3L7DZH0K64) for the complete GUI, benchmark workflow, and the `tarkov-skills.exe` command used by local agents.

Install [Tarkov Performance Benchmark](https://apps.microsoft.com/detail/9PJMPQ06JL21) when you only need the standalone two-minute benchmark and submission workflow.

Microsoft Store provides signed packages and automatic updates. Both applications keep results locally and upload nothing without an explicit user action.

Current public Store versions:

- Tarkov Performance Toolkit: `1.0.0.0`
- Tarkov Performance Benchmark: `1.0.3.0`

### GitHub Release

Each [GitHub Release](https://github.com/thetimmytook/tarkov-skills/releases) includes self-contained Windows x64 archives for the Toolkit and standalone Benchmark, an agent-skills archive, and a complete source archive. Extract the selected portable application and run `TarkovPerformanceToolkit.exe` or `TarkovPerformanceBenchmark.exe`.

Portable builds do not require the .NET runtime, but they are not signed by Microsoft Store and do not update automatically.

### Claude Code

```text
/plugin marketplace add thetimmytook/tarkov-skills
/plugin install tarkov-performance@tarkov-skills
```

The plugin ships all four skills. After installation, ask the agent in plain words, for example: "analyze my Tarkov settings", "capture a 2-minute FPS benchmark", or "help me tune Tarkov performance".

### Claude web

Claude users on a supported paid plan can install the same plugin directly from its GitHub marketplace:

1. Open **Customize**, select **Plugins**, and press **+**.
2. Select **Add marketplace**, then **Add from a repository**.
3. Enter `https://github.com/thetimmytook/tarkov-skills` and install `tarkov-performance`.
4. Start a new chat and select a bundled skill through `/` or the **+** menu.

Claude web cannot run the local Toolkit command. Open Tarkov Performance Toolkit on the same PC, use **Copy JSON** on Overview or **Copy results** on Benchmark, and paste the result into the chat. The Toolkit uploads nothing automatically.

### Codex

Add the GitHub marketplace, then install `tarkov-performance` from the plugin browser:

```text
codex plugin marketplace add thetimmytook/tarkov-skills
codex
/plugins
```

After installation, start a new Codex session so the bundled skills are loaded.

### ChatGPT web

Once the plugin is published in OpenAI's shared plugin directory, install `tarkov-performance` from ChatGPT's **Plugins** tab and start a new chat. Type `@` and select the required skill, or describe the task and let ChatGPT select it automatically. The Codex GitHub marketplace command does not install plugins into ChatGPT web.

ChatGPT web cannot run the local Toolkit command directly. Open Tarkov Performance Toolkit on the same PC, collect the required data, and use **Copy JSON** on Overview or **Copy results** on Benchmark. Paste that JSON into the chat with the selected skill. Nothing is uploaded by the Toolkit automatically.

Attaching a `SKILL.md` file to one chat can provide temporary instructions, but ChatGPT treats it as ordinary chat context rather than an installed skill. Use this only for development while the plugin is not yet listed.

The publication checklist is maintained in [references/chatgpt-plugin-publication.md](references/chatgpt-plugin-publication.md).

### Manual and other agents

Download the `tarkov-skills-codex-<version>.zip` archive from a GitHub Release and unpack it, or use a repository clone. This is a fallback for clients without marketplace support and for development. `AGENTS.md` and the per-skill `agents/` notes drive agent behavior. Skills contain instructions only and can be installed independently.

## Build From Source

Requirements:

- Windows 10/11 x64
- Git
- .NET 8 SDK
- PowerShell 5.1 or newer

```powershell
git clone https://github.com/thetimmytook/tarkov-skills.git
Set-Location tarkov-skills
.\build\check-presentmon-dependency.ps1 -SkipUpstreamCheck
dotnet test apps/tarkov-performance-benchmark/TarkovPerformanceBenchmark.sln -c Release
dotnet test apps/tarkov-performance-toolkit/TarkovPerformanceToolkit.sln -c Release
.\build\build-portable.ps1
```

The portable archives are written to `dist/`. The build is self-contained and includes the repository-pinned PresentMon dependency.

## Skills

| Skill | Purpose |
|---|---|
| `skills/tarkov-config` | Read current EFT settings and system context, explain FPS/stability risks |
| `skills/tarkov-frametime` | Capture/parse FPS and frametime statistics (PresentMon or existing CSV) |
| `skills/tarkov-performance-benchmark` | Capture repeatable benchmark runs into one normalized `benchmark.json` |
| `skills/tarkov-tuning` | Orchestrate the measure -> change -> measure tuning loop using the skills above |

## Local Data

Each Store application owns its data in package `LocalState\TarkovSkills`. Store updates preserve it; uninstalling that application removes its package-local data. Portable builds use `%LOCALAPPDATA%\TarkovSkills`.

## Future Raid Planner

Any future interactive map or raid-planning work follows the [raid planner data and asset policy](references/raid-planner-data-policy.md): use original or separately licensed schematic visuals and documented public data sources; do not redistribute EFT assets or publish hidden loot data.

## Requirements

- Windows 10/11
- Escape from Tarkov installed (for settings/log reading)
- Tarkov Performance Toolkit from Microsoft Store for its globally registered `tarkov-skills.exe` execution alias

## Repository Layout

- `skills/` — the four agent skills
- `src/TarkovSkills.Core/` — shared read-only C# collection library
- `src/TarkovBenchmark.Feature/` — shared benchmark workflow and WPF interface hosted by both Store applications
- `apps/tarkov-performance-benchmark/` — C# WPF Microsoft Store application
- `apps/tarkov-performance-toolkit/` — C# WPF and JSON CLI Microsoft Store application
- `references/` — app-level benchmark rules
- `build/` — release packaging script
