# Tarkov Tuning

Instruction and overview for the `tarkov-tuning` skill.

This is an orchestrator skill. It does not parse configs or collect frametimes directly. It coordinates narrower skills:

- `tarkov-config` - current settings, active goal, and setting-change rules.
- `tarkov-frametime` - FPS and frametime measurement.
- `tarkov-performance-benchmark` - full benchmark run with context when needed.

## Purpose

Use this skill to run an iterative tuning loop:

1. read the user's goal;
2. inspect current config;
3. measure baseline frametime/FPS;
4. suggest a small manual change;
5. measure again;
6. decide whether to keep, revert, repeat, or switch to diagnostics.

## Boundaries

- No automatic config edits.
- No raw PresentMon capture in the main agent when a blocking capture should be delegated.
- No fake precision.
- No guaranteed FPS promises.
- Follow the active goal saved by `tarkov-config`.

## Related Skills

- Use `tarkov-config` when you need to know what setting to change.
- Use `tarkov-frametime` when you need measured performance.
- Use `tarkov-performance-benchmark` when you need a normalized `benchmark.json`.
