# Configuration Rules

These rules guide read-only analysis of Escape from Tarkov FPS/stability settings.

## Goals

- Default to stable 50-60 FPS minimum when realistic.
- If the user chooses a different target, save it in local goal memory and follow it until changed again.
- Valid goal shifts include higher FPS, lower FPS for better quality, fewer stutters, better visibility, or a balanced profile.
- Reduce obvious stutter causes.
- Prioritize stability and practical visibility over pretty graphics.
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

If below minimum, warn that stable 50-60 FPS may be unrealistic.

## Hard Checks

- RAM + pagefile around 64 GB or more is recommended for stability troubleshooting.
- Pagefile should be on SSD/NVMe, not HDD.
- If RAM is 16 GB or lower, recommend Automatic RAM Cleaner ON.
- Only Physical Cores should be ON by default, but allow testing OFF later.
- Screen mode should default to Borderless for this workflow.
- Area Light Instancing should be ON for modern GPUs, especially NVIDIA RTX 20xx+ and AMD RX 6000+.
- Streets lower texture mode should be ON if Streets stutters, target FPS is not met, RAM is limited, or VRAM is limited.

## FPS/Stability Reduction Priority

When the user wants more FPS or stability, suggest manual reductions in this order. Skip any setting that is already disabled/reduced enough in the current config; do not recommend changes the user has already effectively made.

1. Grass shadows OFF
2. Z-Blur OFF
3. Chromatic aberrations OFF
4. High-quality color OFF
5. Noise OFF
6. Cloud Quality reduce
7. PostFX OFF or reduce
8. LOD reduce
9. Visibility / Overall visibility reduce
10. HBAO reduce/OFF
11. Volumetric Lighting reduce
12. Shadow Quality reduce
13. Antialiasing reduce/change
14. Texture Quality reduce if VRAM/RAM limited
15. Anisotropic Filtering reduce
16. DLSS/FSR fallback if still below target

Exceptions:

- Screen mode Borderless
- Area Light Instancing ON for modern GPUs
- Automatic RAM Cleaner ON if RAM <= 16 GB
- Only Physical Cores ON by default

## PostFX

Ask whether the user relies on PostFX for visibility, monitor brightness, or color correction.

- If no/not sure: PostFX OFF is the default troubleshooting start.
- If yes: reduce to a neutral/minimal visibility config instead of blindly disabling.

PostFX can make bright zones too bright and can hurt smoke/bright-zone visibility.

## Upscaling

Use upscaling only after basic reductions do not reach target:

- NVIDIA RTX: try DLSS
- AMD GPU: try FSR
- NVIDIA GTX or unsupported DLSS GPU: try FSR

Start with Quality, then Balanced. Avoid Performance mode unless the system is struggling.

## Baseline Mismatch Guardrail

The diagnostics threshold is defined in `measurement-rules.md`. If hardware, resolution, map, and settings are close to a known good baseline but FPS misses it, stop normal graphics tuning and check:

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
2. Extract the same fields listed in "Hard Checks" and the reduction-priority list; record anything unreadable as `unknown`.
3. Ask the user directly for RAM amount, GPU model, and whether the game is on SSD or HDD, since system collection is also unavailable.
4. Lower confidence one level and say explicitly that the analysis is based on manual input, not files.

## Missing Settings And Script Errors

If settings are missing or key names differ by Tarkov version, continue with `unknown` and lower confidence. If a script fails, create a sanitized error report artifact (no user paths or host names) and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.
