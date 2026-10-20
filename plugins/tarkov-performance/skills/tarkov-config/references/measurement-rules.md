# Measurement Rules

Measurement thresholds shared by the Tarkov skills and the standalone benchmark app.

## Capture

- Automated PresentMon capture through this toolkit is fixed-duration: 120 seconds by default, 240 seconds for longer runs.
- Manual CSV imports from external tools (PresentMon, CapFrameX, FrameView): at least 60 seconds to be useful, 120 or more preferred.
- Use the same map, similar route, and similar activity when comparing runs.
- Prefer the user's problem map. If the user wants a worst-case stress test and has no specific problem map, use Streets.

## Practical Comparability

- Normal user tuning requires no dedicated warmup, repeated preparation route,
  completed preparation raids or restart before every measurement. Use an
  available gameplay segment after loading, menus and an active fight have
  settled enough to capture; do not impose a fixed extra waiting period.
- Record preparation as no deliberate warmup, warmed through ordinary play,
  deliberately warmed, or unknown, when available. Compare similar preparation
  and time since loading where practical. A run without warmup remains useful;
  initial streaming/cache effects lower transfer confidence rather than make
  the result invalid. Do not promise warmed-source lows or use a universal
  cold-to-warm correction. Ask only when this distinction affects the decision.
- A requested controlled first-raid experiment may use matched restarts and
  optional matched warmup. Keep that protocol separate from normal user tuning
  and from a multi-raid memory study.
- Record rain, fog and illumination from user/log context when available;
  accept unknown. A possible weather difference qualifies attribution. Keep
  completed runs and later clarifications instead of silently replacing their
  context or rejecting every weather change. No rain/fog FPS correction is
  calibrated here. Repeat only if uncertainty could change the next action.

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
- Establish that expectation from a comparable baseline: check build, map, AI/server execution context, resolution/render scale, caps, weather/activity and session history. A transferred provisional prediction alone is not an established hardware expectation; first review its assumptions and context.
- Do not over-trust a single Tarkov run; server load and raid randomness are real factors.
- For crashes, worsening stutters or little first-raid memory headroom, assess
  session history and later memory peaks before treating the profile as stable.
  Config/tuning skills assess that session-dependent risk separately; a normal
  benchmark run does not itself establish long-session stability.

## Run Confidence

Canonical tiers, computed by the Store applications and interpreted by the skills:

- `high`: duration is at least 120 seconds and map, weather, and time of day are known.
- `medium`: duration is at least 60 seconds.
- `low`: anything else (short capture, missing map, heavy combat randomness, unclear capture source).

These application tiers do not certify A/B comparability. Report differences in
preparation, weather and session history separately; do not change the tier's
definition or claim the application collected context that the user supplied.
