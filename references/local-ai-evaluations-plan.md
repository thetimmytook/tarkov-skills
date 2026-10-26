# Local AI Behavior Evaluations

Agreed 2026-10-20 as a follow-up after skills publication. On 2026-10-25 the
developer requested preparation of the case set, initially deferred runs, then
explicitly authorized execution with “zapuskaj testi”.
The [local case set](../tests/ai-behavior/README.md) now contains 36 scenarios,
synthetic fixtures and evaluator-only semantic rubrics. The
[catalog](../tests/ai-behavior/CATALOG.md) lists the expected outcome of each.
The first local evaluation is complete: all 36 cases on `gpt-6-luna` (`low`)
and `gpt-6.1-sol` (`medium`), with two additional repetitions of six selected
simple-model cases. The repository-only adapter uses Codex app-server, supplied
skills and mocked tools. No live game or Toolkit calls occurred. Generated
transcripts, criterion reviews and reports remain in the private measurement
directory outside Git. These results do not establish broad model support.
This work is not a publication prerequisite or CI/release gate.

After explicit developer approval, checkpoint `eea2523` preserved the original
state. A second evaluation exercised the revised action-selection, quality-goal,
reference-reading, capture-signalling and feedback-deliverable guidance: 36 cases
per profile, 12 selected repeats and four additional probes, all completed. Keep
the original and follow-up results separate; prompts/rubrics/fixtures and numerical
priors were unchanged. Selected baseline and signalling checks improved, while
goal persistence and some explanatory omissions remain inconsistent. The local
comparison report records both improvements and newly observed failures; there
is still no broad model-support certification.

On 2026-10-26 the developer authorized a checkpoint and bounded experimentation.
Checkpoint `d8e306e` preserves the earlier improvements. Matched three-repeat
screening compared frozen old instructions (A), a broader candidate (B), and a
minimal early-goal-persistence candidate (C) through the same adapter. C was kept;
B's language/next-test/quality additions were removed. The original 36 cases then
ran on both profiles, with two separately reported goal-only holdouts added in
suite 0.2. All 112 executions completed; numerical priors and the original 36
prompts/rubrics/fixtures were unchanged. Goal persistence improved in these
observations, while unsupported numeric bundle forecasts and some explanation
omissions remain. Detailed scores, comparisons and traces stay outside Git.

Suite 0.3 adds six frozen numerical-forecast cases, for 44 cases in total.
The bounded eligibility experiment retained a metric-specific forecast check;
its screening and original-suite regressions remain separate in the local
report. A subsequent unchanged-snapshot `gpt-6.1-sol:high` run completed all 44
cases. It did not establish a numerical-accuracy advantage over the same-snapshot
medium control or a universal minimum reasoning requirement. These are local
supplied-skill evaluations with mocked tools, not client-discovery tests or
model-support certification. See the case-set README for the completed scopes;
raw outputs, scores and criterion evidence remain outside Git.

## Execution boundary

- Run locally on explicit developer invocation only. Do not add these evaluations
  to GitHub Actions, CI, release workflows or publication gates.
- Keep the evaluation runner and any executable tooling outside distributable
  skill/plugin directories. They are development tools, not runtime dependencies.
- Use synthetic or sanitized reports and mocked tool responses. No live game,
  real capture, config writes or submission is required for these evaluations.
- Keep generated responses, traces, scores, costs and reports outside skill
  bundles and Git by default. Use explicit run authorization; do not infer it from
  this planning document. The first run used the existing ChatGPT login and
  account usage, not an API key; monetary cost was unavailable.

## Initial coverage

The prepared set has 36 cases across all four performance skills, including
short multi-turn conversations and the existing publication-review themes.
Prioritize realistic requests and common mistakes from inexpensive models:

- Setting explanations and visual comparisons without unnecessary installation
  or capture; distinguish static images from moving examples.
- Local versus web capabilities, missing Toolkit, and pasted JSON/CSV input.
- Incomplete reports, unknown enums and version-dependent options.
- Comparable and confounded A/B results, correct percentage calculations, noise
  thresholds, and inconsistent repeats such as the Reflex series.
- Conditional forecasts on different hardware, uncertainty and minimal useful
  next steps rather than repeated broad changes or universal presets.
- Quality improvements with spare performance, reverting blurry upscaling,
  conflicting FPS/quality goals, and stopping once the user's goal is met.
- Baseline-first tuning without in-raid evidence, with counterexamples where
  existing GPU/memory measurements justify DLSS or texture tests. Conditional
  future candidates are allowed; do not force a baseline for help-only requests.
- RAM, resident memory, commit and VRAM distinctions; later-raid deterioration
  and Automatic RAM Cleaner interpretation.
- Readiness versus settings confirmation; stop/cancel instructions; immediate
  completion notice and no capture before a fresh ready signal.
- Reusing accepted/declined choices across skills, respecting the requested goal
  and avoiding unnecessary questions or measurements.
- Read-only boundaries, privacy and manual sharing without automatic upload.

## Case and scoring design

Each case should specify the client context, prompt or conversation turns,
available skill/reference content, input fixtures, mocked tools, required facts
or actions, prohibited claims/actions, and expected next step. Evaluate semantic
requirements and tool traces rather than exact answer wording.

Use deterministic checks for arithmetic, allowed calls, state transitions and
other objective conditions. Review ambiguous language manually or with a checked
rubric; an unverified model judge is not ground truth. Record factual errors,
instruction violations, unnecessary questions and proposed measurement count
separately rather than hiding them inside one average score.

Compare the simplest available model with a midrange or stronger control;
running all three tiers is optional. The first selected profiles are listed above.
Repeat cases to expose variability; fix the context, skill version and evaluation
settings when comparing runs. A supplied-skill test alone does not establish
that a client will discover or load the right skill: include selection/reference
loading checks where the chosen local harness supports them.

The case set and manually invoked runner are in `tests/ai-behavior/`; its README
documents isolation limits and invocation. The first run found repeatable
baseline-ordering failures in the simple model and reference/feedback omissions;
selected repeats also exposed inconsistent start signalling. The control model
omitted one required explanatory point while correcting the three associated
performance claims. Keep case-level evidence and adapter errors separate from
aggregate counts. Review proposed skill changes before applying them.
