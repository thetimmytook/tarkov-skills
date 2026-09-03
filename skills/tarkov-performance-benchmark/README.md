# Tarkov Performance Benchmark

Agent instructions for recording a repeatable read-only Tarkov benchmark with settings, non-identifying system context, log-derived map data, and frametime metrics.

## Use

Local agents use the signed **Tarkov Performance Toolkit** through its headless alias:

```text
tarkov-skills.exe status
tarkov-skills.exe capture --duration 120
```

These commands return sanitized machine-readable JSON without opening the Toolkit GUI. Timed capture still requires explicit user consent. The standalone **Tarkov Performance Benchmark** remains available as a focused manual benchmark and submission app.

Web users open the Toolkit **Benchmark** tab and use **Copy results** after saving a run. The button copies only the latest complete run for pasting into the conversation. This skill ships no scripts, never edits game files, and uploads nothing automatically.
