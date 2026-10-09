# Capture-window resource telemetry

Implemented in shared `TarkovSkills.Core` and displayed by `TarkovBenchmark.Feature`
in both Store hosts. Public Store versions have not been updated by this change.

## Local contract

New completed benchmark runs, standalone completed command results and Toolkit capture
reports include `resource_telemetry` with `schema_version: 1`. Legacy runs missing the
field open with `status: not_collected`; explicitly null legacy fields do likewise.
Collection failure produces `unavailable` or `partial` without invalidating complete FPS.
Cancelled/discarded captures are not saved as completed runs.

Every sampled metric has nullable `average`, `minimum`, `maximum`, `last`,
`valid_sample_count`, `valid_duration_sec`, `coverage`, `unit`, `source`, `scope`,
`status` and `reason_codes`. Capacities have nullable `value`, unit/source/scope and
availability. Bytes are stored without GB rounding; UI uses GiB (2^30 bytes).
Unknown values are explicitly null, including when other JSON null fields are omitted.
All minimum/maximum values come from valid samples of this capture, never since boot.

| Section | Fields and meaning |
| --- | --- |
| `window` | Requested 120/240 seconds, valid FPS interval duration, target 1-second sample interval, expected sample count, alignment and coverage method. |
| `cpu` | `total_utilization` and `logical_processors` indexed by processor group and logical index, percentages for the whole system. |
| `gpu` | Sanitized model name, `scope: whole_adapter`, selection status/method, discrete/unified/unknown memory architecture, physical `dedicated_vram_capacity`, `graphics_utilization`, `dedicated_memory_used`, `shared_memory_used`. |
| `ram` | Physically installed and OS-usable capacities; physical used and available RAM, including sampled minimum available. |
| `pagefile` | Global automatic-management policy, file count, live allocated size and use, plus anonymous per-file index/media summaries. Allocation can grow during the capture. |
| `commit` | System committed virtual memory, live commit limit and remaining headroom; last values and capture minimum headroom. |

Commit is memory Windows has promised to back with RAM or pagefiles. It is not
resident RAM, pagefile occupancy or pagefile size. Its limit includes backing resources
and OS overhead; it must not be presented as physical RAM or actual allocated pagefile.
Shared GPU memory uses system RAM and is not extra dedicated VRAM. On UMA hardware,
reserved system memory is not presented as physical discrete VRAM.

## Sampling and Windows limitations

Preparation enumerates hardware, classifies UMA with D3D12 architecture information,
reads static capacities/policy and primes PDH and GPU driver observations before
PresentMon capture. A single worker
then targets 1 Hz. There are no extra processes, services, drivers or runtime scripts.

The pinned PresentMon runs with `--qpc_time`; frame timing calculations stay unchanged.
`MsBetweenPresents` uses the interval ending at `TimeInQPC`/`QPCTime`.
`FrameTime`/`CPUFrameTime` uses the interval starting at `CPUStartQPC`. Unsupported or
missing timestamps make alignment unavailable while preserving valid FPS metrics.
The window is the union of accepted frame intervals, excluding rejected-frame gaps.

PDH rates are accepted only when their entire delta belongs to a valid frame interval
(maximum delta 1.5 seconds). Gauge readings are accepted only inside the window and
support at most one second, bounded by the next reading and valid interval end.
Average is weighted by supported duration; coverage is valid measured seconds divided
by the valid window duration. Preparation, cleanup, missing samples and stale values
do not fill coverage. Boundary samples therefore commonly produce partial coverage.
Sample counts are reported separately from duration coverage.

On NVIDIA and AMD, the existing `graphics_utilization` field uses an installed-driver
provider rather than PDH graphics percentages. Vendor observations support at most one
second, reject repeated/stale AMD timestamps, and must fit wholly inside the valid FPS
window. NVAPI's documented trailing one-second acquisition is also bounded by the
window. ADLX's averaging period is unspecified: its coverage describes observed support,
not exact one-second GPU busy-time bins. Missing observations and long gaps reduce
coverage instead of extending an old value.

| Source | Limitations |
| --- | --- |
| PDH `Processor Information` | System CPU utilization, including other work. Group/index avoids conflating machines with multiple processor groups. A busy logical CPU is evidence to investigate, not proof of a game thread bottleneck. |
| NVAPI `nvapi_gpu_graphics_utilization` | NVIDIA graphics-domain percentage for the selected physical GPU, matched to DXGI by LUID. The API documents a trailing one-second value. Unsupported runtime, domain or mapping produces null; no substitution with PDH. |
| ADLX `adlx_gpu_usage` | AMD GPUUsage from tracked history for the selected adapter, matched through IADLXGPU2 LUID. Duplicate/regressed timestamps and inconsistent clock progress are rejected. The integration period is unspecified. Missing capabilities or runtime produce null; no legacy ADL or PDH substitution. |
| PDH `GPU Engine` 3D | Retained for adapter selection and utilization on other vendors, and as the source of previously saved Windows readings. Aggregate process contributions for each physical graphics engine, then use the busiest engine of the selected adapter. Engine percentages are not added together. Invalid contributions make the reading unknown. Depends on the WDDM driver; first/dynamic counter instances may lack a valid delta. |
| GPU selection | Tarkov 3D activity identifies the adapter using an internal PID/LUID match. A sole hardware adapter is a fallback. Multiple active adapters, ambiguous selection and linked physical nodes remain unknown; GPUs are never combined. No per-game memory is collected. |
| PDH `GPU Adapter Memory` | Whole-adapter dedicated/shared usage, independently sampled. No `GPU Process Memory` or sum of process-memory counters. Missing driver support is unknown. Discrete dedicated usage beyond known physical capacity is rejected. |
| DXGI / D3D12 | DXGI reports dedicated video capacity; D3D12 distinguishes UMA. Unknown architecture or missing discrete capacity produces null, not guessed physical VRAM. |
| `GetPhysicallyInstalledSystemMemory` / `GetPerformanceInfo` | Installed versus OS-usable RAM is explicit. Live physical available/used and commit totals use Windows page size. |
| `EnumPageFilesW` | Current allocated and used pages for every pagefile. Windows since-boot peak usage is ignored. Empty successful enumeration is measured zero; failed enumeration is unknown. |
| WMI policy / storage | Preparation only. Global automatic management is separate from each file's current allocation; false does not prove that all individual files have fixed sizes. Backing media may be unknown. Paths are internal lookup keys only. |

Near-full sampled dedicated VRAM (at least 95% of known discrete capacity) displays a
potential-limitation hint and suggests repeated texture-quality A/B tests. It makes no
causal claim. CPU/GPU percentages, one sampled peak or one run cannot establish the
cause of FPS drops. At 1 Hz, short spikes can be missed.

## Privacy and sharing

Only summary DTOs are saved or copied. Samples, counter instance names, PID/LUID,
pagefile paths, other application names and device identifiers remain internal and
are discarded with the sampler. Failure reasons are fixed codes, not native error text.
There are no automatic uploads. Toolkit Copy results and capture stdout expose the
sanitized completed summary for local/web skills.

