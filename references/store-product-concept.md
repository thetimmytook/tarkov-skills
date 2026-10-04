# Microsoft Store Product Concept

Status: Benchmark `1.0.3.0` and Toolkit `1.0.0.0` publicly published; shared Core and shared Benchmark feature implemented

## Product Boundary

Keep agent skills in GitHub and distribute local executable functionality through two Microsoft Store products:

- Package identity: `TimmyTook.TarkovPerformanceBenchmark`
- Store ID: `9PJMPQ06JL21`
- Platform: Windows x64
- Minimum Windows build: 19041
- Technology: C#, .NET 8, WPF, self-contained packaged desktop application with full trust

The standalone **Tarkov Performance Benchmark** remains the focused manual benchmark/submission product. The separate **Tarkov Performance Toolkit** supplies headless settings/system inspection, capture, and goal memory to local agents, serves web users through its GUI, and hosts the same shared benchmark feature in its Benchmark section.

- Toolkit identity: `TimmyTook.TarkovPerformanceToolkit`
- Store ID: `9N3L7DZH0K64`
- Publisher: `CN=55890398-71D9-4366-AF45-568B3BC3A786`
- Package family name: `TimmyTook.TarkovPerformanceToolkit_kzkg4vyj42m5j`
- Package SID: `S-1-15-2-599854310-2684056050-4152303614-2218492696-4213724081-2583029688-1534160776`
- GUI executable: `TarkovPerformanceToolkit.exe`
- Console alias: `tarkov-skills.exe`
- Shared collectors and contracts: `src/TarkovSkills.Core/TarkovSkills.Core.csproj`
- Shared benchmark workflow and UI: `src/TarkovBenchmark.Feature/TarkovBenchmark.Feature.csproj`
- Each Store package includes private copies of both required DLLs and registers no global library

Neither Store application installs Codex, Claude, or client-specific skills. Skills call trusted installed aliases and contain no PowerShell, CMD, EXE, DLL, or PresentMon copies.

### Getting started with skills

Toolkit's Overview explains the complete manual workflow before the report preview:
collect a read-only report, copy its JSON, and paste it into a chat with the Tarkov
Performance skills. Explain that these optional skills help interpret results and
guide repeatable settings tests; they do not automatically improve FPS or change
game settings. Local agents with the skills installed can also collect through the
Toolkit command interface. Show the instructions on first launch; a small help
button in the top-right corner hides the entire card without leaving an expander
row. A close (x) button at the card's top right also hides it; the small help button
restores the card in Overview. Persist the expanded/collapsed
choice immediately in `LocalState\TarkovSkills\toolkit-ui.json`, independently of
the application version, so restarts and Store updates preserve it. Keep expanded
instructions scrollable at the minimum window size. Uninstallation still removes
package-local preferences.

Both About windows explain AI-assisted analysis and open the same repository
installation guide in the user's browser. Benchmark directs users to Toolkit for
the full skills workflow. The applications do not install skills or send reports
when the guide is opened. Refresh both Store listings with actual screenshots of
the reviewed UI before publishing the corresponding application update.

Keep the standalone Benchmark footer separate from its scrolling content, with
24-DIP side padding and clear top/bottom spacing. Place the author label and the
disclaimer/About actions in separate columns; About stays vertically centered
instead of stretching to fill the footer. Labels must not overlap at minimum size.

### Desktop authentication

Both products share a Clerk public-client OAuth implementation in Core and a shared
account UI in `TarkovBenchmark.Feature`. Both products share one issuer/client ID per
environment. Development configuration is local and ignored; production configuration
is versioned and required for Release builds and both MSIX packages. System-browser
authorization uses S256 PKCE, state validation and a
one-time IPv4 loopback callback. Consent remains enabled. Both products share one
credential encrypted with Windows CurrentUser DPAPI and bound to issuer and client.
MSIX packages use the declared `TarkovDesktopAuth` publisher cache folder; portable
builds share a separate user-local auth directory. Desktop logout in either product
revokes the shared grant for both, without logging out the browser session.

Authentication starts only when Submit is chosen. The shared Submit dialog restores
the credential before opening a browser, provides Sign in / Sign out / Retry, and
requires an explicit review and Send for review action after sign-in. No permanent login banner is
shown on the ordinary collection or inspection screens.
See `references/desktop-auth.md` for migration and the manual test checkpoint.

