# Tarkov Performance Benchmark

Instruction and overview for the `tarkov-performance-benchmark` skill.

This skill records read-only Tarkov performance data. It combines Windows hardware, a current settings snapshot, FPS/frametime metrics, map context from logs, and three short player answers into one local benchmark file.

## How To Use

For a local user, run `app\Start-TarkovBenchmark.cmd` from the repository root, or use the standalone `TarkovBenchmarkApp.zip` release. Enter a raid, choose a 2- or 4-minute capture, and start collection. The app reads settings and system data automatically, then asks only for `BSG servers` or `Local`, weather, and time of day.

Each completed capture is appended to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`. The Upload button copies only runs that were not submitted before and opens the Performance form.

The file contains one top-level `system` object and a `runs` array. Each run keeps its own EFT settings snapshot so later tuning changes remain comparable.

## Requirements

- Windows 10/11 and PowerShell 5.1+
- Tarkov running and already in the raid when capture starts
- PresentMon installed at `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\PresentMon.exe`

PresentMon needs a Windows elevation prompt for its short ETW capture. The app never edits game files.

## Main Files

- `SKILL.md` - agent workflow.
- `scripts/read-eft-settings.ps1` - EFT settings reader.
- `scripts/collect-system-info.ps1` - Windows system info collector.
- `scripts/read-raid-context.ps1` - Tarkov log context reader.
- `scripts/capture-presentmon.ps1` - fixed-duration FPS capture.
- `scripts/add-benchmark-run.ps1` - appends a normalized run to `benchmark.json`.
- `scripts/get-benchmark-upload.ps1` - builds the upload payload from not-yet-submitted runs and tracks `uploaded_run_count`.
- `references/benchmark-protocol.md` - measurement protocol.
- `references/measurement-rules.md` - local benchmark confidence rules.
- `agents/codex.md` and `agents/CLAUDE.md` - agent-specific notes.
