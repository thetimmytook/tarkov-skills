# Session Memory and Crash Diagnosis

Use this reference for crashes, freezes, stutters that worsen after several raids,
or a fresh-run profile with little memory headroom. A good first raid does not
establish stability for a long session. Keep this assessment separate from the
graphics model's first-raid effect estimates.

## Check the session, not only the current FPS

Read available Toolkit reports and game logs first. Ask only for missing context:
whether the problem starts immediately or after several raids, approximately how
many raids since launch, whether maps/settings changed, and whether restarting
temporarily restores normal behavior. Accept unknown. Do not require the user to
complete five test raids or a separate warmup before helping.

Compare naturally available early and later reports with unchanged settings when
possible. Keep raid count, elapsed session time, restart history and background
activity with the comparison. Map changes and normal cache/residency changes can
also affect memory. No observed growth pattern alone proves a leak.

| Quantity | Useful assessment | Boundary |
| --- | --- | --- |
| Physical RAM | Occupied/available RAM, peaks and their progression with symptoms. | Whole-system use includes other applications; little available RAM alone does not prove the cause of a freeze. |
| System commit | Used commit, limit, minimum headroom and proximity to failure. | Commit is distinct from resident RAM and pagefile occupancy. Assess the pagefile policy and free disk space; no fixed RAM-plus-pagefile total. |
| Dedicated/shared GPU memory | Adapter usage, capacity/budget if reported, peaks and texture state. | Adapter capacity is not EFT's allocation budget. Shared use alone does not establish harmful paging or a crash cause. |
| FPS/frametimes and symptoms | Lows, spikes, freezes and timing of deterioration. | Average FPS can remain acceptable while memory headroom shrinks. Correlation is not a confirmed failure mechanism. |

For a crash, retain the last successful report and any available sanitized error
or low-memory evidence from read-only game/Windows records. Describe that source
before a manual read when Toolkit does not supply it. Do not report an interrupted
capture as a valid FPS run or infer the crash-time peak from an earlier average.
Keep other plausible crash causes open when the evidence does not establish
memory pressure. Never read process memory, manipulate the game or automate play.

## Leave headroom beyond the first raid

The seed study on one 32 GiB system observed approximately +2.03 GiB occupied
physical RAM and +3.50 GiB used commit from raids 1 to 5; a later Off raid grew
again. These are contextual observations, not a per-raid slope, universal reserve,
plateau, predicted failure raid or demonstrated leak.

When a fresh run is already near a relevant memory limit, explain the risk of
later deterioration even if FPS is currently satisfactory. Prefer a lower-memory
joint Texture Quality / Streets-mode profile, subject to image quality and local
verification. Compare the user's observed later-session peaks/trend with available
RAM and commit headroom separately. Forecast uncertainty margins are not a
stability reserve, and no one reserve fits all hardware/maps/background workloads.

If no later report exists, label that uncertainty and choose practical headroom
instead of certifying the first-raid profile as session-safe. A naturally occurring
later raid can verify the concern; do not demand a long test session by default.
Treat predicted memory savings as provisional and keep the user's goal in view.

## Lower settings or use a temporary restart schedule

- Prefer lowering memory-heavy settings when limited headroom or repeated
  symptoms make the current profile unsuitable for the intended session length.
- If a user wants to keep the image quality, a restart before their usual
  deterioration point can be a temporary workaround. For example, restarting
  every five raids can be offered as a provisional schedule when that suits the
  reported pattern; verify it from later observations. Five is not a universal
  safe interval, a predicted failure boundary or proof of a fix. With little
  initial headroom or earlier symptoms, recommend lower settings or an earlier
  restart rather than waiting for raid five.
- An observed restart benefit does not identify a leak. Do not promise that a
  restart clears every memory pool or resolves the underlying crash cause.
- Automatic RAM Cleaner is not a substitute for headroom or proof of released
  committed allocations. Consult `automatic-ram-cleaner.md`; the retail event
  linkage and repeatable symptom benefit remain unestablished.

Offer one practical next step and a check using normal play. Reserve a dedicated
multi-raid study for an unresolved session-dependent problem or an explicit
research request. Keep raw reports and session calibration outside skill bundles.