Authentication does not affect collection, aliases or history. Submit now sends
one selected run to Academy using an explicit allowlist DTO and the run's permanent
UUID. The exact request is saved separately for retries. Pending review is not
publication; only the validated server receipt establishes publication state.
Neither local sign-in nor the legacy `submitted` flag proves server acceptance.
The shared dialog persists the last confirmed server status and offers Check status
for the selected run after moderation. Deleted runs are not sent again; uncertain
outcomes are resolved through owner lookup before any explicit retry. Live end-to-end
validation and Store-package checks remain required before release.

## Comparison charts (2026-09-26)

Implemented in the shared `ComparisonView` at the bottom of Benchmark in both
applications. Horizontal paired bars show Average FPS and 1% Low on one zero-based
scale, with exact numeric labels and a green background for the selected local run.
Bars are 12 device-independent pixels thick. Local metrics use the existing green
accent pair; public metrics use the blue pair, with both pairs identified in the legend.
Always default to Public runs by map (Streets initially), including when local runs
exist. Selecting a local run or reopening the view does not send local parameters;
the user must press Compare selected run after seeing which fields will be sent.

- Show a chart comparing the user's selected local benchmark run with other public
  runs, using the existing Academy public search/Position contracts.
- Before the first completed local run, show **Run benchmark to see your position**
  in place of the personal-position chart. Still show public-run charts for a map,
  with the selected map clearly visible and changeable.
- Once a local run exists, offer explicit comparison with comparable public runs.
  Public browsing sends no local benchmark parameters. Comparison sends only the
  disclosed cohort fields and does not require publication or sign-in.
- Keep publication separate and explicit. Pending, rejected or deleted submissions
  must not be presented as public comparison records.

Local comparisons use anonymous `/cohorts/query`, matching hardware, map, execution,
resolution and game version; the selected run is labelled local regardless of its
publication status. Only the existing cohort DTO is sent, without run ID, metrics,
raw captures or credentials. Query preparation reuses the submission privacy
validation; unsupported legacy runs can still use public map browsing.
Public browsing shows previews of up to six hardware groups, labelled as examples
rather than a complete ranking. Display sample counts, limited results, varying
settings/weather and Demo markers for API-designated synthetic runs. No percentile
is invented. Network failures offer refresh without affecting local collection.

The new client read three Demo public examples from the real local API. Automated
checks cover anonymous access, field allowlisting, exact/no-match responses, error
redaction and endpoint selection. The user approved chart layout, colors and
thickness in both hosts. The window height is now capped at the benchmark content
height (and screen work area); the map selector shows names rather than key/value
pairs. The user verified the no-history state and map switching without repeated resizing.
For that check only, Debug builds accept the process-local environment variable
`TARKOV_BENCHMARK_TEST_DATA_DIRECTORY` with an absolute temporary directory. The
Release/Store build does not read it. Never clear real user history to test first run.

## Benchmark MVP

The first Store release provides the standalone two-minute benchmark UI. It:

- checks that Tarkov is running and an active raid is visible in read-only logs;
- reads `Graphics.ini`, `PostFx.ini`, and graphically relevant `Game.ini` data without modifying game files;
- records non-identifying Windows hardware and driver information;
- captures FPS and frametime data through the bundled PresentMon binary;
- asks for BSG server versus Local and optional weather/time context after capture;
- writes completed runs to the Store package's private `LocalState\TarkovSkills\benchmark.json` history;
- uploads nothing without explicit consent.

Do not read Tarkov process memory, inject code, provide an overlay, automate input, or interact with anti-cheat systems.

### Expanded Local Hardware Details (Future Benchmark Work)

Extend the shared Benchmark system collector to retain useful hardware context with each completed local run. Collect what Windows reports reliably; unavailable or ambiguous values remain `unknown`. This is a separate implementation task. Public submission fields, detail-page presentation, and privacy review will be decided before any of these additional fields are uploaded.

