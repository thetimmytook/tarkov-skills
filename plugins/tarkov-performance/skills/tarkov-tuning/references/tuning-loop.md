# Tuning Loop

Numeric thresholds (noise, diagnostics trigger, repeat-run guidance) are defined in `measurement-rules.md`.

## State To Track

- active goal from `tarkov-config`
- baseline result
- workload/session match, resource headroom and cap uncertainty
- preparation state when known; no dedicated warmup required for normal tuning
- raids/time since restart and memory progression when symptoms or headroom warrant it
- last manual change recommended
- frozen forecast with empirical row IDs, center/range, unknowns and couplings
- latest result
- forecast residuals and context-specific local calibration (private)
- interesting model-feedback case and whether an optional report was offered/declined
- verdict
- next change

## Verdict Rules

For memory pressure or session-dependent symptoms, first apply
`session-stability.md`. A first-raid FPS result alone cannot establish a safe
long-session profile; leave practical memory headroom or offer a provisional
restart workaround based on the user's deterioration pattern.

Keep:

- average FPS improves meaningfully and 1% low does not worsen;
- 1% low improves for stability goals;
- better-graphics goal remains above target FPS and user confirms quality improved.

Revert:

- 1% low or 0.1% low worsens meaningfully;
- stutters become more noticeable;
- FPS drops below the active target without quality/visibility benefit.

Repeat:

- result delta is below the noise threshold and resolving it matters to the decision;
- preparation/weather/session differences could change the recommendation;
- capture duration was too short;
- user reports unusual background load or server weirdness.

Switch to diagnostics:

- current settings and hardware should be good enough;
- measured performance still misses the diagnostics threshold versus a relevant baseline;
- stutters/freezes point to RAM/pagefile/storage/driver/background problems, or to thermal throttling verified manually with external tools such as HWiNFO or MSI Afterburner.

## Change Batch Examples

Use `graphics-response-model.md` to choose the next change for the goal and
workload. For example, test the joint texture/Streets-mode group for memory
headroom, HBAO/Shadows/AA for a relevant high-GPU-load scene, or moving LOD
on Streets when the evidence fits. Do not apply a broad preset before learning
which problem the user actually has. Untested options have unknown numeric gains.

A requested 4–5-setting validation uses one frozen bundle forecast. Preserve
automatic couplings and interactions; one bundle result cannot estimate each
member's coefficient. For first-raid validation, restart before each condition
and use only its first raid with matched workload and preparation. Deliberate
warmup is optional for that experiment and never a requirement for normal tuning.

Stop when the goal is met without unacceptable quality or stability loss.
Repeat close/noisy results only to resolve a useful decision or support an
impact claim. A miss against a provisional forecast first triggers a model,
settings and context check, not an unsupported hardware-fault conclusion.

Report the manual diff, predicted range, observed changes, resource scope,
lows/symptoms, verdict and next step. Announce capture completion before this
analysis. Keep raw results and calibration outside the repository/plugin.

For a useful deviation or coverage gap, apply `model-feedback.md` after the
comparability check: proactively draft the optional issue and offer a reviewed
package or selected/full redacted conversation export for manual user sharing.
Do not submit it, demand extra runs for research or keep asking after a decline.
