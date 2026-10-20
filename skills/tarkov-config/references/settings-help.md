# Graphics Settings Help and Visual Comparisons

Use this reference to explain a setting, show its visible tradeoff, or help a
player choose a manual starting profile. Explanation-only requests need no
Toolkit report, running game or benchmark. Answer in the user's language,
explain the requested option first, then offer one or two relevant comparisons.
For a full review, group related options and preserve the player's preferences.

Use [settings-evidence-guide.md](settings-evidence-guide.md) for recommendation
boundaries and [graphics-response-model.md](graphics-response-model.md) for
measured FPS/resource effects. These images add visual context, not numerical
priors. A visible difference does not establish its performance cost.

## Using the comparisons

Prefer the author's comparison viewer for variants of the same scene; preserve
its default layout and let the user choose another layout. Use the standalone
illustrations below to explain a mechanism or show the manual tuning order.
Individual capture links are available for direct viewing when wanted or when
the viewer is unavailable; do not give every link for a simple question. Do not
substitute a video platform link. Some viewer comparisons contain moving clips,
identified below; the others use still images. If the user wants stills only,
omit the moving ones.

Preset names and labels were checked on 2026-10-20. Their capture builds and
complete settings are not recorded in every preset. Treat them as examples of
those scenes, rather than proof for every current build, map or weather state.
Read the displayed labels; do not invent an intermediate setting or mode.
Stills help compare detail, edges and reflections. Judge shimmering, ghosting,
pop-in and temporal stability in motion. Neither format measures FPS by itself.

## Display resolution, Resampling and upscaling

**Resolution** sets the selected output dimensions. **Resampling** changes the
internal rendering scale before the image is scaled to that output. Below 1x
trades detail for less rendering work; 1x is the native-scale starting point;
above 1x renders more detail before downsampling and can add substantial GPU
work. Do not interpret a menu multiplier as a per-axis pixel multiplier without
verifying its definition. Lower internal resolution can raise FPS when GPU
rendering is limiting; a CPU limit or frame cap can hide that benefit.

