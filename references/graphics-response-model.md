# Graphics Response Model

Use this reference with `graphics-response-priors.json` when choosing a manual
change or forecasting its effect. Model version 0.1.2 is a provisional decision
policy with empirical seeds, not a trained or validated hardware predictor.
Read only the relevant rows. Raw reports and session calibration remain private;
the portable skill contains aggregate observations and declarative metadata only.

## Scope and interpretation

The seed data come from one i9-9900KF / RTX 2080 8 GiB / 32 GiB RAM system,
2560x1440 native, EFT 1.2.0.0.47965, mostly Practice without AI. Static and moving
Streets scenes and static Factory scenes are separate workloads. Online AI/server
load, other hardware, render scales, builds and scenes require local calibration.
A capture's high confidence tier does not make a prediction transferable.
Use `settings-evidence-guide.md` for per-option recommendation boundaries;
a full review must not turn unmeasured preferences into universal defaults.

On a new user's system, the first forecast is a starting hypothesis and may
miss. A local baseline anchors the FPS/resource level, but does not calibrate
how that system responds to a setting change. Treat a different system as a
transfer until that transition has local evidence, even when map, resolution
and GPU utilization resemble the seed system. Do not carry the seed system's
fitted residuals, accuracy or setting ranking over as local calibration.
Choose one useful change, compare its observed response and refine only the
supported local transition/context. Keep a first pair provisional; repeat only
when uncertainty could change the decision. Success means reaching the user's
goal with few useful measurements, not predicting the first result exactly.

Version 0.1.1 adds four Area Light Instancing captures to the original 81 raw
reports, with three dry captures contributing one shared-control comparison.
The original 17 transitions and forecast policy are unchanged. Keep the frozen
0.1.0 snapshot and its preselected five-setting validation separate; do not
retroactively replace a frozen forecast or redraw that validation bundle.

Version 0.1.2 clarifies normal capture preparation, later-session memory risk
and weather comparability. All 18 numerical transitions remain unchanged;
private compound validation outcomes do not refit individual setting effects.
Use `session-stability.md` for crashes or limited first-raid headroom. Normal
user tuning requires no separate warmup or completed preparation raids.

The 2026-10-24 follow-up below is a separate build 1.2.5.0.48146 screening
series, not an extension or refit of the 85-report, 18-transition seed dataset.
The JSON predictive seeds and their numerical policy parameters are unchanged.

Memory gauges cover the whole system or adapter, not EFT-only allocations.
Pagefile occupancy is not paging I/O; adapter capacity is not the application's
VRAM budget. Resource changes can reflect background activity, residency and
session history. Do not infer a leak, a cleanup mechanism or a required RAM/VRAM
capacity from these figures.

All percentages below describe the stated direction. A 25.69% loss when enabling
HBAO becomes approximately a 34.58% gain when disabling it, not a 25.69% gain.
Arithmetic means of per-run lows are descriptive; they are not pooled percentiles.

## Empirical seeds

These are observed changes, not promised gains or prediction intervals.
The JSON records pair effects, eligibility, priority, units and limitations.

