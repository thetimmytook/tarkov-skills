# Community Benchmark Design

Status: agreed product direction; not implemented. This describes both Store hosts of the shared Benchmark feature and a future Cloudflare-hosted comparison service. Backend storage and framework choices remain open.

## User Value

The primary value is to search real Escape from Tarkov results for a chosen CPU, GPU, and RAM capacity, on a chosen map, then inspect the graphics settings and conditions behind each result. A user's own before/after history is a second value. Community results are observed runs, not a guaranteed hardware potential or proof that a particular setting caused an FPS difference.

Search and reading public results do not require an account. A verified email account is required to publish a run or manage one's published runs. Email stays private; a public nickname is optional and is not an identity or ownership key.

## Benchmark Screen

Below the existing latest-result summary, add one section with two real tabs: **Diff** and **Position**. Both tabs share the latest saved, complete local run as their subject. Empty, loading, offline, insufficient-data, and error states must be explicit and must not displace the existing capture and result controls.

### Diff

- Work entirely from package-local history, without a network request, sign-in, or publication.
- Compare the latest run with the previous saved complete run; allow choosing another local run when history exists. Show the two dates and the settings that changed.
- Show absolute and percentage changes for Average FPS and 1% Low; keep 0.1% Low and P95 frametime available in details. Higher FPS and lower frametime have opposite improvement directions.
- Label differing map, BSG servers versus Local, resolution, capture duration, or game version as reduced comparability. Show the measured difference but do not claim the settings caused it. If there is only one run, explain that another run is needed for a local comparison.
- Never delete or upload local history as a side effect of opening Diff.

### Position

- Before the first community-data request, obtain explicit consent for network comparison and explain which fields the query sends. Opening the tab must not publish or upload the full run. No community lookup occurs without that consent.
- Display horizontal bars for comparable published runs, with a concise `CPU · GPU · RAM` label and one primary number: Average FPS. Highlight the user's local run even if it has not been published. Selecting a community bar opens that run's public detail page in the browser.
- Show the active cohort definition, number of distinct contributors and runs, and why a wider or weaker match was used. Keep 1% Low and other metrics in the detail view rather than pretending a single FPS number captures smoothness.
- If there are too few trustworthy matches, show examples without a precise position or percentile. If offline or consent is declined, keep Diff and the local result usable.
- Do not call the highest bar the system's potential or imply a leaderboard reward for extreme or unstable runs.

## Matching And Presentation

- Do not calculate a universal normalized score. Compare raw Average FPS only within an explicitly defined cohort.
- Initial Position comparison uses exact map, BSG servers versus Local, selected in-game screen resolution, exact game build, and canonical CPU/GPU models plus installed RAM capacity class. If no exact runs exist, report no data rather than widening to different hardware or conditions; do not present a rank or percentile. Public search can use any subset of these filters and shows individual runs grouped by CPU, GPU and RAM capacity for browsing.
- The current `system.gpu[].current_resolution` is a Windows display mode and must not supply the game's resolution. Controlled game-UI changes on 2026-09-20 confirmed `graphics.DisplaySettings.Resolution.Width/Height` as the selected game screen resolution: changing Borderless to 1920×1080 changed that saved pair while Windows remained at 2560×1440; Windowed and Fullscreen also updated the active pair. The saved `FullScreenMode` codes were `0` Fullscreen, `1` Borderless and `2` Windowed. This is a configured game value, not a measured internal render size or monitor output mode. Missing/invalid saved values remain unknown and cannot support strict Position.
- Graphics/PostFX settings, render scale, and upscaling mode are not default exact-match requirements: they are part of what the visitor wants to discover. Preserve them for the detail page and optional filters, and distinguish substantially different quality or upscaling settings visibly.
- Route, player activity, weather, time of day, server conditions, and local PvE load can change FPS within the same map. Show known context, never imply those factors were normalized away, and use minimum sample/quality rules before showing distribution statistics.
- Retain multiple runs so a player's settings experiments remain visible. Show individual runs and distinct-contributor/run counts; do not compute distribution statistics or percentiles in the initial contract.
- Preserve optional self-reported hardware tuning class as described below; do not infer it from clocks.

