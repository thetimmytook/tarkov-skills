# Settings: Evidence and Recommendation Boundaries

Use this guide for the evidence limits in a full settings review or an individual
recommendation. For plain descriptions, visual comparisons and the optional manual
starting route, use `settings-help.md`. It complements `graphics-response-model.md`;
that file and its JSON hold numerical observations and forecasting rules.
Reviewed 2026-10-20; numerical observations unchanged.
Measured effects refer to EFT **1.2.0.0.47965**, one hardware configuration,
mostly Practice without AI. They are starting evidence, not a validated FPS
database for arbitrary hardware, maps or live raids.

## How to describe a recommendation

- Distinguish a measured comparison, a version-specific implementation finding,
  a general documented mechanism and the player's image/usability preference.
- An unmeasured option has unknown cost. A small or noisy FPS difference does
  not prove zero computation, zero memory cost or no effect on stutters.
- Review all reported settings, but propose only useful changes for the goal.
  Keep manual changes and couplings visible; do not turn a full review into a
  requirement to measure every option.
- Physical RAM, resident working set, system commit and pagefile occupancy are
  different quantities. RAM plus pagefile is not installed RAM. Do not derive
  capacity recommendations or a hardware fault from a transferred seed alone.
- For crashes, worsening later-raid stutters or a fresh run near memory limits,
  apply `session-stability.md`; assess the intended session length as well as
  the measured first-raid footprint. Normal tuning needs no dedicated warmup;
  qualify preparation and weather under `measurement-rules.md`.
- A personal starting value is allowed when labelled as a preference. It does
  not become a universal default or an established performance improvement.

## Graphics and performance-related Game options

