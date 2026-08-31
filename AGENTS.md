# Agent Instructions

This repository contains agent skills and scripts for Escape from Tarkov performance testing, packaged as a Claude Code plugin and usable by Codex from a repository checkout.

## Repository Layout

- `skills/` - the four agent skills (`tarkov-config`, `tarkov-frametime`, `tarkov-performance-benchmark`, `tarkov-tuning`).
- `scripts/` - master copies of shared PowerShell logic; used directly by the standalone app and vendored into skills.
- Each skill owns its executable PowerShell dependencies under its own `scripts/` folder and its measurement rules under `references/`.
- `references/measurement-rules.md` - master copy of the benchmark rules; skill copies are vendored from it.
- `build/sync-map.json` and `build/sync-skills.ps1` - vendoring manifest and sync script; CI fails when vendored copies drift from their masters.
- `app/` - standalone WinForms benchmark wizard for non-agent users; packaged into a release zip by `build/build-release.ps1`.
- `.claude-plugin/` - Claude Code plugin and marketplace manifests.

## Store Product Direction

- Keep agent skills on GitHub and distribute the benchmark application through Microsoft Store. Do not install or manage Codex, Claude, or other client-specific skills from the Store package.
- The Store product is `Tarkov Performance Benchmark`, package identity `TimmyTook.TarkovPerformanceBenchmark`, Store ID `9PJMPQ06JL21`.
- Build the Store application as a Windows x64 packaged desktop application: C#, .NET 8, WPF, self-contained, full trust, minimum Windows build 19041.
- Place the Store application in `apps/tarkov-performance-benchmark/`. Keep its build, MSIX packaging, resources, and tests isolated so monorepo pipelines can build skills and the application independently.
- Ship no `.ps1` or `.cmd` runtime inside the Store application. Existing PowerShell is prototype/reference logic only; implement production log parsing, settings reads, system collection, PresentMon orchestration, and JSON generation in C#.
- Keep the application single-instance. Expose the stable execution alias `tarkov-benchmark.exe` and support `tarkov-benchmark.exe collect --source skill` for agent-driven collection.
- A skill-initiated collection opens the GUI, still requires an explicit Start action, saves a completed run, briefly shows the result, closes automatically, and returns a machine-readable summary to the caller. Normal Start-menu launches remain open.
- Return the run ID, map, Average FPS, 1% Low, 0.1% Low, P95 frametime, local-save status, and upload status. State explicitly that data was saved locally and was not uploaded unless the user separately consented to upload.
- Keep `%LOCALAPPDATA%\TarkovSkills\benchmark.json` as the shared local data contract. Do not include user-specific paths, user names, host names, IP addresses, serial numbers, or machine GUIDs in benchmark artifacts.
- Never read Tarkov process memory, inject into the game, automate input, or provide an overlay. Limit interaction to process-presence checks, read-only logs/configuration files, Windows system information, and external ETW capture through PresentMon.
- Bundle the tested PresentMon binary in the MSIX. Do not search for or execute user-provided PresentMon copies from portable or shared tool directories.
- Run the main application without elevation. First attempt capture with normal rights; on ETW access denial, offer a one-time elevated `--setup-permissions` flow that adds the current user to `Performance Log Users`. Do not install a custom Windows service in the MVP.
- Build and test MSIX packages in GitHub Actions, but keep Partner Center upload manual until the first Store certification succeeds. Test the first package as a closed Store release before public submission.
- Keep Store artwork original and neutral: frametime/FPS imagery and a dark interface, without EFT logos, Battlestate Games artwork, or other official game assets. Include the unofficial/non-affiliation notice in the application and listing.
- Keep `references/store-product-concept.md` aligned with this section before implementation begins; this section is authoritative when the two conflict.

Persistent local state (goal memory, captures, and runs) lives in `%LOCALAPPDATA%\TarkovSkills\`, never inside the repository, plugin tree, or MSIX installation directory, so application and skill updates cannot destroy user data.

## PresentMon Dependency Management

- Treat PresentMon as an external bundled dependency. Pin an exact tested version; never resolve `latest` during a build or at application runtime.
- Record the upstream release URL, version, SHA-256, license, and copyright notice in the dependency manifest and Store package.
- Update PresentMon only through a deliberate manual change followed by dependency, capture, permissions, cancellation, raid-end, and metric-validation tests on supported Windows 10 and Windows 11 systems.
- Verify the pinned binary's SHA-256 on every relevant local/build check. A mismatch is an error.
- A local pre-commit check may query upstream for a newer PresentMon release no more than once per 24 hours. Cache the result under `.git/`, use a short network timeout, silently continue when offline, and emit only a non-blocking warning when a newer version exists.
- Never replace the pinned binary, modify the manifest, or publish a release automatically in response to the version check.

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
- Shared logic is mastered in the root `scripts/` folder and vendored into skills by `build/sync-skills.ps1` per `build/sync-map.json`. Edit the master and run the sync; never edit vendored copies directly. To let a copy intentionally diverge, remove it from `sync-map.json` in the same change.
- Store detailed procedural notes in `references/` inside the skill.
- Each skill that applies benchmark thresholds keeps its own `references/measurement-rules.md` so it remains portable; the copies are vendored from the root master by the same sync.
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
- Keep commit messages and PR descriptions focused on the change summary. Do not add generic `Verification`/`Testing` sections or command lists unless the user asks for them or a meaningful test limitation needs disclosure.
