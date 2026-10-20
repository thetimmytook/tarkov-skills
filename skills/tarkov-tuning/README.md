# Tarkov Tuning

Orchestrator instructions for a measure, change, measure loop. The skill combines config inspection with frametime results, preserves the player's FPS/quality goal, and recommends only manual game-setting changes.

Before a first-time tuning baseline, it can offer the same optional **Game**-tab starting
profile as `tarkov-config`. It is not FPS optimization and is not repeated between runs.

The signed **Tarkov Performance Toolkit** supplies local JSON through `inspect`, `capture`, and `goal` commands. Web users copy the same report from the GUI. No scripts or executables are bundled with this skill.

The [response model](references/graphics-response-model.md) uses aggregate empirical
priors to choose useful tests and forecast conditional FPS/resource ranges. It is
provisional, separates workload and memory effects, and keeps raw captures and local
calibration private. Requested multi-setting validation freezes a bundle prediction
before measurement; it cannot identify each setting's individual contribution.

Use the [settings help](references/settings-help.md) for plain descriptions,
published comparisons and a requested manual starting order; explanations need
no capture. The [settings evidence guide](references/settings-evidence-guide.md)
covers unmeasured candidates and the limits of universal advice.

Normal tuning uses available gameplay without a mandatory warmup. Apply
[session stability](references/session-stability.md) for memory growth, crashes
or little first-raid headroom; a clean run does not guarantee a stable long session.

For interesting model deviations, the skill can proactively prepare an optional
[feedback report](references/model-feedback.md) with measurements and a ready
issue text. Users review and submit it to GitHub themselves; selected excerpts
or a redacted full conversation export are optional.