The Academy submission contract coordinated on 2026-10-06 requires `resource_telemetry`.
Updated desktop builds project the typed summary, validate the exact nested allowlist,
numeric/status/coverage/array rules and required null keys, and include it only with the
selected run after explicit Send for review. The review shows the frozen request's
summary, independently of later history or GPU-dropdown changes. Its consent explains
whole-system/whole-adapter scope and publication after moderator approval. Authentication
and ownership data remain separate. Deletion removes telemetry; the smaller closed,
unlinked measurement archive does not retain it. Comparison requests and cohort keys
remain unchanged and exclude telemetry.

The coordinated Academy allowlist accepts exactly Windows
`pdh_gpu_engine_3d_busiest_engine`, NVIDIA `nvapi_gpu_graphics_utilization` and AMD
`adlx_gpu_usage` in the existing graphics-utilization field. The temporary vendor-send
guard was removed after the staging/production rollout and live checks recorded below.
Each request retains the measured source, nullable values and coverage; neither vendor
data nor previously saved Windows requests are relabelled or regenerated.

Both new and saved outbox requests are limited to 262,144 UTF-8 bytes. Frozen requests
are immutable across retries. A saved pre-telemetry DTO fails with an explicit unsupported
contract message: no automatic upgrade, UUID replacement, cleanup or resend. Existing
local status checkpoints remain untouched; a known status followed by server 404 after
the Academy reset does not authorize republication. Old local history without telemetry
still opens and can prepare a new explicit `not_collected` block if no frozen request
already exists. The measured adapter is never relabelled from the hardware dropdown.

To verify C# JSON against the actual Academy TypeScript source schema locally, run
`build/test-academy-resource-contract.ps1 -AcademyRoot C:\projects\timmy-academy`.
This development-only check uses the backend checkout's existing Node/tsx dependencies,
exports synthetic test summaries to a uniquely owned temporary directory, checks both
accepted and rejected cases, and cleans it afterward. It does not modify backend code,
access a database or send requests. No script is shipped in either Store application.

## Verification and release limits

Automated coverage includes weighted calculations, valid-window boundaries/gaps,
partial/unavailable counters, processor groups, multi-GPU ambiguity and linked nodes,
UMA/discrete memory distinctions, growing/multiple pagefiles, commit versus physical
memory, legacy loading, mandatory JSON, saving/copying, strict submission projection and
privacy. Shared lifecycle tests simulate cancellation, game/raid exit and collector
failure without touching a game process. WPF tests render both hosts at minimum sizes
and verify their unload cancellation and telemetry display.

Submission tests also cover explicit legacy/unavailable/partial/UMA blocks, forbidden
nested fields, nullable-key preservation, duplicate JSON keys, array bounds, independent
six-decimal rounding, GPU selection separation, 256-KiB new/saved limits, frozen retries,
unsupported old outboxes and aggregate owner cards without telemetry in receipt caches.
The online weighted mean bounds floating-point rounding within sampled extrema before
local persistence; validation never repairs inconsistent saved or submitted statistics.

Read-only Windows smoke tests exercise native PDH, memory/pagefile APIs, DXGI/D3D12,
sampler cleanup, privacy and measured polling overhead on the test machine. These
measure sampler cost; they do not establish FPS impact during a real raid.

Local verification on 2026-10-04: 240 Core/CLI tests, 22 Benchmark tests and 70 UI
regression tests passed. A separate six-poll Windows run returned six valid readings
for CPU, graphics, dedicated/shared GPU memory, RAM and pagefiles; GPU capacity was
available. Poll elapsed time averaged 4.69 ms, maximum 11.76 ms; process CPU time was
109.38 ms and managed allocation 1,476,032 bytes across the six-poll test. These are
single-machine smoke measurements, not general hardware guarantees. Both unsigned
Release MSIX packages passed MakeAppx/content validation and pinned PresentMon checks.

Local follow-up verification on 2026-10-06: 299 Core/CLI tests, 22 Benchmark tests and
71 UI regression tests passed (392 total). The source-schema check accepted 11 valid
payloads and rejected 43 invalid payloads with zero C#/TypeScript contract mismatches,
including a native Windows summary paired with synthetic FPS. Both rebuilt unsigned
Release MSIX packages passed MakeAppx/content validation, public auth configuration
validation and pinned PresentMon checksum checks. No real run was uploaded; authenticated
staging submission and Microsoft-signed Store flight checks remain pending.

Staging preparation on 2026-10-06: both local self-contained Debug GUI builds and the
Toolkit CLI use the staging API and Development Clerk binding, with separate empty
histories/outboxes resolved by the existing Debug-only test-directory option. No
existing outbox/history or production configuration was changed. Pinned PresentMon
checks and both guarded local launchers passed. Live staging verification passed the
13 existing public smoke cases, eight telemetry demo cases, public auth configuration
and anonymous owner-access denial (23 checks); response JSON also matched source
schemas. Browser checks confirmed near-full VRAM, partial/unknown and UMA wording.
Toolkit's no-game capture guard refused capture and saved no run. No authenticated
request, selected-run upload, moderator action or deletion was performed; the real
selected-run end-to-end test remains pending.

Real staging verification on 2026-10-07: the user completed a Benchmark GUI capture
and explicitly sent the selected run. The desktop confirmed `pending_review`. Browser
moderation showed the same measured summary; the authorized approval completed and
My Bench showed `published`. Anonymous public detail returned HTTP 200 and passed the
source TypeScript schema and recursive privacy-key checks. Every FPS statistic and
the complete telemetry block matched the frozen desktop request exactly, including
logical processors and anonymous pagefile summaries. This confirms transport,
moderation and publication; it does not validate the physical accuracy of every
Windows counter. The observed low graphics-utilization reading still needs a live
counter comparison. The user checked status in Benchmark after approval and saw
`Published`; read-only inspection confirmed the matching local checkpoint persisted
`published`. The user also confirmed the same Benchmark status after restart.
A distinct real capture in Toolkit on the same map was explicitly sent and received
`pending_review`, then approved through staging browser moderation. Its anonymous
public detail passed the same strict schema, exact FPS/full telemetry equality and
privacy checks; both measurements remained separate published runs. Toolkit status
check/restart after approval was subsequently confirmed by the user; read-only
inspection confirmed its local `published` checkpoint. Live cancellation/exit,
overhead and Store flight checks remain pending. Graphics utilization was similarly
low in both captures and remains
a counter-validation question, not evidence of a hardware bottleneck. Production was
not changed by these checks.

Live GPU-counter diagnostics on 2026-10-07 collected two sets of 60 samples using
the actual staging Core collector, an independent full-name PDH engine aggregation
and periodic WMI formatted readings. The collector and full-name aggregation matched
exactly; no 3D instances were omitted. The first collector mean was 5.73% (range
1.76-14.23%); the second was 26.76% (range 23.13-29.32%), with a WMI mean of 26%.
These diagnostics were separate from FPS capture and did not upload data. Matching
readings from the same Windows provider do not establish physical accuracy. A Task
Manager screenshot from a different moment showed 42% on the 3D graph; simultaneous
visual comparison is still pending, so graphics-counter validation remains open.