[Resampling: Woods, 0.5x / 1x / 4x](https://thetimmytook.github.io/media/?pres=./presets/settings/resampling.json&view=woods).
Inspect distant foliage, fine edges and softness at the same viewing size.

**NVIDIA DLSS / AMD FSR** reconstruct the output image from a lower-resolution
input in their upscaling modes. Availability and mode names depend on the GPU
and game version. When GPU work is limiting, try an available Quality mode
before moving toward Balanced or Performance; other graphics settings need not
all be Low first. Compare target clarity, fences, foliage and movement at the
intended output resolution. Reconstruction artifacts can outweigh the FPS gain
for a particular player. Do not assume all reconstruction or native-AA modes
lower internal resolution, or infer a supported mode from the GPU brand alone.

[DLSS / FSR mechanism illustration](https://thetimmytook.github.io/media/screens/settings/help/upscaling-explainer.png)
shows the lower-resolution input and reconstructed output. It is a conceptual
diagram, not a captured quality comparison or a measured FPS result.

[DLSS: Factory, Off / Balanced / Performance, static comparison](https://thetimmytook.github.io/media/?pres=./presets/settings/dlss.json&view=factory-static)
is the primary link for comparing these modes in stills, captured on build
**1.2.0.0.47965**. Inspect fine surface detail and edges; it does not show moving
artifacts or an FSR implementation. No Quality capture is included in this set.
Individual frames: [Off](https://thetimmytook.github.io/media/screens/settings/dlss/factory/dlss-factory-off.png),
[Balanced](https://thetimmytook.github.io/media/screens/settings/dlss/factory/dlss-factory-balanced.png),
[Performance](https://thetimmytook.github.io/media/screens/settings/dlss/factory/dlss-factory-performance.png).

[DLSS: Factory, Off / On, moving comparison](https://thetimmytook.github.io/media/?pres=./presets/settings/dlss.json&view=factory).
This older moving view labels the second condition only as On; its quality mode
is unknown. Do not infer that mode from the separate static set. Neither DLSS
view measures mode-specific FPS gains.

**VSync, FPS limits and NVIDIA Reflex Low Latency** affect presentation and
latency decisions as well as comparisons. Reflex aims to reduce input latency;
it is different from NVIDIA's driver Low Latency Mode. Check the effective FPS
cap when changing it: a disabled menu control or saved number does not prove
the active cap. FPS and memory readings do not measure input latency. Preserve
the player's tearing, responsiveness and frame-pacing preferences.

Reflex On/Off was tested: a repeatable FPS penalty was not established. The
forward contrast was about -3%, but the reverse contrast was +17.74% and the
Off repeats differed by 17.23%, with coupled limiter flags. The series is
inconclusive; describing the entire result as within measurement noise would
also overstate it. This supports no fixed FPS or memory coefficient and does
not establish zero processing cost.

## Texture Quality and Streets texture mode

**Texture Quality** changes surface texture detail. Higher levels can require
more texture memory; average FPS may change little while memory headroom,
streaming stutters or low percentiles matter more. Inspect bricks, signs and
surface detail at the distances the player actually uses.

[Textures: Factory, Low / Medium / High / Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/textures.json&view=factory).
This visual four-level example is separate from the measured transitions; it
does not supply a four-level memory curve or a minimum VRAM requirement.

**Streets of Tarkov Lower Texture Resolution Mode** is a separate candidate
for texture-memory pressure on Streets. Record it together with Texture Quality
and compare image quality, memory gauges and symptoms. Do not assume it fully
overrides every texture level, behaves the same on another map, or increases
average FPS. A VRAM gauge near capacity alone does not prove harmful paging.
For crashes or increasing memory use over several raids, use
[session-stability.md](session-stability.md).

## Shadows, LOD, Overall Visibility and clouds

**Shadows Quality** changes rendered shadow detail and may also change shadow
distance. In the measured build the saved distance changed with the quality
level, so the comparison includes that coupling. Inspect tree/building shadows
and edge detail; a character's own shadow is not the only possible difference.
For an A/B, use a scene with visible responsive shadows and comparable light.

[Shadows: Customs, Low / Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/shadows.json&view=customs).

**Object LOD Quality** controls how long more detailed object models are kept
as they get farther away or smaller on screen. Lower levels can reduce geometry
work but make detail transitions more visible. Inspect distant objects while
moving toward them. Do not assign equal FPS or RAM cost to each slider step.

[LOD: Customs, 2 / 4](https://thetimmytook.github.io/media/?pres=./presets/settings/lod.json&view=customs),
[Streets, 2 / 4](https://thetimmytook.github.io/media/?pres=./presets/settings/lod.json&view=streets)
(both moving comparisons).

**Overall Visibility** controls the draw distance of affected environment
objects. Lower values can reduce distant scenery and work, but fog, map content
and the current limit affect the result. Compare useful landmarks, cover and
pop-in rather than treating the slider as a direct enemy-visibility distance.
Characters visible around 200 m in the author's minimum-setting checks do not
establish every object's culling or occlusion behavior. A value of 1000 is a
possible manual compromise, not an optimum for every map or play style.

All these still comparisons are **400 / 3000**:

- [Lighthouse: Chalets](https://thetimmytook.github.io/media/?pres=./presets/settings/visibility.json&view=lighthouse-chalets).
- [Reserve: Radar](https://thetimmytook.github.io/media/?pres=./presets/settings/visibility.json&view=reserve-radar).
- [Woods: scene 1](https://thetimmytook.github.io/media/?pres=./presets/settings/visibility.json&view=woods-1), [scene 2](https://thetimmytook.github.io/media/?pres=./presets/settings/visibility.json&view=woods-2), [scene 3](https://thetimmytook.github.io/media/?pres=./presets/settings/visibility.json&view=woods-3).

**Cloud Quality** concerns cloud rendering. Low may be the player's preferred
starting choice, but its FPS cost and effect on gameplay visibility were not
measured here. Changing weather or lighting can confound other comparisons.

## Lighting, contact shadows and reflections

**Volumetric Lighting** concerns illuminated air, haze and visible light beams.
Its visual value depends on the scene, lights and smoke. Off, Low, Medium, High
and Ultra were available in the tested menu; do not infer a cost for unmeasured
levels from their names. Low can be a personal compromise. The performance
captures did not establish a reliable average-FPS gain or a smoke mechanic.

[Smoke lighting: settings comparison](https://thetimmytook.github.io/media/?pres=./presets/smoke.json&view=settings)
is a separate static visual example. Its labels are Original, Low, Medium,
High and Ultra; Original is not confirmed to mean Off. Keep its capture/setup
limits visible. Do not generalize it into a promise of seeing through every
smoke, or a conclusion about AI, thermals or the current build's mechanics.

**HBAO (Horizon-Based Ambient Occlusion)** adds shading around contacts, corners
and nearby objects, giving depth to the scene. Inspect where objects meet walls
or floors. Turning it down can reduce GPU work; the strong measured Colored
Ultra/Off response does not establish the cost of the intermediate levels.

[HBAO: Factory 1, Off / Colored Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/hbao.json&view=factory-1),
[Factory 2, Off / Colored Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/hbao.json&view=factory-2),
[Factory 3, Off / Max Performance / Colored Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/hbao.json&view=factory-3).
Use the actual menu levels rather than asking for a nonexistent generic Low.

**SSR (Screen Space Reflections)** adds reflections to affected shiny or wet
surfaces using screen information. Inspect cars, puddles and reflective ground;
their contribution depends on the scene and weather. Apparent wetness can
change with SSR, so record the setting before attributing that difference to
rain. GPU-use differences without an FPS change do not establish zero cost.

[SSR: Streets, Off / Ultra](https://thetimmytook.github.io/media/?pres=./presets/settings/ssr.json&view=streets).

**Area Light Instancing** is described by BSG as moving part of the lighting
work from CPU to GPU. It is a useful separate On/Off candidate where supported,
especially when investigating CPU limits and available GPU headroom. Keep it
only when the user's result supports it. This stated mechanism and the one
measured Streets scene do not establish a fixed gain or an interaction formula
for other settings. Do not deliberately increase GPU work as a general FPS fix.

## Edge smoothing, texture filtering and sharpness

**Anti-Aliasing (AA)** smooths jagged edges. Modes trade GPU work, edge stability
and softness; temporal methods can introduce ghosting or blur. Compare fences,
foliage and fine details in movement. There is no measured quality/performance
ranking of all modes here, and the player's preferred mode matters.

[AA: Reserve, Off / TAA High, moving comparison](https://thetimmytook.github.io/media/?pres=./presets/settings/antialiasing.json&view=reserve).
These visual endpoints differ from the TAA Low/Off performance measurement.

**Anisotropic Filtering (AF)** improves texture clarity on surfaces viewed at
an angle, such as a road or floor stretching away from the camera. Its repeated
Factory FPS effect was small; keep the clarity preference unless a useful test
requires otherwise. Small does not mean universally free.

[AF mechanism illustration](https://thetimmytook.github.io/media/screens/settings/help/anisotropic-explainer.png)
explains texture sampling on an angled surface. Use it for understanding the
effect; it does not establish a universal performance cost.

[AF: Factory, Off / On](https://thetimmytook.github.io/media/?pres=./presets/settings/anisotropic.json&view=factory).
Look along the surface rather than only at a wall facing the camera.

**Sharpness** increases contrast around detail and edges; it does not restore
missing geometry or texture information. Excessive sharpening can produce
halos and an artificial look. Choose it for image preference; useful FPS savings
were not established. Its purpose and processing differ from Reflex.

[Sharpness: Factory, 0 / 0.6 / 3.0](https://thetimmytook.github.io/media/?pres=./presets/settings/sharpness.json&view=factory).
Inspect brick edges, text and fences. The visual middle value is 0.6, not the
1.5 used in part of the performance study.

## PostFX, other effects and Game options

**PostFX** adjusts the image, including color and contrast. Start from the
player's visibility and accessibility needs; Off is an option, not a requirement.
Compare the specific profile/effects in the scene of interest. No aggregate
PostFX FPS coefficient or universal visibility improvement was measured here.

**High Quality Color, Z-Blur, Chromatic Aberrations, Noise and Grass Shadows**
affect image presentation or extra visual detail. Explain only the requested
option: Z-Blur adds blur, Chromatic Aberrations separate colors near edges,
Noise adds grain, and Grass Shadows concern grass shading. Color quality and
effect behavior can vary by version. The player can start with unwanted effects
Off; neither the grouping nor subtle appearance establishes equal or small cost.

**FOV (Field of View)** trades the amount of peripheral scene shown against the
apparent size of objects at a fixed distance. Higher values show more at the
edges; lower values make central targets appear larger. Compare comfort, sight
picture and peripheral awareness, including through optics. A preference around
60–65 is an optional starting point, not a required map-specific setting or a
measured FPS optimization. Hold FOV fixed during other graphics comparisons.

[FOV: Factory, 50 / 65 / 75](https://thetimmytook.github.io/media/?pres=./presets/settings/fov.json&view=factory),
[Customs](https://thetimmytook.github.io/media/?pres=./presets/settings/fov.json&view=customs),
[Customs through optics](https://thetimmytook.github.io/media/?pres=./presets/settings/fov.json&view=customs-zoom).

**Only Use Physical Cores** is a CPU-dependent On/Off candidate, not a universal
Intel/AMD recommendation. **Automatic RAM Cleaner** needs the implementation and
measurement distinctions in [automatic-ram-cleaner.md](automatic-ram-cleaner.md);
lower resident memory alone does not prove a leak fix or better FPS. For other
beginner Game-tab preferences, use [game-starting-profile.md](game-starting-profile.md).
Controls and Sound are outside these performance skills.

## Optional manual order that preserves gameplay preferences

Offer this only when the user wants a manual starting route. It groups changes
by the author's tolerance for gameplay/image loss, then uses expected cost to
guide choices within those groups. It is not a measured global cost ranking.
Let the player move a setting when its visual value matters more to them.

[Manual graphics optimization order — table image](https://thetimmytook.github.io/media/screens/settings/help/manual-optimization-order.png).
Offer this overview when the player wants the whole sequence. The same order
and concrete example values are available as text below.

| Step | Setting group | Example action when reducing settings |
| --- | --- | --- |
| 1 | Resampling above 1x | Return to 1x. |
| 2 | Unwanted High Quality Color, Z-Blur, Chromatic Aberrations, Noise, Grass Shadows, PostFX | Try Off; preserve useful PostFX/accessibility preferences. |
| 3 | HBAO, SSR | Try Off individually. |
| 4 | Cloud Quality, Shadows Quality | Try Low individually. |
| 5 | Object LOD Quality, Overall Visibility, Volumetric Lighting | If currently higher: LOD 3, Visibility 1000, Volumetric Medium. |
| 6 | Same distance/lighting group | If still needed: LOD 2, Visibility 400, Volumetric Low; test Off separately if acceptable. |
| 7 | Texture Quality / Streets texture mode | Lower textures or test Streets mode according to memory pressure. |
| 8 | Available DLSS / FSR upscaling | If GPU-limited, compare Quality, then Balanced/Performance as needed. |
| 9 | Anti-Aliasing | Compare available modes or Off; assess moving detail. |
| 10 | Anisotropic Filtering | Reduce only if the tradeoff is useful. |
| 11 | Resampling below 1x | An additional image-quality compromise when suitable reconstruction is unavailable or insufficient. |

Skip already-applied steps and values at or below the example targets; do not
increase a lower setting while trying to reduce load. With memory pressure,
consider textures/Streets mode earlier. With a GPU limit, reconstruction may be
worth trying earlier while retaining other quality settings. Evaluate Area
Light Instancing separately; do not automatically disable it with visual effects.

For a weak system, a low-quality baseline can be a practical starting experiment;
raise only wanted quality while the target and memory headroom allow it. Do not
enable blur/grain or raise every setting merely to reverse the table. Hardware
capacity alone does not guarantee a comfortable frame rate or stability.

Use the player's problem map; use Streets if they request a stress test and have
no specific map. Practice makes repeated checks convenient, but record AI and
execution context and qualify transfer to actual play. Change one useful group
at a time and compare after each step; do not apply the whole table as a batch.
Some settings require leaving a raid or restarting; follow the current game's
prompt and verify the applied values. A help request alone needs no capture.

## General mechanism sources

- [Unity: Level of Detail](https://docs.unity3d.com/Manual/LevelOfDetail.html) explains the geometry/detail mechanism; it does not establish EFT's slider response.
- [AMD: temporal super resolution](https://gpuopen.com/manuals/fsr_sdk/techniques/super-resolution-temporal/) explains reconstruction; it does not establish which modes a given EFT build exposes.
- [NVIDIA: EFT Reflex integration](https://www.nvidia.com/en-us/geforce/news/escape-from-tarkov-reflex-out-now/) documents its latency purpose historically; do not transfer its percentage examples to the current build.

Descriptions reviewed 2026-10-20 against the author's setup text and published
comparisons, including the published mechanism diagrams and static DLSS set.
