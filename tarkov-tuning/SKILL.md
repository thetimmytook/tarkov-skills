---
name: tarkov-tuning
description: Orchestrate Escape from Tarkov performance tuning using read-only skills. Use when an agent needs to run an iterative tuning loop: read current settings and active goal with tarkov-config, collect FPS/frametime results with tarkov-frametime, optionally assemble full benchmark context with tarkov-performance-benchmark, decide whether a manual change improved or worsened results, and choose the next manual setting change without editing game files.
---

# Tarkov Tuning

Orchestrate the tuning loop. Do not collect raw data directly when a narrower skill can do it.

Use:

- `tarkov-config` to inspect current settings, active goal, and candidate setting changes.
- `tarkov-frametime` to collect FPS/frametime results.
- `tarkov-performance-benchmark` when a full run record needs settings + system + frametime + map/log context.

Core rules:

- Never edit EFT config files automatically.
- Recommend small manual changes, then measure.
- Keep the user's active goal from `tarkov-config` as the target.
- Use measured frametime/FPS results to decide if a change helped.
- If PresentMon capture is needed, treat it as blocking and delegate it to a subagent/separate worker when available.
- If results are at least 15% worse than expected for similar settings/hardware, stop normal graphics tuning and switch to diagnostics.

## Loop

1. Establish or update goal:
   - Use `tarkov-config` local goal memory.
   - Examples: more FPS, fewer stutters, better graphics at 45 FPS, better visibility, balanced.

2. Read current config:
   - Use `tarkov-config`.
   - Identify current risky settings and candidate changes.

3. Measure baseline:
   - Use `tarkov-frametime`.
   - If map/context is important, use `tarkov-performance-benchmark`.

4. Recommend one change batch:
   - Use `tarkov-config` rules.
   - Keep changes small and related.
   - Ask the user to apply them manually.

5. Measure again:
   - Use the same capture duration and similar scenario where possible.

6. Decide:
   - If average FPS and 1% low improve without hurting the user's goal, keep the change.
   - If average FPS improves but 1% low/stutters worsen, treat the change as risky.
   - If quality goal is active, accept lower FPS only if it remains above target and user-visible quality improves.
   - If results are worse, recommend reverting the last manual change.
   - Treat differences under 3% as noise unless repeated. Treat a 15% or larger miss versus a relevant baseline as a diagnostic trigger, not a normal tuning result.

7. Continue or stop:
   - Continue until the active goal is met, results plateau, or diagnostics are more appropriate.

## Comparison Heuristics

- Prioritize 1% low and stutter reduction over average FPS for stability goals.
- Prioritize average FPS only when the user explicitly asks for maximum FPS.
- For better-graphics goals, accept lower average FPS if target minimum FPS remains satisfied.
- Treat changes under 3% as noise unless repeated.
- Prefer 2-3 repeated runs when results are close.

## Output

Use this format:

```text
Current goal:
<goal and target>

Last result:
<avg FPS, 1% low, 0.1% low, notes>

Verdict:
kept / revert / needs repeat / switch to diagnostics

Next manual change:
<one small setting batch>

Why:
<short explanation>

Next measurement:
<what to run again>
```