| Manual transition | Workload | Average FPS change | Interpretation |
| --- | --- | ---: | --- |
| HBAO Colored Ultra → Off | Streets, static, GPU near saturation | +34.58% | One dry pair; expensive-level repeat agrees. Rainy Off return qualified separately. Other HBAO levels unmeasured in this seed profile; separate Max Performance screening below is not its interpolation. |
| LOD 4 → 2 | Streets, static | +18.00% | Repeated dry direction; BTR and rainy return excluded. |
| LOD 3 → 2 | Streets, static | +14.60% | Measured node; do not assume equally spaced costs. |
| LOD 4 → 2 | Streets, moving | +8.54% | Repeated direction; keep separate from static. |
| Shadows Ultra → Low | Factory, static, GPU near saturation | +5.64% | Quality also changes ShadowDistance 80 → 30. |
| TAA Low → Off | Factory, static, GPU near saturation | +5.57% | Lows inconsistent; other AA modes unmeasured. |
| Visibility 3000 → 400 | Streets, static, stable fog, LOD 2 | +7.03% | Final 400 in a new raid. 1000 also measured; upper segment small. |
| Visibility 3000 → 400 | Streets, moving, stable fog, LOD 2 | +2.94% | Inside noise; raid/date boundary and tail variability. |
| AF On → Off | Factory, static, GPU near saturation | +2.80% | Small repeated direction; preserve quality preference. |
| Area Light Instancing Off → On | Streets, static, day, first raid, GPU below saturation | +3.75% | Two On captures share one dry Off control. Use single-pair uncertainty including zero; no demonstrated stability or resource saving. |
| SSR Ultra → Off | Streets, static, car | +0.18% | Pair signs disagree. GPU use −7.01 percentage points. |
| Volumetric Ultra → Off | Streets, static, night/flashlight | +0.91% | FPS inside noise; GPU use −12.29 points. Visible source response unconfirmed. |
| Texture High → Medium, Streets mode Off | Streets, moving | +1.23% | FPS not established; weather-qualified memory evidence. |
| Texture Ultra → Low, Streets mode Off | Streets, moving | +2.02% | Average inside noise; repeated memory/tail/symptom pattern. |
| Streets mode Off → On, Texture High | Streets, moving | +1.45% | Average inside noise; both On lows worse in this study. |
| Texture High → Medium, Streets mode On | Streets, moving | +0.87% | Cross-series controls; lower VRAM, higher physical RAM. |
| Sharpness 3 → 0 | Factory, static, GPU near saturation | −0.36% | One pair, no reverse; no useful FPS effect established. |
| Sharpness 1.5 → 0 | Streets, static | −0.04% | One dry pair; level 3 rainy. |

Memory observations, after minus before, GiB:

| Transition | Dedicated VRAM | Physical RAM | Used commit | Forecast use |
| --- | ---: | ---: | ---: | --- |
| Texture High → Medium, Streets mode Off | −0.89 | −2.36 | −3.38 | Conditional memory seed; weather/session qualifications. |
| Texture Ultra → Low, Streets mode Off | −2.19 | −8.30 | −13.51 | Conditional seed; first-raid transfer needs verification. |
| Streets mode Off → On, Texture High | −0.95 | −2.25 | −3.33 | Joint texture group; no guaranteed stutter improvement. |
| Texture High → Medium, Streets mode On | −0.74 | +0.72 | −0.20 | VRAM seed only; no independent RAM/commit estimate. |
| Shadows Ultra → Low, Factory | −0.11 | unknown | unknown | Small VRAM observation; restart/history confounds RAM. |

Ultra/Low texture captures also showed Low mean 1% low +20.39% and 0.1% low
+78.71%; the user reported freezes throughout both Ultra runs. Keep this as
contextual stability evidence, not a numerical low-FPS coefficient or a proven
paging explanation. High/Medium/Low/Ultra comparisons from different series do
not form one controlled four-level experiment. Streets mode does not establish
complete High/Medium equivalence.

BSG's in-game description, supplied by the user for EFT 1.2.0.0, states that
Area Light Instancing moves part of the lighting calculations from CPU to GPU
and moderately improves performance. Preserve this as the declared purpose;
do not describe the whole mechanism as unknown. Implementation details, the
amount of CPU time saved and the interaction with other GPU settings were not
verified. Spare GPU capacity is a plausible condition for benefit, not a
measured rule that predicts a gain from utilization alone.

