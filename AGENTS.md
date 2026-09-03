# Agent Instructions

This repository contains Escape from Tarkov performance skills and signed Microsoft Store applications, packaged as a Claude Code plugin and usable by Codex from a repository checkout.

## Repository Layout

- `skills/` - the four agent skills (`tarkov-config`, `tarkov-frametime`, `tarkov-performance-benchmark`, `tarkov-tuning`).
- `src/TarkovSkills.Core/` - shared read-only C# collectors and JSON contracts used by both Store products.
- `src/TarkovBenchmark.Feature/` - the single shared WPF benchmark workflow and UI hosted by both Store products.
- Skills contain instructions and references only. They do not ship scripts, executables, DLLs, or PresentMon.
- `references/measurement-rules.md` - master copy of the benchmark rules; skill copies are vendored from it.
- `build/sync-map.json` and `build/sync-skills.ps1` - reference vendoring manifest and sync script; CI fails when copies drift from their masters.
- `apps/tarkov-performance-benchmark/` - standalone benchmark/submission Store product.
- `apps/tarkov-performance-toolkit/` - Store companion with a normal WPF GUI and the `tarkov-skills.exe` JSON alias.
- `.claude-plugin/` - Claude Code plugin manifests and the legacy-compatible GitHub marketplace consumed by both Claude Code and Codex.

## Store Product Direction

- Keep agent skills on GitHub and distribute the benchmark application through Microsoft Store. Do not install or manage Codex, Claude, or other client-specific skills from the Store package.
- The Store product is `Tarkov Performance Benchmark`, package identity `TimmyTook.TarkovPerformanceBenchmark`, Store ID `9PJMPQ06JL21`.
- Build the Store application as a Windows x64 packaged desktop application: C#, .NET 8, WPF, self-contained, full trust, minimum Windows build 19041.
- Place the Store application in `apps/tarkov-performance-benchmark/`. Keep its build, MSIX packaging, resources, and tests isolated so monorepo pipelines can build skills and the application independently.
- Ship no `.ps1` or `.cmd` runtime inside either Store application or skill release. PowerShell is repository build tooling only; never distribute or offer it as the user fallback. Production log parsing, settings reads, system collection, PresentMon orchestration, and JSON generation live in C#.
- Keep the application single-instance. Expose the stable execution alias `tarkov-benchmark.exe` and retain `tarkov-benchmark.exe collect --source skill` for compatibility with command-initiated standalone collection.
- A command-initiated standalone collection opens the GUI, still requires an explicit Start action, saves a completed run, briefly shows the result, closes automatically, and returns a machine-readable summary to the caller. Normal Start-menu launches remain open.
- Return the run ID, map, Average FPS, 1% Low, 0.1% Low, P95 frametime, local-save status, and upload status. State explicitly that data was saved locally and was not uploaded unless the user separately consented to upload.
- The Store benchmark application owns its history under the package `LocalState\TarkovSkills\benchmark.json`. External callers interact with it through the stable `tarkov-benchmark.exe` alias and machine-readable command output, not by opening package files directly. Do not include user-specific paths, user names, host names, IP addresses, serial numbers, or machine GUIDs in benchmark artifacts.
- Never read Tarkov process memory, inject into the game, automate input, or provide an overlay. Limit interaction to process-presence checks, read-only logs/configuration files, Windows system information, and external ETW capture through PresentMon.
- Bundle the tested PresentMon binary in the MSIX. Do not search for or execute user-provided PresentMon copies from portable or shared tool directories.
- Run the main application without elevation. First attempt capture with normal rights; on ETW access denial, offer a one-time elevated `--setup-permissions` flow that adds the current user to `Performance Log Users`. Do not install a custom Windows service in the MVP.
- Build and test MSIX packages in GitHub Actions, but keep Partner Center upload manual until the first Store certification succeeds. Test the first package as a closed Store release before public submission.
- Build the Store artifact as an unsigned MSIX. Microsoft Store signs it after certification; do not create, trust, or install local self-signed certificates as part of the normal release check.
- Keep Microsoft Store as the primary application installation source. GitHub Releases also publish self-contained Windows x64 portable archives for both applications, the agent-skills archive, and a complete source archive. State clearly that portable builds are not Microsoft Store-signed, do not auto-update, and do not register Store execution aliases.
- Validate the unsigned package locally through tests, PresentMon checksum verification, `MakeAppx` semantic validation, and package-content inspection. Validate Store installation, execution alias registration, PresentMon execution, package `LocalState` persistence across updates, and machine-readable alias output in a closed Store flight.
- Use package versions with a nonzero first component and a zero fourth component, for example `1.0.0.0`; the fourth component is reserved for Microsoft Store.
- For the first Store submission, use a private audience containing personal Microsoft accounts, not work or school accounts. Publish automatically after certification so the Microsoft-signed package becomes available to that private group; move to a public audience only after the signed package passes the release checks.
- Keep the MVP Store listing in English (United States) only. Add another listing language only after the application UI supports that language.
- Keep Partner Center field choices, privacy text, listing copy, and the `runFullTrust` justification aligned with `references/store-submission.md`. Update that file whenever an accepted submission changes the process or wording.
- Keep Store artwork original and neutral: frametime/FPS imagery and a dark interface, without EFT logos, Battlestate Games artwork, or other official game assets. Include the unofficial/non-affiliation notice in the application and listing.
- Do not ship the default executable icon. Keep original application artwork wired to the WPF window, executable, taskbar, Start menu, installed-app entry, MSIX assets, and Store listing, and verify those surfaces using the Microsoft-signed package.
- Keep `references/store-product-concept.md` aligned with this section before implementation begins; this section is authoritative when the two conflict.

