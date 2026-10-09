# Tarkov Performance Toolkit

Microsoft Store companion for the Tarkov performance skills. It provides read-only configuration and system inspection, persistent tuning goals, and the same shared benchmark workflow as the standalone Benchmark product without distributing unsigned scripts.

The GUI is organized into Overview, Benchmark, and Goal sections. Benchmark capture, context questions, history, and submission are implemented once in `src/TarkovBenchmark.Feature/` and hosted by both Store applications. Toolkit enables an additional **Copy results** action for web-client users; the standalone Benchmark shell leaves it hidden.

Normal launch opens the WPF interface. Agents use the console alias:

```text
tarkov-skills.exe status
tarkov-skills.exe inspect
tarkov-skills.exe capture --duration 120
tarkov-skills.exe goal get
tarkov-skills.exe goal set --goal stable-fps --target-fps 60 --quality "balanced visibility/performance"
```

Commands write sanitized JSON to standard output. Reports omit user names, host names, local paths, IP addresses, serial numbers, and machine identifiers. Nothing is uploaded automatically.

Repository capture builds include mandatory `resource_telemetry` summaries in completed capture JSON and saved benchmark runs. Both hosts show the shared summary; Toolkit Copy results includes it. GPU memory is for the whole selected adapter, with dedicated and shared usage separate. NVIDIA/AMD graphics load uses installed NVAPI/ADLX driver APIs. Explicit Send for review includes supported frozen selected-run summaries, with publication after moderator approval; vendor-source runs stay local until Academy's allowlist supports those sources. Comparison requests exclude telemetry. See [measurement definitions and release checks](../../references/resource-telemetry.md). This feature is not yet in the public Store version.

Build the unsigned Store package from the repository root:

```powershell
.\build\build-toolkit-msix.ps1 -PackageVersion 1.0.2.0
```

The current public Store version is `1.0.0.0`; the command above uses the next flight candidate. Version `1.0.1.0` was already accepted into a previous flight, so changed binaries use a higher version. Keep the fourth component at `0` because Microsoft Store reserves it.

The manifest uses the reserved identity `TimmyTook.TarkovPerformanceToolkit`, publisher `CN=55890398-71D9-4366-AF45-568B3BC3A786`, and Store ID `9N3L7DZH0K64`.

Store page: https://apps.microsoft.com/detail/9N3L7DZH0K64
