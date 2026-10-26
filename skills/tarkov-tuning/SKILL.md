---
name: tarkov-tuning
description: Orchestrate iterative Escape from Tarkov performance tuning and conditional FPS/resource forecasts from read-only settings and frametime data. Use the signed Toolkit, choose useful manual changes with empirical priors, and adapt to before/after captures with minimal iterations.
---

# Tarkov Tuning

Coordinate `tarkov-config` and `tarkov-frametime`; use `tarkov-performance-benchmark` when full run context matters. Never edit EFT settings automatically.

## Apply An Explicit Goal Change

Before forecasting or selecting a test, persist an explicitly changed FPS/quality
goal with the available Toolkit goal command and check its result. Acknowledging
it in prose does not save it. Use clear supplied values without asking again;
the old report's goal must not override them. Without working persistence, keep
the new goal in the conversation and explain that it was not saved.

## Choose The Next Action

Follow the user's active FPS, stutter or image-quality goal:

| Request and evidence | Immediate action |
| --- | --- |
| Explain settings or a general manual route | Read `references/settings-help.md` and answer without requiring a capture. |
| Tune this PC, with no usable in-raid baseline | Measure the current settings unchanged after readiness; choose a change after the result. |
| A recent comparable baseline exists | Reuse it and choose one useful change for the measured constraint. |
| Goal met and quality/stability accepted | Keep the profile and stop. |

Specifications, saved settings and menu memory readings do not establish the
in-raid limit or quality budget. Before a baseline, settings are conditional
future candidates, not instructions to apply first. If the user chooses
unmeasured manual advice instead, qualify it and continue helping.

For better graphics, spend measured headroom on one visual improvement while
preserving the FPS/stability target. Removing a visual effect is a tradeoff,
not an automatic quality upgrade.

## Explain Before Measuring When Requested

Read `references/settings-help.md` before giving setting explanations or published
comparisons or a requested manual graphics starting route. Help alone needs no
Toolkit or capture. Distinguish stills from moving examples and honor a stills-only
request. For existing test findings, read `references/graphics-response-model.md`
and the relevant entry in `references/settings-evidence-guide.md` before answering;
do not ask for research already included in those files. The optional manual
order preserves gameplay preferences; select changes
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
6. After a fresh ready signal: send a visible **START** message with duration immediately before invoking capture; on return send **COMPLETE — you can stop** before analysis or questions. Use the same duration and similar scenario, following `references/measurement-rules.md`. Follow a requested first-raid policy with a new game launch before each condition; keep across-raid memory studies separate.
7. Compare the result with the frozen forecast and goal, retaining residuals privately. Keep, revert, selectively repeat, or switch to diagnostics using `references/measurement-rules.md`. For an interesting deviation or coverage gap, read `references/model-feedback.md` and present the filled, redacted issue title/body plus `https://github.com/thetimmytook/tarkov-skills/issues/new` in the same response. Leave submission to the user; do not delay tuning or repeat a declined offer. A first transfer miss alone is not atypical. Stop when the goal is met with acceptable quality/stability; do not sweep every setting or fit individual effects from one bundle.

If Toolkit is missing, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`. Do not download, create, or execute unsigned scripts. Transparent manual config review is allowed, and an existing CSV may be interpreted, but automated frametime capture requires Toolkit.

Prioritize low percentiles and symptoms for stability goals and average FPS for maximum-FPS goals. For quality goals, accept lower FPS only while the user's target remains satisfied. Results inside the noise threshold require repeats before claiming a benefit; leave the effect unestablished if no decision needs resolving. A miss of at least 15% against a relevant expectation triggers diagnostics; an unvalidated transferred prior alone is not that expectation.

Use the output format in `references/tuning-loop.md`.