## Performance Toolkit Direction

- The separate Store product is **Tarkov Performance Toolkit**, package identity `TimmyTook.TarkovPerformanceToolkit`, Store ID `9N3L7DZH0K64`.
- Its publisher is `CN=55890398-71D9-4366-AF45-568B3BC3A786`, publisher display name is `TimmyTook`, and PFN is `TimmyTook.TarkovPerformanceToolkit_kzkg4vyj42m5j`.
- Keep normal Start-menu launch in `TarkovPerformanceToolkit.exe` and headless agent commands in the console executable exposed as `tarkov-skills.exe`.
- Support `status`, `inspect`, `capture --duration 120|240`, `goal get`, and `goal set`. Commands emit sanitized JSON to stdout and use nonzero exit codes for failures.
- The GUI provides separate Overview, Benchmark, and Goal sections. The Benchmark section hosts the same shared benchmark feature as the standalone product; Overview provides sanitized report Copy JSON and Save JSON actions for web-client users. Nothing is uploaded automatically.
- Use `TarkovSkills.Core.dll` through project references. Each Store package includes its own copy; do not install a shared/global DLL or create a background service.
- Host `TarkovBenchmark.Feature.dll` in both Store products. Keep benchmark capture, context questions, history display, and submission behavior in that project; product-specific applications remain thin shells and must not fork the benchmark workflow.
- Parameterize shell-specific actions through `BenchmarkFeatureOptions`. Toolkit exposes `Copy results` for web clients and copies only the latest complete run; the standalone Benchmark product keeps that action hidden.
- Bundle and verify the pinned PresentMon dependency in the Toolkit package. Do not discover or run arbitrary user-provided copies.
- Automated local skill collection requires the signed Toolkit. Do not provide PowerShell, CMD, downloaded binary, or generated-script fallbacks.
- Local agents use headless `tarkov-skills.exe` commands and consume stdout JSON without opening Toolkit UI. Reserve GUI collection and Copy JSON/Copy results for web clients or explicit manual use.
- When Toolkit is unavailable, agents may transparently explain and perform individual standard read-only file/system queries. Web users may attach settings, system information, or an existing capture export. This is manual diagnosis, not a software fallback.
- Skills must work in web clients: guide the user to Collect/Capture, Copy JSON, then paste or attach it.
- Run the signed-package checks in `references/toolkit-store-test-cases.md` before making the Toolkit public.