Area Light Instancing dry captures were Off 90.49 FPS and On 93.61 / 94.15 FPS:
On mean +3.39 FPS, with individual contrasts +3.45% / +4.04%. The On repeat
agrees, but there is no second dry Off control, so these are not two independent
A/B pairs. Apply the single-primary-pair 5-percentage-point planning margin,
including zero. The player's earlier +5–7 FPS observation was not reproduced
in this static scene. On 1% lows were 53.94 / 59.39 versus Off 60.16; 0.1% lows
were 37.05 / 43.48 versus 50.74. No stability benefit or numerical tail forecast
is established. RAM/commit/VRAM changes disagree between contrasts, and CPU
savings did not repeat; no resource coefficient is eligible. Initial Off 73.10
FPS had light rain in the final approximately 20 seconds and is excluded from
the estimate. Its large contrast cannot be assigned to the option or wholly
to rain. Other scenes, movement and hardware remain unmeasured.

Additional evidence that must not become ordinary first-raid coefficients:

- Reflex: Off repeats diverged by 17.23%; pair effects −2.97% and +17.74%.
  Limiter flags changed with Reflex. FPS/resource effect inconclusive; input
  latency unmeasured. Do not classify its computation as zero or equate its
  purpose with Sharpness.
- Five successive raids: RAM +2.03 GiB, commit +3.50 GiB, VRAM −0.30 GiB from
  raid 1 to 5; later Off raid RAM rose again. No fixed growth slope, plateau,
  universal failure-raid forecast or demonstrated leak.
- Automatic RAM Cleaner: same-raid On−Off RAM +0.09 GiB. Next On raid versus
  preceding On RAM −6.02 GiB while commit +0.47 GiB. Static retail 47965
  analysis now confirms a native EmptyWorkingSet path; checkbox/event linkage,
  runtime API success/volume, the cause of the measured drop and repeatable
  FPS/symptom benefit remain unknown. Older local 0.16.9.40087 named menu/start
  events do not establish retail timing; see `automatic-ram-cleaner.md`.
  This adds mechanism evidence, not a first-raid coefficient or a universal
  capacity-based recommendation.
- Volumetric Low repeats drifted about +3.51 GiB RAM / +3.62 GiB commit.
  Rain-qualified and dry restart controls remain diagnostic observations,
  not isolated proof of restart benefit or memory leakage.
- Woods shadows: sunny Low only; Ultra preparation was cloudy and not captured.
  Factory measurements cannot serve as its paired result.
- Environmental shadow changes were later confirmed by the user from saved
  side-by-side images, superseding the initial own-shadow-only impression.
  The agent did not inspect those images.
- Cloud Quality, Grass Shadows, PostFX, upscaling, render scale, physical-core
  affinity and untested levels have no measured numeric coefficient here.

## Separate follow-up screening: 2026-10-24

Ten unique completed 120-second Toolkit captures on EFT **1.2.5.0.48146**, the
same i9-9900KF / RTX 2080 8 GiB / 32 GiB system at 2560x1440 native, supplied
the observations below. The intended scenario was daytime Streets Practice
without AI. Most run context was user-confirmed; qualifications remain explicit.
Some completed runs serve as baselines for more than one transition. Reusing
them avoids redundant captures but does not create independent replicate pairs.
Resources cover the whole system/adapter. These are descriptive observations,
not new forecasting coefficients or bounds for GPU-saturated workloads.

| Comparison | Average FPS observation | Interpretation |
| --- | --- | --- |
| FOV 50 / 75 / 50, static | 82.63 / 91.78 / 82.56; dry 75 about +11.1% versus either 50 | A close 50 return supports a local view-specific response. Only one dry 75 run, taken later in the same first raid as a rain-qualified 75 capture; not two independent dry pairs. |
| FOV 50 → 75, moving | 96.46 → 94.97, -1.55% | One matched route pair, inside noise; no gain, penalty or zero cost established. The static response did not transfer to this route. |
| Woods → Server Room background, moving | 94.97 → 95.92, +1.00% | One pair; similar GPU use (73.02% / 73.64%). No FPS recommendation or independent memory effect. |
| HBAO Max Performance → Off, moving | 95.92 → 95.98, +0.06% | GPU use 73.64% → 69.86%, -3.78 percentage points. No repeatable FPS gain or saturated-GPU cost established. |
| High Quality Color Off / On / Off, moving | 95.98 / 90.09 / 91.11 | Off controls drifted -5.07%. The first -6.14% contrast is not an established option cost; On versus the return Off differs by only -1.12%. |

