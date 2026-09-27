# Tuning Loop

Numeric thresholds (noise, diagnostics trigger, repeat-run guidance) are defined in `measurement-rules.md`.

## State To Track

- active goal from `tarkov-config`
- baseline result
- last manual change recommended
- latest result
- verdict
- next change

## Verdict Rules

Keep:

- average FPS improves meaningfully and 1% low does not worsen;
- 1% low improves for stability goals;
- better-graphics goal remains above target FPS and user confirms quality improved.

Revert:

- 1% low or 0.1% low worsens meaningfully;
- stutters become more noticeable;
- FPS drops below the active target without quality/visibility benefit.

Repeat:

- result delta is below the noise threshold;
- scenario changed too much;
- capture duration was too short;
- user reports unusual background load or server weirdness.

Switch to diagnostics:

- current settings and hardware should be good enough;
- measured performance still misses the diagnostics threshold versus a relevant baseline;
- stutters/freezes point to RAM/pagefile/storage/driver/background problems, or to thermal throttling verified manually with external tools such as HWiNFO or MSI Afterburner.

## Change Batch Examples

For more FPS/stability:

- disable grass shadows, Z-Blur, chromatic aberrations, high-quality color, noise;
- reduce cloud quality;
- reduce/disable PostFX if the user does not rely on it;
- reduce LOD/visibility only after cheaper extras are handled.

For better graphics:

- raise textures only if RAM/VRAM is sufficient;
- raise shadows after target FPS is stable;
- raise LOD/visibility late;
- avoid sacrificing 1% low for a small visual gain.
