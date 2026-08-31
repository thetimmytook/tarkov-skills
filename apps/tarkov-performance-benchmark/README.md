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

The application reads Tarkov logs and `Graphics.ini`, `PostFx.ini`, and `Game.ini` without modifying game files. Completed runs are appended to `%LOCALAPPDATA%\TarkovSkills\benchmark.json`; nothing is uploaded automatically.

PresentMon is an external MIT-licensed dependency pinned in `third_party/presentmon/dependency.json`.