A third user-coordinated 60-sample diagnostic returned a collector mean of 14.67%
and range of 5.44-28.26%; full-name PDH aggregation matched exactly, with no omitted
3D instances. Periodic WMI returned a mean of 13% and range of 5-25%. The user's
Task Manager screenshot from this test showed current 3D utilization of 25% and
a recent graph visually consistent with the diagnostic range. This provides a
single-machine visual cross-check; the current percentage is not a capture mean,
and the screenshot does not establish exact per-sample agreement. It also does not
retroactively validate the earlier FPS captures' low GPU means or demonstrate a
hardware bottleneck.

The user subsequently completed another 120-second Toolkit FPS capture and supplied
a Task Manager screenshot taken near the first-minute boundary. Read-only checks
of the saved run passed FPS/telemetry valid-duration alignment within FPS rounding,
graphics coverage ratio, statistic bounds, unit and whole-adapter scope. The first
minute's visual graph showed predominantly low 3D activity with brief spikes,
consistent with the possibility of a low full-capture mean. A screenshot covering
the second minute was not supplied, so exact whole-window and per-sample visual
agreement remains unverified. Diagnostic evidence and actual run data remain
outside the repository; no request or upload was performed by these checks.

The user then supplied a game-plus-Task-Manager screenshot without capture showing
sustained higher 3D activity than the screenshot taken during capture. This raises
an unresolved capture-interference question: agreement between two readers during
capture does not exclude a change to workload or Windows-reported utilization caused
by measurement. Separate, development-only telemetry-only and pinned-PresentMon-only
diagnostic modes were prepared outside the repository, compiled and checksum-verified;
the telemetry-only phase subsequently ran with the actual staging sampler and no
PresentMon process. The user's Task Manager screenshots showed mostly low activity
around this phase and a transition back to sustained higher activity after it stopped.
This strengthens the interference suspicion around the telemetry path but does not
distinguish workload changes, counter-reporting effects or scene/focus confounders.
The PresentMon-only phase subsequently completed after the user entered a new raid,
using the pinned binary and production capture flags with a unique diagnostic ETW
session and no telemetry worker. The user's beginning/end screenshots also showed
mostly low Task Manager 3D readings. A same-focus, same-scene baseline in this new
raid was not supplied; earlier high-utilization screenshots had a maximized Task
Manager while the isolation screenshots showed it floating over the game. Window
focus and scene differences are possible confounders, so these observations do not
isolate either component or prove a performance regression. Controlled baseline/capture/baseline
observations with matching game focus and scene are required before attributing an
effect to either component or considering counter/overhead validation complete.

A subsequent no-capture control screenshot showed sustained higher 3D readings
across most of its history followed by a short drop at its right edge. Read-only
process presence confirmed PresentMon was absent and both diagnostic processes had
completed. The user confirmed the final drop coincided with taking the screenshot.
This demonstrates a screenshot-related confound in instantaneous values even without
capture; it does not explain every prolonged low interval in earlier histories.
Further isolation must keep game focus/scene fixed and avoid chat or screenshot-tool
switches during each observed phase. The impact of either capture component on
actual FPS or GPU utilization remains unestablished.

A further isolation run sampled only the GPU Engine PDH counterset, without
PresentMon, CPU/memory/pagefile queries, WMI or DXGI/D3D12 device creation. It returned
valid samples throughout its minute; the supplied graphs showed predominantly low
readings during observation and a higher plateau afterward. The user identified the
initial brief dips as chat-window switches. To avoid losing phase boundaries as the
graph scrolls, subsequent development-only diagnostics were shortened to a single
cycle: ten seconds with no query, twenty seconds of GPU Engine polling, then fifteen
seconds after disposing the query. The short cycle completed with valid readings
throughout the polling phase. The user reduced Task Manager update speed to retain
the full graph; the graph initially appeared to show a sustained higher plateau
before, predominantly low readings during observation, and recovery afterward.
The user subsequently clarified that spikes coincided with switching to the chat
window and that low readings also persisted after stopping the query. Those focus
changes invalidate a confident attribution of the apparent transitions to PDH
start/stop. A development-only observer
using the installed NVIDIA driver's graphics-domain utilization API has been prepared
outside the repository; no vendor API has been added to either product. Product
benchmark durations remain governed by the existing 120/240-second rules.
Its single-read preflight succeeded on the expected sole NVIDIA adapter through the
installed driver DLL. The prepared comparison reads the driver graphics-domain busy
percentage throughout the before/during/after phases and enables PDH only during the
middle phase. These sources have different definitions and sampling boundaries;
the comparison evaluates changes across phases, not exact percentage equality.
The comparison cycle completed with all expected vendor and PDH samples valid.
The NVIDIA driver graphics-domain percentage remained consistently high across the
before/during/after phases, while the simultaneous Windows PDH 3D percentage was
substantially lower. The user supplied a Task Manager screenshot with similarly
low Windows readings. This establishes a material source disagreement in this
configuration; it does not establish an actual load/FPS regression caused by PDH
or PresentMon, and the different source definitions must remain explicit.
The current production collector's PDH percentage cannot support a low-GPU-load
diagnosis on this configuration. Academy currently allows only the exact PDH
graphics source literal, so adopting a vendor source requires separately agreed
desktop and backend contract support. Frozen runs must retain their original source.

