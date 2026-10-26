# Local AI behavior cases

Status: **first local evaluation completed on 2026-10-25**, after the developer's explicit request to run. Both `gpt-6-luna` (`low`) and `gpt-6.1-sol` (`medium`) completed all 36 cases. Six selected simple-model cases received two additional repetitions. Results and transcripts stay outside Git; this is not a model-support certification.

A second explicitly requested evaluation followed the behavior-guidance changes
above checkpoint `eea2523`: the same 36 cases on both profiles, the same 12
selected repeats, and four additional probes of newly observed failures. All 88
executions completed. Prompts, rubrics and fixtures stayed unchanged; the final
adapter matched the first evaluation's focused-repeat adapter. Earlier full
batches had adapter fixes, so before/after results are exploratory rather than
a randomized causal estimate. Raw records and the comparison report remain
outside Git. The original scores were not revised.

On 2026-10-26, checkpoint `d8e306e` preceded a bounded matched A/B/C instruction
experiment. The minimal early-goal-persistence candidate was retained after
screening, followed by the original 36 cases on both profiles and the two new
goal holdouts separately: 112 completed executions in total. The experiment
improved the observed persistence action; it did not eliminate unsupported
numeric forecasts or all explanatory omissions. Scores and transcripts remain
outside Git.

This repository-only set covers all four performance skills. It is independent of CI, publication gates and application tests. Its runner does not write production skill instructions or numerical priors; instruction edits are separate, explicitly authorized development work.

## Files and separation

- `suite.json`: six original case groups plus goal holdouts, model tiers, tool contracts and execution boundaries.
- `cases/*.json`: user prompts, attachments, environment and scripted continuation turns.
- `fixtures.json`: synthetic inputs and mock tool results.
- `CATALOG.md`: operator-only overview of the 36 original scenarios and two goal holdouts, with expected meanings.
- `rubrics/*.json`: evaluator-only expected meanings, permissible alternatives, prohibited behaviour and objective checks.
- `run-plan.json`: selected profiles, smoke/full phases and the completed initial execution scope.
- `scorecard-template.json`: an empty result format, not a measured result.
- `run.mjs` and `rpc-client.mjs`: manually invoked local Codex app-server adapter, with mock tools and restricted skill-reference reads. These development files are not part of a skill/plugin package.

Never include rubrics, expected outcomes, criterion IDs, case titles, case classifications, source-reference hints or the operator's assessment in the tested model's messages. They are operator/evaluator metadata. Feed only the case's user-visible history, prompt, attachments, continuations and the selected skills. Fixture identifiers are resolved to their full content, not shown as answer hints.

The manual report summaries intentionally do not pretend to be full Toolkit serialization examples. They test supported interpretation of user-provided data and mocked collection. CLI status/error fixtures mirror inspected command contracts. Application serialization is covered by separate Core tests.

## Inputs and tools

Use a fresh isolated conversation per case/repetition. Read the same selected skill/package version for every model. Record all loaded entrypoints and references. Never let a model read `rubrics/`, the whole case directory, the live user's settings, Store LocalState, private measurements or the developer's chats.

The `local` tool profile can contain mocked status, inspect, capture, goal and individual manual queries. Expose only the mock tool mappings supplied for that case, plus `skill.read`; unprovided tools are unavailable. A `web` case has no local commands. `skill.read` can read only files within the selected four skill roots. Shell, computer use, file writes, web/network tools and uploads are disabled for the tested model. Model inference itself uses the existing online Codex/ChatGPT service: local orchestration does not mean offline inference.

The tested Codex client can expose built-in `list_mcp_resources` and `list_mcp_resource_templates` discovery. The adapter verifies that the configured native MCP server inventory is empty before every case and records those empty read-only discovery events. This ambient discovery is an adapter limitation, not a Toolkit connection. Any other native tool request aborts the attempt; never fall back to a real host tool.

A tool's response is obtained from its case mapping and `fixtures.json`. A `sequence` mapping returns successive fixtures on successive calls; additional calls are recorded as unplanned. `goal_set` changes only in-memory mocked goal state. Capture is instantaneous or event-driven simulation, with no ETW or real waiting. Stateful fixtures can deliver a user interruption while capture is pending.

Scripted continuations must follow their `deliver_after` condition. Do not send every future turn up front. A missing prerequisite or unexpected host capability is `blocked`/`not_observable`, not evidence that the model passed. Never continue after an unknown fixture or accidentally fall back to a live tool.

For supplied-skill adherence, provide the four SKILL.md entrypoints and allow relevant reference reads. For native skill selection, use a separately isolated client adapter that exposes the normal metadata and the same mocked world. If a client cannot isolate tools or record selection, do not run that mode or claim routing was tested.

## Local execution

The case set remains provider-neutral; the implemented adapter uses an installed Codex CLI and its existing ChatGPT login. The first run used `codex-cli 0.164.0-alpha.11.1`. It did not use an API key or create a hosted evaluation job. It consumes account usage; monetary cost is unavailable, not zero. No model calls happen merely by reading these files.

Example, from the repository root in PowerShell, after explicit developer authorization:

```powershell
node tests/ai-behavior/run.mjs --run --phase=smoke --profiles=gpt-6-luna:low,gpt-6.1-sol:medium --repeats=1 --out=C:/projects/skill-perf-measurements/ai-behavior/runs/NEW-EMPTY-DIRECTORY
```

