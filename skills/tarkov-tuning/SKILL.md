---
name: tarkov-tuning
description: Orchestrate iterative Escape from Tarkov performance tuning from read-only configuration and measured frametime data. Use the signed Tarkov Performance Toolkit, preserve the user's FPS/quality goal, recommend manual changes, and compare repeatable before/after captures.
---

# Tarkov Tuning

Coordinate `tarkov-config` and `tarkov-frametime`; use `tarkov-performance-benchmark` when full run context matters. Never edit EFT settings automatically.

## Loop

1. Run `tarkov-skills.exe inspect` and read the saved goal. In a web client, ask the user to use **Overview → Collect report → Copy JSON** and paste it into the conversation.
2. If the user changes the target or quality tradeoff, save it through `tarkov-skills.exe goal set ...`; in web/manual mode, retain it in the conversation.
3. Measure a baseline with Toolkit after explicit confirmation. In a web client, guide the user through **Benchmark → Start collection → Save benchmark → Copy results** and read the pasted latest-run JSON.
4. Recommend one small manual setting group that is not already applied.
5. Repeat the same duration and similar scenario.
6. Keep, revert, repeat, or switch to diagnostics using `references/measurement-rules.md`.

If Toolkit is missing, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`. Do not download, create, or execute unsigned scripts. Transparent manual config review is allowed, and an existing CSV may be interpreted, but automated frametime capture requires Toolkit.

Prioritize 1% low for stability goals and average FPS for maximum-FPS goals. For quality goals, accept lower FPS only while the user's target remains satisfied. Results inside the noise threshold require another run; a miss of at least 15% against a relevant expectation triggers diagnostics.

Use the output format in `references/tuning-loop.md`.
