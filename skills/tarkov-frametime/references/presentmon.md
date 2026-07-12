# PresentMon Notes

PresentMon is the preferred FPS/frametime capture utility for this skill.

Official download page:

```text
https://github.com/GameTechDev/PresentMon/releases
```

Expected local locations, in check order:

- a user-provided path (`-PresentMonPath`)
- `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\PresentMon.exe` (preferred; survives plugin/repo updates)
- skill-local `tools/PresentMon/PresentMon.exe` (legacy fallback)
- `PresentMon.exe` available on PATH

If PresentMon is missing, give the user these choices:

1. Download from the official GitHub releases page.
2. Extract/copy `PresentMon.exe` to `%LOCALAPPDATA%\TarkovSkills\tools\PresentMon\PresentMon.exe`.
3. Provide a custom `PresentMon.exe` path.
4. Use manual CSV mode only if they already have a CSV export from PresentMon, CapFrameX, or FrameView.

Keep download/install automation explicit. Do not silently download binaries.

## Elevation

PresentMon starts an ETW trace session, which normally requires an elevated (Run as administrator) console or membership in the Performance Log Users group. This is the one documented admin exception in this repository (see `AGENTS.md`). Explain the reason to the user before asking them to elevate. CSV parsing never needs elevation.

## CLI Flags

The exact command-line flags may differ between PresentMon releases. `capture-presentmon.ps1` uses a conservative invocation: `-process_name`, `-timed`, `-terminate_after_timed`, `-output_file`. `-terminate_after_timed` matters: without it PresentMon keeps running after the timed capture and the wrapper would wait forever. If the invocation fails on a given release, report the error and ask for manual CSV export.
