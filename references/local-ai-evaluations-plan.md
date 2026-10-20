# Local AI Behavior Evaluations — Deferred Plan

Agreed 2026-10-20. Implement after the current skills publication. This is a
recorded follow-up, not an implementation or a prerequisite for that publication.

## Execution boundary

- Run locally on explicit developer invocation only. Do not add these evaluations
  to GitHub Actions, CI, release workflows or publication gates.
- Keep the evaluation runner and any executable tooling outside distributable
  skill/plugin directories. They are development tools, not runtime dependencies.
- Use synthetic or sanitized reports and mocked tool responses. No live game,
  real capture, config writes or submission is required for these evaluations.
- Keep generated responses, traces, scores, costs and reports outside skill
  bundles and Git by default. Choose models and invocation method when work starts;
  this plan does not authorize API runs or incur charges.

## Initial coverage

Start with approximately 25–30 cases across all four performance skills, including
short multi-turn conversations. Reuse the existing publication review cases.
Prioritize realistic requests and common mistakes from inexpensive models:

- Setting explanations and visual comparisons without unnecessary installation
  or capture; distinguish static images from moving examples.
- Local versus web capabilities, missing Toolkit, and pasted JSON/CSV input.
- Incomplete reports, unknown enums and version-dependent options.
- Comparable and confounded A/B results, correct percentage calculations, noise
  thresholds, and inconsistent repeats such as the Reflex series.
- Conditional forecasts on different hardware, uncertainty and minimal useful
  next steps rather than repeated broad changes or universal presets.
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

Compare inexpensive and midrange models, with a stronger model as a control.
Repeat cases to expose variability; fix the context, skill version and evaluation
settings when comparing runs. A supplied-skill test alone does not establish
that a client will discover or load the right skill: include selection/reference
loading checks where the chosen local harness supports them.

Deliver a local case set, a manually invoked runner, and a report of failures and
reproducibility. Do not claim a model is supported until its results are measured.
