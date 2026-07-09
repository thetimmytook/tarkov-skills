# Agent Instructions

This repository contains Codex/agent skills and scripts for Escape from Tarkov performance testing.

## Core Rules

- Keep all interactions with Escape from Tarkov read-only.
- Do not edit game config files unless the user explicitly asks for a separate, non-skill tool that does so.
- Do not require Python for normal skill usage.
- Prefer PowerShell for Windows automation and data processing.
- Avoid admin-only workflows unless there is no practical read-only alternative.
- Do not automate gameplay, input, anti-cheat-adjacent behavior, or game process manipulation.
- Treat FPS capture tools as external sources. Parse their exported CSV files instead of hooking the game.
- TODO: when a `.ps1` script fails, create a small error report artifact and offer to open an upload/report form. The upload URL is intentionally not decided yet.

## Skill Design

- Skills should guide non-technical users step by step.
- Ask simple questions and accept `unknown` when the user is unsure.
- Prefer reading fields from local files/logs first; ask the user only for missing or low-confidence fields.
- Store repeatable logic in `scripts/`.
- Store shared reusable PowerShell logic in the repository root `scripts/` folder and call it from skills instead of duplicating parsing/system-info code.
- Store detailed procedural notes in `references/`.
- Store agent-specific notes in `agents/`, for example `agents/codex.md`, `agents/claude.md`, and later `agents/gemini.md`.
- Every skill directory should include a `README.md` for humans.
- Keep `SKILL.md` focused on the agent workflow.
- Avoid extra docs beyond `README.md` unless they are directly useful to agents.

## Benchmarking Principles

- Prefer repeatable A/B tests over broad preset recommendations.
- Change one setting group at a time.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.
- Record map, mode, weather, time of day, route/activity, and notes. Read map/mode/raid context from EFT logs when available, then ask only for missing context.
- Prefer 90-180 second captures.
- Compare average FPS, 1% low, 0.1% low, and p95/p99 frametime.
- Do not over-trust a single Tarkov run; recommend repeated captures when results are close.

## PowerShell

- Scripts must be safe to run without administrator privileges.
- Scripts should accept explicit paths where practical.
- Scripts should emit JSON for agent consumption.
- Scripts should use `ConvertTo-Json` / `ConvertFrom-Json` and `Import-Csv`.
- If execution policy blocks a script, agents may use inline PowerShell or `-ExecutionPolicy Bypass` for the current process only.

## Git Hygiene

- Do not commit unless the user asks.
- Use commit message format: `feat|fix # AI | UI | BE # Description`.
- Do not rewrite history or discard user changes.
- Keep generated capture data, temporary outputs, and run results out of Git unless the user asks to version examples.