FOV static: an earlier 75 capture at 92.33 FPS had rain start during recording,
with onset time unknown, and is retained separately. The final 50 return was
confirmed dry with the same viewpoint, and its restart/settings were verified;
separate post-run mode/first-raid and daytime/fog/illumination answers were not
supplied. Other FOV runs had their recorded context confirmed. Do not infer a
CPU-thread bottleneck, altered AI simulation or a particular LOD implementation.

Background: the user reported all available choices animated; /CyberTark/ and
Hotline Tarkov were unavailable. Woods saved `WoodEnvironmentUiType`; Server
Room saved `SeasonalBDEnvironmentUiType` after the user selected that UI label.
This is a two-background comparison, not animation On/Off, a test of unavailable
backgrounds or proof that menu rendering/decoding continues in raid. A single
0.1% low contrast is not a repeatable stutter effect; neither run had user-noticed
stutters. Lower physical RAM with nearly equal commit is not a memory-saving
coefficient.

HBAO Max Performance saved `FastestPerformance`. Its current-profile moving pair
must stay separate from the old Colored Ultra/Off static coefficient. GPU load
was not saturated on average; a noise-level FPS effect cannot bound the cost at
saturation. Nor does a GPU-use difference establish an exact per-frame GPU time.

High Quality Color: GPU use was higher in the single On capture (72.06%) than
in either Off (69.86% / 68.09%). These contrasts share one On run. Do not average
drifting Off controls into an asserted -3.7% setting coefficient, claim memory
savings or assign the FPS drift to weather, hardware, preparation or another
unmeasured cause. The cause remains unknown despite confirmed approximate
weather/route matching and no user-noticed stutters.

Across the moving runs, time since loading and preparation were not identical;
preserve that qualification. These findings establish neither a linear response
curve nor a general nonlinear curve for every setting. Intermediate-level and
cross-setting interactions remain unmeasured. No later-build observation here
silently replaces a seed coefficient or becomes a universal percentage.

## Select the next useful change

Use 0–3 priority points separately for FPS and memory; they are ordinal, cannot
be added across settings and do not convert into FPS. Zero means no useful seed
for that goal, not zero computation. Choose according to the active goal:

1. If stutters and little memory headroom coincide, test the joint texture group
   first. Prioritize lows and symptoms; include commit and available RAM.
2. With sustained GPU load near saturation and no evident cap, prefer an applied
   HBAO expensive level, then Shadows or AA as quality preferences allow.
3. With lower GPU load and no evident cap, moving LOD evidence is useful on
   Streets. Low total CPU load cannot exclude a main-thread limit.
4. Visibility has scene/LOD/fog dependence. SSR and Volumetric are GPU-load
   candidates with no established FPS gain in the tested non-saturated scenes.
5. AF and Area Light Instancing are small average-FPS candidates in their
   measured contexts; the latter has no demonstrated low-FPS benefit. Sharpness
   and Reflex are poor choices for chasing memory savings. Untested options
   stay unknown rather than receive guessed gains from their menu names.

Treat sustained roughly 95%+ GPU load as a clue, not proof; caps, VSync, Reflex,
power/clock behavior and other adapter work can change interpretation. Below
roughly 85% leaves more uncertainty; the intervening range is ambiguous. These
are working classification heuristics, not measured bottleneck thresholds.
Prefer a candidate likely to close the goal gap with acceptable quality loss;
break ties using closest workload match, stronger evidence, then lower setup
effort. Skip changes already applied. Do not require testing every setting.

## Numeric forecast eligibility

