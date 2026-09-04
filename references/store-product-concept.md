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

A completed benchmark must eventually explain what the run means, not only display isolated FPS numbers. After a run, show the user's position within a comparable submitted cohort using a clear percentile/distribution chart for Average FPS, 1% Low, 0.1% Low, and P95 frametime.

- Compare like-for-like results by map, resolution, execution type, and broadly comparable hardware/settings where sample size permits.
- Show the cohort definition and sample count next to the chart. Do not present a precise rank when the cohort is too small or poorly matched.
- Keep the local result useful before upload. Fetch or display community comparison data only after the user explicitly consents to the relevant network action.
- Use a backend API or periodically published aggregate dataset for comparisons. The temporary Google Form is collection-only and is not a runtime data source for the application.
- Prefer a distribution/percentile view over a competitive leaderboard. The purpose is diagnosis and expectation-setting, not encouraging unsafe tuning.

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

Privacy-policy hosting and Partner Center submission remain separate release concerns. Both products have passed initial certification and are public. Upload packages manually until an authenticated Store deployment pipeline is deliberately introduced.

Do not use local self-signed package installation as a release gate. Build and inspect the unsigned package locally, then validate installation, alias registration, capture, and package-local persistence with the Microsoft-signed Store update. Use a closed Store flight when an update needs limited distribution before production.