- **CPU:** retain the detected model, core/thread counts, and reported maximum clock. A current clock reading is only a snapshot; never label it as the frequency sustained during the FPS capture. Do not add continuous hardware sensor sampling solely for this feature.
- **RAM:** retain total capacity and, per module, capacity, manufacturer, sanitized part/model number, memory type, and configured speed. Preserve module configuration rather than presenting total capacity as the whole story. Do not claim to know memory timings or the active channel mode from fields that do not report them.
- **GPU:** retain the detected GPU name/model, chip vendor, VRAM, and driver version. Investigate Windows PCI device/subsystem IDs to identify the board vendor and, where an offline lookup has an unambiguous match, the board model. A subsystem ID or lookup result must not be treated as proof of a precise retail SKU when multiple variants share it. Keep raw hardware/device/instance IDs out of saved and shareable run records; store only a sanitized resolved label and its confidence, or `unknown`.
- **Game storage:** identify the volume holding Tarkov and retain its storage media type, reported physical-disk model and bus type where mapping is reliable, plus total/free space at capture time. Do not label interface specifications or a disk model's advertised speed as measured read/write throughput. A real storage-speed benchmark would require separate design and consent.
- **Pagefile:** retain its allocated size and whether its backing storage is SSD, HDD, or unknown; current/peak usage and backing-disk details may be retained locally if reliable and useful for later analysis. Support multiple pagefiles. Never save or publish their local paths or drive letters.

Apply explicit field selection and sanitization before persistence or sharing. Never include serial numbers, raw PCI/PnP identifiers, machine IDs, host/user names, or local paths in benchmark artifacts. Check Windows-reported values on varied systems, including laptops, multiple GPUs/disks, and missing WMI/Storage data. These details are context for individual runs, not additional mandatory keys for the initial CPU/GPU/RAM-capacity search grouping. Do not upload them automatically; publication still requires the user's explicit review and consent.

## Accepted Next Steps (2026-10-04)

### First-Time Game Profile: Skills First

Implement optional beginner guidance in `tarkov-config` and `tarkov-tuning` before
implementing resource telemetry. The agreed values and agent behavior are mastered in
[game-starting-profile.md](game-starting-profile.md) and vendored into both skills.
This is manual Game-tab usability guidance, not an FPS preset or automatic game-setting
change. Offer it for first-time setup or an explicit request; skip an established setup,
respect existing preferences, and do not repeat it between tuning captures. It requires
no Store application or API change.

### Capture-Window Resource Telemetry: Accepted, Implementation Deferred

Add resource measurements to shared Core and both Store hosts in a separate application
change. The current apps do not supply this telemetry; skills must not invent readings
or describe this decision as an available command feature. This is distinct from the
static hardware enrichment above and does not authorize temperature/voltage collection.

- Sample resource counters over the same valid 120/240-second capture window as FPS,
  initially targeting one sample per second. Retain average/peak summaries, sample count,
  valid coverage, units, source, and scope. Peaks must belong to this capture window,
  not lifetime or since-boot counters. Account for sampler overhead in validation.
- CPU: overall utilization and logical-processor utilization. Do not infer a CPU
  bottleneck solely from a low overall percentage.
- GPU: graphics-engine utilization for the active adapter, dedicated VRAM capacity and
  average/peak use, plus shared GPU memory use separately. Do not sum unrelated GPUs or
  label shared system memory as additional VRAM. Prefer adapter-wide memory readings;
  per-game counters are supplemental and need reliability checks. Almost-full VRAM is
  a reason to test texture quality, not proof that textures alone caused a slowdown.
- Memory: physically installed RAM, OS-usable RAM, used/available RAM over the capture,
  and minimum available RAM. Keep actual allocated pagefile size and use separate from
  RAM; support multiple and system-managed pagefiles. Also report current system commit
  and commit limit. Never label RAM plus pagefile as physical RAM or pagefile size as
  commit limit. Preserve useful existing backing-storage media context without paths.
- Use C# Windows APIs/performance counters and the pinned bundled PresentMon's CPU/GPU
  frame timings where reliable. No extra user utility, driver, custom service, overlay,
  gameplay automation or game-memory access is required by the proposed baseline.
  Driver-dependent or missing readings remain `unknown`/null with a warning, not zero.
  Missing telemetry must not invalidate otherwise complete FPS data.
- Store the sanitized summary with a completed local run and expose it to local/web
  skill workflows. Discard interrupted captures as before; do not retain or publish a
  partial run as a complete benchmark. Diagnostics should describe possible bottlenecks
  with confidence and use repeatable A/B tests rather than categorical percent rules.
- Include the summary only with a run the user explicitly sends for review/publication,
  after updating the displayed sharing explanation. No automatic upload or raw
  per-second trace publication. Distinguish whole-system load from game-specific load;
  never include process IDs, other application names, device identifiers or private paths.