Apply this check before publishing any **predicted** FPS/resource value. Historic
observations, user targets and arithmetic comparisons of completed runs remain
distinct from forecasts. The check works on supplied artifacts in web/manual
mode as well as local collection; it adds no runtime dependency or capture.

For each requested metric, establish:

| Basis | Required for a numerical forecast |
| --- | --- |
| Baseline | A relevant measured starting value, its run/attachment identity and units/scope. Menu FPS/RAM and a target are not raid baselines. A relative effect can be discussed without inventing an absolute baseline. |
| Response | Eligible transition row IDs and fields, or actual comparable user-local A/B pairs. Read applicable `graphics-response-priors.json` rows: nonzero FPS seeds require `numeric_fps_seed_eligible`; a resource delta requires that metric in `numeric_resource_seed_fields`. Raw values outside those fields are descriptive observations, including inside a subset. Noise-only FPS rows use only the separately allowed contextual zero-effect hypothesis below. |
| Applicability | Exact transition or permitted numeric interpolation, workload/load and cap/session context; distinguish matched local response from the declared transfer hypothesis. A baseline alone does not calibrate sensitivity. |
| Calculation | Trace baseline × ratio or baseline + delta, transfer/combination policy, uncertainty and the settings/metric covered. Briefly show this basis with numerical results; use a compact table only when useful. |

Decide coverage **per metric** before calculating totals:

- A complete path of eligible transitions is sufficient; a directly measured
  start-to-finish row is not required. Evaluate every edge for the same metric.
  For High/Off → High/On → Medium/On, both edges permit VRAM, so their propagated
  VRAM forecast is supported. Only the first edge permits RAM/commit: an
  intermediate High/On estimate is possible, but final Medium/On totals are unknown.
- If any changed component has no eligible effect or defensible bounds for that
  metric, the full-change **center and range are unknown**. Do not invent a floor,
  ceiling or convenient error bar from the baseline, target or expected direction.
  "Rough", "conservative", "provisional" and "assuming favorable effects" do
  not turn missing evidence into numerical bounds.
- A supported component/subset may have its own conditional point/range, explicitly
  labelled with its included changes. State that omitted effects are unquantified;
  do not present the subset as the complete result by assigning missing effects
  zero. A useful next A/B or qualitative direction can proceed with unknown totals.
- Coverage in FPS, GPU utilization, dedicated memory, physical RAM and commit is
  separate. An eligible VRAM path can coexist with unknown full-path RAM/commit.
  Do not infer future lows or CPU/memory effects from average FPS, or saturated-GPU
  FPS from utilization percentage points. Follow the metric-specific rules below.
- When the baseline, response and applicability do support a metric, provide the
  useful conditional calculation. Prefer matching user-local pairs over seed
  coefficients. Incomplete coverage elsewhere or unavailable app access is not a
  reason to refuse every supported number or demand a redundant baseline.

Before sending, remove any predicted number whose baseline, response, calculation
or declared scope cannot be traced. Replace it with **unknown and the reason**,
not a wider guessed interval. This is a forecast check, not a requirement to print
an exhaustive audit or a table of every resource for ordinary qualitative advice.

## Freeze the forecast before capture

Record the baseline identity, full setting diff including automatic couplings,
build/hardware/resolution, workload, cap uncertainty, session policy, applicable
row IDs, forecast center/range, assumptions and unknowns. Save this locally or
retain it in the conversation; never replace it with the measured outcome.

For each eligible measured endpoint transition:

- Let `r = after FPS / before FPS`. Reverse direction uses `1/r`.
- On the seed system with closely matched workload/load, or for a locally
  calibrated transition under comparable conditions: start with the observed
  local ratio. Use paired
  min/max gains expanded by at least 3 percentage points, or 5 for a single
  primary pair, and expand further for repeat drift/context uncertainty.
- Transferred hardware or workload: initially use half the observed log effect as the
  central hypothesis, `r_transfer = exp(0.5 * log(r))`. Include attenuation
  from zero to the full source log effect plus the margin in the planning
  envelope. This 0.5 is a design choice, not a learned coefficient.
