# Tarkov Frametime

Instruction and overview for the `tarkov-frametime` skill.

This skill collects FPS and frametime metrics only. It does not collect map, weather, PvE/PvP mode, route, or settings analysis; `tarkov-performance-benchmark` combines those fields with the metrics later.

Use `tarkov-tuning` when these measurements need to decide whether to keep/revert a config change.

## Preferred Tool

Use PresentMon when available. The skill can also parse CSV exports from PresentMon, CapFrameX, or FrameView.

## Installing PresentMon

Download PresentMon from the official GitHub releases page:

```text
https://github.com/GameTechDev/PresentMon/releases
```

Recommended local layout (outside the skill tree, so updates never remove it):

```text
%LOCALAPPDATA%\TarkovSkills\
  tools\
    PresentMon\
      PresentMon.exe
      LICENSE.txt
      VERSION.txt
```

After placing `PresentMon.exe` there, run the check command below. If the executable is somewhere else, pass its path to the scripts with `-PresentMonPath`.

PresentMon starts an ETW trace session. The capture script first tries without elevation and, when started with `-RequestElevation`, retries once through a Windows UAC prompt if needed. Parsing existing CSV files never needs elevation.

## How To Use

Check for PresentMon:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-presentmon.ps1
```

Check a custom path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-presentmon.ps1 -PresentMonPath "C:\Tools\PresentMon\PresentMon.exe"
```

Capture with PresentMon:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-presentmon.ps1 -DurationSec 120 -RequestElevation
```

PresentMon capture is blocking by design: the command waits until `-DurationSec` finishes. Only 120 or 240 seconds are accepted; start with 120. Captured CSVs land in `%LOCALAPPDATA%\TarkovSkills\captures\`.

Capture with a custom PresentMon path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-presentmon.ps1 -DurationSec 120 -RequestElevation -PresentMonPath "C:\Tools\PresentMon\PresentMon.exe"
```

Parse an existing CSV:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\parse-fps-csv.ps1 -Path .\captures\capture.csv
```

Manual CSV mode requires an existing export from PresentMon, CapFrameX, or FrameView. If the user has no tool and no CSV, guide them to install/export first.

## Output

The parsed JSON includes:

- average FPS
- 1% low FPS
- 0.1% low FPS
- average frametime
- p95/p99 frametime
- duration
- sample count
- method

## Boundaries

- No game settings edits.
- No map/context collection in this skill; benchmark/tuning skills own context assembly.
- No gameplay automation.
- Manual CSV parsing must work when a CSV already exists.