- Coordinate optional allowlisted fields, validation, storage and public detail responses
  with the Academy API before updated clients publish them. Old runs without telemetry
  stay valid; missing readings cannot be used as measured zeros. This is not a silent
  addition to the existing strict submission payload or a new required cohort key.

Before release, validate overhead and sampling alignment, unavailable/partial counters,
GPU selection and memory-counter reliability, physical RAM/pagefile/commit distinctions,
privacy, old-run compatibility, consented submission, cancellation and raid/game exit in
both signed Store products. App/API contracts and package versions remain unchanged in
the skills-only implementation.

References for the future collector: [Windows GPU telemetry](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/),
[GPU process-memory counter limitations](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value),
[installed RAM](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getphysicallyinstalledsystemmemory),
[pagefile usage](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pagefileusage),
[system commit and physical memory](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information),
and [PresentMon 2.5.1 console metrics](https://github.com/GameTechDev/PresentMon/blob/v2.5.1/README-ConsoleApplication.md).

## Standalone Benchmark Contract

Expose a stable application execution alias and command:

```text
tarkov-benchmark.exe collect --source skill
```

The command opens the normal GUI and still requires the user to press Start. After a successful save, it returns a machine-readable summary with the run ID, map, Average FPS, 1% Low, 0.1% Low, P95 frametime, local-save status, and upload status. Command-initiated sessions close after briefly showing the result; normal Start-menu sessions remain open. Agent skills use Toolkit's headless `tarkov-skills.exe` commands as their primary local contract instead of opening this UI.

## PresentMon

PresentMon is an external MIT-licensed dependency bundled inside the MSIX. Pin its version and SHA-256, retain its license, and update it only through a manual change followed by real capture tests. The application must never discover or execute arbitrary external PresentMon copies.

Attempt ETW capture without elevation first. If Windows denies access, the future Store package may offer a one-time elevated setup that adds the user to `Performance Log Users`. Do not install a custom Windows service in the MVP.

## Future Result Comparison

A completed benchmark must eventually explain what the run means, not only display isolated FPS numbers. The agreed design for the Benchmark screen, public search, submission, and backend contract is in [community-benchmark-design.md](community-benchmark-design.md). Its two lower tabs are **Diff** (local comparison with previous runs) and **Position** (consented comparison with community runs). The temporary Google Form is collection-only and is not a runtime data source for the application.

### Tuned Hardware

Do not infer overclocking from observed clocks or automatically exclude unusually fast results. Detection is unreliable because boost behavior, power limits, undervolting, memory profiles, cooling, and vendor defaults overlap.

- Add an optional self-reported tuning classification when community comparison is implemented: `stock`, `overclocked`, `undervolted`, `mixed`, or `unknown`.
- Preserve tuned runs and label them; they are useful evidence rather than invalid data.
- Use stock results as the default baseline. Compare tuned systems within the same tuning class when enough data exists, or show them as a separately marked cohort.
- Keep `unknown` results in broad aggregate analysis, but do not use them for a strict stock-versus-tuned claim.
- Do not add temperature, voltage, clock, or other hardware-sensor collection solely to classify overclocking. Such diagnostics belong to a separate hardware-debugging feature with its own consent and validation.

## Distribution

The monorepo keeps the application under `apps/tarkov-performance-benchmark/` with a dedicated Windows pipeline. CI restores, tests, verifies PresentMon, and creates a temporary self-contained build artifact.

The repository builds an unsigned x64 MSIX with the reserved Store identity, bundled PresentMon, neutral package artwork, and the `tarkov-benchmark.exe` execution alias. The package declares the WPF executable as a full-trust packaged desktop app without requesting the highly restricted `unvirtualizedResources` capability. Benchmark history belongs to package `LocalState`; skills use the execution alias and machine-readable output rather than direct file access.

Privacy-policy hosting and Partner Center submission remain separate release concerns. Both products have passed initial certification and are public. Manual GitHub workflows build and validate unsigned packages, submit them to private Store flights after protected approval, and promote only the exact flight-tested artifacts through a separate protected public-release workflow.

Do not use local self-signed package installation as a release gate. Build and inspect the unsigned package locally, then validate installation, alias registration, capture, and package-local persistence with the Microsoft-signed Store update. Use a closed Store flight when an update needs limited distribution before production.