## Public Search And Detail

- Provide a read-only web search for CPU, GPU, RAM capacity, map, resolution, execution type, and game version. Starting from a completed benchmark should prefill known fields; visitors can refine them.
- A public run page shows its Average FPS, 1% Low, 0.1% Low, frametime metrics, date, hardware, graphics settings, map, execution type, game version, duration, context and any relevant quality warnings. Show the author's optional public nickname, never their email or internal account ID.
- Public URLs use a server-issued opaque run ID, not a local path or user identifier. A removed or unpublished run must no longer appear in public search or detail responses.

## Submission And Identity

- Keep Save local. The user must separately choose Submit, review the exact shareable fields, and explicitly consent to public publication. Neither saving a benchmark nor viewing Position publishes it.
- Replace the Google Form handoff with authenticated submission to the backend. Verify email ownership through a sign-in flow; use a stable private account ID for ownership and per-account controls. A nickname is optional, editable, and not necessarily unique.
- The application sends only completed, sanitized run records selected by the user. Do not send raw CSV, settings/log paths, user or host names, IPs, serial numbers, machine IDs, or private account details in the public run payload.
- Mark a local run as published only after a successful server acknowledgement with its public run ID. A timeout or validation failure leaves it locally saved and retryable. Repeated submissions of the same account and local run ID must be idempotent.
- Let an authenticated owner list and delete their published runs. Published measurements are immutable; a nickname can change without changing ownership.
- Email verification limits casual abuse but does not prove that a benchmark is genuine. Keep server-side validation, moderation, and abuse controls independent of authentication.

## Backend Requirements

- Expose a versioned read API for search, cohort summaries, and individual public runs. Anonymous reading is allowed; return only the public field allowlist.
- Expose an authenticated write API for submission and owner management. Verify the account server-side on every write; never trust a claimed email, nickname, local run ID, or client-supplied `submitted` flag as authority.
- Validate schema version, payload size, field types/ranges, completed duration and sample count, known map/execution values, and metric consistency. Reject or quarantine invalid, incomplete, duplicate, or suspicious runs without silently changing their measurements.
- Canonicalize CPU/GPU model names and map/game-version values for search while retaining the original sanitized values for display. Store settings with each run; do not rely on the uploader's current settings for old measurements.
- Enforce per-account submission limits and endpoint-level abuse protection; use Turnstile on sign-in or submission if abuse warrants it. CAPTCHA is not a substitute for email ownership, server validation, or limits. Do not use a public nickname as a rate-limit key.
- Keep private account/email data separate from public benchmark records. Minimize operational logging, define retention and deletion behavior, and update the privacy policy before launch. Never expose private fields through search, detail, or aggregate APIs.
- Provide a stable public detail URL per accepted run and deterministic, documented exact-match rules. Distinguish no exact data, missing required comparison conditions, offline, unauthorized, invalid, duplicate, and rate-limited responses so the app can present honest states.
- Do not assume the current local history or Google Form `submitted` boolean proves server ownership or successful backend publication. Migration from the PoC form is not required for the first backend release.

## Release Acceptance

- Test Diff with zero, one, multiple, and mismatched local runs, including offline use and captures discarded after cancellation, raid exit, or game crash.
- Test Position with consent declined/granted, exact matches, no exact matches, missing required conditions, offline failures, and a local result that was never published. Do not widen to another hardware or game-condition group in the initial contract.
- Test anonymous search and public detail against the privacy allowlist, and verify that clicking a bar opens the intended public run.
- Test email verification, ownership, idempotent retries, duplicate and malformed submissions, rate limiting, removal, and that only a server acknowledgement marks a run published locally.
- Run the shared Benchmark regression cases in both Store hosts before release.
