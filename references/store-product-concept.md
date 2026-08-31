# Microsoft Store Product Concept

Status: benchmark prototype and reproducible Store MSIX packaging implemented

## Product Boundary

Keep agent skills in GitHub and publish one Microsoft Store application: **Tarkov Performance Benchmark**.

- Package identity: `TimmyTook.TarkovPerformanceBenchmark`
- Store ID: `9PJMPQ06JL21`
- Platform: Windows x64
- Minimum Windows build: 19041
- Technology: C#, .NET 8, WPF, self-contained packaged desktop application with full trust

The Store application must not install Codex, Claude, or other client-specific skills. Skills call the trusted installed application and contain no executable PowerShell, CMD, EXE, or PresentMon copies.

## Benchmark MVP

The first Store release provides the standalone two-minute benchmark UI. It:

- checks that Tarkov is running and an active raid is visible in read-only logs;
- reads `Graphics.ini`, `PostFx.ini`, and graphically relevant `Game.ini` data without modifying game files;
- records non-identifying Windows hardware and driver information;
- captures FPS and frametime data through the bundled PresentMon binary;
- asks for BSG server versus Local and optional weather/time context after capture;
- writes completed runs to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`;
- uploads nothing without explicit consent.

Do not read Tarkov process memory, inject code, provide an overlay, automate input, or interact with anti-cheat systems.

## Skill Contract

Expose a stable application execution alias and command:

```text
tarkov-benchmark.exe collect --source skill
```

The command opens the normal GUI and still requires the user to press Start. After a successful save, it returns a machine-readable summary with the run ID, map, Average FPS, 1% Low, 0.1% Low, P95 frametime, local-save status, and upload status. Skill-initiated sessions close after briefly showing the result; normal Start-menu sessions remain open.

## PresentMon

PresentMon is an external MIT-licensed dependency bundled inside the MSIX. Pin its version and SHA-256, retain its license, and update it only through a manual change followed by real capture tests. The application must never discover or execute arbitrary external PresentMon copies.

Attempt ETW capture without elevation first. If Windows denies access, the future Store package may offer a one-time elevated setup that adds the user to `Performance Log Users`. Do not install a custom Windows service in the MVP.

## Distribution

The monorepo keeps the application under `apps/tarkov-performance-benchmark/` with a dedicated Windows pipeline. CI restores, tests, verifies PresentMon, and creates a temporary self-contained build artifact.

The repository builds an unsigned x64 MSIX with the reserved Store identity, bundled PresentMon, neutral package artwork, and the `tarkov-benchmark.exe` execution alias. The package declares the WPF executable as a full-trust packaged desktop app without requesting the highly restricted `unvirtualizedResources` capability. Validate visibility of the documented local skill contract during the closed Store flight.

Privacy-policy hosting and Partner Center submission remain separate release work. Upload the package manually until the first certification and closed Store test succeed.

Do not use local self-signed package installation as a release gate. Build and inspect the unsigned package locally, then use the Microsoft-signed closed Store flight to test installation, alias registration, capture, and shared data visibility under the real distribution identity.