A later preparation check found one orphaned, uniquely named diagnostic ETW session,
despite the absence of a PresentMon process. The development-only cleanup command
had incorrectly used `--stop_existing_session`, which stops an existing trace and
starts another, rather than `--terminate_existing_session`, which stops and exits.
The owned session was terminated successfully; ETW enumeration then confirmed zero
remaining sessions with the diagnostic prefix. Both diagnostic cleanup paths were
corrected outside the repository. Earlier process-presence checks therefore do not
establish a clean no-ETW control, and earlier before/after attribution must be
retested. The simultaneous Windows/vendor disagreement remains an observation,
but does not establish why it occurred or exclude a trace-related effect.
A new short NVAPI/PresentMon/NVAPI diagnostic subsequently completed with explicit
session termination and QPC boundary checks. It read NVAPI at approximately 1 Hz
for ten seconds before, twenty seconds during pinned PresentMon capture, and fifteen
seconds afterward; no PDH, other resource collectors or DXGI device were started.
All expected vendor readings were valid. GPU-Z's log, matched to the phase wall-clock
boundaries, returned the same phase means and ranges as NVAPI. Driver-reported activity
remained near its maximum throughout; no large utilization collapse was observed in
these two readers. This is phase-level agreement, not proof of independent hardware
measurement or identical per-sample timing. Samples crossing nominal FPS QPC
boundaries were excluded from an additional diagnostic summary; exact vendor sampling
timestamps remain unavailable. Post-test checks confirmed no PresentMon process and
zero owned diagnostic ETW sessions. The raw frame CSV was deleted, no benchmark run
was saved, and no upload occurred. Windows utilization during this clean cycle has
not yet been independently recorded, and FPS overhead remains unmeasured because
there is no comparable FPS baseline without PresentMon.
A subsequent clean NVAPI/PDH/NVAPI diagnostic used ten seconds before, twenty
seconds with only GPU Engine PDH polling, and fifteen seconds after disposing the
query. No PresentMon or DXGI device was started. Every expected reading was valid:
NVAPI phase means were 80.8%, 80.2% and 67.6%, while simultaneous PDH averaged
approximately 1.65%. Enabling PDH did not coincide with a large fall in the driver
percentage in this cycle; the lower after-phase mean does not establish a causal
effect. Checks after completion confirmed no PresentMon process and zero owned
diagnostic ETW sessions. GPU-Z was not started for this cycle, so there is no GPU-Z
corroboration for it. No run was saved or uploaded. The source disagreement is
reproduced without an active diagnostic PresentMon trace; its cause and actual FPS
overhead remain unresolved. Vendor busy-time percentages and Windows 3D engine
percentages must retain separate source semantics rather than being relabelled or
treated as interchangeable measurements.
A user-supplied earlier GPU-Z log
also showed sustained high GPU Load while the nearby Task Manager screenshot showed
a low Windows percentage; those records are not an exactly synchronized sample pair.
Static inspection of the installed Task Manager, using matching public Microsoft
symbols, confirmed its GPU counter registration and update functions use PDH and
the same GPU utilization and adapter-memory counter paths as the application;
the complete displayed-percentage calculation was not traced.

## AMD vendor utilization research

Read-only research on 2026-10-07 selected ADLX as the first AMD vendor provider
for the existing utilization metric. The repository implementation is described below;
no backend contract change or bundled vendor runtime has been added.
The inspected official SDK headers were pinned for this investigation to commit
`32b5a740d42295c5dfe9026b9f52683da0f3af91` (SDK version 2.0.0.125).

