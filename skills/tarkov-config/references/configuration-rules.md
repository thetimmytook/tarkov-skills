# Configuration Rules

These rules guide read-only analysis of Escape from Tarkov FPS/stability settings.

## Goals

- Default to stable 50-60 FPS minimum when realistic.
- If the user chooses a different target, save it in local goal memory and follow it until changed again.
- Valid goal shifts include higher FPS, lower FPS for better quality, fewer stutters, better visibility, or a balanced profile.
- Investigate observed stutters using measured symptoms and resource headroom.
- Follow the active quality/performance goal. For a better-graphics goal, use
  measured headroom to improve one relevant visual group while preserving the
  requested FPS/stability floor and visibility preferences.
- Do not promise perfect frametimes.

## Local Goal Memory

When the user changes the target, persist:

- goal name
- target minimum FPS
- quality preference
- notes if available
- update timestamp

Use the saved goal in future analysis. Do not keep recommending 50-60 FPS if the saved target is different.

## System Expectations

Published [BSG Steam requirements](https://store.steampowered.com/app/3932890/Escape_from_Tarkov/),
checked 2026-10-12. These are compatibility references, not measured FPS
targets for every map/mode. The RAM figures refer to installed physical RAM,
not RAM plus pagefile.

Minimum reference:

- OS: Windows 10
- CPU: AMD Ryzen 5 3600 or similar
- RAM: 16 GB
- GPU: GTX 1660 or similar
- DirectX: 11
- Storage: 80 GB available

Recommended reference:

- OS: Windows 11
- CPU: Intel Core i7-14700F or better
- RAM: 64 GB
- GPU: RTX 4070 or better
- DirectX: 11
- Storage: 80 GB available

If below minimum, explain the limitation without assigning an FPS from the
requirements alone. Meeting the recommended specifications does not guarantee
a particular average or low FPS in every raid.

## Hard Checks

- Separate installed/available physical RAM, occupied physical RAM, system commit/limit, pagefile occupancy and dedicated/shared GPU memory. Whole-system or adapter gauges are not EFT-only allocations.
- Keep an enabled, system-managed pagefile as a starting point when the user has no established sizing requirement; prefer SSD/NVMe with sufficient free space. Diagnose peak commit headroom, dump requirements and symptoms before suggesting a size. Do not require a fixed RAM + pagefile sum or treat pagefile as equivalent physical RAM. [Microsoft pagefile sizing](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows).
- Treat Automatic RAM Cleaner as a separately tested memory-management option. The observations below do not establish a universal capacity threshold or a repeatable FPS benefit; do not enable it solely from a forecast.
- Only Use Physical Cores is a CPU-dependent On/Off candidate; no universal On rule or measured coefficient here.
- Record Screen Mode and preserve the user's current choice. Mode recommendations are deferred pending dedicated tests.
- BSG describes Area Light Instancing as moving part of the lighting calculations from CPU to GPU, with a moderate performance increase (user-supplied game description). Our static daytime Streets first-raid captures showed a small conditional average-FPS signal (+3.75%). Two On runs share one dry Off control; single-pair uncertainty includes zero. No lows, CPU-utilization reduction or memory-saving benefit was demonstrated. Implementation details and the hardware-family enable rule remain unverified; preserve the declared purpose separately from measured gains.
- On Streets, consider the joint Texture Quality / Streets lower texture mode group when memory headroom or repeated stutters are the problem. Average-FPS gains and stutter improvement are not guaranteed; both On lows were worse in this study. Verify locally.

For a full review, use `settings-evidence-guide.md` to distinguish measured
effects, version-specific mechanisms and preferences across all options.

For crashes, later-raid deterioration or little first-raid memory headroom, use
`session-stability.md`. Assess physical RAM, commit and GPU memory separately;
consider lowering memory-heavy settings or a temporary restart schedule before
the user's usual symptom onset. Every five raids is an example to verify, not a
universal safe interval. A restart or RAM Cleaner does not establish a leak fix.

## FPS/Stability Reduction Priority

Use `graphics-response-model.md` and `graphics-response-priors.json` for
goal-dependent priorities, conditional FPS/resource ranges and the next useful
manual test. A single fixed graphics order is not appropriate across CPU/GPU,
memory, static/moving and map differences.

- For memory pressure and stutters, prioritize the joint texture/Streets-mode
  group; compare lows, symptoms, physical RAM, commit and VRAM together.
- For high GPU load, relevant HBAO, Shadows and AA evidence usually offers
  more useful tests than Sharpness or small AF changes.
- On Streets with lower GPU load and no established cap, consider LOD using
  moving evidence for a moving workload. Visibility depends on fog and LOD.
- SSR and Volumetric have GPU-load observations without a reliable FPS effect
  in the tested scenes. Do not label them computationally free.
- Respect visual preferences for AF/AA/PostFX. Untested Cloud Quality, Grass
  Shadows, upscaling and other options may be candidates, but have no numeric
  coefficient in this model. Skip settings already applied.

The points rank a test for the current goal; they are not additive performance
scores. Recommend a useful small group, forecast before measuring, and stop
when the goal is met with acceptable quality/stability.

## Automatic RAM Cleaner

The response model records the completed same-session study: no sustained RAM
reduction in the measured same-raid On window, followed by lower physical RAM
but higher commit in the next measured On raid. Static retail 1.2.0.0.47965
analysis confirms a native EmptyWorkingSet path, but its checkbox/event
linkage, API success/volume and cause of the observed RAM drop remain unknown. No
repeatable FPS/symptom benefit was established. Keep this long-session
evidence separate from first-raid forecasts.

`automatic-ram-cleaner.md` records separate static evidence for local
0.16.9.40087: menu/start events reach an EmptyWorkingSet request; applying
the flag affects the next suitable event subject to backend override.
GC/Unity resource cleanup and the SPT inventory GC patch are separate.
Do not transfer those named events, delay, override or application behavior
to retail: the retail call sites have not been mapped to UI/events.

Individual raw reports and context notes stay private; the portable skill
contains summary provenance only. Do not infer released committed allocations, corrected leakage
or an activation rule such as only after raid / always on ESC. See the aggregate
observations and limitations in `graphics-response-model.md`.


## PostFX

Ask whether the user relies on PostFX for visibility, monitor brightness, or color correction.

- If no/not sure: PostFX OFF is the default troubleshooting start.
- If yes: reduce to a neutral/minimal visibility config instead of blindly disabling.

Assess the specific PostFX values and the player's scene. Keep separate visual
evidence for smoke/bright zones; these performance captures do not establish
a universal visibility effect or a PostFX FPS coefficient.

## Upscaling

Consider supported upscaling when GPU rendering is the limiting work; it can
preserve other image-quality settings, so lowering them all first is unnecessary:

- NVIDIA RTX: try DLSS
- AMD GPU: try FSR
- NVIDIA GTX or unsupported DLSS GPU: try FSR

Start with Quality, then Balanced. Avoid Performance mode unless the system is struggling.

Treat this as a quality preference to test, not a guaranteed gain or mode
ranking. Check moving detail, blur, ghosting and shimmering at the target
resolution. CPU limits and caps can constrain FPS gains; coefficients are unknown.

## Baseline Mismatch Guardrail

The diagnostics threshold is defined in `measurement-rules.md`. First check
baseline comparability: build, hardware, map, AI/server execution context,
resolution/render scale, effective limits, settings, weather/activity and
session history. A transferred provisional prior alone is not a known-good
hardware expectation. If the shortfall remains against a relevant comparable
baseline, stop ordinary graphics tuning and investigate causes without
declaring a hardware fault:

- manual/external thermal throttling check with tools such as HWiNFO or MSI Afterburner; not collected automatically by this skill
- laptop power mode / Windows power plan
- GPU driver issues: report the installed driver version from Windows when available and ask the user to compare it with the current AMD/NVIDIA driver page; automatic online latest-driver verification is a future enhancement
- game on HDD or slow/bad SSD
- pagefile too small or on HDD
- RAM without XMP/EXPO
- background apps, browser tabs, Discord, OBS
- overlays
- antivirus scanning
- corrupted game files
- Windows issues
- PvE/local server load
- Tarkov/server-side factors

## Manual Mode (Screenshots / Pasted Settings)

When local settings files are unavailable (different PC, permissions, unusual install), analyze from user-provided screenshots or pasted settings text:

1. Ask for screenshots of the Graphics and PostFX screens, or the copied settings text.
2. Review the reported options with `settings-evidence-guide.md`, the context checks and the response model; record anything unreadable as `unknown`.
3. Ask the user directly for RAM amount, GPU model, and whether the game is on SSD or HDD, since system collection is also unavailable.
4. Lower confidence one level and say explicitly that the analysis is based on manual input, not files.

## Missing Settings And Script Errors

If settings are missing or key names differ by Tarkov version, continue with `unknown` and lower confidence. If a script fails, create a sanitized error report artifact (no user paths or host names) and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.