- Rows with FPS inside noise use a zero-effect central hypothesis and an
  envelope covering zero and their observed pair changes plus the margin.
  They do not prove zero cost. Reflex has no usable FPS seed.
- A noise-only SSR/Volumetric result cannot bound FPS cost after GPU saturation
  or in a different lighting/reflection workload. Keep that transferred cost
  unknown rather than reuse a small noise interval as its maximum effect.
- For unmeasured numeric levels, optional interpolation in log FPS ratios
  between measured neighbors is explicitly a hypothesis, not a measured response
  curve; bound it by the full neighbor effects plus uncertainty. Equal slider
  steps do not imply equal FPS, frametime, GPU-load or memory increments. Never
  assign equal spacing to HBAO/AA/menu labels. If neither direction nor bounds
  are supported, use unknown and choose a measured endpoint when practical.

When a stepped research profile is requested, start with the endpoints and an
available middle level, then add actual UI steps where they could resolve a
useful threshold or curvature question. Keep each curve's build, profile,
static/moving activity and limiting-work regime separate. Record the absolute
FPS/frametime/resources at each level and a useful return control, rather than
fitting a straight line through two endpoints or pooling drifting controls.
Nominal modes need individual measurements, not arithmetic menu-rank steps.
This research sweep is not a requirement for ordinary minimum-capture tuning.

Convert the relative forecast to `FPS_after = FPS_baseline * r`.
When an unknown component is omitted from a conditional working point, label it
as a supported-subset result and retain the full-bundle center/range as unknown. A few close
compound checks on one system do not establish a universal accuracy band.
Mean frame duration may be estimated as `1000 / FPS_after`; this does not
predict p95/p99 or the low percentiles. Do not numerically forecast 1%/0.1%
lows from average FPS. Record a qualitative stability hypothesis instead.

For resources, use only the eligible JSON fields, with their stated scope.
Expand paired-delta ranges by at least the JSON margin floors, or the largest
same-profile repeat difference when larger. Floors are initial planning
tolerances, not instrument accuracy or confidence limits. Add the delta to
the local baseline; physical quantities stay nonnegative and utilization
within 0–100%. First-raid memory transfers must also include zero until
locally checked. Other resource components remain unknown; do not turn
session drift into an independent CPU/RAM/commit coefficient. Report CPU
utilization as unknown unless locally calibrated. GPU-utilization deltas
from SSR/Volumetric cannot predict GPU render time or FPS at saturation.

These uncertainty margins do not supply a reserve for several more raids.
If a fresh run is already near a memory limit, use `session-stability.md` to
account for later growth, intended session length and crash/stutter evidence.
Prefer lower-memory settings or a provisional symptom-based restart schedule;
do not turn the five-raid observation into a fixed growth slope or safe interval.

No rain/fog or cold-to-warm FPS/resource coefficient is calibrated. Record
available preparation and weather context, including later user qualifications.
Keep completed weather-qualified runs; widen or withhold the affected forecast
and repeat only when it could change the recommendation. Do not attribute a
whole residual to weather or automatically require dry-weather retesting.

## Multiple changes and interactions

Ordinary tuning changes one setting group. A requested multi-setting validation
may change 4–5 settings, with one frozen bundle forecast and a declared exception
to attribution. Use one compatible row per setting; do not add static and moving
LOD, LOD endpoint/subsegment effects, or Shadows and its automatic distance twice.

Texture Quality and Streets mode are one joint group. Prefer directly measured
joint states; for a supported intermediate state use one path through these
states, propagate its uncertainty and label reused/cross-series controls.
For example High/Off → Medium/On uses High/Off → High/On → Medium/On,
not High/Off → Medium/Off plus the independently assumed High-mode saving.
RAM/commit on the second edge remain unknown. Ultra/Low with mode On are unknown.