ADLX supports Windows 10/11. AMD documents that its runtime is installed with the
display driver; the x64 runtime name in the SDK is `amdadlx64.dll`. Missing or
incompatible runtime support must produce unknown values, not an installation
prompt or an obligatory monitoring utility/service/driver.
See [supported systems](https://gpuopen.com/manuals/adlx/programming-with-adlx/adlx-programming-guide/specifications/supported-operating-systems/),
[runtime distribution](https://gpuopen.com/manuals/adlx/programming-with-adlx/adlx-programming-guide/quick-start/building-python-bindings-for-adlx/)
and the [pinned runtime header](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/blob/32b5a740d42295c5dfe9026b9f52683da0f3af91/SDK/Include/ADLX.h).

The narrow read path is `GetPerformanceMonitoringServices`,
`GetSupportedGPUMetrics(selectedGPU)`, `IsSupportedGPUUsage`, then `GPUUsage`
on that adapter's samples. The usage method returns a percentage and has existed
since ADLX 1.0. Its reference does not specify a mathematical busy-time formula or
the integration period; a 1000 ms polling interval must not be presented as proof
of a one-second averaging window or equivalence with Windows 3D/NVAPI.
Only utilization and timestamps are consumed, with no tuning, application-list,
temperature, voltage or vendor FPS queries.
See [GPUUsage](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/performance-monitoring/iadlxgpumetrics/gpuusage/).

`IADLXGPU2::LUID`, available from ADLX 1.4, provides the Windows adapter identity
needed to match the existing internal DXGI/PDH selection. An array index, marketing
name or ADLX UniqueId must not be assumed to equal a DXGI LUID. On older drivers,
an alternative mapping would need its own validation; ambiguous multi-GPU matching
must remain unknown. No IDs are saved or shared.
See [LUID](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu/iadlxgpu2/luid/)
and [compatibility](https://gpuopen.com/manuals/adlx/programming-with-adlx/adlx-programming-guide/specifications/compatibility/).

For continuous collection AMD recommends `GetGPUMetricsHistory` instead of repeatedly
using the slower single-acquisition `GetCurrentGPUMetrics`. Tracking has paired,
reference-counted start/stop calls. Collection reads the newest history sample
approximately once per second, deduplicates timestamps, and stops only
the tracking it started. Default sample spacing is 1000 ms, while default history
retention is 100 seconds; waiting until a 120/240-second capture ends would lose its
early samples. Existing history must not be cleared to manufacture a run boundary.
See [single acquisition limitations](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/performance-monitoring/iadlxperformancemonitoringservices/getcurrentgpumetrics/),
[history API](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/performance-monitoring/iadlxperformancemonitoringservices/getgpumetricshistory/)
and the [pinned monitoring header](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/blob/32b5a740d42295c5dfe9026b9f52683da0f3af91/SDK/Include/IPerformanceMonitoring.h).

ADLX sample timestamps are documented as milliseconds from the system epoch.
That statement alone does not establish a QPC or Unix-epoch conversion. Clock mapping,
sample freshness, duplicates, gaps and any unknown integration period require
conservative alignment with the valid FPS window; CPU PDH intervals cannot simply
be reused as AMD sample intervals. Boundary and missing samples must reduce coverage.
See [TimeStamp](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/performance-monitoring/iadlxgpumetrics/timestamp/).

The official C# sample uses SWIG and a native C++ wrapper. The SDK also exposes
explicit C interface vtables, making a small managed native binding a feasible
implementation option that fits the existing Core interop style. This is an
implementation choice with automated ABI/lifetime tests. It handles one-byte `adlx_bool`,
native calling conventions, interface-version negotiation, Release/Terminate and
DLL lifetime. SDK/source licensing must be reviewed before vendoring any material.
See [official C# bindings](https://gpuopen.com/manuals/adlx/programming-with-adlx/adlx-programming-guide/quick-start/building-csharp-bindings-for-adlx/)
and the [pinned C interface definitions](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/blob/32b5a740d42295c5dfe9026b9f52683da0f3af91/SDK/Include/IPerformanceMonitoring.h).

Legacy ADL is an alternative for older Radeon drivers, not a universal drop-in.
LibreHardwareMonitor's AMD implementation uses several ADL generations and PMLog
paths for GPU core load, separately from Windows engine readings. AMD's ADL release
notes deprecate `ADL2_New_QueryPMLogData_Get` and describe replacement PMLog examples,
as well as a previously fixed invalid GPU activity value. A broad legacy fallback
would therefore require additional adapter mapping, capability and validity tests.
It is deferred rather than claiming support for all AMD cards.
See [ADL release notes](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/blob/master/Public-Documents/README.md)
and [LibreHardwareMonitor's AMD collector](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/Hardware/Gpu/AmdGpu.cs).

### What other monitoring tools actually implement

Static inspection on 2026-10-07 covered the user's signed GPU-Z 2.71.0.0 x86
and HWiNFO64 8.48-5990 executables. Both were UPX-packed; only external diagnostic
copies were unpacked, without running the monitoring executables or their drivers.
The following findings concern implementation paths, not a claim that a particular
Radeon model takes those paths at runtime:

- GPU-Z loads AMD ADL and contains calls to Overdrive5 CurrentActivity, OverdriveN
  PerformanceStatus and PMLog APIs. Its inspected CurrentActivity routine reads
  offset 16 of the returned 40-byte `ADLPMActivity`, matching AMD's
  `iActivityPercent` field. This confirms a GPU-utilization read path, beyond merely
  finding an API name in the executable. Its QueryPMLog wrapper reads supported
  sensor values; the mapping of that wrapper to the current UI's GPU Load sensor
  was not established for every Radeon generation.
- HWiNFO64 resolves ADL CurrentActivity and PerformanceStatus and contains indirect
  calls through both resolved pointers, with corresponding 40-/72-byte output
  structures. It also resolves PMLog and `D3DKMTQueryStatistics` functions.
- LibreHardwareMonitor provides inspectable C# source: GPU core load reads ADL
  `iActivityPercent` or PMLog `ADL_PMLOG_INFO_ACTIVITY_GFX`, while Windows D3D node
  utilization is a separate sensor path. Its full collector also queries controls
  and sensors outside our scope and must not be adopted wholesale.
- HWiNFO's author confirmed in 2020, after consulting MSI Afterburner's author,
  that Afterburner's optional unified monitoring used the same method as HWiNFO's
  D3D Usage. This is historical confirmation of an alternative Windows path,
  not a current binary audit or evidence that all Afterburner modes use it.

No ADLX markers were found in the inspected outer GPU-Z/HWiNFO images. This does
not establish absence from all embedded code. ADLX is our modern provider,
not an assertion about GPU-Z's implementation. The concrete utility
precedent for AMD vendor load is ADL/PMLog, with capability-dependent branches;
production support still needs validation on varied Radeon drivers and multi-GPU systems.
See [AMD activity layout](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/blob/master/include/adl_structures.h),
[PMLog sensor definitions](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/blob/master/include/adl_defines.h),
[LibreHardwareMonitor source](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/Hardware/Gpu/AmdGpu.cs)
and [HWiNFO author's explanation](https://www.hwinfo.com/forum/threads/gpu-utilization-spikes.6263/).

Automated tests can cover native layouts/lifetime with a fake API, missing runtime,
unsupported interfaces/metrics, exact adapter matching, invalid percentages,
stale/duplicate/gapped timestamps, coverage, boundaries, cancellation and privacy.
Actual AMD driver values, clock semantics and capture overhead still require Radeon
hardware validation; they have not been measured on the current NVIDIA test machine.
The source literal is `adlx_gpu_usage`, retaining the existing field and
whole-adapter scope. Academy's source allowlist must explicitly accept it before any
submission; no automatic source relabelling or upload is permitted.

### Returned Radeon diagnostic results (2026-10-07)

The separately requested portable diagnostic, outside the repository and skill
bundles, now completed two 30-second collections on one Radeon RX 9070 with
ADLX runtime 1.5.0.124 and ADL runtime 7.26.10.1609. Its initial worker-input
blocking bug was fixed before these completed reports; diagnostic version 0.1.2
also fixes DPI layout. It does not use PresentMon or create a benchmark run.

The first completed collection had no active GPU workload, as confirmed by the
user: ADLX history and timestamped shared PMLog each returned 30 valid polls,
3..9% with mean 7.0667%, and all paired values matched. Query PMLog returned
30 valid polls, 1..11% with mean 6.8333%.

The subsequent collection reported sustained high activity: ADLX and shared
PMLog each returned 30 valid polls, 99..100% with mean 99.9333%; all paired values
again matched. Query PMLog returned 30 valid polls, 99..100% with mean 99.9667%.
Each of these sources had diagnostic coverage 30/30. OD5 and ODN were unavailable
in both reports; errors were kept as null readings, never substituted with zero.

These results validate collection at low and high activity on one card/driver,
not independent hardware ground truth, universal Radeon support, cross-API
multi-GPU identity mapping, FPS-window coverage or absence of FPS overhead.
Diagnostic valid-poll coverage must not be copied into the benchmark's supported
duration calculation. The integrated Core binding adds selected-adapter LUID
matching, conservative timestamp/window handling and lifetime/failure tests; those
production paths have not yet been exercised on the participant's Radeon system.
No raw reports, device identifiers or sample series are checked into Git.
The Academy allowlist remains unchanged.

### Shared Core vendor integration (2026-10-07)

Both hosts and Toolkit capture commands use the same Core sampler. NVIDIA NVAPI and
AMD ADLX replace only the existing utilization metric; dedicated/shared memory remains
whole-adapter Windows telemetry. Exact vendor/LUID matching rejects duplicates, linked
physical GPUs and ambiguous inventory. Adapter selection still uses Tarkov graphics
activity or the sole hardware adapter, never the busiest unrelated GPU or model-name
matching. IDs, driver timestamps and observations stay internal.

The native bindings load only `nvapi64.dll` or `amdadlx64.dll` from Windows System32.
No SDK binary, downloaded runtime, monitoring utility or additional driver is shipped.
NVAPI initialization/unload and ADLX tracking/interface/runtime ownership are paired
with sampler disposal, including cancellation and failure. A foreign ADLX runtime is
not terminated. ADLX requires the LUID interface and usage capability; legacy ADL/PMLog
remains diagnostic-only because its production adapter mapping is not validated.

Automated native fakes exercise layouts, vtable slots, calling conventions, version
negotiation, zero/invalid readings, missing APIs, exact multi-adapter mapping, duplicate
history, ownership and cleanup. Summary tests exercise FPS boundaries/gaps, weighted
coverage, no PDH substitution, serialization/privacy and the explicit submission guard.
Both hosts display vendor graphics load separately from Windows 3D wording.

Final local suites passed: 338 Core/CLI tests, 22 Benchmark tests and 75 UI regression
tests (435 total). The read-only Academy source-schema check accepted 10 supported
payloads and rejected 45 invalid payloads, including both unsupported vendor source
literals, with zero mismatches. Both rebuilt unsigned Release MSIX packages passed
MakeAppx/content validation, public configuration checks and the pinned PresentMon
checksum check. Neither package was installed or published; no run was sent.

A native smoke test on the current NVIDIA system successfully mapped the adapter and
returned six valid NVAPI graphics readings, alongside six CPU, memory and pagefile
readings. Preparation took 449.73 ms; complete polls averaged 7.47 ms, maximum 19.95 ms.
The six-poll test consumed 171.88 ms of process CPU and 1,941,488 managed allocated bytes.
These are test-process measurements without PresentMon or a game, not proof of negligible
FPS impact. Integrated AMD capture, varied/hybrid/multi-GPU hardware and signed-package
checks remain manual release validation.

Before release, manually validate Windows 10/11 with varied GPU drivers, hybrid/UMA,
multi-GPU and multiple/system-managed pagefile configurations; compare repeat captures
with/without sampling for FPS overhead. Check real PresentMon QPC alignment, cancellation
and raid/game exit, alias output, package-local persistence across updates and consent
in both Microsoft-signed Store flight packages. No flight, publication or backend
deployment is authorized by this implementation.

### Academy vendor rollout verification (2026-10-08)

The user deployed the coordinated Academy change to staging and production and
updated the guarded demo seed in the separate backend session. The successful
[deployment](https://github.com/thetimmytook/timmy-academy/actions/runs/37683211195)
promoted revision `24a306afa4c15baa057e177573e9df24db95f226` to both environments.
The earlier descriptions of unsupported vendor sources above record the pre-rollout
state; the current desktop submission boundary now accepts exactly the Windows,
NVAPI and ADLX source literals, retaining schema version 1 and the existing field.
The temporary vendor-source send guard has been removed after the live checks.

All 26 public/read-only checks passed in each environment: 13 smoke scenarios,
nine resource scenarios, public auth configuration, anonymous owner/moderator access
denial and deployed web source labels. Pagination checks explicitly selected a
synthetic group with at least four runs: staging's first group now contains the
user's two real runs and correctly has no continuation after a two-item page.
Public config follows its same-origin version redirect. The only POST requests
were read-only Position queries; no benchmark was uploaded by these checks.

Both vendor demo details preserve their exact source, percent unit, whole-adapter
scope and independent Windows memory sources. Six before/after demo comparisons
confirmed unchanged non-telemetry fields and memory summaries. Both previously
published real staging runs still match every FPS and resource field of their
frozen requests. Browser checks confirmed NVAPI and ADLX/UMA descriptions, legacy
Windows 3D wording and the existing published owner cards/aggregate summary.

The sender validates sample-duration bounds against the actual source: Windows
PDH percent retains its 1.5-second cap, vendor observations retain one second.
Tests cover all three sources with available/partial/unavailable graphics data,
unknown/null/private fields, and all nine combinations of frozen versus later
source. Saved requests retain their exact bytes, source and statistics after restart;
different current readings never relabel or regenerate them. Consent and explicit
Send for review remain required.

Final desktop suites passed: 362 Core/CLI, 22 Benchmark and 75 UI regression tests
(459 total, none skipped). C# JSON passed the read-only TypeScript contract check:
21 accepted, 48 rejected, zero mismatches, including a real native summary paired
with explicitly synthetic FPS solely for local contract testing. Academy's 150
contract tests and 80 focused API tests also passed, including vendor-source
submission/retry/moderation/owner/public round trips in isolated test databases.
These API tests are not authenticated round trips against the deployed environments.

An isolated six-poll native test returned six valid NVAPI readings and six valid
CPU, dedicated/shared memory, RAM and pagefile samples. Preparation took 408.24 ms;
polls averaged 5.50 ms and peaked at 12.36 ms. The test process used 171.875 ms CPU
and allocated 1,715,528 managed bytes during those polls. No PresentMon or game was
running; this does not establish FPS overhead in a raid.

Both rebuilt external Debug staging hosts contain identical shared Core and retain
their original isolated histories. The actual published Core opened and strictly
projected the existing one Benchmark and two Toolkit local runs without changing
their summaries or history files. Toolkit status and the no-game capture rejection
passed. Staging API/OAuth configuration, launcher configuration pins and bundled
PresentMon were checked; launchers were compiled but not automatically executed.
Both rebuilt unsigned Release MSIX packages passed MakeAppx and package-content
validation with production API/auth configuration and pinned PresentMon. No Store
installation, app publication, commit or pull request was performed.

The user completed a new real NVIDIA capture in the rebuilt staging Benchmark
host and explicitly sent the selected run for review. The live service accepted
the unchanged NVAPI source and reports pending review. The frozen request passes
the strict TypeScript submission contract and matches the completed local run's
entire telemetry document. Owner and moderator pages preserve vendor graphics
wording, source, whole-adapter scope and separate memory metrics. Moderator detail
also preserves the aligned window, logical processors and individual pagefiles.
All sampled categories have valid observations; partial status describes incomplete
coverage rather than an unavailable counter. After the user's specific approval,
the selected measurement was published through staging moderation. Anonymous detail
returns HTTP 200 and passes the strict public contract and privacy-key checks. Every
FPS field and the entire telemetry document match the frozen request exactly; the
public page retains NVAPI source, whole-adapter scope and independent memory data.
The user confirmed Published in the desktop UI. The isolated Benchmark outbox now
persists the published status, and a fresh anonymous comparison after that status
check still matches the frozen request's complete FPS and telemetry documents.

The user also completed and explicitly submitted a distinct real NVIDIA capture
from the rebuilt staging Toolkit host. Its unchanged NVAPI telemetry matches the
complete local run and passes the strict submission and privacy checks. The live
service accepted it as pending review. Owner and moderator pages retain its vendor
source, whole-adapter scope, aligned window, logical-processor count and separate
dedicated/shared memory, physical RAM, pagefile and commit values. After the user's
specific authorization, the Toolkit measurement was also published through staging
moderation. Its anonymous HTTP 200 detail passes the strict public schema and
privacy checks and preserves every FPS field and the complete telemetry document
from the frozen request. That request's bytes are unchanged from before approval.
Both hosts have now completed distinct real NVIDIA capture, explicit send,
pending-owner/moderator review, authorized approval and public-detail comparisons.

Both isolated outboxes now persist Published. The user also confirmed Published
after the desktop status/restart-check instructions. A fresh non-GUI process using
the actual rebuilt shared Core opened and strictly projected all two Benchmark and
three Toolkit local runs, including both new vendor captures, without modifying
either history. Toolkit's frozen request still has its pre-approval SHA-256. This
fresh-process check verifies Core persistence; the GUI restart observation is the
user’s report rather than an automated native-UI test.
The live Toolkit raid-exit check also passed. The user started a capture and exited
the raid while leaving Tarkov and Toolkit open, then reported the expected discard
outcome. An independent read-only observer saw the owned capture running, raid state
become inactive with Tarkov still running, and the owned PresentMon process stop.
The owned ETW session was absent afterwards and no capture temporary directories
remained. Both hosts' history files, run IDs, and outbox request/status files were
byte-identical to their pre-test baselines; no completed partial run or submission
was created. The observer did not stop any process or tracing session itself.

The live standalone Benchmark game-exit check passed as well. The user closed
Tarkov during an active capture and reported Measurement discarded. The read-only
observer saw Tarkov exit before the 120-second capture could finish, then the owned
PresentMon process stop. The owned ETW session was absent and capture temporary
directories were removed. Both hosts retained byte-identical histories and outbox
request/status files; no partial run or submission was created. The observer did
not stop any process or tracing session itself.

That check exposed a separate current-state inconsistency: Toolkit CLI status
reported `tarkov_running=false` with `raid_active=true`, because the final raid log
had no end marker. The capture process-presence guard discarded the run correctly.
After the user's separate approval, a shared Core current-raid read now requires
process presence for Active in CLI status and InspectionService, including GUI
Collect. Historical log reads, map, version and start/end timestamps are preserved;
closing the game does not invent an end timestamp or change stored runs.

Eleven new automated checks cover unfinished logs with no process, active and
completed raids, process restart, unavailable start time and missing logs/start
markers, plus the CLI status invariant. The full desktop suites now pass 373
Core/CLI, 22 Benchmark and 75 UI tests (470 total, none skipped). Both external
staging hosts and their CLI were rebuilt. Real closed-game status and inspect both
report no active raid while retaining the previous Factory context. A fresh process
using the rebuilt Core strictly projected all two Benchmark and three Toolkit runs.
Both histories and outbox files remain byte-identical, and staging API/OAuth
configuration bytes and launcher pins were retained. These read-only checks did
not upload data or launch either GUI.
Both unsigned production MSIX packages were also rebuilt and passed MakeAppx
semantic and package-content checks with the pinned PresentMon and production
API/OAuth configuration. They were not installed or published.

Live Cancel checks passed in both hosts. The user reported Collection canceled in
Toolkit and discarded in standalone Benchmark after using Cancel during capture.
For each test the read-only observer saw the owned PresentMon start and stop before
capture completion while Tarkov, the raid and the respective host remained active.
The owned ETW session was absent and no capture temporary folders remained. Both
hosts retained byte-identical histories and outbox request/status files; no partial
run or submission was created. The observer stopped no process or tracing session
itself.

The standalone Benchmark raid-exit functional check also passed. The user reported
Measurement discarded, and the application wrote a fresh terminal result explicitly
identifying raid end before capture completion with saved_locally and uploaded both
false. Follow-up status showed Tarkov still running and the raid inactive. Benchmark
remained open; all owned PresentMon processes and the ETW session were absent,
temporary captures were removed, and both histories/outboxes were byte-identical.

Observer limitation discovered during that check: the earlier process-path-only
snapshots include PresentMon ETW cleanup helpers, which use the same executable.
Their process edges establish owned-process presence, not actual frame-capture
boundaries or capture-stage proof. The user-reported outcomes, fresh application
result where available, unchanged saved-state files and final cleanup are separate
functional evidence. Subsequent observation distinguishes actual capture arguments
from cleanup-only processes without exporting command lines, PIDs or paths. The
earlier timing snapshots are retained with this limitation, not used as measured
discard latency or evidence of FPS overhead.

The live Toolkit game-exit check passed with corrected instrumentation. The
observer distinguished cleanup helpers from the actual FPS capture using the owned
executable and capture arguments, then saw real frame capture start before Tarkov
exited, frame capture stop after game exit, and all owned PresentMon processes stop
after cleanup. Toolkit remained open and the user reported Measurement discarded.
CLI status and inspect both reported no running game or active raid. The owned ETW
session was absent, temporary captures were removed, and both histories/outboxes
were byte-identical. No command lines, PIDs or paths were exported; the observer
stopped no process or tracing session itself.

The manual functional lifecycle matrix now covers Cancel, raid exit and game exit
in both staging hosts. Earlier process-path-only snapshots retain the limitation
above; the last Toolkit test independently identifies real frame capture and helper
cleanup. Integrated ADLX on Radeon and actual FPS overhead remain measurement
checks. The user has an AMD participant who can run a real Tarkov capture; that
check will exercise the integrated Core collector and full application path.
Physical multi-GPU testing is unavailable in the current group. Adapter selection,
duplicate identities, linked nodes and ambiguous activity are covered by automated
tests; broader hardware support retains this explicit validation limitation rather
than requiring the user to find or buy another GPU.

Microsoft-signed Store-flight checks are a separate delivery requirement: installed
aliases, bundled PresentMon execution, package-local storage and update persistence.
They do not measure the FPS impact of a digital signature. FPS overhead is compared
between matched PresentMon captures with and without resource sampling. No temperatures
or other new fields were added to the upload contract.

An isolated, test-only Windows C# harness was prepared on 2026-10-08 for the
in-raid overhead check. It references the actual Release Core without changing
production behavior and runs A-B-B-A, 120 seconds per capture: A uses PresentMon
and the shared capture lifecycle; B adds the production resource sampler. Both
use the pinned, checksum-verified PresentMon, the same log/process guards and
valid-frame window. A five-second start delay and finish sound let the participant
keep the game focused in an unchanged scene. It saves only local sanitized
diagnostic summaries, never application history or uploads. Cancellation or
raid/game exit excludes the unfinished capture and suppresses suite comparison.
Twenty-three automated harness checks passed, including Release configuration,
dependency integrity, arithmetic, repeat spread, privacy, save roundtrip and
cancel/discard handling. No real FPS overhead result is claimed yet. Operation
CPU/allocation costs describe the test process across capture orchestration and
cleanup; they are explicitly separate from the aligned FPS/resource window.
Comparisons under 3% or within repeat spread remain inconclusive; larger repeated
decreases are candidates requiring confirmation. This measures incremental
sampling cost with PresentMon constant, not absolute PresentMon or host UI cost.

The participant subsequently completed the first A-B-B-A suite and confirmed the
same position, camera and game focus throughout. Valid frame durations were
119.951/119.967/119.975/119.957 seconds. B used NVAPI for the selected whole adapter
and sampled all 16 logical processors. All 60 sampled summaries had values, with
98.877-99.603% coverage and only partial-coverage reasons; aligned resource windows
matched the frame durations within rounding. Independent recalculation matched
the saved comparisons. Relative to A means, B average FPS was 4.816% lower, 1% Low
8.533% lower, 0.1% Low 0.154% lower, p95 frametime 9.539% higher and p99 12.025%
higher. A average-FPS repeat spread was 6.172%, B 4.330%; B 1% Low spread was
8.309%. This is an inconclusive overhead result with a possible slowdown signal,
not validation of negligible cost or proof of causation. A counterbalanced
B-A-A-B repeat is proposed before drawing a conclusion. Operation CPU averaged
1359.375 ms for A and 1382.8125 ms for B, but those figures include log monitoring,
CSV parsing and cleanup and cannot rule out brief interference during capture.
Both host histories remained byte-identical, the owned ETW session was absent,
no temporary capture directories remained, and nothing was uploaded. The
report and its SHA-256 are recorded in the isolated local verification evidence.

The user proposed training mode without bots for the next suite. A separate
B-A-A-B diagnostic build was prepared, keeping the exact Release Core and pinned
PresentMon hashes from the first suite. Comparison now selects A/B summaries by
their labels and preserves B-relative-to-A signs and adjacent pair comparisons
for either balanced order. Twenty-six checks passed, including both orders,
pairing and rejection of mismatched telemetry labels. The training suite will be
interpreted within its own conditions; it must not be pooled with the earlier
bot-enabled suite to attribute a difference to the sampler. No training capture
has been started by the agent, and no production behavior was changed.

The user then completed the training B-A-A-B suite. Average FPS per capture was
138.63/140.53/140.44/140.58; 1% Low was 76.39/105.85/102.37/105.71 and p99 frametime
9.516/8.247/8.448/8.244 ms. All four remain included: B means relative to A were
-0.626% average FPS, -12.544% 1% Low, -28.244% 0.1% Low, +3.396% p95 and +6.379%
p99. A average-FPS repeat spread was 0.064%; B 1.397%. B 1% Low spread was 32.202%,
and B p99 spread 14.324%. The final B closely matched A, while the first B had
substantially worse lows/tails. This does not confirm consistent sampling slowdown
or negligible overhead. Warmup and first-use effects remain hypotheses; no early
capture is removed to improve the result. A repeat of the same suite in the already
stable training scene, without restarting the harness/game, is proposed. All 60
resource metrics had values and only partial-coverage reasons, with 98.867-99.582%
coverage; frame-window alignment and recalculated comparisons matched. Both host
histories were unchanged, cleanup removed the owned ETW session and temporary
captures, and no upload occurred. This training result is assessed separately from
the earlier bot-enabled suite. It is specific to the tested hardware and workload.

The participant completed another B-A-A-B training suite and explicitly reported
extended warmup. All four valid windows were approximately 120 seconds and remain
included. A/B means were 144.755/144.800 average FPS (+0.031%), 107.420/108.075
1% Low (+0.610%), 64.895/63.625 0.1% Low (-1.957%), 7.462/7.467 ms p95 (+0.067%)
and 8.0205/8.019 ms p99 (-0.019%). Average-FPS repeat spread was 0.076% for A and
0.055% for B. Differences are below the repository 3% noise threshold and neither
B consistently worsened metrics; the previously bad first B did not recur.
This completes the comparison for the tested warmed NVIDIA training scene with
no discernible FPS/frametime impact. It establishes neither zero overhead nor a
statistical upper bound, and does not causally identify warmup as the explanation
for earlier anomalies. All earlier suites are retained and assessed separately.
All 60 sampled resource summaries had values and only partial-coverage reasons,
with 98.878-99.616% coverage; QPC alignment, independent arithmetic, Core provenance
and pinned PresentMon checksum matched. Both host histories stayed unchanged,
the owned ETW session and temporary captures were absent, and nothing was uploaded.
Probe-process operation CPU means were 851.5625/1812.5 ms for A/B and managed
allocation totals 704807744/742605736 bytes; these span approximately 132-second
capture operations including log guards, CSV parsing and cleanup, exclude sampler
preparation and are not aligned CPU percentages or retained memory. This shows
measurable collection work despite no discernible FPS impact in this scene.
Integrated Radeon validation and broader first-use/CPU-limited/multi-GPU workload
coverage remain limitations; further identical user runs are not required to
report this scoped result. No application behavior or backend contract changed.

At the user's request, a separate self-contained Windows x64 Toolkit 1.0.2.0
portable Release archive was prepared for the Radeon participant. Its runtime
public configuration targets staging, and Russian instructions cover full
120-second capture, Copy results, restart persistence, Cancel and optional
explicit Send for review. It includes the exact Core hash from the warmed
NVIDIA comparison and pinned PresentMon 2.5.1. All 618 ZIP entries were checked
against their input hashes; the archive contains no user state, credentials,
raw captures, debug symbols, executable scripts, certificates or AMD/NVIDIA
driver libraries. GUI/CLI versions, bundled runtime presence and staging config
allowlists were verified offline. Runtime smoke for this artifact was not run:
automatic approval review rejected a combined setup/delete/CLI command as
blocked by policy. Packaging was completed without application execution or
file deletion. Actual integrated Radeon validation remains pending the
participant's manual test; no Store submission or publication was performed.

### Pre-flight review fixes (2026-10-09)

The approved review fixes began with an extraction of the existing shared GUI
completion workflow without removing its late lifetime checks. The initial
regression run recorded ten failures and two passes: eight failures reproduced
discarding a validated capture on game/raid exit or transition before Save;
two architectural checks demonstrated that emergency construction depended on
aggregation and frozen-request validation constructed a local BenchmarkRun.
These latter failures are dependency-boundary proof, not a reproduced second
exception from the former emergency fallback.

Core now returns the immutable raid context from its final completion validation;
the existing CaptureReport inspection carries that snapshot without new JSON
fields. The shared GUI saves from it, including capture date, and never queries
live raid/game state after validated completion. User cancellation/declining
review still saves nothing. Closing the game or ending/changing raid while the
capture is unfinished still discards it, including transitions during summary
construction. The same testable workflow is wired into both hosts.

Unavailable/not-collected construction now builds explicit nullable summaries
independently of ResourceSummary. A malformed internal inventory deliberately
raises an aggregation exception in a test; complete FPS survives with a sanitized,
contract-valid unavailable resource summary. Legacy runs retain not_collected.

Frozen submission requests are deserialized into a separate strict request model
and validated directly, without local BenchmarkRun reconstruction or submission
reprojection. Required nullable keys, recursive duplicate-key rejection, nested
unknown-field rejection, shared setting-value rules, metric/resource consistency,
run identity and unchanged UTF-8 request bytes are covered. Outbox reads never
rewrite the frozen request. No backend field or schema version was added.

Final automated suites passed 445 Core/CLI, 22 Benchmark and 75 UI tests (542 total,
none failed or skipped). The read-only cross-check against the current backend
schema agreed on all 21 accepted and 48 rejected fixtures. Both unsigned Release
MSIX packages (Benchmark 1.0.5.0 and Toolkit 1.0.2.0) passed pinned PresentMon
verification, MakeAppx semantic validation and package-content inspection.
Package assembly versions follow their respective host versions.

A new staging Radeon participant archive,
`AMD-Tarkov-Toolkit-Test-1.0.2-staging-review-win-x64.zip`, supersedes the earlier
packet without deleting it. All 624 file entries matched their input hashes;
Core/Feature match the new Toolkit MSIX, the bundled frameworks and public staging
configuration were checked offline, and no user state or executable scripts were
included. Its SHA-256 is
`BF734A60513E6625A4080E19A9B6EF35AAE936A344F5F888C0F5F2090A05C15C`.
The fixes change Core/Feature binary hashes: the preceding NVIDIA overhead result
is not presented as a fresh measurement of these final binaries. No polling source
or sampling arithmetic was added by the review fixes. This archive was not executed;
integrated Radeon capture, a manual completed-capture exit-before-Save check in both
hosts (CAP-12), and Microsoft-signed closed-flight delivery validation remain pending.
At the end of this verification phase, no commit, PR, flight dispatch, application
publication or production deploy had occurred.

Primary references: [Windows GPU telemetry](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/),
[GPU process-memory limitations](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value),
[installed RAM](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getphysicallyinstalledsystemmemory),
[system memory and commit](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information),
[pagefile enumeration](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-enumpagefilesw),
[PresentMon 2.5.1](https://github.com/GameTechDev/PresentMon/blob/v2.5.1/README-ConsoleApplication.md).