| Setting or group | Evidence and useful decision | Boundary |
| --- | --- | --- |
| Screen Mode | Record the current mode and leave the user's choice unchanged during other comparisons. | No measured coefficient or mode recommendation; dedicated tests are deferred. |
| Resolution / Resampling | Consider lower internal resolution when GPU rendering is the limiting work; compare detail at the intended output resolution. | No measured coefficient here. CPU limits or a cap can prevent an FPS gain. Saved display resolution alone does not prove internal render resolution. |
| VSync / FPS limits | Record together with Reflex and distinguish saved flags from an observed effective cap. Hold them consistent for comparisons. | No universal Off recommendation; frame pacing, tearing and latency preferences matter. An inactive/disabled UI field does not establish the effective cap. |
| Texture Quality | Memory headroom, lows and moving stutters are useful alongside average FPS. Use only the measured transitions and joint Streets-mode state. | No controlled four-level curve or universal VRAM threshold. Do not describe VRAM overflow as a simple RAM-then-crash switch. |
| Streets Lower Texture Resolution Mode | Candidate for memory pressure on Streets; observed lower memory gauges at High. Check image quality, lows and symptoms. | Average FPS benefit unestablished; both On lows were worse in this series. No complete override/equivalence of texture levels established. |
| Shadows Quality | Factory Ultra/Low supplies a conditional comparison; saved ShadowDistance changed 80/30 with the UI level. | The measured effect includes distance coupling, not just edge quality. Woods had no comparable Ultra capture. Environmental shadow differences were user-confirmed from images. |
| Object LOD Quality | Separate static 2/3/4 evidence from moving 2/4 evidence; consider object detail and transitions. | No equal cost per step or independent RAM-growth curve. A character visible around 200 m at minimum establishes only that visibility observation. |
| Overall Visibility | Use the fog/LOD-qualified static and moving evidence. Choose a starting distance for the maps and landmarks the player uses. | 1000 is not a universal optimum. Character visibility alone does not establish all environmental culling or occlusion rules. |
| Cloud Quality | An image/weather preference or a separate candidate test. | No measured coefficient or established guarantee of unchanged gameplay visibility. Low can be a personal starting choice. |
| Anti-Aliasing | Only TAA Low/Off was measured. Preserve the player's preference; judge softness, thin details and shimmering in motion. | No ranking of all AA modes. A static FPS comparison does not establish motion image quality. |
| Volumetric Lighting | Off/Low/Ultra observations include changed GPU use without a reliable average-FPS gain; the performance scene's visible source response was unconfirmed. | No free-cost conclusion, universal Low rule or grenade-smoke visibility mechanism established by those captures. Keep separate visual comparisons with their build/context; do not discard them or treat them as an FPS test. |
| DLSS / FSR | A candidate when GPU work is limiting; reconstruction may allow other quality settings to remain higher. Compare moving detail at the target resolution. | No measured mode coefficient here. Lowering all other options first is unnecessary. Blur, ghosting and shimmering can matter; CPU/cap limits constrain gains. |
| HBAO | Expensive Colored Ultra/Off evidence supports a high-priority GPU-load test when applicable. | Do not assign its effect to Max Performance, High Performance, High or Ultra; those levels were not measured. |
| SSR | Off/Ultra changed GPU use in the car scene; no repeatable average-FPS gain established there. | Cost and image value can depend on reflective surfaces, weather and GPU limits. Other levels unmeasured. |
| Anisotropic Filtering | Small repeated On/Off effect in Factory; weigh the player's texture clarity preference. | Small is not zero; no universal free-quality claim. |
| NVIDIA Reflex Low Latency | Its purpose is latency. On/Off was tested; no repeatable FPS penalty was established. The full series was inconclusive because the Off repeats diverged and limiter flags changed. | The first contrast near -3% does not establish that the whole series was within noise. Distinguish from driver Low Latency Mode; FPS/resource capture does not measure input latency or establish zero cost, RAM savings or a stability defect. |
| Sharpness | Choose for image clarity/halos; useful FPS savings were not established. | Do not infer zero computation or equate its purpose with Reflex. |
| Area Light Instancing | BSG describes moving part of the lighting calculations from CPU to GPU and a moderate performance increase (user-supplied game description). Measured +3.75% average FPS in a static daytime Streets first-raid scene, with two On runs sharing one dry Off control. | Single-pair uncertainty includes zero; no demonstrated lows or resource-saving benefit. Build-specific implementation details, hardware-family rule and interaction with other settings remain unverified. The stated purpose does not establish a numerical CPU saving or a universal On rule. |
| Grass Shadows | Preserve preference or test separately if relevant. | No measured coefficient; visual subtlety does not establish small performance cost. |
| High Quality Color / Z-Blur / Noise / Chromatic Aberrations / other effects | Preserve image/comfort preferences; record the actual reported value. | No measured coefficients here. Do not assign a common minor cost to remaining checkboxes. |
| PostFX | Preserve visibility/color preferences; a neutral or Off profile can be a troubleshooting choice. Inspect the specific effect in the scene of interest. | No aggregate FPS coefficient or universal smoke/bright-zone visibility conclusion from these captures. |
| Only Use Physical Cores | CPU-dependent On/Off candidate with matched map, mode and workload. | No measured coefficient or universal On rule for Intel/AMD. Low overall CPU use alone does not exclude a main-thread limit. |
| Automatic RAM Cleaner | Consult `automatic-ram-cleaner.md`: retail native trim path exists; the measured long-session series is separate context. | Checkbox/event/timer linkage and repeated FPS/stutter benefit remain unestablished in retail. No RAM-capacity-based On rule or first-raid seed. |
| FOV | Explain peripheral view versus apparent target size; use the published 50/65/75 examples, including optics, in `settings-help.md`. Hold fixed during other graphics comparisons. | No measured FPS coefficient or universal map-specific value. A personal 60–65 starting preference is not an optimum. |
| Head bobbing / Hideout preload / other relevant Game values | Record when present and hold them fixed during a graphics comparison. Offer usability preferences separately. | No measured coefficients here; do not invent UI labels or interpret absent enums. |

Controls and Sound remain outside these performance skills. The optional
`game-starting-profile.md` covers manual Game-tab usability preferences; its
acceptance is not an FPS optimization or a forecast input.

## Reporting and transfer

The workflow assists collection and interpretation. The user changes settings
and plays the scene; web users also collect and paste Toolkit output. Describe
these steps accurately rather than promise fully automated tuning.

Before attributing an FPS shortfall to hardware or Windows, check version,
map, AI/server execution context, resolution/render scale, caps, settings,
weather, activity and session history. Local Practice without AI does not
reproduce every live raid's CPU load. Discuss server-hosted PvE only where the
selected mode actually provides it; do not promise arbitrary hosting choice
or a fixed gain. Confirm the constraint before suggesting hardware purchases
or system tuning.

For public summaries, show build/context and qualify the dataset's coverage.
Use the verified Store/installation guidance already in the project. Do not
claim a public plugin-directory listing without its verified listing link.

## Primary mechanism references

- [Microsoft: pagefile sizing](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows): sizing depends on peak commit and crash-dump requirements, not a fixed RAM-plus-pagefile target.
- [NVIDIA: EFT Reflex integration](https://www.nvidia.com/en-us/geforce/news/escape-from-tarkov-reflex-out-now/): historical evidence for its name/purpose, not a current-build percentage promise.

Sources checked 2026-10-12. Game-specific numerical evidence and its limitations
remain in `graphics-response-model.md`; raw review/research artifacts stay private.
