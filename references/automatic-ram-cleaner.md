# Automatic RAM Cleaner: Version-Specific Evidence

Use this reference when explaining Automatic RAM Cleaner. Keep static code
evidence, runtime observations and version applicability separate. This note
adds implementation knowledge; it supplies no numeric FPS/memory forecast.

## Evidence scope

A local static-analysis report dated 2026-10-11 examined EFT executable version
**0.16.9.40087**, Unity **2022.3.43f1**, and installed **SPT 4.0.13** modules.
The analysis followed metadata, IL, state machines, imports and call sites.
It did not observe live execution or API success. The patched installation and
its `.spt-bak` are not authenticated as an original retail distribution.

The user's benchmark captures are **retail 1.2.0.0.47965**. A separate native
addendum examined a current installation with EXE product version
**1.2.0.0-47965-a0aa6211**, Unity **2022.3.43f2**, and **IL2CPP**. Static analysis
confirms an EmptyWorkingSet path, but does not map its conditions to the
checkbox or game events. Matching build numbers do not prove the files are
byte-identical to those used for capture: hashes were not recorded at capture
time. Backend state and runtime API success were not observed.

| Evidence | Established | Still unknown |
| --- | --- | --- |
| Local 0.16.9.40087 / SPT 4.0.13 static analysis | Named menu/start call sites, flag/override condition and Windows trim request. | Live execution, API success and bytes trimmed; applicability to retail. |
| Retail 47965 native static analysis | Unity/IL2CPP version and resolved Windows API path with three found direct conditional callers. | Checkbox/field mapping, named events, timer/thresholds, application timing, runtime success and complete neighboring cleanup chain. |
| Retail long-session capture sequence | Whole-system RAM/commit observations and reported symptoms. | Owning process, cause/timing of the drop and repeatable benefit. |

## Local 0.16.9.40087 call chain

Three static call sites reach `MemoryControllerClass.EmptyWorkingSet`,
conditional on the applied setting or backend override:

| Event | Established local code path | Limits |
| --- | --- | --- |
| Showing `MenuScreen` | One callback after three coroutine frames, then the Cleaner condition and API request. | Includes ESC in raid when it actually shows this screen; not every physical ESC press. Component must start the coroutine. |
| Late local-raid preparation | `BaseLocalGame.method_7` reaches the Cleaner condition. | Exact visible countdown timing and actual SPT execution path unverified. |
| Network raid `GameSpawned` | After separate asynchronous collection, reaches the Cleaner condition. | Static network path present; not proof this path executed in a measured session. |

The wrapper obtains its own process handle and calls the real P/Invoke
`psapi.dll!EmptyWorkingSet`. It discards the returned Boolean. Thus the
evidence establishes a request/potential execution path, not a successful
trim or a measured number of freed bytes.

The API requests removal of resident pages from the process working set.
It does not itself destroy managed objects, unload Unity assets/textures,
decommit their allocations or clear the system standby list. A smaller
working set need not produce an equal increase in system available RAM or
an equal amount of pagefile writes.
[Microsoft EmptyWorkingSet](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset),
[Working Set](https://learn.microsoft.com/en-us/windows/win32/memory/working-set).

The inspected call chain has no periodic Cleaner scheduler or current-RAM/
commit threshold. This is a bounded negative finding for the examined code,
not a claim about every version, native path or mod. A separate explicit-GC
cooldown of 600 seconds is not a Cleaner timer.

## Applying the setting and separate operations

After successful Save/Apply, these call sites read the current applied
`AutoEmptyWorkingSet` value at the next suitable event, including in the
same raid. This local chain requires neither a new raid nor a restart.
Toggling the temporary UI value alone does not directly call the API.

The condition can instead use backend
`OverrideRamCleanerSettings ? RamCleanerEnabled : AutoEmptyWorkingSet`.
The inspected local database has override disabled; an actual backend
response was not captured. Do not promise that the checkbox controls the
operation regardless of override.

Managed GC can run beside these events independently of the Cleaner checkbox.
Unity asset unloading and pool/scene cleanup also have separate paths.
Do not attribute all cleanup on a menu/raid transition to Cleaner.

SPT 4.0.13 separately patches `MenuTaskBar.OnScreenChanged`. On Inventory,
with an instantiated `GameWorld`, it enables Unity GC and requests one
incremental step with a **25 ms budget**, independently of the Cleaner flag.
That budget is not guaranteed elapsed time or collected volume. This is an
inventory patch, not the ESC-menu Cleaner implementation.
[Installed SPT source snapshot](https://github.com/sp-tarkov/modules/blob/5ca4ccff6ca1853f7d36f28562941bd069c67b6b/project/SPT.Custom/Patches/MemoryCollectionPatch.cs).

## Retail 1.2.0.0.47965 and the measured series

`GameAssembly.dll` contains a statically established path through a process
object constructed with the current PID, a handle getter, dynamic resolution
of `psapi.dll!EmptyWorkingSet` through `GetProcAddress`, and the native call.
The common wrapper at RVA `0x03EC7C90` calls the resolved API at `0x03EC7D89`
without checking its Boolean result in that section. Three direct conditional
calls were found at `0x03724D7E`, `0x0410B5D7` and `0x0462C539`.
These are possible execution paths, not observed invocations. They do not
exclude indirect or inlined calls; the complete OS-handle creation path was
not reconstructed.

The managed field/getter names and event identities remain unmapped. Retail
ESC/menu/start/exit/inventory behavior, three-frame delay, backend override,
timer/RAM thresholds and Save/restart requirements are **unverified**. Similar
branch structure to the older client is a hypothesis, not proof of those
events. Do not transfer the SPT inventory patch to retail or change the
first-raid validation protocol on this basis.

No GC or Unity resource-unload call was identified inside this wrapper, but
neighboring calls remain unnamed. Therefore neither "the full Cleaner chain
only trims" nor "Cleaner also collects objects/unloads assets" is established
for retail. The API itself changes residency; it does not establish decommit
or leak repair. Pages can be needed again, so evaluate frametimes as well as
working set and commit if testing this mechanism.

The retail capture sequence showed no sustained RAM reduction in its measured
same-raid On window. The next measured On raid had approximately **6.02 GiB
less occupied system RAM** while system commit was **0.47 GiB higher**.
Menus, saving, raid exit and loading between windows were not continuously
captured; On capture windows had no user-reported menu/inventory openings.

These whole-system gauges cannot identify the operation, owning process or
exact event. They do not establish released committed allocations, a fixed
leak, delayed application until the next raid or repeatable FPS/stutter benefit.
The retail trimming path is compatible with a residency hypothesis;
its existence does not prove the cause of the measured RAM difference.

## Provenance

Source: private report **Automatic RAM Cleaner: sources and static analysis**,
2026-10-11, with IL/event tables and a separate SHA-256 file manifest.
Report SHA-256:
`D83F04922CFC16DC398CA858BA6272E7D05DA493E347B1330398F9FBA0A95AA1`.
Full report, manifest and individual raw measurements stay outside the skill
bundle. This summary records supplied research; it is not a new runtime test.

Separate source: **Retail 47965 native addendum**, 2026-10-11, with its own
SHA-256 file manifest and native call-site evidence. Addendum SHA-256:
`2E29BBF2B52E6537CBAC4BB67B854A02734192247DBC930AAF3D8BB53AFC6B09`.
Analyzed `GameAssembly.dll` SHA-256:
`308EFA0EBD2ED19260752DC2AE17284B3C6E9C5B0A0195024E611BCAEA720DB5`.
The full addendum and file manifest remain private; game files are not shipped
with the skill.
