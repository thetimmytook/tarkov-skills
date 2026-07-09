# Tarkov Config

Instruction and overview for the `tarkov-config` skill.

This skill reads Escape from Tarkov settings from:

```text
%APPDATA%\Battlestate Games\Escape from Tarkov\Settings
```

It analyzes current graphics, PostFX, game settings, and basic Windows system context, then produces a read-only FPS/stability report. It does not change game files.

Use `tarkov-tuning` when you need an iterative tuning loop based on measured results.

The report includes the installed GPU driver version when Windows exposes it. Latest-driver verification is manual for now: compare the reported version with the vendor driver page shown in the report.

## How To Use

From this skill folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analyze-tarkov-fps-config.ps1
```

Optional JSON output:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analyze-tarkov-fps-config.ps1 -JsonOutputPath .\out\config-analysis.json
```

Update and save the local tuning goal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\analyze-tarkov-fps-config.ps1 -Goal better-graphics -TargetFpsMin 45 -QualityPreference "higher quality, acceptable lower FPS" -SaveGoal
```

Saved goals live in `memory/current-goal.json` and are not meant to be committed.

## What It Checks

- screen mode
- texture/shadow/LOD/visibility-style settings
- HBAO, SSR, volumetric/cloud-style settings
- PostFX presence and visibility/performance risk
- grass shadows, Z-Blur, chromatic aberrations, noise, high-quality color
- Automatic RAM Cleaner
- Only Physical Cores
- Area Light Instancing
- Streets lower texture mode
- RAM and pagefile stability risks
- installed GPU driver version and vendor page for manual latest-driver checks

## Main Files

- `SKILL.md` - agent workflow.
- `scripts/analyze-tarkov-fps-config.ps1` - read settings/system info and produce a report.
- `references/configuration-rules.md` - configuration rules and troubleshooting guidance.
- `agents/codex.md` and `agents/claude.md` - agent-specific notes.

Shared PowerShell utilities live in the repository root `scripts/` folder.

## Boundaries

- Read-only by default.
- No automatic config edits.
- No guaranteed FPS promises.
- Manual mode should still work with screenshots/settings text.
- User goals are local memory: when the target changes, save it and use it in later reports.