Use `--phase=full` for all cases (38 since suite 0.2), or `--cases=opt-01,capture-01` for selected cases. `--repeats=1|2|3` controls repetitions; `--codex=...` can select the installed executable. `--run` and an empty `--out` outside the repository are mandatory. The runner refuses to overwrite existing results. It never launches the real game or Toolkit.

For a controlled instruction comparison, `--skill-snapshot=<path>` loads a prior
run's `skill-snapshot.json` instead of the current checkout's skill text. The
snapshot must contain text files within the four allowed skill roots and all
four SKILL.md entrypoints. Invalid paths, traversal and missing entrypoints are
rejected before inference. The manifest records snapshot provenance and actual
content hashes. Tool mocks, prompts and rubrics still come from the selected
suite, so verify their hashes separately when comparing variants.

Suite 0.2 adds two goal-only holdouts: a successful 110-FPS save and a failed
75-FPS save with no retry. The original 36 prompts, rubrics and fixtures are
unchanged; report those 36 and the two new cases separately for historical
comparisons. The 2026-10-26 experiment uses matched three-repeat A/B/C screening
and a full follow-up of the original 36 on both model profiles. All evaluation
artifacts remain private and outside Git.

The adapter starts a fresh app-server process and ephemeral conversation for every case/repetition. A temporary child-only `CODEX_HOME` isolates client configuration and capabilities. Existing login credentials are copied there solely for inference authentication and the copied `auth.json` is removed in cleanup; it is never written to result artifacts. Keep normal credential handling and do not commit authentication material. The operator's parent configuration is not rewritten.

For a new evaluation:

1. Choose exact model/provider identifiers for the simple tier and at least one medium/high control tier; using all three is optional. Use comparable client/tool capabilities and record reasoning settings. Do not silently substitute models. An API-backed model can be used by a local runner only with separate authorization and normal credential handling; never put credentials in this set.
2. Freeze the commit and hashes of the actual skill/reference files, including uncommitted changes, suite hash, client/adapter version, generation settings and context mode. A commit name alone does not identify a dirty checkout.
3. Start with the smoke subset in `run-plan.json`, then the full 36 cases. Three full repetitions remain a possible extended comparison: two tiers would mean 216 full-suite case executions. The initial evaluation instead used one full pass per tier plus selected repeats, sufficient to find regressions but not certify their frequency. Multi-turn cases require more than one message.
4. Store outputs, exact public tool traces, criterion scores, run settings, token usage and known cost outside the repository and distributable packages, for example in a developer-selected sibling measurement directory. Do not store private chain-of-thought or pretend unknown cost/latency is zero.
5. Score objective arithmetic and trace rules first, then the semantic rubric. Grade each turn where a checkpoint is defined. Compare models per category and repetition; report critical failures separately.
6. When a case fails, inspect the response, loaded references and tool trace. Distinguish a skill issue, model issue, missing reference, host limitation or ambiguous rubric. Revise only after review. Do not mark any model supported before its runs exist.

The smoke subset and repetition plan are not a mandate to consume a fixed budget. Stop on adapter/isolation errors and preserve incomplete attempts separately. A timeout is not a semantic failure or a pass. A model that ends without performing a required mock action can be a behavior failure; future scripted checkpoints then remain unobserved. `status: completed` in a raw record means generation finished, not that the case passed.

The first run required adapter repairs for between-conversation timeouts and empty native resource discovery. All attempts were retained; the primary simple-model series combines an initial 23 cases with the remaining 13 from a retry batch. The control series uses a fresh full batch. The actual skill-file hashes matched across every attempt; no production skill changes were made during evaluation. One older execution-question trigger supplied a continuation too broadly; that checkpoint is qualified in the private review and the trigger is corrected for future runs.

## Semantic scoring

Each case defines an expected outcome in ordinary language, not a golden paragraph. Different wording, language-equivalent answers and different well-supported next settings can pass. Count only recommendations made as necessary/immediate actions: mentioning a future conditional DLSS or texture test is not by itself a failure.

A help-only request may be answered without a benchmark. A tuning request without an adequate baseline should prioritize obtaining/reusing one. Do not turn the recent regression into an unconditional “always benchmark before answering” rule.

For each requirement, record `pass`, `fail`, `not_observable` or `not_applicable`, with a short quote or tool-event locator. Use an explicit `needs_review` state for unresolved semantic ambiguity, never a silent pass. A case passes only when all applicable required criteria pass and no prohibited outcome occurs. An unsupported tool-trace criterion stays unobserved. Report critical safety/privacy/readiness failures even if the prose looks correct.

Also record unsupported factual claims, unnecessary questions, proposed and performed capture counts, extra preparation raids, useful progress, and preservation of the user's FPS/quality goal. Do not reward verbosity or exact phrases. Do not require a single arbitrary setting choice when multiple options fit the data.

Manual review is the initial reference. A future model judge must be blind to provider/model identity where practical, must receive the observed response/trace and rubric separately from the tested model, and must be checked against human-reviewed examples. Do not treat an unvalidated judge as ground truth or request private reasoning.

The initial results use primary-agent manual semantic review, not an independent human panel or validated model judge. Deterministic tool-trace assertions supplement that review. Ambiguous criteria stay explicitly unresolved; do not rewrite the predeclared rubrics after seeing answers to improve scores. Exact counts of proposed captures or unnecessary questions require separate review and must remain unknown when not reliably assessed.

Design reference: [Evaluation best practices](https://developers.openai.com/api/docs/guides/evaluation-best-practices). Execution remains explicitly invoked local development work, with no CI integration.