Store-owned goal memory, reports, captures, and benchmark runs live under each package's `LocalState\TarkovSkills\` directory. They never belong inside the repository, plugin tree, or read-only MSIX installation directory. Updates preserve package state; uninstalling a Store package removes its package-local state.

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
- Do not require Python or PowerShell for normal skill usage.
- Avoid admin-only workflows. Attempt PresentMon capture with normal rights. If Windows denies ETW access, return a clear permission status; do not silently elevate or install a service.
- Do not automate gameplay, input, anti-cheat-adjacent behavior, or game process manipulation.
- Treat FPS capture tools as external sources. Parse their exported CSV files instead of hooking the game.
- Artifacts intended for sharing or upload must not contain user names, host names, user-specific paths, IP addresses, serial numbers, or machine identifiers. Enforce this boundary in Core tests.
- On app capture/save failures, create a short sanitized text report under the benchmark application's current data directory (`LocalState\TarkovSkills\reports\` when packaged), copy it to the clipboard, and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`. The form should have a required multiline `Crash report` field for the pasted text.

## Skill Design

- Skills should guide non-technical users step by step.
- Ask simple questions and accept `unknown` when the user is unsure.
- Before changing executable scripts, app behavior, data schemas, or skill workflow semantics, first discuss the intended change with the user and wait for explicit approval. Documentation-only clarifications and small typo fixes may proceed directly when they do not change behavior.
- Prefer reading fields from local files/logs first; ask the user only for missing or low-confidence fields.
- Store repeatable executable logic in `TarkovSkills.Core` and thin Store application shells.
- Keep each skill portable as instructions: it may depend only on stable Store aliases or user-supplied artifacts, not repository-relative executables.
- Shared references are mastered at the repository root and vendored by `build/sync-skills.ps1` per `build/sync-map.json`.
- Store detailed procedural notes in `references/` inside the skill.
- Each skill that applies benchmark thresholds keeps its own `references/measurement-rules.md` so it remains portable; the copies are vendored from the root master by the same sync.
- Store agent-specific notes in `agents/`, for example `agents/codex.md`, `agents/CLAUDE.md`, and later `agents/gemini.md`.
- Every skill directory should include a `README.md` for humans.
- Keep `SKILL.md` focused on the agent workflow.
- Avoid extra docs beyond `README.md` unless they are directly useful to agents.
- Publish the existing Claude-compatible bundle to ChatGPT as a **Skills only** plugin; do not add an MCP server merely to distribute these skills. Keep the reproducible preparation, review, and post-publication process in `references/chatgpt-plugin-publication.md`.
- The ChatGPT web workflow must work from user-pasted or attached Toolkit output and must never claim local computer access. GitHub marketplace installation is for Codex or Claude; public ChatGPT users install the reviewed plugin from OpenAI's universal Plugins Directory.

## Benchmarking Principles

- Prefer repeatable A/B tests over broad preset recommendations.
- Change one setting group at a time.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.
- Record map from logs, then collect BSG servers versus Local, weather, and time of day for benchmark runs. Do not ask for route/activity in the simplified app flow.
- Capture durations, noise thresholds, the diagnostics trigger, and confidence tiers are defined in the relevant skill's `references/measurement-rules.md`.
- Do not over-trust a single Tarkov run; recommend repeated captures when results are close.

## Git Hygiene

- Do not commit unless the user asks.
- Name new branches by the change type and purpose, for example `feat/store-msix`, `fix/raid-detection`, `docs/store-submission`, or `chore/dependency-update`. Do not prefix branches with an agent or tool name.
- Merge pull requests with a merge commit. Do not squash or rebase PRs; preserve branch topology in Git history.
- Use commit message format: `feat|fix # UI | BE # Description`.
- Commit only as the repository author/user configured in Git. Do not add tool names, bot names, generated-by attribution text, or co-author trailers to commit messages, PR text, release notes, or other repository metadata unless the user explicitly asks.
- Do not rewrite history or discard user changes.
- Keep generated capture data, temporary outputs, and run results out of Git unless the user asks to version examples.
- When creating or editing a PR body from PowerShell, pass multiline Markdown through a here-string variable (`@' ... '@`), not literal `\n` escapes. Verify the rendered body afterward with `gh pr view <number> --json body --jq .body`.
- Keep commit messages and PR descriptions focused on the change summary. Do not add generic `Verification`/`Testing` sections or command lists unless the user asks for them or a meaningful test limitation needs disclosure.
