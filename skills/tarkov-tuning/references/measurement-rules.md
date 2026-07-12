# Measurement Rules

Local measurement thresholds used by this skill. Keep shared numbers aligned with the other benchmark-capable skills when they change.

## Capture

- Preferred capture duration: 90-180 seconds. Minimum useful duration: 60 seconds.
- Use the same map, similar route, and similar activity when comparing runs.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.

## Metrics To Compare

- average FPS
- 1% low FPS
- 0.1% low FPS
- p95/p99 frametime

Priorities by goal:

- Stability/stutter goals: 1% low and 0.1% low first, average FPS second.
- Maximum FPS goal: average FPS first, but do not accept clearly worse 1% low.
- Better-graphics goal: accept lower average FPS only while it stays above the active target minimum FPS.

## Decision Thresholds

- Differences under 3% are noise unless they repeat across runs.
- Recommend 2-3 repeated runs per profile when results are close.
- If measured performance is at least 15% worse than expected for similar hardware and settings, stop normal graphics tuning and switch to diagnostics (power plan, drivers, storage, pagefile, XMP/EXPO, background apps, thermal throttling checked manually with external tools).
- Do not over-trust a single Tarkov run; server load and raid randomness are real factors.
