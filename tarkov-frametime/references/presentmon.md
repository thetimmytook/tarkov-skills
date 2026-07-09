# PresentMon Notes

PresentMon is the preferred FPS/frametime capture utility for this skill.

Official download page:

```text
https://github.com/GameTechDev/PresentMon/releases
```

Expected local locations:

- `tools/PresentMon/PresentMon.exe`
- `tools/PresentMon/PresentMon-*.exe`
- a user-provided path
- `PresentMon.exe` available on PATH

If PresentMon is missing, give the user these choices:

1. Download from the official GitHub releases page.
2. Extract/copy `PresentMon.exe` to repository root `tools/PresentMon/PresentMon.exe`.
3. Provide a custom `PresentMon.exe` path.
4. Use manual CSV mode only if they already have a CSV export from PresentMon, CapFrameX, or FrameView.

Keep download/install automation explicit. Do not silently download binaries.

Recommended local layout:

```text
tarkov-skills/
  tools/
    PresentMon/
      PresentMon.exe
      LICENSE.txt
      VERSION.txt
```

The exact command-line flags may differ between PresentMon releases. `capture-presentmon.ps1` tries a conservative CSV capture invocation first. If that fails, report the error and ask for manual CSV export.
