# Agent Instructions

This repository contains agent skills and scripts for Escape from Tarkov performance testing, packaged as a Claude Code plugin and usable by Codex from a repository checkout.

## Repository Layout

- `skills/` - the four agent skills (`tarkov-config`, `tarkov-frametime`, `tarkov-performance-benchmark`, `tarkov-tuning`).
- `scripts/` - shared PowerShell logic; skill `scripts/` folders contain thin wrappers around it.
- `references/measurement-rules.md` - single source of truth for measurement thresholds and run-confidence tiers.
- `app/` - standalone WinForms benchmark wizard for non-agent users; packaged into a release zip by `build/build-release.ps1`.
- `.claude-plugin/` - Claude Code plugin and marketplace manifests.

Local state (goal memory, captures, runs, PresentMon binary) lives in `%LOCALAPPDATA%\TarkovSkills\`, never inside the repository or plugin tree, so updates cannot destroy user data.

## Core Rules

- Keep all interactions with Escape from Tarkov read-only.
- Do not edit game config files unless the user explicitly asks for a separate, non-skill tool that does so.
- Do not require Python for normal skill usage.
- Prefer PowerShell for Windows automation and data processing.
- Avoid admin-only workflows. Exception: PresentMon frametime capture needs an elevated session for ETW access; tell the user why before elevating and keep everything else non-admin.
- Do not automate gameplay, input, anti-cheat-adjacent behavior, or game process manipulation.
- Treat FPS capture tools as external sources. Parse their exported CSV files instead of hooking the game.
- Artifacts intended for sharing or upload (such as `run.json`) must not contain user names, host names, or user-specific paths; `Hide-TarkovUserPath` in `scripts/TarkovCommon.ps1` strips them.
- TODO: when a `.ps1` script fails, create a small error report artifact and offer to open an upload/report form. The upload URL is intentionally not decided yet.

## Skill Design

- Skills should guide non-technical users step by step.
- Ask simple questions and accept `unknown` when the user is unsure.
- Prefer reading fields from local files/logs first; ask the user only for missing or low-confidence fields.
- Store repeatable logic in `scripts/`.
- Store shared reusable PowerShell logic in the repository root `scripts/` folder and call it from skills instead of duplicating parsing/system-info code.
- Store detailed procedural notes in `references/` inside the skill.
- Numeric thresholds and comparison rules shared by several skills live only in root `references/measurement-rules.md`; other documents link to it instead of restating the numbers.
- Store agent-specific notes in `agents/`, for example `agents/codex.md`, `agents/CLAUDE.md`, and later `agents/gemini.md`.
- Every skill directory should include a `README.md` for humans.
- Keep `SKILL.md` focused on the agent workflow.
- Avoid extra docs beyond `README.md` unless they are directly useful to agents.

## Benchmarking Principles

- Prefer repeatable A/B tests over broad preset recommendations.
- Change one setting group at a time.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.
- Record map, mode, weather, time of day, route/activity, and notes. Read map/mode/raid context from EFT logs when available, then ask only for missing context.
- Capture durations, noise thresholds, the diagnostics trigger, and confidence tiers are defined in `references/measurement-rules.md`.
- Do not over-trust a single Tarkov run; recommend repeated captures when results are close.

## PowerShell

- Scripts must be safe to run without administrator privileges (PresentMon capture is the documented exception).
- Scripts should accept explicit paths where practical.
- Scripts should emit JSON for agent consumption.
- Scripts should use `ConvertTo-Json` / `ConvertFrom-Json` and `Import-Csv`/`ConvertFrom-Csv`.
- If execution policy blocks a script, agents may use inline PowerShell or `-ExecutionPolicy Bypass` for the current process only.

## Git Hygiene

- Do not commit unless the user asks.
- Use commit message format: `feat|fix # UI | BE # Description`.
- Commit only as the repository author/user configured in Git. Do not add tool names, bot names, generated-by attribution text, or co-author trailers to commit messages, PR text, release notes, or other repository metadata unless the user explicitly asks.
- Do not rewrite history or discard user changes.
- Keep generated capture data, temporary outputs, and run results out of Git unless the user asks to version examples.
