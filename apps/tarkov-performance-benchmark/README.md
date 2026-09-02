# Tarkov Performance Benchmark

Windows desktop prototype for collecting a two-minute Escape from Tarkov frametime benchmark with bundled PresentMon.

## Development

```powershell
dotnet build .\TarkovPerformanceBenchmark.sln -c Debug
dotnet run --project .\src\TarkovPerformanceBenchmark\TarkovPerformanceBenchmark.csproj
```

Skill invocation contract:

```powershell
TarkovPerformanceBenchmark.exe collect --source skill
```

The application reads Tarkov logs and `Graphics.ini`, `PostFx.ini`, and `Game.ini` without modifying game files. Development builds append completed runs to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`; nothing is uploaded automatically.

PresentMon is an external MIT-licensed dependency pinned in `third_party/presentmon/dependency.json`.

## Microsoft Store package

From the repository root, build the unsigned x64 MSIX for Partner Center:

```powershell
.\build\build-benchmark-msix.ps1 -PackageVersion 1.0.0.0
```

The package is written to `artifacts\msix`. Its identity matches Store product `9PJMPQ06JL21`, and Microsoft signs it after Store certification. The package exposes `tarkov-benchmark.exe` as an application execution alias.

The Store package runs as a full-trust packaged desktop app and stores benchmark history in its package `LocalState\TarkovSkills\benchmark.json` directory. `Open folder` resolves that physical directory. Agent skills invoke the stable execution alias and consume machine-readable command output instead of reading package files directly. The application does not upload benchmark data automatically.

Store package versions must use a nonzero first component and `0` as the fourth component. Start with `1.0.0.0`; Microsoft Store reserves the fourth component.

### Release check

1. Run the test suite and the pinned PresentMon checksum check.
2. Build the unsigned MSIX with `build-benchmark-msix.ps1`; `MakeAppx` performs manifest and package validation.
3. Inspect the package identity, architecture, execution alias, bundled PresentMon, and SHA-256 block map.
4. Upload the unsigned MSIX manually to a closed Partner Center flight.
5. Install the Microsoft-signed flight package and verify launch, `tarkov-benchmark.exe`, PresentMon capture, package-local benchmark history, and persistence across a Store update.

Do not create or trust a local self-signed certificate for routine testing. A locally signed package does not reproduce the Store trust chain and adds machine cleanup without validating the actual distribution path.

Partner Center field values, listing copy, privacy text, restricted-capability justification, and the signed private-release checklist are maintained in [`references/store-submission.md`](../../references/store-submission.md).