For independent candidate ratios, a provisional center is
`R = exp(sum(log(r_i)))`, not the sum of percentage gains. Shared bottlenecks,
LOD×Visibility and GPU-pass interactions make that center unvalidated.
The envelope must include both the strongest individual effect and the product
for changes in the same direction, plus uncertainty. With mixed directions,
allow partial response of each effect (`alpha_i` from 0 to 1 in
`exp(sum(alpha_i * log(r_i)))`) so cancellation is not presumed exact.
Honor an actually established cap; a saved FPS value or disabled UI alone does
not establish it. Flag a predicted bottleneck change and widen rather than
extrapolate gains beyond a new limiter.

A bundle has at most one effective new observation for calibration. Do not
credit its full improvement to every member or fit individual coefficients
from it. Do not treat reused AA/AF or texture/Streets baselines as independent
extra observations. Resource envelopes include unsupported bundle components
as unknown; do not silently assign zero to them.
The two Area Light Instancing contrasts also share one Off control and do not
increase the independent baseline count.

## Minimum-capture loop and local learning

1. Inspect settings and reuse a recent genuinely comparable baseline. If none,
   collect one after explicit readiness. Use 120 seconds normally; retain 240
   for a requested long route/memory test. Use available ordinary gameplay;
   no dedicated warmup or preparation raids are required. Qualify preparation
   differences under `measurement-rules.md` instead of forcing extra play.
2. Forecast one promising change, or the requested validation bundle; let the
   user apply it and check the saved diff. If UI and snapshot disagree, retain
   both, obtain the actual applied value and lower confidence.
3. Capture once after a fresh ready signal. Give a clear start cue with duration
   immediately before invoking capture, then announce completion immediately
   before analysis, per `measurement-rules.md`. Ask missing context together,
   accepting unknown.
4. Compare to the frozen forecast and goal. Record observed/predicted ratio,
   resource residuals, in/out of envelope, lows/symptoms, then keep/revert.
5. If the target is comfortably satisfied with acceptable quality and no
   deterioration, stop. A single run remains provisional; do not force a full
   ABBA for an obvious large improvement. Inside-noise benefits need repeats
   before attribution, per `measurement-rules.md`; otherwise leave the
   claimed benefit unestablished and retain the prior known-good profile.
   A requested model-validation experiment still completes its planned
   comparison even when the baseline already meets the playability goal.
6. If close to the goal/decision boundary or repeats disagree, repeat only the
   condition that could change the decision. Use 2–3 runs per profile when
   needed. Weather differences/interference remain preserved and qualified.
7. On a forecast miss, check settings/caps/context first. Prefer testing the
   dominant uncertain group or splitting a failed bundle; do not restart the
   whole setting sweep. Keep session-specific calibration private.

Store an observed log residual `log(FPS_observed / FPS_predicted)` and resource
residuals with context. A bundle residual may adjust that bundle/context's next
forecast or widen its uncertainty; it must not silently alter its members.
An isolated, repeatable single-group result may replace the local prior for
that exact transition. Keep the original prior and frozen predictions.
Never divide by a near-zero predicted change to infer sensitivity.
A miss against an unvalidated prior is a model check; the diagnostics trigger
requires the relevant expectation described in `measurement-rules.md`.

For interesting deviations or coverage gaps after that review, use
`model-feedback.md`. Proactively explain the value of the case and prepare an
optional, reviewed report for manual user submission. Do not treat every first
transfer miss as anomalous, require research captures or upload anything.

For an explicitly requested controlled first-raid validation: restart before
each condition, verify a new launch, use its first raid only and match map/mode,
preparation, workload and capture duration. Deliberate warmup is optional; if
chosen, keep it inside the first raid rather than completing extra raids.
These restarts/preparation steps are not defaults for normal user tuning.
Record first-raid status from
user confirmation when logs cannot establish it. Match weather/light and avoid
a sunny-versus-cloudy shadow comparison. Keep memory progression studies
separate. No capture starts until the user explicitly says ready.
