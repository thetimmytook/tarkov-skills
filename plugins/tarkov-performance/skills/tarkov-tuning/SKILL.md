---
name: tarkov-tuning
description: Orchestrate iterative Escape from Tarkov performance tuning and conditional FPS/resource forecasts from read-only settings and frametime data. Use the signed Toolkit, choose useful manual changes with empirical priors, and adapt to before/after captures with minimal iterations.
---

# Tarkov Tuning

Coordinate `tarkov-config` and `tarkov-frametime`; use `tarkov-performance-benchmark` when full run context matters. Never edit EFT settings automatically.

## Explain Before Measuring When Requested

Use `references/settings-help.md` for setting explanations, relevant published
comparisons or a requested manual graphics starting route. Help alone needs no
Toolkit or capture. Distinguish stills from moving examples and honor a stills-only
request. Its optional manual order preserves gameplay preferences; select changes
for the user's actual constraint during tuning rather than applying the table.

## Optional Setup Before The Baseline

When the user is setting up Tarkov for the first time or requests beginner Game settings,
offer `references/game-starting-profile.md` before the first baseline. Reuse any choice
already handled by `tarkov-config` in the conversation. It is manual usability guidance,
not an FPS change batch; declining it does not block tuning. Skip it for an established
setup and do not repeat it during the loop unless the user asks to revisit it.

## Loop

1. Run `tarkov-skills.exe inspect` and read the saved goal. In a web client, ask the user to use **Overview → Collect report → Copy JSON** and paste it into the conversation.
2. If the user changes the target or quality tradeoff, save it through `tarkov-skills.exe goal set ...`; in web/manual mode, retain it in the conversation.
3. Reuse a recent comparable baseline when available; otherwise measure one with Toolkit after explicit readiness. Normal tuning needs no dedicated warmup or preparation raids; use ordinary gameplay and qualify preparation differences under `references/measurement-rules.md`. In a web client, guide the user through **Benchmark → Start collection → Save benchmark → Copy results** and read the pasted latest-run JSON.
4. Use `references/graphics-response-model.md` and its declarative priors to select one useful manual setting group that is not already applied. Condition on the goal, workload, cap uncertainty and resource headroom; preserve unknowns. For crashes, worsening later-raid stutters or little initial memory headroom, use `references/session-stability.md` before accepting the profile as stable. For a full review or an unmeasured option, consult `references/settings-evidence-guide.md`; do not substitute universal On defaults or preferences for measurements.
5. Freeze a conditional FPS/resource forecast before capture, including its range, assumptions and applicable evidence. On a new system this is a starting hypothesis; its baseline does not establish setting sensitivity. Apply the model's transfer policy and refine from that user's measured response rather than reuse the seed system's local calibration. The user applies the change; verify the saved diff and any coupled values. A requested multi-setting validation is a bundle with separate attribution limits.
6. After a fresh ready signal, capture the same duration and similar scenario. Announce completion immediately before analysis. Follow a requested first-raid policy with a new game launch before each condition; keep across-raid memory studies separate.
7. Compare the result with the frozen forecast and goal, retaining residuals privately. Keep, revert, selectively repeat, or switch to diagnostics using `references/measurement-rules.md`. For an interesting deviation or coverage gap, proactively apply `references/model-feedback.md`: explain why, draft an issue and offer reviewed evidence for the user to submit manually. A first transfer miss alone is not atypical; do not delay tuning or repeat a declined offer. Stop when the goal is met with acceptable quality/stability; do not sweep every setting or fit individual effects from one bundle.

If Toolkit is missing, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`. Do not download, create, or execute unsigned scripts. Transparent manual config review is allowed, and an existing CSV may be interpreted, but automated frametime capture requires Toolkit.

Prioritize low percentiles and symptoms for stability goals and average FPS for maximum-FPS goals. For quality goals, accept lower FPS only while the user's target remains satisfied. Results inside the noise threshold require repeats before claiming a benefit; leave the effect unestablished if no decision needs resolving. A miss of at least 15% against a relevant expectation triggers diagnostics; an unvalidated transferred prior alone is not that expectation.

Use the output format in `references/tuning-loop.md`.
