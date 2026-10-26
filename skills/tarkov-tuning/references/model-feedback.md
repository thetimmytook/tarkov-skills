# Model Feedback and User-Submitted Issues

Use this reference after comparing a frozen forecast with actual measurements,
or when local evidence exposes a useful gap in the model. Take initiative: identify
why the case is useful, draft a ready-to-copy issue, and offer the user a small
report package. Reporting is optional and must not delay helping the user tune.

Destination: [new GitHub issue](https://github.com/thetimmytook/tarkov-skills/issues/new).
The user creates the issue and attaches files manually. Never submit an issue,
upload files, send a chat or call a reporting API on the user's behalf in this
workflow. GitHub issues are public; show the prepared material for user review.

The response should contain the filled issue title/body and the manual submission
link. Describing how to draft an issue does not supply that deliverable. An inline
draft is enough when file/archive tools are unavailable; do not claim a ZIP or
full chat export was created. Use known evidence and `unknown` for missing fields.

## Identify a useful case

First check the applied setting diff and automatic couplings, effective-cap
uncertainty, capture validity, build/map/mode, workload, weather, preparation and
session history. Preserve original context and later corrections. Use existing
reports; do not require extra raids or repeats solely to obtain a report.

Flag a candidate when one of these applies:

- A comparable measured result falls outside the frozen planning envelope. It
  becomes stronger evidence when the response repeats after the checks above.
- A setting response reverses the locally predicted direction by more than the
  noise/repeat variation in `measurement-rules.md`, or an important memory,
  frametime or symptom pattern contradicts the supported local expectation.
- A reproducible hardware/workload interaction or an unmeasured setting/state
  changes the practical recommendation and would improve model coverage.
- A severe or unusual single event has useful evidence but cannot reasonably be
  repeated, such as a crash. Label it preliminary; an interrupted capture is not
  a valid FPS result and does not establish the crash-time memory peak.

A first forecast miss on another PC is expected transfer uncertainty, not by
itself an atypical case. Different hardware can still provide valuable coverage
when its actual response is documented. Do not treat a provisional prediction as
an established hardware expectation or reuse the 15% diagnostics trigger as an
automatic model-bug threshold.

If the frozen full-bundle range was unknown, call it a coverage gap or a miss of
the explicitly conditional working point, not an out-of-range result. A changed
weather/preparation/session state can justify a qualified report but not a
confirmed setting effect. Do not invent a universal anomaly percentage or a
statistical confidence level for the heuristic envelope.

Compare like quantities and units: FPS with FPS, resource means with means and
peaks with peaks. Where defined, report signed center residual
`100 * (observed_FPS / predicted_FPS - 1)` and the observed before/after FPS
effect separately. For memory use signed GiB deltas and residuals; preserve
whole-system/whole-adapter scope. Never divide by a near-zero predicted change
to claim sensitivity. Do not forecast lows from average FPS.

## Offer help and prepare the report

After normal result analysis, briefly explain the specific reason for interest
and its qualification. For example, adapt this to the evidence and user's language:

> This case could help improve the model: [specific observation and limitation].
> I have drafted an issue below. If you want to share it, open
> [a new issue](https://github.com/thetimmytook/tarkov-skills/issues/new), paste the
> text and attach the reviewed report. A few relevant measurements/excerpts are
> enough; a redacted full conversation export is optional.

Fill the template below from available evidence before making the offer. Accept
unknown; do not ask the user to retype data already available. State whether the
case is preliminary, repeated, context-qualified or a coverage gap. Include
helpful unexpected gains as well as losses. Track that the offer was made in the
conversation/private notes; do not repeat it after a decline or for the same case
without materially new evidence.

Prefer a small package containing:

- `issue.md`: filled title/body, measurement comparison and remaining uncertainty.
- `baseline.json` and `candidate.json`: relevant sanitized Toolkit outputs or
  user-supplied exports, including relevant repeats and contrary results.
- `forecast.md`: the frozen prediction, model version, empirical row IDs,
  assumptions, bounds/unknown components and any local calibration used.
- `context.md`: the setting diff/couplings, session/preparation/weather details,
  symptoms, context corrections and checks not already clear in the issue.
- Optional `conversation.md`: selected relevant excerpts or a redacted full
  export, according to the user's choice. Selective sharing must retain known
  confounders and contrary results rather than improve the apparent fit.

Only include available, relevant evidence; these filenames do not require new
measurements, a new report schema or empty files. A minimal inline issue without
an archive is also useful. Use file/archive tools available in the client to
prepare a ZIP in its private artifact/output location outside the skill/plugin
tree and Store-owned state. Show the contents and a link to the artifact. If
those tools are unavailable, give the filled text and the short attachment list;
do not require scripts, extra software or claim an archive was created.

Use only outputs already provided through Toolkit commands, the conversation or
explicit user exports. Do not read Store history files directly, scrape a client
database, harvest other chats or imply that a web client can access local files.
Offer a full-chat option only when the user chooses it and the client exposes an
export or the user supplies one. State any missing history; a reconstruction or
summary is not a complete export.

## Review the sharing boundary

Before presenting a shareable file, remove credentials/tokens, account/contact
details, user/host names, user-specific paths, IP addresses, serial numbers,
machine identifiers and unrelated personal content from text, filenames and
attachments. A sanitized Toolkit output does not make an accompanying full chat
or raw log sanitized. Preserve technical meaning, metrics and uncertainty; use
neutral aliases for redactions and retain untouched originals privately.

Do not bundle game binaries, full installation/configuration folders, unreviewed
logs, process dumps or the entire measurement directory. If an attachment cannot
be reviewed/redacted, omit it and describe the missing evidence. Provide only a
small reviewed package for the user to upload. This does not modify global
priors, publish calibration or automatically send benchmark data to the author.
Application capture/save failures keep the existing Crash-form workflow;
model feedback is a separate optional issue.

## Ready-to-copy issue template

Replace placeholders with known values or `unknown`. Keep the final body concise
and use the user's language when helpful. Remove inapplicable fields.

```markdown
Title: [Model feedback] <setting/group, map and notable response>

## Why this case may help
<Observation; preliminary/repeated/context-qualified/coverage-gap status.>

## Environment and comparison
- Model / skill-package version: <known versions>
- EFT build; CPU; GPU / VRAM; RAM; OS / driver: <non-identifying details>
- Resolution / render scale / upscaling; cap / Reflex state: <values>
- Map / mode / AI; time of day / rain / fog: <context or unknown>
- Preparation; raids/time since restart; workload match: <context or unknown>
- Manual setting diff and automatic couplings: <before → after>

## Frozen forecast and measured result
- Baseline/candidate and repeat aliases: <A1, B1, ...; shared controls noted>
- Predicted candidate average FPS: <center, range or unknown; assumptions>
- Measured average FPS: <baseline → candidate; observed effect and residual>
- Measured lows / frametimes / symptoms: <relevant observations>
- Physical RAM / used commit and limit / dedicated/shared GPU memory:
  <before → after; units, means/peaks, headroom and system/adapter scope>
- Model rows/local calibration used; unsupported components: <IDs or unknown>

## Checks and remaining uncertainty
<Applied settings/caps/context checks, repeats, contrary results and corrections.
Separate suspected explanations from established findings.>

## Attachments
<Reviewed files; selected excerpts/full redacted export only if chosen.
Mention unavailable evidence.>
```
