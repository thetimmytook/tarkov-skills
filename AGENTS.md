# Agent Instructions

This repository contains agent skills and scripts for Escape from Tarkov performance testing, packaged as a Claude Code plugin and usable by Codex from a repository checkout.

## Repository Layout

- `skills/` - the four agent skills (`tarkov-config`, `tarkov-frametime`, `tarkov-performance-benchmark`, `tarkov-tuning`).
- `scripts/` - PowerShell runtime used by the standalone app and its release package.
- Each skill owns its executable PowerShell dependencies under its own `scripts/` folder and its measurement rules under `references/`.
- `references/measurement-rules.md` - app-level copy of the benchmark rules.
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
- Artifacts intended for sharing or upload (such as `benchmark.json`) must not contain user names, host names, or user-specific paths; each relevant skill keeps its own `TarkovCommon.ps1` with `Hide-TarkovUserPath`.
- On app capture/save failures, create a short sanitized text report under `%LOCALAPPDATA%\TarkovSkills\reports\`, copy it to the clipboard, and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`. The form should have a required multiline `Crash report` field for the pasted text.

## Skill Design

- Skills should guide non-technical users step by step.
- Ask simple questions and accept `unknown` when the user is unsure.
- Before changing executable scripts, app behavior, data schemas, or skill workflow semantics, first discuss the intended change with the user and wait for explicit approval. Documentation-only clarifications and small typo fixes may proceed directly when they do not change behavior.
- Prefer reading fields from local files/logs first; ask the user only for missing or low-confidence fields.
- Store repeatable logic in `scripts/`.
- Keep each skill portable: scripts it executes must live inside that skill's `scripts/` folder and must not depend on a repository-relative path outside the skill.
- The root `scripts/` folder is reserved for the standalone app/release. When app and skill behavior intentionally match, update their local copies together.
- Store detailed procedural notes in `references/` inside the skill.
- Each skill that applies benchmark thresholds keeps its own `references/measurement-rules.md` so it remains portable.
- Store agent-specific notes in `agents/`, for example `agents/codex.md`, `agents/CLAUDE.md`, and later `agents/gemini.md`.
- Every skill directory should include a `README.md` for humans.
- Keep `SKILL.md` focused on the agent workflow.
- Avoid extra docs beyond `README.md` unless they are directly useful to agents.

## Benchmarking Principles

- Prefer repeatable A/B tests over broad preset recommendations.
- Change one setting group at a time.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.
- Record map from logs, then collect BSG servers versus Local, weather, and time of day for benchmark runs. Do not ask for route/activity in the simplified app flow.
- Capture durations, noise thresholds, the diagnostics trigger, and confidence tiers are defined in the relevant skill's `references/measurement-rules.md`.
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
- When creating or editing a PR body from PowerShell, pass multiline Markdown through a here-string variable (`@' ... '@`), not literal `\n` escapes. Verify the rendered body afterward with `gh pr view <number> --json body --jq .body`.
